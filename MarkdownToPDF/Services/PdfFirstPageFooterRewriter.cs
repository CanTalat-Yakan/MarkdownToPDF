using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MarkdownToPDF.Services;

public static class PdfFirstPageFooterRewriter
{
    public static void ClearFooterOnFirstPage(string pdfPath, double footerRegionHeightMm)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
        if (document.Pages.Count == 0) return;

        var page = document.Pages[0];
        using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
        {
            double heightPts = MmToPoint(footerRegionHeightMm);
            double pageWidthPt = page.Width.Point;
            double pageHeightPt = page.Height.Point;

            // Draw across full width at the bottom of the page
            var rect = new XRect(0, pageHeightPt - heightPts, pageWidthPt, heightPts);
            gfx.DrawRectangle(XBrushes.White, rect);
        }

        document.Save(pdfPath);
    }

    // New: clear a header region on ALL pages
    public static void ClearHeaderOnAllPages(string pdfPath, double headerRegionHeightMm)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
        if (document.Pages.Count == 0) return;

        double heightPts = MmToPoint(headerRegionHeightMm);

        for (int i = 0; i < document.Pages.Count; i++)
        {
            var page = document.Pages[i];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            double pageWidthPt = page.Width.Point;

            // Draw across full width at the top of the page
            var rect = new XRect(0, 0, pageWidthPt, heightPts);
            gfx.DrawRectangle(XBrushes.White, rect);
        }

        document.Save(pdfPath);
    }

    private static double MmToPoint(double mm) => mm * 72.0 / 25.4;
}
