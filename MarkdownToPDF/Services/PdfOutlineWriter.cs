using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MarkdownToPDF.Services;

public static class PdfOutlineWriter
{
    public static void InjectOutline(string pdfPath, IReadOnlyList<HeadingInfo> headings)
    {
        if (headings.Count == 0) return;

        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);

        List<PdfOutline> parent = new();
        for (int i = 0; i < 10; i++)
            parent.Add(null);

        foreach (var h in headings)
        {
            PdfOutline outline = null;

            if (h.Level == 1)
                outline = document.Outlines.Add(h.Text, document.Pages[h.Page], true);
            if (h.Level > 1)
                outline = parent[h.Level - 1].Outlines.Add(h.Text, document.Pages[h.Page], true);

            parent[h.Level] = outline;
        }

        document.Save(pdfPath);
    }
}