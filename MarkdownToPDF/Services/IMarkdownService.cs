namespace MarkdownToPDF.Services;

public interface IMarkdownService
{
    Task<string> BuildCombinedHtmlAsync(
        IReadOnlyList<MarkdownFileModel> orderedFiles,
        FormattingOptions options,
        CancellationToken ct);
}
