using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace MarkdownToPDF.Services;

public sealed class PuppeteerPdfService : IPdfService
{
    public async Task CreatePDFAsync(string html, ExportOptions opts, CancellationToken ct)
    {
        await new BrowserFetcher().DownloadAsync();
        var launchOptions = new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox" } };
        using var browser = await Puppeteer.LaunchAsync(launchOptions);
        using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);

        var pdfOpts = new PdfOptions
        {
            Format = opts.PaperFormat switch { "A3" => PaperFormat.A3, "A4" => PaperFormat.A4, "Letter" => PaperFormat.Letter, _ => PaperFormat.A4 },
            Landscape = opts.Landscape,
            PrintBackground = opts.PrintBackground,
            PreferCSSPageSize = opts.PreferCssPageSize,
            MarginOptions = BuildMargins(opts)
        };

        if (opts.ShowPageNumbers)
        {
            pdfOpts.DisplayHeaderFooter = true;
            pdfOpts.FooterTemplate = "<div style='font-size:10px;text-align:center'>Page <span class='pageNumber'></span> of <span class='totalPages'></span></div>";
            pdfOpts.HeaderTemplate = "<div></div>";
        }

        await page.PdfAsync(opts.OutputPath, pdfOpts);
    }

    private static MarginOptions BuildMargins(ExportOptions opts)
    {
        static string MmToString(double mm) => mm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "mm";
        return new MarginOptions
        {
            Top = MmToString(opts.TopMarginMm),
            Right = MmToString(opts.RightMarginMm),
            Bottom = MmToString(opts.BottomMarginMm),
            Left = MmToString(opts.LeftMarginMm)
        };
    }
}