using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Contracts.Controller;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Machines;

namespace Yottaverse.MachineOps.Infrastructure.Controller;

public sealed class TcpControllerSession : IControllerSession, IAsyncDisposable
{
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private readonly object snapshotGate = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ControllerEventMessage>> pending = new();
    private readonly IControllerAuditStore auditStore;
    private readonly TimeProvider timeProvider;
    private CancellationTokenSource? sessionCancellation;
    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private Task? readLoop;
    private MachineSnapshot snapshot;
    private bool disconnectRequested;
    private Guid? auditSessionId;
    private long inboundAuditSequence;
    private long outboundAuditSequence;

    public TcpControllerSession(TimeProvider timeProvider, IControllerAuditStore auditStore)
    {
        this.timeProvider = timeProvider;
        this.auditStore = auditStore;
        snapshot = MachineSnapshot.Disconnected(Guid.Empty, timeProvider.GetUtcNow());
    }

    public event EventHandler<MachineSnapshotChangedEventArgs>? SnapshotChanged;

    public event EventHandler<ControllerAlarmEventArgs>? AlarmRaised;

    public MachineSnapshot Snapshot
    {
        get
        {
            lock (snapshotGate)
            {
                return snapshot;
            }
        }
    }

    public async Task<MachineSnapshot> ConnectAsync(
        ControllerConnectionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectCoreAsync(CancellationToken.None);
            disconnectRequested = false;
            SetSnapshot(MachineSnapshot.Disconnected(options.MachineId, timeProvider.GetUtcNow()) with
            {
                ConnectionStatus = ConnectionStatus.Connecting,
            });

            sessionCancellation = new CancellationTokenSource();
            client = new TcpClient();
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(options.Timeout);
            await client.ConnectAsync(options.Host, options.Port, timeout.Token);

            NetworkStream stream = client.GetStream();
            reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
            writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };
            auditSessionId = await auditStore.BeginSessionAsync(
                options.MachineId,
                options.Host,
                options.Port,
                timeProvider.GetUtcNow(),
                cancellationToken);
            inboundAuditSequence = 0;
            outboundAuditSequence = 0;
            readLoop = ReadLoopAsync(sessionCancellation.Token);

            ControllerEventMessage response = await SendAndWaitAsync(
                new ControllerCommandMessage(
                    ControllerMessageTypes.Hello,
                    Guid.NewGuid()),
                options.Timeout,
                cancellationToken);
            if (response.Type != ControllerMessageTypes.HelloAccepted)
            {
                throw new InvalidDataException(
                    response.Error ?? $"Expected '{ControllerMessageTypes.HelloAccepted}'.");
            }

            MachineSnapshot connected = Snapshot with
            {
                MachineId = options.MachineId,
                ConnectionStatus = ConnectionStatus.Connected,
                LastError = null,
                ObservedAtUtc = timeProvider.GetUtcNow(),
            };
            if (response.State is not null)
            {
                connected = MapState(connected, response);
            }

            SetSnapshot(connected);
            return connected;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            SetFault("The simulator connection timed out.");
            throw new TimeoutException("The simulator connection timed out.", exception);
        }
        catch (Exception exception) when (exception is SocketException or IOException or InvalidDataException)
        {
            SetFault(exception.Message);
            throw;
        }
        finally
        {
            connectionGate.Release();
        }
    }

    public async Task<MachineSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        if (Snapshot.ConnectionStatus != ConnectionStatus.Connected)
        {
            return Snapshot;
        }

        ControllerEventMessage response = await SendAndWaitAsync(
            new ControllerCommandMessage(ControllerMessageTypes.GetState, Guid.NewGuid()),
            TimeSpan.FromSeconds(3),
            cancellationToken);
        if (response.State is not null)
        {
            SetSnapshot(MapState(Snapshot, response));
        }

        return Snapshot;
    }

    public async Task<MachineSnapshot> ExecuteAsync(
        ControllerOperation operation,
        CancellationToken cancellationToken)
    {
        string messageType = operation switch
        {
            ControllerOperation.Start => ControllerMessageTypes.Start,
            ControllerOperation.Pause => ControllerMessageTypes.Pause,
            ControllerOperation.Resume => ControllerMessageTypes.Resume,
            ControllerOperation.Cancel => ControllerMessageTypes.Cancel,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        ControllerEventMessage response = await SendAndWaitAsync(
            new ControllerCommandMessage(messageType, Guid.NewGuid()),
            TimeSpan.FromSeconds(3),
            cancellationToken);
        if (response.Type != ControllerMessageTypes.CommandAccepted)
        {
            throw new InvalidDataException(response.Error ?? $"The simulator rejected '{messageType}'.");
        }

        if (response.State is not null)
        {
            SetSnapshot(MapState(Snapshot, response));
        }

        return Snapshot;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            await DisconnectCoreAsync(cancellationToken);
        }
        finally
        {
            connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectCoreAsync(CancellationToken.None);
        connectionGate.Dispose();
    }

    private async Task<ControllerEventMessage> SendAndWaitAsync(
        ControllerCommandMessage command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        StreamWriter activeWriter = writer
            ?? throw new InvalidOperationException("The controller is not connected.");
        TaskCompletionSource<ControllerEventMessage> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(command.CorrelationId, completion))
        {
            throw new InvalidOperationException("A duplicate controller correlation identifier was created.");
        }

        try
        {
            string payload = ControllerProtocolJson.Serialize(command);
            if (auditSessionId is Guid sessionId)
            {
                await auditStore.RecordAsync(
                    sessionId,
                    Interlocked.Increment(ref outboundAuditSequence),
                    "outbound",
                    command.Type,
                    payload,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }

            await activeWriter.WriteLineAsync(payload);
            return await completion.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            pending.TryRemove(command.CorrelationId, out _);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && reader is not null)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                ControllerEventMessage message = ControllerProtocolJson.DeserializeEvent(line)
                    ?? throw new InvalidDataException("The simulator returned an empty message.");
                if (auditSessionId is Guid sessionId)
                {
                    await auditStore.RecordAsync(
                        sessionId,
                        Interlocked.Increment(ref inboundAuditSequence),
                        "inbound",
                        message.Type,
                        line,
                        timeProvider.GetUtcNow(),
                        cancellationToken);
                }

                if (message.CorrelationId is Guid correlationId &&
                    pending.TryGetValue(correlationId, out TaskCompletionSource<ControllerEventMessage>? completion))
                {
                    completion.TrySetResult(message);
                }

                if (message.State is not null && message.Sequence > Snapshot.Sequence)
                {
                    SetSnapshot(MapState(Snapshot, message));
                }

                if (message.Type == ControllerMessageTypes.Alarm)
                {
                    AlarmRaised?.Invoke(
                        this,
                        new ControllerAlarmEventArgs(
                            $"{auditSessionId:N}:{message.Sequence}",
                            message.AlarmCode ?? "CONTROLLER",
                            message.Error ?? "The controller raised an alarm."));
                }
            }

            if (!disconnectRequested && !cancellationToken.IsCancellationRequested)
            {
                SetFault("The simulator closed the connection.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (JsonException)
        {
            SetFault("The simulator returned malformed JSON.");
        }
        catch (IOException exception)
        {
            if (!disconnectRequested)
            {
                SetFault(exception.Message);
            }
        }
        finally
        {
            Exception closed = new IOException("The controller session ended.");
            foreach (TaskCompletionSource<ControllerEventMessage> completion in pending.Values)
            {
                completion.TrySetException(closed);
            }
        }
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        string? disconnectReason = Snapshot.LastError;
        disconnectRequested = true;
        if (writer is not null && client?.Connected == true)
        {
            try
            {
                await SendAndWaitAsync(
                    new ControllerCommandMessage(
                        ControllerMessageTypes.Disconnect,
                        Guid.NewGuid()),
                    TimeSpan.FromSeconds(1),
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException or TimeoutException or InvalidOperationException)
            {
            }
        }

        if (sessionCancellation is not null)
        {
            await sessionCancellation.CancelAsync();
        }

        client?.Dispose();
        if (readLoop is not null)
        {
            try
            {
                await readLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        reader?.Dispose();
        writer?.Dispose();
        sessionCancellation?.Dispose();
        client = null;
        reader = null;
        writer = null;
        readLoop = null;
        sessionCancellation = null;
        pending.Clear();
        if (auditSessionId is Guid sessionId)
        {
            await auditStore.EndSessionAsync(
                sessionId,
                disconnectReason,
                timeProvider.GetUtcNow(),
                cancellationToken);
            auditSessionId = null;
        }

        Guid machineId = Snapshot.MachineId;
        SetSnapshot(MachineSnapshot.Disconnected(machineId, timeProvider.GetUtcNow()));
    }

    private MachineSnapshot MapState(
        MachineSnapshot current,
        ControllerEventMessage message)
    {
        ControllerStateWire state = message.State!;
        OperatingStatus operatingStatus = Enum.TryParse(
            state.OperatingState,
            ignoreCase: true,
            out OperatingStatus parsed)
            ? parsed
            : OperatingStatus.Unknown;
        return current with
        {
            ConnectionStatus = ConnectionStatus.Connected,
            OperatingStatus = operatingStatus,
            Position = new Position3D(state.X, state.Y, state.Z),
            FeedRate = state.FeedRate,
            SpindleSpeed = state.SpindleSpeed,
            Progress = state.Progress,
            LastAcknowledgedCommand = state.LastAcknowledgedCommand,
            Sequence = message.Sequence,
            LastError = message.Error,
            ObservedAtUtc = timeProvider.GetUtcNow(),
        };
    }

    private void SetFault(string error)
    {
        SetSnapshot(Snapshot with
        {
            ConnectionStatus = ConnectionStatus.Faulted,
            LastError = error,
            ObservedAtUtc = timeProvider.GetUtcNow(),
        });
    }

    private void SetSnapshot(MachineSnapshot value)
    {
        lock (snapshotGate)
        {
            snapshot = value;
        }

        SnapshotChanged?.Invoke(this, new MachineSnapshotChangedEventArgs(value));
    }
}
