namespace Yottaverse.MachineOps.Core.GCode;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

public sealed record GCodeDiagnostic(
    int LineNumber,
    DiagnosticSeverity Severity,
    string Code,
    string Message);
