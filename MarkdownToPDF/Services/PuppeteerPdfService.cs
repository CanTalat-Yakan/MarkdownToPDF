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
            pdfOpts.DisplayHeaderFooter = true;

            string header = "<div></div>";
            string footer = "<div></div>";

            // Compute alignment + whether to place in header or footer
            (bool top, string align) = opts.PageNumberPosition switch
            {
                "TopLeft" => (true, "left"),
                "TopCenter" => (true, "center"),
                "TopRight" => (true, "right"),
                "BottomLeft" => (false, "left"),
                "BottomCenter" => (false, "center"),
                _ => (false, "right") // BottomRight default
            };

            string horizontalPadding = $"{opts.RightMarginMm.ToString("0.##", CultureInfo.InvariantCulture)}mm";
            string verticalLift = "4mm";

            string numberSpan = "<span class=\"pageNumber\"></span>";

            string block = $"""
                <div style='
                width:100%;
                font-size:12px;
                padding:{(top ? verticalLift : "0")} {horizontalPadding} {(top ? "0" : verticalLift)} {horizontalPadding};
                box-sizing:border-box;
                text-align:{align};
                font-family:Segoe UI,Arial,
                sans-serif;
                color:#444;'>
                {numberSpan}</div>
                """;

            if (top) header = block; else footer = block;

            pdfOpts.HeaderTemplate = header;
            pdfOpts.FooterTemplate = footer;
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