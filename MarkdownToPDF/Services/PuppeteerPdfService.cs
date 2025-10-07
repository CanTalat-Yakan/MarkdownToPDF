using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Globalization;

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
            // Footer: page number only, lower-right, horizontally aligned with page content edge.
            // We mimic body horizontal inset by padding-right = RightMarginMm.
            // Add a small bottom padding (4mm) to pull it upward for readability while respecting the PDF bottom margin.
            string rightPad = $"{opts.RightMarginMm.ToString("0.##", CultureInfo.InvariantCulture)}mm";
            const double footerLiftMm = 4; // visual lift inside the margin box
            string bottomPad = $"{footerLiftMm.ToString("0.##", CultureInfo.InvariantCulture)}mm";

            pdfOpts.DisplayHeaderFooter = true;
            pdfOpts.HeaderTemplate = "<div></div>";
            pdfOpts.FooterTemplate =
                $"<div style='width:100%; font-size:12px; " +
                $"padding:0 {rightPad} {bottomPad} 0; box-sizing:border-box; " +
                $"text-align:right; font-family:Segoe UI, Arial, sans-serif; color:#444;'>"
                + "<span class=\"pageNumber\"></span>"
                + "</div>";
        }

        await page.PdfAsync(opts.OutputPath, pdfOpts);
    }

    private static MarginOptions BuildMargins(ExportOptions opts)
    {
        static string MmToString(double mm) =>
            mm.ToString("0.##", CultureInfo.InvariantCulture) + "mm";

        return new MarginOptions
        {
            Top = MmToString(opts.TopMarginMm),
            Right = MmToString(opts.RightMarginMm),
            Bottom = MmToString(opts.BottomMarginMm),
            Left = MmToString(opts.LeftMarginMm)
        };
    }
}