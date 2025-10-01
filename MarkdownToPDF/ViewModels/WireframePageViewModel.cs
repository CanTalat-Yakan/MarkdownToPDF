using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarkdownToPDF.Models;
using MarkdownToPDF.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MarkdownToPDF.ViewModels;

public sealed class WireframePageViewModel : ObservableObject
{
    private readonly IMarkdownService _mdService;
    private readonly IPdfService _pdfService;

    public ObservableCollection<BitmapImage> PreviewPages { get; } = new();

    // For backward compatibility (first selected file)
    public string? CurrentMarkdownPath { get; private set; }
    public string? CurrentMarkdownFileName => CurrentMarkdownPath is null ? null : Path.GetFileName(CurrentMarkdownPath);

    // New: full ordered selection
    public IReadOnlyList<string> CurrentMarkdownPaths { get; private set; } = Array.Empty<string>();

    public bool CanExport => CurrentMarkdownPaths.Count > 0;

    private string? _currentHtml;

    // Centralized options for both preview and export
    public FormattingOptions Formatting { get; } = new()
    {
        UseAdvancedExtensions = true,
        UsePipeTables = true,
        UseAutoLinks = true,
        BodyMarginPx = 0,
        BaseFontFamily = "Segoe UI, sans-serif",
        // When selecting multiple files, ensure page breaks between them
        InsertPageBreaksBetweenFiles = true
    };

    public ExportOptions Export { get; } = new()
    {
        PaperFormat = "A4",
        Landscape = false,
        PrintBackground = true,
        PreferCssPageSize = true,

        TopMarginMm = 25.4,
        RightMarginMm = 25.4,
        BottomMarginMm = 25.4,
        LeftMarginMm = 25.4,

        PreviewDestinationWidthPx = 794,
        PreviewDpi = 96
    };

    public WireframePageViewModel(IMarkdownService mdService, IPdfService pdfService)
    {
        _mdService = mdService;
        _pdfService = pdfService;
    }

    // Backward-compatible single-file entry point
    public async Task LoadFromFileAsync(string markdownPath, CancellationToken ct = default)
    {
        await LoadFromFilesAsync(new[] { markdownPath }, ct);
    }

    // New: multi-file support (preserves order)
    public async Task LoadFromFilesAsync(IReadOnlyList<string> markdownPaths, CancellationToken ct = default)
    {
        CurrentMarkdownPaths = (markdownPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        CurrentMarkdownPath = CurrentMarkdownPaths.FirstOrDefault();

        // Apply base CSS for consistent preview/PDF
        ApplyBaseHeadCss();

        var models = CurrentMarkdownPaths.Select(p => new MarkdownFileModel(p)).ToArray();

        _currentHtml = await _mdService.BuildCombinedHtmlAsync(models, Formatting, ct);

        // Build a temporary A4 PDF and render its pages as images
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        var previewExport = BuildExportOptions(tempPdfPath);

        await _pdfService.CreatePDFAsync(_currentHtml, previewExport, ct);

        var pdfFile = await StorageFile.GetFileFromPathAsync(tempPdfPath);
        await RenderPdfPagesAsync(pdfFile);
    }

    public async Task ExportToAsync(string outputPdfPath, CancellationToken ct = default)
    {
        if (CurrentMarkdownPaths.Count == 0)
            throw new InvalidOperationException("No markdown files loaded.");

        // Ensure same CSS for export
        ApplyBaseHeadCss();

        var html = _currentHtml ?? await _mdService.BuildCombinedHtmlAsync(
            CurrentMarkdownPaths.Select(p => new MarkdownFileModel(p)).ToArray(),
            Formatting,
            ct);

        var export = BuildExportOptions(outputPdfPath);

        await _pdfService.CreatePDFAsync(html, export, ct);
    }

    private async Task RenderPdfPagesAsync(StorageFile pdfFile)
    {
        PreviewPages.Clear();

        var doc = await PdfDocument.LoadFromFileAsync(pdfFile);
        for (uint i = 0; i < doc.PageCount; i++)
        {
            using var page = doc.GetPage(i);
            using IRandomAccessStream stream = new InMemoryRandomAccessStream();

            // Render to configured A4-like width in pixels for preview
            var opts = new PdfPageRenderOptions { DestinationWidth = (uint)Export.PreviewDestinationWidthPx };
            await page.RenderToStreamAsync(stream, opts);

            stream.Seek(0);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream);
            PreviewPages.Add(bmp);
        }
    }

    private void ApplyBaseHeadCss()
    {
        Formatting.AdditionalHeadHtml = $@"
            <style>
                :root {{ --mtpdf-border-color: #d0d7de; }}
                body {{ font-family:{Formatting.BaseFontFamily}; margin:{Formatting.BodyMarginPx}px; }}
                img {{ max-width:100%; }}
                pre {{ overflow:auto; }}
                table {{ border-collapse: collapse; border-spacing: 0; width: 100%; }}
                table, th, td {{ border: 1px solid var(--mtpdf-border-color); }}
                th, td {{ padding: 6px 8px; vertical-align: top; }}
                thead th {{ background: #f6f8fa; }}
                tbody tr:nth-child(even) td {{ background:#fbfbfb; }}
                /* prevent overflow from breaking layout */
                td, th {{ word-break: break-word; }}
            </style>";
    }

    private ExportOptions BuildExportOptions(string outputPath)
    {
        return new ExportOptions
        {
            OutputPath = outputPath,
            PaperFormat = Export.PaperFormat,
            Landscape = Export.Landscape,
            PrintBackground = Export.PrintBackground,
            PreferCssPageSize = Export.PreferCssPageSize,
            TopMarginMm = Export.TopMarginMm,
            RightMarginMm = Export.RightMarginMm,
            BottomMarginMm = Export.BottomMarginMm,
            LeftMarginMm = Export.LeftMarginMm,
            ShowPageNumbers = Export.ShowPageNumbers,
            PreviewDestinationWidthPx = Export.PreviewDestinationWidthPx,
            PreviewDpi = Export.PreviewDpi
        };
    }
}
