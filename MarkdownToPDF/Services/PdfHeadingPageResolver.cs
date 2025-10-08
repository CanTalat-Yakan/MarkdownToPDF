using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace MarkdownToPDF.Services;

public static class PdfHeadingPageResolver
{
    public static void AssignPages(string pdfPath, IList<HeadingInfo> headings)
    {
        if (headings.Count == 0) return;

        using var doc = PdfDocument.Open(pdfPath);

        // Work only with unresolved headings
        var pending = headings.Where(h => h.Page == 0).ToList();
        if (pending.Count == 0) return;

        // Group headings by normalized text; we will assign from the end of each group
        var groups = pending
            .GroupBy(h => Normalize(h.Text))
            .ToDictionary(
                g => g.Key,
                g => new HeadingGroup
                {
                    Headings = g.ToList(),
                    NextUnassignedReverseIndex = g.Count() - 1
                });

        // Scan pages from last to first
        for (int pageIdx = doc.NumberOfPages; pageIdx >= 1 && groups.Count > 0; pageIdx--)
        {
            var page = doc.GetPage(pageIdx);
            var pageTextNorm = Normalize(page.Text);

            // Iterate over a snapshot of keys to allow removal inside loop
            foreach (var kvpKey in groups.Keys.ToList())
            {
                var group = groups[kvpKey];
                if (group.NextUnassignedReverseIndex < 0)
                {
                    groups.Remove(kvpKey);
                    continue;
                }

                // Count occurrences of the full normalized key on this page
                int occurrenceCount = CountOccurrences(pageTextNorm, kvpKey);

                // If no full match and key is very long, try a relaxed prefix
                if (occurrenceCount == 0 && kvpKey.Length > 60)
                {
                    string relaxed = kvpKey[..50];
                    occurrenceCount = CountOccurrences(pageTextNorm, relaxed);
                }

                if (occurrenceCount <= 0) continue;

                // Assign as many headings (from the tail of the list) as we have occurrences.
                // Each occurrence corresponds to a body usage when scanning backwards.
                while (occurrenceCount > 0 && group.NextUnassignedReverseIndex >= 0)
                {
                    var h = group.Headings[group.NextUnassignedReverseIndex];
                    if (h.Page == 0)
                        h.Page = pageIdx;
                    group.NextUnassignedReverseIndex--;
                    occurrenceCount--;
                }

                if (group.NextUnassignedReverseIndex < 0)
                    groups.Remove(kvpKey);
            }
        }
    }

    private sealed class HeadingGroup
    {
        public List<HeadingInfo> Headings { get; set; } = default!;
        public int NextUnassignedReverseIndex { get; set; }
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.ToLowerInvariant(), @"\s+", " ").Trim();

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        int count = 0;
        int start = 0;
        while (true)
        {
            int idx = haystack.IndexOf(needle, start, StringComparison.Ordinal);
            if (idx < 0) break;
            count++;
            start = idx + needle.Length;
        }
        return count;
    }
}