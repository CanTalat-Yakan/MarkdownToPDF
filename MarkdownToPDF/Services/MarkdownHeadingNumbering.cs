using System.Text;
using System.Text.RegularExpressions;
namespace MarkdownToPDF.Services;

internal sealed class MarkdownHeadingNumbering
{
    internal sealed record HeaderDescriptor(int MarkdownLevel, int LogicalLevel, string Text, string Numbering, string Anchor);

    internal sealed record Result(string ProcessedMarkdown,
        IReadOnlyList<HeaderDescriptor> Headers,
        IReadOnlyList<HeadingInfo> PublicHeadings);

    private enum NumberingStyleKind { Numeric, LowerAlpha, UpperAlpha }

    public Result Process(string markdown, FormattingOptions opts, string tocPlaceholder)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        bool hasPlaceholder = lines.Contains(tocPlaceholder);

        int placeholderIndex = hasPlaceholder ? lines.IndexOf(tocPlaceholder) : -1;

        var headerRegex = new Regex(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);

        var counters = new int[6];
        string pattern = opts.HeaderNumberingPattern?.Trim() ?? "1.1.1";
        bool numberingEnabled = opts.AddHeaderNumbering && !string.IsNullOrWhiteSpace(pattern);
        bool trailingDot = pattern.EndsWith('.');
        var styleKind = DetermineStyleKind(pattern.TrimEnd('.'));

        var headers = new List<HeaderDescriptor>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == tocPlaceholder) continue;

            var m = headerRegex.Match(line);
            if (!m.Success) continue;

            bool suppressFirstFileHeaders = opts.AddTableOfContents &&
                                            opts.TableOfContentsAfterFirstFile &&
                                            hasPlaceholder &&
                                            placeholderIndex > -1 &&
                                            i < placeholderIndex;

            int markdownLevel = m.Groups[1].Value.Length;
            string rawText = m.Groups[2].Value.Trim();

            bool isSuperHeading = markdownLevel == 1;
            int logicalLevel = isSuperHeading ? 0 : markdownLevel - 1;

            if (!suppressFirstFileHeaders)
            {
                if (!isSuperHeading && numberingEnabled &&
                    (Regex.IsMatch(rawText, @"^[0-9]+(\.[0-9]+)+\.?\s") ||
                     Regex.IsMatch(rawText, @"^[A-Za-z]+(\.[A-Za-z]+)+\.?\s")))
                {
                    string anchorExisting = BuildAnchor(rawText, "", keepDots: false);
                    headers.Add(new HeaderDescriptor(markdownLevel, logicalLevel, rawText, "", anchorExisting));
                    continue;
                }
            }

            string numbering = "";

            if (!suppressFirstFileHeaders)
            {
                if (!isSuperHeading && numberingEnabled)
                {
                    counters[logicalLevel - 1]++;
                    for (int d = logicalLevel; d < counters.Length; d++) counters[d] = 0;
                    numbering = BuildNumbering(counters, logicalLevel, styleKind, trailingDot);
                }
                else if (isSuperHeading)
                {
                    for (int d = 0; d < counters.Length; d++) counters[d] = 0;
                }
            }

            string anchor = BuildAnchor(rawText, numbering, keepDots: false);

            if (!suppressFirstFileHeaders && numberingEnabled && !isSuperHeading && numbering.Length > 0)
                lines[i] = $"{new string('#', markdownLevel)} {numbering} {rawText} {{#{anchor}}}";
            else
                lines[i] = $"{new string('#', markdownLevel)} {rawText} {{#{anchor}}}";

            if (!suppressFirstFileHeaders)
                headers.Add(new HeaderDescriptor(markdownLevel, logicalLevel, rawText, numbering, anchor));
        }

        var publicHeadings = headers
            .Where(h => h.MarkdownLevel > 1)
            .Select(h => new HeadingInfo
            {
                Level = h.LogicalLevel,
                Text = string.IsNullOrEmpty(h.Numbering) ? h.Text : $"{h.Numbering} {h.Text}",
                Anchor = h.Anchor,
                Y = 0,
                Page = 0
            })
            .ToList();

        return new Result(string.Join('\n', lines), headers, publicHeadings);
    }

    private static NumberingStyleKind DetermineStyleKind(string patternNoTrailingDot)
    {
        char c = patternNoTrailingDot.FirstOrDefault(ch => ch != '.');
        if (char.IsDigit(c)) return NumberingStyleKind.Numeric;
        if (char.IsLetter(c) && char.IsUpper(c)) return NumberingStyleKind.UpperAlpha;
        return NumberingStyleKind.LowerAlpha;
    }

    private static string BuildNumbering(int[] counters, int logicalLevel, NumberingStyleKind kind, bool trailingDot)
    {
        var parts = new List<string>();
        for (int i = 0; i < logicalLevel; i++)
        {
            if (counters[i] == 0) break;
            parts.Add(kind switch
            {
                NumberingStyleKind.Numeric => counters[i].ToString(),
                NumberingStyleKind.LowerAlpha => ToAlpha(counters[i], false),
                NumberingStyleKind.UpperAlpha => ToAlpha(counters[i], true),
                _ => counters[i].ToString()
            });
        }
        var core = string.Join(".", parts);
        return trailingDot && core.Length > 0 ? core + "." : core;
    }

    private static string ToAlpha(int n, bool upper)
    {
        var sb = new StringBuilder();
        int num = n;
        while (num > 0)
        {
            num--;
            char ch = (char)('a' + (num % 26));
            sb.Insert(0, ch);
            num /= 26;
        }
        var result = sb.ToString();
        return upper ? result.ToUpperInvariant() : result;
    }

    private static string BuildAnchor(string headerText, string numbering, bool keepDots)
    {
        string numPart = numbering.TrimEnd('.');
        if (!keepDots) numPart = numPart.Replace('.', '-');
        string text = headerText.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s-]", "");
        text = Regex.Replace(text, @"\s+", "-");
        text = Regex.Replace(text, "-{2,}", "-").Trim('-');
        string anchor = string.IsNullOrEmpty(numPart) ? text : $"{numPart}-{text}";
        return anchor.Trim('-');
    }
}
