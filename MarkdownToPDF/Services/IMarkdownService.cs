using MarkdownToPDF.Models;

namespace MarkdownToPDF.Services;

public interface IMarkdownService
{
    Task<string> BuildCombinedHtmlAsync(
        IReadOnlyList<MarkdownFileModel> orderedFiles,
        FormattingOptions options,
        CancellationToken ct);

    IReadOnlyList<HeadingInfo> GetExtractedHeadings(); // Exposes latest extracted headings (H2+)
}
