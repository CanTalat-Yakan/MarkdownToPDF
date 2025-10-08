using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace MarkdownToPDF.Services;

public static class PdfHeadingPageResolver
{
    // Assigns Page for each heading (0-based) by scanning PDF text.
    public static void AssignPages(string pdfPath, IList<HeadingInfo> headings)
    {
        if (headings.Count == 0) return;

        using var doc = PdfDocument.Open(pdfPath);

        // Pending headings not yet mapped
        var pending = headings.Where(h => h.Page == 0).ToList();

        // Track how many times we've matched a normalized heading (for duplicates)
        var occurrenceCursor = new Dictionary<string, int>();
        foreach (var h in pending)
        {
            var key = Normalize(h.Text);
            if (!occurrenceCursor.ContainsKey(key))
                occurrenceCursor[key] = 0;
        }

        for (int pageIdx = 0; pageIdx < doc.NumberOfPages && pending.Count > 0; pageIdx++)
        {
            var page = doc.GetPage(pageIdx + 1); // PdfPig pages are 1-based
            var pageText = Normalize(page.Text);

            // Copy to avoid modifying while iterating
            foreach (var h in pending.ToList())
            {
                string key = Normalize(h.Text);
                int occurrenceNeeded = occurrenceCursor[key];

                int foundAt = FindNthOccurrence(pageText, key, occurrenceNeeded);
                if (foundAt >= 0)
                {
                    h.Page = pageIdx; // keep 0-based
                    occurrenceCursor[key] = occurrenceNeeded + 1;
                    pending.Remove(h);
                }
                else
                {
                    // Try a relaxed match (first 50 chars) for very long headings that wrap oddly
                    var relaxed = key.Length > 60 ? key[..50] : null;
                    if (!string.IsNullOrEmpty(relaxed))
                    {
                        int relaxedFound = FindNthOccurrence(pageText, relaxed, occurrenceNeeded);
                        if (relaxedFound >= 0)
                        {
                            h.Page = pageIdx;
                            occurrenceCursor[key] = occurrenceNeeded + 1;
                            pending.Remove(h);
                        }
                    }
                }
            }
        }
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"\s+", " ").Trim();

    private static int FindNthOccurrence(string haystack, string needle, int n)
    {
        if (n < 0) return -1;
        int idx = -1;
        int start = 0;
        for (int i = 0; i <= n; i++)
        {
            idx = haystack.IndexOf(needle, start, StringComparison.Ordinal);
            if (idx < 0) return -1;
            start = idx + needle.Length;
        }
        return idx;
    }
}