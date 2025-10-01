using System.Text;
using Markdig;

namespace MarkdownToPDF.Services;

public sealed class MarkdownService : IMarkdownService
{
    public Task<string> BuildCombinedHtmlAsync(IReadOnlyList<MarkdownFileModel> orderedFiles,
        FormattingOptions opts, CancellationToken ct)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < orderedFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var md = File.ReadAllText(orderedFiles[i].FilePath, Encoding.UTF8);
            sb.AppendLine(md);

            if (opts.InsertPageBreaksBetweenFiles && i < orderedFiles.Count - 1)
            {
                // Insert a page-break and then a blank line to terminate the HTML block.
                sb.AppendLine("<div style='page-break-after: always;'></div>");
                sb.AppendLine(); // critical: blank line so following markdown is parsed normally
            }
        }

        var builder = new MarkdownPipelineBuilder();
        if (opts.UseAdvancedExtensions) builder = builder.UseAdvancedExtensions();
        if (opts.UsePipeTables) builder = builder.UsePipeTables();
        if (opts.UseAutoLinks) builder = builder.UseAutoLinks();
        var pipeline = builder.Build();

        var bodyHtml = Markdown.ToHtml(sb.ToString(), pipeline);
        var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'>{opts.AdditionalHeadHtml}</head><body>{bodyHtml}</body></html>";
        return Task.FromResult(html);
    }
}