using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MarkdownToPDF.Services;

public static class PdfOutlineWriter
{
    public static void InjectOutline(string pdfPath, IReadOnlyList<HeadingInfo> headings)
    {
        if (headings.Count == 0) return;

        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);

        var parent = new PdfOutline[16]; // allow deeper nesting safely

        foreach (var h in headings)
        {
            // Skip unresolved or invalid page references
            if (h.Page <= 0) continue;              // 0 means not resolved
            int pageIndex = h.Page - 1;             // convert 1-based to 0-based
            if (pageIndex < 0 || pageIndex >= document.Pages.Count)
                continue;

            var targetPage = document.Pages[pageIndex];

            PdfOutline outline;
            if (h.Level == 1)
            {
                outline = document.Outlines.Add(h.Text, targetPage, true);
            }
            else
            {
                var parentNode = parent[h.Level - 1];
                outline = parentNode == null
                    ? document.Outlines.Add(h.Text, targetPage, true)
                    : parentNode.Outlines.Add(h.Text, targetPage, true);
            }

            parent[h.Level] = outline;
        }

        document.Save(pdfPath);
    }
}