using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Yottaverse.MachineOps.Contracts.Controller;

namespace Yottaverse.MachineOps.Simulator;

public sealed class SimulatorServer : IAsyncDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions ReplayJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentDictionary<int, Task> sessions = new();
    private readonly SimulatorOptions options;
    private ControllerStateWire[] replayStates = [];
    private TcpListener? listener;
    private Task? acceptLoop;
    private int sessionNumber;

    public SimulatorServer(SimulatorOptions options)
    {
        this.options = options;
    }

    public int BoundPort { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (listener is not null)
        {
            throw new InvalidOperationException("The simulator is already running.");
        }

        replayStates = LoadReplayStates();
        listener = new TcpListener(options.ListenAddress, options.Port);
        listener.Start();
        BoundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            shutdown.Token,
            cancellationToken);
        acceptLoop = AcceptLoopAsync(linked);
        return Task.CompletedTask;
    }

    public static async Task WaitForShutdownAsync(CancellationToken cancellationToken) =>
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await shutdown.CancelAsync();
        listener?.Stop();
        if (acceptLoop is not null)
        {
            try
            {
                await acceptLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }

        await Task.WhenAll(sessions.Values);
        shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationTokenSource linked)
    {
        using (linked)
        {
            while (!linked.IsCancellationRequested && listener is not null)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(linked.Token);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    break;
                }

                int id = Interlocked.Increment(ref sessionNumber);
                Task session = RunSessionAsync(client, linked.Token);
                sessions[id] = session;
                _ = session.ContinueWith(
                    completedTask => sessions.TryRemove(id, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }
    }

    private async Task RunSessionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, new UTF8Encoding(false), leaveOpen: true);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };
            long sequence = 0;
            int replayIndex = 0;
            ControllerRunPlanWire? runPlan = null;
            ControllerStateWire state = new("Idle", 0, 0, 0, null, null, 0, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                ControllerCommandMessage? command;
                try
                {
                    command = ControllerProtocolJson.DeserializeCommand(line);
                }
                catch (System.Text.Json.JsonException)
                {
                    await SendAsync(
                        writer,
                        new ControllerEventMessage(
                            ControllerMessageTypes.ProtocolError,
                            null,
                            ++sequence,
                            Error: "Malformed command JSON."));
                    continue;
                }

                if (command is null)
                {
                    continue;
                }

                if (options.Scenario == SimulatorScenario.Slow)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                }

                switch (command.Type)
                {
                    case ControllerMessageTypes.Hello:
                        await SendAsync(
                            writer,
                            new ControllerEventMessage(
                                ControllerMessageTypes.HelloAccepted,
                                command.CorrelationId,
                                ++sequence,
                                state));
                        if (options.Scenario == SimulatorScenario.Disconnect)
                        {
                            return;
                        }

                        if (options.Scenario == SimulatorScenario.Alarm)
                        {
                            state = state with { OperatingState = "Alarm" };
                            await SendAsync(
                                writer,
                                new ControllerEventMessage(
                                    ControllerMessageTypes.Alarm,
                                    null,
                                    ++sequence,
                                    state,
                                    Error: "Emergency stop input is active.",
                                    AlarmCode: "E_STOP"));
                        }

                        break;

                    case ControllerMessageTypes.GetState:
                        if (options.Scenario == SimulatorScenario.Malformed)
                        {
                            await writer.WriteLineAsync("{ this-is-not-json");
                            break;
                        }

                        if (options.Scenario == SimulatorScenario.Replay)
                        {
                            state = replayStates[Math.Min(replayIndex, replayStates.Length - 1)];
                            replayIndex++;
                        }
                        else if (state.OperatingState == "Running")
                        {
                            state = AdvanceRun(state, runPlan);
                        }

                        ControllerEventMessage stateMessage = new(
                            ControllerMessageTypes.State,
                            command.CorrelationId,
                            ++sequence,
                            state);
                        await SendAsync(writer, stateMessage);
                        if (options.Scenario == SimulatorScenario.Duplicate)
                        {
                            await SendAsync(writer, stateMessage);
                        }
                        else if (options.Scenario == SimulatorScenario.OutOfOrder)
                        {
                            await SendAsync(
                                writer,
                                stateMessage with { Sequence = sequence + 2 });
                            await SendAsync(
                                writer,
                                stateMessage with { Sequence = sequence + 1 });
                            sequence += 2;
                        }
                        else if (options.Scenario == SimulatorScenario.Burst)
                        {
                            for (int index = 0; index < 50; index++)
                            {
                                await SendAsync(
                                    writer,
                                    new ControllerEventMessage(
                                        ControllerMessageTypes.State,
                                        null,
                                        ++sequence,
                                        state));
                            }
                        }

                        break;

                    case ControllerMessageTypes.Start:
                        if (state.OperatingState != "Idle")
                        {
                            await SendProtocolErrorAsync(
                                writer,
                                command,
                                ++sequence,
                                "A run can only start while the simulator is idle.");
                            break;
                        }

                        try
                        {
                            runPlan = string.IsNullOrWhiteSpace(command.Payload)
                                ? null
                                : ControllerProtocolJson.DeserializeRunPlan(command.Payload);
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            runPlan = null;
                        }

                        if (runPlan?.Segments is not { Count: > 0 })
                        {
                            await SendProtocolErrorAsync(
                                writer,
                                command,
                                ++sequence,
                                "A start command requires a valid execution plan.");
                            break;
                        }

                        ControllerPathSegmentWire firstSegment = runPlan.Segments[0];
                        state = state with
                        {
                            OperatingState = "Running",
                            X = firstSegment.FromX,
                            Y = firstSegment.FromY,
                            Z = firstSegment.FromZ,
                            FeedRate = firstSegment.FeedRate,
                            SpindleSpeed = 12_000,
                            Progress = 0,
                            LastAcknowledgedCommand = 0,
                        };
                        await SendAcceptedAsync(writer, command, ++sequence, state);
                        break;

                    case ControllerMessageTypes.Pause:
                        if (state.OperatingState != "Running")
                        {
                            await SendProtocolErrorAsync(
                                writer,
                                command,
                                ++sequence,
                                "Only a running job can be paused.");
                            break;
                        }

                        state = state with { OperatingState = "Paused", FeedRate = 0 };
                        await SendAcceptedAsync(writer, command, ++sequence, state);
                        break;

                    case ControllerMessageTypes.Resume:
                        if (state.OperatingState != "Paused")
                        {
                            await SendProtocolErrorAsync(
                                writer,
                                command,
                                ++sequence,
                                "Only a paused job can be resumed.");
                            break;
                        }

                        state = state with { OperatingState = "Running", FeedRate = 600 };
                        await SendAcceptedAsync(writer, command, ++sequence, state);
                        break;

                    case ControllerMessageTypes.Cancel:
                        if (state.OperatingState is not ("Running" or "Paused"))
                        {
                            await SendProtocolErrorAsync(
                                writer,
                                command,
                                ++sequence,
                                "There is no active job to cancel.");
                            break;
                        }

                        state = state with
                        {
                            OperatingState = "Idle",
                            FeedRate = 0,
                            SpindleSpeed = 0,
                        };
                        await SendAcceptedAsync(writer, command, ++sequence, state);
                        break;

                    case ControllerMessageTypes.Disconnect:
                        await SendAsync(
                            writer,
                            new ControllerEventMessage(
                                ControllerMessageTypes.CommandAccepted,
                                command.CorrelationId,
                                ++sequence,
                                state));
                        return;

                    default:
                        await SendAsync(
                            writer,
                            new ControllerEventMessage(
                                ControllerMessageTypes.ProtocolError,
                                command.CorrelationId,
                                ++sequence,
                                Error: $"Unsupported command '{command.Type}'."));
                        break;
                }
            }
        }
    }

    private static Task SendAcceptedAsync(
        StreamWriter writer,
        ControllerCommandMessage command,
        long sequence,
        ControllerStateWire state) =>
        SendAsync(
            writer,
            new ControllerEventMessage(
                ControllerMessageTypes.CommandAccepted,
                command.CorrelationId,
                sequence,
                state));

    private static ControllerStateWire AdvanceRun(
        ControllerStateWire state,
        ControllerRunPlanWire? runPlan)
    {
        if (runPlan?.Segments is not { Count: > 0 } segments)
        {
            return state with
            {
                OperatingState = "Idle",
                FeedRate = 0,
                SpindleSpeed = 0,
            };
        }

        double progress = Math.Min(100, state.Progress + 5);
        double totalDistance = segments.Sum(SegmentLength);
        double distanceToTravel = totalDistance * (progress / 100);
        ControllerPathSegmentWire activeSegment = segments[^1];
        double x = activeSegment.ToX;
        double y = activeSegment.ToY;
        double z = activeSegment.ToZ;

        foreach (ControllerPathSegmentWire segment in segments)
        {
            double segmentLength = SegmentLength(segment);
            if (segmentLength <= 0)
            {
                activeSegment = segment;
                continue;
            }

            if (distanceToTravel <= segmentLength)
            {
                double fraction = distanceToTravel / segmentLength;
                activeSegment = segment;
                x = Interpolate(segment.FromX, segment.ToX, fraction);
                y = Interpolate(segment.FromY, segment.ToY, fraction);
                z = Interpolate(segment.FromZ, segment.ToZ, fraction);
                break;
            }

            distanceToTravel -= segmentLength;
        }

        bool completed = progress >= 100;
        int acknowledgedCommand = Math.Min(
            segments.Count,
            (int)Math.Ceiling(segments.Count * (progress / 100)));
        return state with
        {
            X = x,
            Y = y,
            Z = z,
            FeedRate = completed ? 0 : activeSegment.FeedRate,
            SpindleSpeed = completed ? 0 : 12_000,
            Progress = progress,
            LastAcknowledgedCommand = acknowledgedCommand,
            OperatingState = completed ? "Idle" : "Running",
        };
    }

    private static double SegmentLength(ControllerPathSegmentWire segment)
    {
        double x = segment.ToX - segment.FromX;
        double y = segment.ToY - segment.FromY;
        double z = segment.ToZ - segment.FromZ;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    private static double Interpolate(double from, double to, double fraction) =>
        from + ((to - from) * fraction);

    private ControllerStateWire[] LoadReplayStates()
    {
        if (options.Scenario != SimulatorScenario.Replay)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(options.ReplayFile))
        {
            throw new InvalidOperationException(
                "The replay scenario requires a JSON Lines file supplied with --replay.");
        }

        string path = Path.GetFullPath(options.ReplayFile);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The simulator replay file was not found.", path);
        }

        ControllerStateWire[] states = File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(
                line => System.Text.Json.JsonSerializer.Deserialize<ControllerStateWire>(
                    line,
                    ReplayJsonOptions) ??
                    throw new InvalidDataException("A replay line contained an empty state."))
            .ToArray();
        return states.Length == 0
            ? throw new InvalidDataException("The simulator replay file has no states.")
            : states;
    }

    private static Task SendProtocolErrorAsync(
        StreamWriter writer,
        ControllerCommandMessage command,
        long sequence,
        string error) =>
        SendAsync(
            writer,
            new ControllerEventMessage(
                ControllerMessageTypes.ProtocolError,
                command.CorrelationId,
                sequence,
                Error: error));

    private static Task SendAsync(
        StreamWriter writer,
        ControllerEventMessage message) =>
        writer.WriteLineAsync(ControllerProtocolJson.Serialize(message));
}
