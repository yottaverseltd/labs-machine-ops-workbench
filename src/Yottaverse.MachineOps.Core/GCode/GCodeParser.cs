using System.Globalization;
using System.Text.RegularExpressions;

namespace Yottaverse.MachineOps.Core.GCode;

public sealed partial class GCodeParser
{
    private readonly double millimetresPerInch;

    public GCodeParser(double millimetresPerInch = 25.4)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(millimetresPerInch);
        this.millimetresPerInch = millimetresPerInch;
    }

    public ParsedGCodeProgram Parse(string name, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);

        List<ToolpathSegment> segments = [];
        List<GCodeDiagnostic> diagnostics = [];
        Position3D current = Position3D.Origin;
        MotionMode? motionMode = null;
        double? feedRate = null;
        bool absolutePositioning = true;
        double unitScale = 1;

        string[] lines = source.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = RemoveComments(lines[index]).Trim();
            if (line.Length == 0 || line == "%")
            {
                continue;
            }

            List<Word> words = ParseWords(line, lineNumber, diagnostics);
            if (words.Count == 0)
            {
                continue;
            }

            bool unsupportedArc = false;
            foreach (Word word in words.Where(word => word.Letter == 'G'))
            {
                int code = (int)word.Value;
                switch (code)
                {
                    case 0:
                        motionMode = MotionMode.Rapid;
                        break;
                    case 1:
                        motionMode = MotionMode.Linear;
                        break;
                    case 2:
                    case 3:
                        unsupportedArc = true;
                        diagnostics.Add(new GCodeDiagnostic(
                            lineNumber,
                            DiagnosticSeverity.Warning,
                            "GCODE002",
                            "Arc motion is not previewed yet. The line was skipped."));
                        break;
                    case 20:
                        unitScale = millimetresPerInch;
                        break;
                    case 21:
                        unitScale = 1;
                        break;
                    case 90:
                        absolutePositioning = true;
                        break;
                    case 91:
                        absolutePositioning = false;
                        break;
                }
            }

            Word? feedWord = words.Find(word => word.Letter == 'F');
            if (feedWord is not null)
            {
                if (feedWord.Value <= 0)
                {
                    diagnostics.Add(new GCodeDiagnostic(
                        lineNumber,
                        DiagnosticSeverity.Error,
                        "GCODE003",
                        "Feed rate must be greater than zero."));
                }
                else
                {
                    feedRate = feedWord.Value * unitScale;
                }
            }

            bool hasAxis = words.Any(word => word.Letter is 'X' or 'Y' or 'Z');
            if (!hasAxis || unsupportedArc)
            {
                continue;
            }

            if (motionMode is null)
            {
                diagnostics.Add(new GCodeDiagnostic(
                    lineNumber,
                    DiagnosticSeverity.Error,
                    "GCODE004",
                    "An axis value was supplied before a G0 or G1 motion mode."));
                continue;
            }

            Position3D next = ResolvePosition(words, current, absolutePositioning, unitScale);
            if (next == current)
            {
                diagnostics.Add(new GCodeDiagnostic(
                    lineNumber,
                    DiagnosticSeverity.Information,
                    "GCODE005",
                    "The move has no travel and was ignored."));
                continue;
            }

            segments.Add(new ToolpathSegment(current, next, motionMode.Value, lineNumber, feedRate));
            current = next;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            diagnostics.Add(new GCodeDiagnostic(
                0,
                DiagnosticSeverity.Error,
                "GCODE001",
                "The program is empty."));
        }
        else if (segments.Count == 0)
        {
            diagnostics.Add(new GCodeDiagnostic(
                0,
                DiagnosticSeverity.Warning,
                "GCODE006",
                "No supported tool movement was found."));
        }

        return new ParsedGCodeProgram(
            name,
            source,
            segments,
            diagnostics,
            ToolpathBounds.From(segments));
    }

    private static Position3D ResolvePosition(
        IReadOnlyList<Word> words,
        Position3D current,
        bool absolutePositioning,
        double unitScale)
    {
        double? x = FindCoordinate(words, 'X', unitScale);
        double? y = FindCoordinate(words, 'Y', unitScale);
        double? z = FindCoordinate(words, 'Z', unitScale);

        if (absolutePositioning)
        {
            return new Position3D(x ?? current.X, y ?? current.Y, z ?? current.Z);
        }

        return new Position3D(
            current.X + (x ?? 0),
            current.Y + (y ?? 0),
            current.Z + (z ?? 0));
    }

    private static double? FindCoordinate(IReadOnlyList<Word> words, char letter, double unitScale)
    {
        Word? word = words.FirstOrDefault(candidate => candidate.Letter == letter);
        return word is null ? null : word.Value * unitScale;
    }

    private static List<Word> ParseWords(
        string line,
        int lineNumber,
        List<GCodeDiagnostic> diagnostics)
    {
        List<Word> words = [];
        foreach (Match match in WordPattern().Matches(line))
        {
            char letter = char.ToUpperInvariant(match.Value[0]);
            string number = match.Value[1..];
            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                words.Add(new Word(letter, value));
            }
            else
            {
                diagnostics.Add(new GCodeDiagnostic(
                    lineNumber,
                    DiagnosticSeverity.Error,
                    "GCODE007",
                    $"'{match.Value}' is not a valid G-code word."));
            }
        }

        string remainder = WordPattern().Replace(line, string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);

        if (remainder.Length > 0)
        {
            diagnostics.Add(new GCodeDiagnostic(
                lineNumber,
                DiagnosticSeverity.Warning,
                "GCODE008",
                $"Unrecognised content was ignored: {remainder}"));
        }

        return words;
    }

    private static string RemoveComments(string line)
    {
        int semicolon = line.IndexOf(';');
        string withoutLineComment = semicolon >= 0 ? line[..semicolon] : line;
        return ParenthesisedCommentPattern().Replace(withoutLineComment, string.Empty);
    }

    private sealed record Word(char Letter, double Value);

    [GeneratedRegex(@"[A-Za-z][-+]?(?:\d+(?:\.\d*)?|\.\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesisedCommentPattern();
}
