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

        // Compose head styles for TOC leaders
        var headExtras = new StringBuilder();
        headExtras.AppendLine("<style>");
        headExtras.AppendLine(".toc-list, .toc-list ol { list-style-type: none; }");
        headExtras.AppendLine(".toc-list { padding: 0; }");
        headExtras.AppendLine(".toc-list ol { padding-inline-start: 2ch; }");
        headExtras.AppendLine(".toc-list li > a { text-decoration: none; display: grid; grid-template-columns: auto max-content; align-items: end; }");
        headExtras.AppendLine(".toc-list li > a > .page { text-align: right; }");
        headExtras.AppendLine(".visually-hidden { clip: rect(0 0 0 0); clip-path: inset(100%); height: 1px; overflow: hidden; position: absolute; width: 1px; white-space: nowrap; }");
        headExtras.AppendLine(".toc-list li > a > .title { position: relative; overflow: hidden; }");

        var leadersSetting = (opts.TableOfContentsLeaders ?? "Dotted").Trim().ToUpperInvariant();
        if (leadersSetting == "NONE")
        {
            headExtras.AppendLine(".toc-list li > a .leaders::after { content: none; }");
        }
        else // DOTTED default: use long content so it reaches the page number reliably
        {
            headExtras.AppendLine(@".toc-list li > a .leaders::after {
    position: absolute;
    padding-inline-start: .25ch;
    content: "" . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . ""
        "". . . . . . . . . . . . . . . . . . . . . . . "";
    text-align: right;
}");
        }
        headExtras.AppendLine("</style>");

        var html = $"""
            <!DOCTYPE html>
            <html>
                <head>
                    <meta charset='utf-8'>{opts.HeadHtml + opts.BaseHeadHtml + headExtras}
                </head>
                <body>
                    {bodyHtml}
                </body>
            </html>
            """;
        return Task.FromResult(html);
    }
}