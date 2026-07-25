using System.Net;
using Yottaverse.MachineOps.Simulator;

namespace Yottaverse.MachineOps.Simulator.Tests;

public sealed class SimulatorOptionsTests
{
    [Fact]
    public void ParseUsesExpectedDefaults()
    {
        SimulatorOptions options = SimulatorOptions.Parse([]);

        Assert.Equal(5099, options.Port);
        Assert.Equal(SimulatorScenario.Normal, options.Scenario);
        Assert.Equal(IPAddress.Loopback, options.ListenAddress);
    }

    [Fact]
    public void ParseReadsPortAndScenarioWithoutDependingOnCase()
    {
        SimulatorOptions options = SimulatorOptions.Parse(
            ["--port", "6012", "--scenario", "outoforder"]);

        Assert.Equal(6012, options.Port);
        Assert.Equal(SimulatorScenario.OutOfOrder, options.Scenario);
    }

    [Fact]
    public void ParseRejectsAnUnknownOption()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => SimulatorOptions.Parse(["--surprise"]));

        Assert.Contains("--surprise", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayOptionSelectsTheReplayScenarioAndFile()
    {
        SimulatorOptions options = SimulatorOptions.Parse(["--replay", "samples/run.jsonl"]);

        Assert.Equal(SimulatorScenario.Replay, options.Scenario);
        Assert.Equal("samples/run.jsonl", options.ReplayFile);
    }
}
