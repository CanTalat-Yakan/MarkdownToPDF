using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    public string? CurrentMarkdownPath { get; private set; }
    public string? CurrentMarkdownFileName => CurrentMarkdownPath is null ? null : Path.GetFileName(CurrentMarkdownPath);
    public IReadOnlyList<string> CurrentMarkdownPaths { get; private set; } = Array.Empty<string>();
    public bool CanExport => CurrentMarkdownPaths.Count > 0;

    private string? _currentHtml;

    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            var clamped = value;
            if (clamped < 1) clamped = 1;
            if (PreviewPages.Count > 0 && clamped > PreviewPages.Count) clamped = PreviewPages.Count;
            if (PreviewPages.Count == 0) clamped = 0;
            if (SetProperty(ref _currentPage, clamped))
                OnPropertyChanged(nameof(PageIndicator));
        }
    }

    public int TotalPages => PreviewPages.Count;
    public string PageIndicator => TotalPages == 0 ? "0 / 0" : $"{CurrentPage} / {TotalPages}";

    public FormattingOptions Formatting { get; } = new()
    {
        UseAdvancedExtensions = true,
        UsePipeTables = true,
        UseAutoLinks = true,
        BodyMarginPx = 0,
        BodyFontSizePx = 12,
        BaseFontFamily = "Segoe UI, sans-serif",
        InsertPageBreaksBetweenFiles = false,
        BodyTextAlignment = "Justify"
    };

    public ExportOptions Export { get; } = new()
    {
        PaperFormat = "A4",
        Landscape = false,
        PrintBackground = true,
        ShowPageNumbers = true,
        PageNumberPosition = "BottomRight",
        TopMarginMm = 25.4,
        RightMarginMm = 25.4,
        BottomMarginMm = 25.4,
        LeftMarginMm = 25.4,
        PreviewDestinationWidthPx = 794,
        PreviewDpi = 96,
    };

    // Dynamic preview page dimensions based on selected paper format + orientation + preview DPI.
    public int PagePreviewWidthPx => ComputePagePixelSize().widthPx;
    public int PagePreviewHeightPx => ComputePagePixelSize().heightPx;

    public WireframePageViewModel(IMarkdownService mdService, IPdfService pdfService)
    {
        _mdService = mdService;
        _pdfService = pdfService;
        PreviewPages.CollectionChanged += PreviewPages_CollectionChanged;
    }

    private void PreviewPages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (PreviewPages.Count == 0)
            CurrentPage = 0;
        else if (CurrentPage == 0)
            CurrentPage = 1;

        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(TotalPages));
    }

    public async Task LoadFromFileAsync(string markdownPath, CancellationToken ct = default)
        => await LoadFromFilesAsync(new[] { markdownPath }, ct);

    public async Task LoadFromFilesAsync(IReadOnlyList<string> markdownPaths, CancellationToken ct = default)
    {
        CurrentMarkdownPaths = (markdownPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        CurrentMarkdownPath = CurrentMarkdownPaths.FirstOrDefault();
        await RebuildPreviewAsync(ct);
    }

    public async Task ApplySettingsAsync(FormattingOptions newFormatting, ExportOptions newExport, CancellationToken ct = default)
    {
        var layoutChanged =
            Export.Landscape != newExport.Landscape ||
            !string.Equals(Export.PaperFormat, newExport.PaperFormat, StringComparison.OrdinalIgnoreCase) ||
            Export.PreviewDpi != newExport.PreviewDpi;

        // Formatting
        Formatting.UseAdvancedExtensions = newFormatting.UseAdvancedExtensions;
        Formatting.UsePipeTables = newFormatting.UsePipeTables;
        Formatting.UseAutoLinks = newFormatting.UseAutoLinks;
        Formatting.InsertPageBreaksBetweenFiles = newFormatting.InsertPageBreaksBetweenFiles;
        Formatting.BaseFontFamily = newFormatting.BaseFontFamily;
        Formatting.BodyMarginPx = newFormatting.BodyMarginPx;
        Formatting.BodyFontSizePx = newFormatting.BodyFontSizePx;
        Formatting.BodyTextAlignment = newFormatting.BodyTextAlignment;

        // Export
        Export.PaperFormat = newExport.PaperFormat;
        Export.Landscape = newExport.Landscape;
        Export.PrintBackground = newExport.PrintBackground;
        Export.TopMarginMm = newExport.TopMarginMm;
        Export.RightMarginMm = newExport.RightMarginMm;
        Export.BottomMarginMm = newExport.BottomMarginMm;
        Export.LeftMarginMm = newExport.LeftMarginMm;
        Export.ShowPageNumbers = newExport.ShowPageNumbers;
        Export.PageNumberPosition = newExport.PageNumberPosition;
        Export.PreviewDpi = newExport.PreviewDpi;

        if (layoutChanged)
        {
            // Recalculate preview width used for PDF page rendering -> affects clarity of preview images.
            Export.PreviewDestinationWidthPx = PagePreviewWidthPx;
            OnPropertyChanged(nameof(PagePreviewWidthPx));
            OnPropertyChanged(nameof(PagePreviewHeightPx));
        }

        await RebuildPreviewAsync(ct);
    }

    private (int widthPx, int heightPx) ComputePagePixelSize()
    {
        // Sizes in millimeters for supported formats (portrait orientation baseline)
        // A4: 210 x 297 mm, A3: 297 x 420 mm, Letter: 215.9 x 279.4 mm
        var (wMm, hMm) = GetPaperSizeMm(Export.PaperFormat);
        if (Export.Landscape)
            (wMm, hMm) = (hMm, wMm);

        double dpi = Export.PreviewDpi <= 0 ? 96 : Export.PreviewDpi;

        // Convert mm -> inches then -> pixels
        double wPx = (wMm / 25.4d) * dpi;
        double hPx = (hMm / 25.4d) * dpi;

        return (widthPx: (int)Math.Round(wPx, MidpointRounding.AwayFromZero),
                heightPx: (int)Math.Round(hPx, MidpointRounding.AwayFromZero));
    }

    private static (double wMm, double hMm) GetPaperSizeMm(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return (210, 297); // default A4

        switch (format.Trim().ToUpperInvariant())
        {
            case "A3": return (297, 420);
            case "LETTER": return (215.9, 279.4); // 8.5 x 11 in
            case "A4":
            default: return (210, 297);
        }
    }

    private async Task RebuildPreviewAsync(CancellationToken ct)
    {
        if (CurrentMarkdownPaths.Count == 0)
        {
            PreviewPages.Clear();
            _currentHtml = null;
            CurrentPage = 0;
            return;
        }

        ApplyBaseHeadCss();

        var models = CurrentMarkdownPaths.Select(p => new MarkdownFileModel(p)).ToArray();
        _currentHtml = await _mdService.BuildCombinedHtmlAsync(models, Formatting, ct);

        // temp PDF for preview
        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        var previewExport = BuildExportOptions(tempPdfPath);

        await _pdfService.CreatePDFAsync(_currentHtml, previewExport, ct);
        var pdfFile = await StorageFile.GetFileFromPathAsync(tempPdfPath);
        await RenderPdfPagesAsync(pdfFile);
        CurrentPage = PreviewPages.Count > 0 ? 1 : 0;
    }

    public async Task ExportToAsync(string outputPdfPath, CancellationToken ct = default)
    {
        if (CurrentMarkdownPaths.Count == 0)
            throw new InvalidOperationException("No markdown files loaded.");

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
        var align = (Formatting.BodyTextAlignment ?? "Justify").Trim();
        string paragraphRule = align.ToLowerInvariant() switch
        {
            "left" => "p, li { text-align: left; }",
            "center" => "p, li { text-align: center; }",
            "right" => "p, li { text-align: right; }",
            _ => "p, li { text-align: justify; text-justify: inter-word; hyphens: auto; }"
        };

        Formatting.AdditionalHeadHtml = $@"
            <style>
                :root {{ --mtpdf-border-color: #d0d7de; }}
                body {{ font-family:{Formatting.BaseFontFamily}; font-size:{Formatting.BodyFontSizePx}px; margin:{Formatting.BodyMarginPx}px; }}
                {paragraphRule}
                h1, h2, h3, h4, h5, h6, pre, code {{ text-align: left; }}
                img {{ max-width:100%; }}
                pre {{ overflow:auto; }}
                table {{ border-collapse: collapse; border-spacing: 0; width: 100%; }}
                table, th, td {{ border: 1px solid var(--mtpdf-border-color); }}
                th, td {{ padding: 6px 8px; vertical-align: top; }}
                thead th {{ background: #f6f8fa; }}
                tbody tr:nth-child(even) td {{ background:#fbfbfb; }}
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
            ShowPageNumbers = Export.ShowPageNumbers,
            PageNumberPosition = Export.PageNumberPosition,
            TopMarginMm = Export.TopMarginMm,
            RightMarginMm = Export.RightMarginMm,
            BottomMarginMm = Export.BottomMarginMm,
            LeftMarginMm = Export.LeftMarginMm,
            PreviewDestinationWidthPx = PagePreviewWidthPx,
            PreviewDpi = Export.PreviewDpi,
        };
    }
}
