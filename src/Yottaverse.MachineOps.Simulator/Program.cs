using Yottaverse.MachineOps.Simulator;

SimulatorOptions options = SimulatorOptions.Parse(args);
using CancellationTokenSource shutdown = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await using SimulatorServer server = new(options);
await server.StartAsync(shutdown.Token);
Console.WriteLine(
    $"MachineOps simulator listening on 127.0.0.1:{server.BoundPort} with scenario '{options.Scenario}'.");
Console.WriteLine("Press Ctrl+C to stop.");

try
{
    await SimulatorServer.WaitForShutdownAsync(shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
}
