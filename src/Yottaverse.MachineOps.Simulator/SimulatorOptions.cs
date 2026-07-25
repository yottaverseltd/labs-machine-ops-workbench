using System.Net;

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
    SimulatorScenario Scenario,
    IPAddress ListenAddress,
    string? ReplayFile = null)
{
    public static SimulatorOptions Parse(IReadOnlyList<string> args)
    {
        int port = 5099;
        SimulatorScenario scenario = SimulatorScenario.Normal;
        IPAddress listenAddress = IPAddress.Loopback;
        string? replayFile = null;
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
                case "--listen" when index + 1 < args.Count:
                    if (!IPAddress.TryParse(args[++index], out IPAddress? parsedAddress))
                    {
                        throw new ArgumentException($"Invalid listen address '{args[index]}'.");
                    }

                    listenAddress = parsedAddress;
                    break;
                case "--scenario" when index + 1 < args.Count:
                    if (!Enum.TryParse(args[++index], ignoreCase: true, out scenario))
                    {
                        throw new ArgumentException($"Unknown simulator scenario '{args[index]}'.");
                    }

                    break;
                case "--replay" when index + 1 < args.Count:
                    replayFile = args[++index];
                    scenario = SimulatorScenario.Replay;
                    break;
                default:
                    throw new ArgumentException($"Unknown simulator option '{args[index]}'.");
            }
        }

        return new SimulatorOptions(port, scenario, listenAddress, replayFile);
    }
}
