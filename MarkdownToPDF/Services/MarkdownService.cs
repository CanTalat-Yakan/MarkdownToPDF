using System.Text;
using Markdig;

namespace MarkdownToPDF.Services;

public sealed class MarkdownService : IMarkdownService
{
    private const string TocPlaceholder = "<!--__TOC_PLACEHOLDER__-->";
    private const string PageBreakHtml = "<div style='page-break-after: always;'></div>";

    private readonly MarkdownHeadingNumbering _headingNumbering = new();
    private readonly MarkdownTableOfContentsGenerator _tocGenerator = new();

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

        string combinedMarkdown = sb.ToString();

        // Always process headings so we can extract them for the hierarchy tree.
        var processed = _headingNumbering.Process(combinedMarkdown, opts, TocPlaceholder);
        _extractedHeadings = processed.PublicHeadings.ToList();

        string working = processed.ProcessedMarkdown;

        // Only insert a TOC block if requested and headers were found
        if (opts.AddTableOfContents && processed.Headers.Count > 0)
        {
            var lines = working.Replace("\r\n", "\n").Split('\n').ToList();
            int placeholderIndex = lines.IndexOf(TocPlaceholder);

            if (placeholderIndex >= 0)
            {
                string tocBlock = _tocGenerator.Build(processed.Headers, opts, PageBreakHtml);
                var tocLines = tocBlock.Replace("\r\n", "\n").Split('\n');
                lines.RemoveAt(placeholderIndex);
                lines.InsertRange(placeholderIndex, tocLines);
            }
            else
            {
                string tocBlock = _tocGenerator.Build(processed.Headers, opts, PageBreakHtml);
                lines.Insert(0, ""); // blank after TOC
                lines.Insert(0, tocBlock);
            }

            working = string.Join('\n', lines);
        }

        // If numbering is not requested, working still contains anchors but without numbering.
        combinedMarkdown = working;

        var builder = new MarkdownPipelineBuilder();
        if (opts.UseAdvancedExtensions) builder = builder.UseAdvancedExtensions();
        if (opts.UsePipeTables) builder = builder.UsePipeTables();
        if (opts.UseAutoLinks) builder = builder.UseAutoLinks();
        var pipeline = builder.Build();

        var bodyHtml = Markdig.Markdown.ToHtml(combinedMarkdown, pipeline);
        var html = $"""
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset='utf-8'>{opts.HeadHtml + opts.BaseHeadHtml}
                </head>
                <body>
                    {bodyHtml}
                </body>
            </html>
            """;
        return Task.FromResult(html);
    }
}