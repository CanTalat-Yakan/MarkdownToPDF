using System.Globalization;
using Microsoft.Web.WebView2.Core;

namespace MarkdownToPDF.Services;

public sealed class WebView2PdfService : IPdfService
{
    private readonly IMarkdownService _markdownService;
    public WebView2PdfService(IMarkdownService markdownService) => _markdownService = markdownService;

    public async Task CreatePDFAsync(string html, ExportOptions opts, CancellationToken ct)
    {
        // Use a per-user, writable WebView2 profile (works in MSIX sandboxes)
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarkdownToPDF", "WebView2Profile");
        Directory.CreateDirectory(userDataFolder);

        var options = new CoreWebView2EnvironmentOptions()
        {
            AllowSingleSignOnUsingOSPrimaryAccount = true,
        };
        var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder: userDataFolder, options);

        CoreWebView2Controller? controller = null;
        try
        {
            var webParent = CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)App.Hwnd);
            var webOptions = env.CreateCoreWebView2ControllerOptions();
            controller = await env.CreateCoreWebView2ControllerAsync(webParent, webOptions);
            controller.IsVisible = false; // run offscreen
            controller.Bounds = new Windows.Foundation.Rect { X = 0, Y = 0, Width = 1, Height = 1 };

            var core = controller.CoreWebView2;
            // Harden settings
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            // Navigate to our HTML string and wait for DOMContentLoaded
            var domLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void DomHandler(object? s, object e)
            {
                core.DOMContentLoaded -= DomHandler;
                domLoadedTcs.TrySetResult(true);
            }
            core.DOMContentLoaded += DomHandler;
            core.NavigateToString(html);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
            await Task.WhenAny(domLoadedTcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
            timeoutCts.Token.ThrowIfCancellationRequested();

            // Build print settings
            var settings = env.CreatePrintSettings();
            settings.Orientation = opts.Landscape ? CoreWebView2PrintOrientation.Landscape : CoreWebView2PrintOrientation.Portrait;
            settings.ShouldPrintBackgrounds = opts.PrintBackground;
            // Map margins (mm -> inches)
            settings.MarginTop = MmToInches(opts.TopMarginMm);
            settings.MarginRight = MmToInches(opts.RightMarginMm);
            settings.MarginBottom = MmToInches(opts.BottomMarginMm);
            settings.MarginLeft = MmToInches(opts.LeftMarginMm);

            // Header/footer: WebView2 can only show default header/footer. Use this when page numbers are requested.
            settings.ShouldPrintHeaderAndFooter = opts.ShowPageNumbers;
            if (opts.ShowPageNumbers)
            {
                // Use document title as header; leave FooterUri empty to avoid noisy URL text
                settings.HeaderTitle = ""; // keep clean header
                settings.FooterUri = "";
            }

            // Paper format
            // WebView2 currently uses default paper unless CustomPaperSize is set.
            // Provide common sizes (in inches) for A3/A4/Letter.
            (double wIn, double hIn) = opts.PaperFormat?.Trim().ToUpperInvariant() switch
            {
                "A3" => (11.69, 16.54), // 297 x 420 mm
                "LETTER" => (8.5, 11.0),
                _ => (8.27, 11.69) // A4 210 x 297 mm
            };
            if (opts.Landscape) (wIn, hIn) = (hIn, wIn);
            settings.PageWidth = wIn;
            settings.PageHeight = hIn;

            // Create PDF
            await core.PrintToPdfAsync(opts.OutputPath, settings);

            // Assign actual page numbers by inspecting the produced PDF
            var headings = _markdownService.GetExtractedHeadings();
            PdfHeadingPageResolver.AssignPages(opts.OutputPath, (IList<HeadingInfo>)headings);

            // Inject outline with resolved pages
            PdfOutlineWriter.InjectOutline(opts.OutputPath, headings);

            // Remove first page footer if requested (no effect when header/footer disabled)
            if (!opts.ShowPageNumberOnFirstPage)
                PdfFirstPageFooterRewriter.ClearFooterOnFirstPage(opts.OutputPath, opts.BottomMarginMm);
        }
        finally
        {
            try { controller?.Close(); } catch { }
        }
    }

    private static double MmToInches(double mm) => mm / 25.4;
}
