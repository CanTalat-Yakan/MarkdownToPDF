namespace MarkdownToPDF.Services;

public interface IPdfService
{
    Task CreatePDFAsync(string html, ExportOptions options, CancellationToken ct);
}
