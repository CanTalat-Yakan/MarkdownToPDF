using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Globalization;

namespace MarkdownToPDF.Services;

public sealed class PuppeteerPdfService : IPdfService
{
    private readonly IMarkdownService _markdownService;
    public PuppeteerPdfService(IMarkdownService markdownService) => _markdownService = markdownService;

    public async Task CreatePDFAsync(string html, ExportOptions opts, CancellationToken ct)
    {
        // Prefer using an already installed Chromium (Edge/Chrome). Fall back to download only if needed.
        var launchOptions = new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-dev-shm-usage", "--allow-file-access-from-files" }
        };

        string? executablePath = TryFindInstalledChromium();
        if (!string.IsNullOrEmpty(executablePath))
        {
            launchOptions.ExecutablePath = executablePath;
        }
        else
        {
            // As a fallback, try to download a local Chromium copy to a writable location under LocalAppData.
            // This can take a long time or be blocked by firewalls; we do it only if no installed browser is found.
            try
            {
                var fetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "MarkdownToPDF", "puppeteer")
                });
                await fetcher.DownloadAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Unable to locate Edge/Chrome and Chromium download failed. Ensure Microsoft Edge or Google Chrome is installed, or allow Chromium download.", ex);
            }
        }

        try
        {
            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            using var page = await browser.NewPageAsync();

            // Ensure print media CSS is applied (helps consistent layout)
            await page.EmulateMediaTypeAsync(PuppeteerSharp.Media.MediaType.Print);

            // Be conservative with waits to avoid indefinite hangs on external resources
            page.DefaultNavigationTimeout = 60000; // 60s
            page.DefaultTimeout = 60000; // 60s

            // Load HTML into the page. Use DOMContentLoaded to avoid waiting for slow external assets.
            var navOpts = new NavigationOptions
            {
                Timeout = 60000,
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }
            };
            await page.SetContentAsync(html, navOpts);

            var pdfOpts = new PdfOptions
            {
                Format = opts.PaperFormat switch
                {
                    "A3" => PaperFormat.A3,
                    "A4" => PaperFormat.A4,
                    "Letter" => PaperFormat.Letter,
                    _ => PaperFormat.A4
                },
                Landscape = opts.Landscape,
                PrintBackground = opts.PrintBackground,
                MarginOptions = BuildMargins(opts)
            };

            if (opts.ShowPageNumbers)
            {
                pdfOpts.DisplayHeaderFooter = true;

                string header = "<div></div>";
                string footer = "<div></div>";

                (bool top, string align) = opts.PageNumberPosition switch
                {
                    "Top Left" => (true, "left"),
                    "Top Center" => (true, "center"),
                    "Top Right" => (true, "right"),
                    "Bottom Left" => (false, "left"),
                    "Bottom Center" => (false, "center"),
                    _ => (false, "right")
                };

                string horizontalPadding = $"{opts.RightMarginMm.ToString("0.##", CultureInfo.InvariantCulture)}mm";
                string verticalLift = "4mm";

                string numberContent = "<span class=\"pageNumber\"></span>";

                string block = $"""
                <div style='
                width:100%;
                font-size:12px;
                padding:{(top ? verticalLift : "0")} {horizontalPadding} {(top ? "0" : verticalLift)} {horizontalPadding};
                box-sizing:border-box;
                text-align:{align};
                font-family:Segoe UI,Arial,sans-serif;
                color:#444;'>
                {numberContent}</div>
                """;

                if (top) header = block; else footer = block;

                pdfOpts.HeaderTemplate = header;
                pdfOpts.FooterTemplate = footer;
            }

            // Generate the PDF first
            await page.PdfAsync(opts.OutputPath, pdfOpts);

            // Assign actual page numbers by inspecting the produced PDF
            var headings = _markdownService.GetExtractedHeadings();
            PdfHeadingPageResolver.AssignPages(opts.OutputPath, (IList<HeadingInfo>)headings);

            // Inject outline with resolved pages
            PdfOutlineWriter.InjectOutline(opts.OutputPath, headings);

            // Finally, replace the first page's footer
            if (!opts.ShowPageNumberOnFirstPage)
                PdfFirstPageFooterRewriter.ClearFooterOnFirstPage(opts.OutputPath, opts.BottomMarginMm);
        }
        catch (OperationCanceledException)
        {
            throw; // bubble cancellations
        }
        catch (Exception ex)
        {
            // Surface a clearer error to the UI instead of hanging silently
            throw new InvalidOperationException("PDF generation failed while launching browser or rendering the page.", ex);
        }
    }

    private static string? TryFindInstalledChromium()
    {
        try
        {
            // Candidate paths for Edge and Chrome
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
        }
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