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

    private static double MmToPoint(double mm) => mm * 72.0 / 25.4;
}
