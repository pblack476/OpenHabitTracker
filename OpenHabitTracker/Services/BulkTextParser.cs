using System.Text.RegularExpressions;

namespace OpenHabitTracker.Services;

/// <summary>
/// Recognizes plain lines, bullet lists, ordered lists, and Markdown checklists.
/// Parsing is all-or-nothing: oversized input is rejected before writing data.
/// </summary>
public static partial class BulkTextParser
{
    public const int MaxItems = 250;
    public const int MaxTitleLength = 500;

    [GeneratedRegex(@"^(?:[-*•]\s+(?:\[[ xX]\]\s*)?|\d+[.)]\s+)", RegexOptions.Compiled)]
    private static partial Regex Prefix();

    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        List<string> titles = new();
        foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string title = Prefix().Replace(raw.Trim(), "").Trim();
            if (title.Length == 0)
                continue;

            if (title.Length > MaxTitleLength)
                throw new ArgumentException($"One title is longer than {MaxTitleLength} characters.");
            titles.Add(title);
            if (titles.Count > MaxItems)
                throw new ArgumentException($"A paste can contain at most {MaxItems} items.");
        }

        return titles;
    }
}
