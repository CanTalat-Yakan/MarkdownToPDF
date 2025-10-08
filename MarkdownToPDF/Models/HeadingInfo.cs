namespace MarkdownToPDF.Models;

public sealed class HeadingInfo
{
    public int Level { get; set; }      // Logical level: H2 => 1, H3 => 2, etc.
    public string Text { get; set; } = "";
    public double Y { get; set; }       // CSS px from top of document (filled in later)
    public int Page { get; set; }       // 1-based resolved page index
    public string Anchor { get; set; } = ""; // HTML id attribute for lookup
}
