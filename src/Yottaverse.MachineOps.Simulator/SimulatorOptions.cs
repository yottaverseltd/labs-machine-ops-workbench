namespace Yottaverse.MachineOps.Simulator;

public enum SimulatorScenario
{
    Normal,
    Slow,
    Malformed,
    Duplicate,
    OutOfOrder,
    Alarm,
    Disconnect,
    Burst,
    Replay,
}

public sealed record SimulatorOptions(
    int Port,
    SimulatorScenario Scenario)
{
    public static SimulatorOptions Parse(IReadOnlyList<string> args)
    {
        int port = 5099;
        SimulatorScenario scenario = SimulatorScenario.Normal;
        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--port" when index + 1 < args.Count:
                    if (!int.TryParse(args[++index], out port) || port is < 0 or > 65535)
                    {
                        throw new ArgumentException("Simulator port must be between 0 and 65535.");
                    }

                    break;
                case "--scenario" when index + 1 < args.Count:
                    if (!Enum.TryParse(args[++index], ignoreCase: true, out scenario))
                    {
                        throw new ArgumentException($"Unknown simulator scenario '{args[index]}'.");
                    }

                    break;
                default:
                    throw new ArgumentException($"Unknown simulator option '{args[index]}'.");
            }
        }

        return new SimulatorOptions(port, scenario);
    }
}
