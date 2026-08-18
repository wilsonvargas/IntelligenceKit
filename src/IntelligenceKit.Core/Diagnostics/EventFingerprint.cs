using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Diagnostics;

/// <summary>
/// Derives a stable grouping key ("fingerprint") for an event so that repeated
/// occurrences of the same problem collapse into a single issue.
///
/// For exceptions the key is <c>projectId + exceptionType + top stack frame</c>;
/// for everything else it falls back to <c>projectId + eventType + a normalized
/// message</c> (digits and GUIDs stripped) so that "User 12 not found" and
/// "User 34 not found" group together.
/// </summary>
public static partial class EventFingerprint
{
    public sealed record Result(string Fingerprint, string Title, string? Culprit);

    public static Result Compute(IntelligenceEvent e)
    {
        if (e.Exception is { } ex && !string.IsNullOrWhiteSpace(ex.Type))
        {
            var frame = TopFrame(ex.StackTrace);
            var title = ShortTypeName(ex.Type);
            var culprit = frame is null ? null : ShortFrame(frame);
            var fingerprint = Hash($"{e.ProjectId}\n{ex.Type}\n{frame}");
            return new Result(fingerprint, title, culprit);
        }

        var message = e.Message ?? string.Empty;
        var normalized = NormalizeMessage(message);
        var fallbackTitle = string.IsNullOrWhiteSpace(message)
            ? e.EventType.ToString()
            : Truncate(message, 120);

        return new Result(
            Hash($"{e.ProjectId}\n{e.EventType}\n{normalized}"),
            fallbackTitle,
            null);
    }

    /// <summary>First "at ..." frame of a stack trace, with the file/line suffix removed.</summary>
    private static string? TopFrame(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return null;

        foreach (var raw in stackTrace.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("at ", StringComparison.Ordinal))
                line = line[3..];

            // Drop the volatile " in <file>:line N" tail so line-number churn
            // doesn't split one problem into many issues.
            var inIndex = line.IndexOf(" in ", StringComparison.Ordinal);
            if (inIndex >= 0)
                line = line[..inIndex];

            line = line.Trim();
            if (line.Length > 0)
                return line;
        }

        return null;
    }

    /// <summary>Compact "Type.Method" form of a frame, for display as the culprit.</summary>
    private static string ShortFrame(string frame)
    {
        var withoutArgs = frame;
        var paren = withoutArgs.IndexOf('(');
        if (paren >= 0)
            withoutArgs = withoutArgs[..paren];

        var parts = withoutArgs.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[^2]}.{parts[^1]}"
            : withoutArgs;
    }

    private static string ShortTypeName(string type)
    {
        var lastDot = type.LastIndexOf('.');
        return lastDot >= 0 && lastDot < type.Length - 1 ? type[(lastDot + 1)..] : type;
    }

    private static string NormalizeMessage(string message)
    {
        var noGuids = GuidRegex().Replace(message, "#");
        return DigitsRegex().Replace(noGuids, "#");
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static string Hash(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}
