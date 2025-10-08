using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using MarkdownToPDF.Models;

namespace MarkdownToPDF.Services;

public sealed class MarkdownService : IMarkdownService
{
    private sealed record HeaderInfo(int MarkdownLevel, int LogicalLevel, string Text, string Numbering, string Anchor);

    private const string TocPlaceholder = "<!--__TOC_PLACEHOLDER__-->";
    private const string PageBreakHtml = "<div style='page-break-after: always;'></div>";

    private List<HeadingInfo> _extractedHeadings = new();
    public IReadOnlyList<HeadingInfo> GetExtractedHeadings() => _extractedHeadings;

    public Task<string> BuildCombinedHtmlAsync(IReadOnlyList<MarkdownFileModel> orderedFiles,
        FormattingOptions opts, CancellationToken ct)
    {
        var sb = new StringBuilder();

        if (orderedFiles.Count > 0)
        {
            var firstMd = File.ReadAllText(orderedFiles[0].FilePath, Encoding.UTF8);
            sb.AppendLine(firstMd);

            if (opts.AddTableOfContents && opts.TableOfContentsAfterFirstFile)
            {
                sb.AppendLine(PageBreakHtml);
                sb.AppendLine();
                sb.AppendLine(TocPlaceholder);
                sb.AppendLine();
            }
        }

        for (int i = 1; i < orderedFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var md = File.ReadAllText(orderedFiles[i].FilePath, Encoding.UTF8);
            sb.AppendLine(md);

            if (opts.InsertPageBreaksBetweenFiles && i < orderedFiles.Count - 1)
            {
                sb.AppendLine(PageBreakHtml);
                sb.AppendLine();
            }
        }

        var combinedMarkdown = sb.ToString();

        if (opts.AddHeaderNumbering || opts.AddTableOfContents)
            combinedMarkdown = ProcessHeadersAndToc(combinedMarkdown, opts);
        else
            _extractedHeadings = new(); // no headers processed

        var builder = new MarkdownPipelineBuilder();
        if (opts.UseAdvancedExtensions) builder = builder.UseAdvancedExtensions();
        if (opts.UsePipeTables) builder = builder.UsePipeTables();
        if (opts.UseAutoLinks) builder = builder.UseAutoLinks();
        var pipeline = builder.Build();

        var bodyHtml = Markdown.ToHtml(combinedMarkdown, pipeline);
        var html = $"""
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset='utf-8'>{opts.AdditionalHeadHtml}
                </head>
                <body>
                    {bodyHtml}
                </body>
            </html>
            """;
        return Task.FromResult(html);
    }

    private string ProcessHeadersAndToc(string markdown, FormattingOptions opts)
    {
        bool hasPlaceholder = markdown.Contains(TocPlaceholder, StringComparison.Ordinal);
        var lines = markdown.Replace("\r\n", "\n").Split('\n').ToList();
        int placeholderIndex = hasPlaceholder ? lines.IndexOf(TocPlaceholder) : -1;

        var headerRegex = new Regex(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);

        var counters = new int[6];
        string pattern = opts.HeaderNumberingPattern?.Trim() ?? "1.1.1";
        bool numberingEnabled = opts.AddHeaderNumbering && !string.IsNullOrWhiteSpace(pattern);
        bool trailingDot = pattern.EndsWith('.');
        var styleKind = DetermineStyleKind(pattern.TrimEnd('.'));
        var headers = new List<HeaderInfo>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == TocPlaceholder) continue;

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
                    headers.Add(new HeaderInfo(markdownLevel, logicalLevel, rawText, "", anchorExisting));
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
                headers.Add(new HeaderInfo(markdownLevel, logicalLevel, rawText, numbering, anchor));
        }

        // Project headers to public HeadingInfo (H2+ only; H2 => Level 1)
        _extractedHeadings = headers
            .Where(h => h.MarkdownLevel > 1)
            .Select(h => new HeadingInfo
            {
                Level = h.LogicalLevel, // H2 => 1
                Text = string.IsNullOrEmpty(h.Numbering) ? h.Text : $"{h.Numbering} {h.Text}",
                Anchor = h.Anchor,
                Y = 0,
                Page = 0
            })
            .ToList();

        if (opts.AddTableOfContents && headers.Count > 0)
        {
            var tocBlock = BuildTableOfContents(headers, opts);
            if (hasPlaceholder)
            {
                var idx = lines.FindIndex(l => l == TocPlaceholder);
                if (idx >= 0)
                {
                    var tocLines = tocBlock.Replace("\r\n", "\n").Split('\n');
                    lines.RemoveAt(idx);
                    lines.InsertRange(idx, tocLines);
                }
            }
            else
            {
                lines.Insert(0, "");
                lines.Insert(0, tocBlock);
            }
        }

        return string.Join('\n', lines);
    }

    private enum NumberingStyleKind { Numeric, LowerAlpha, UpperAlpha }

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

    private string BuildTableOfContents(IEnumerable<HeaderInfo> headers, FormattingOptions opts)
    {
        string headerText = string.IsNullOrWhiteSpace(opts.TableOfContentsHeaderText)
            ? "Table of Contents"
            : opts.TableOfContentsHeaderText.Trim();

        var sb = new StringBuilder();
        sb.AppendLine($"## {headerText}");
        bool numbered = string.Equals(opts.TableOfContentsBulletStyle, "1.", StringComparison.Ordinal);

        foreach (var h in headers)
        {
            if (h.MarkdownLevel == 1) continue;
            int indentLevel = h.LogicalLevel <= 1 ? 0 : h.LogicalLevel - 1;
            string indent = opts.IndentTableOfContents
                ? new string(' ', indentLevel * 2)
                : string.Empty;
            string labelCore = string.IsNullOrEmpty(h.Numbering) ? h.Text : $"{h.Numbering} {h.Text}";
            string bullet = numbered ? "1." : "-";
            sb.AppendLine($"{indent}{bullet} [{labelCore}](#{h.Anchor})");
        }
        sb.AppendLine();
        sb.AppendLine(PageBreakHtml);
        sb.AppendLine();
        return sb.ToString().TrimEnd('\r', '\n');
    }
}