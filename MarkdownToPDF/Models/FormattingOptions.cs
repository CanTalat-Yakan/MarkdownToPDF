namespace MarkdownToPDF.Models;

public sealed class FormattingOptions
{
    public bool UseAdvancedExtensions { get; set; }
    public bool UsePipeTables { get; set; }
    public bool UseAutoLinks { get; set; }
    public bool InsertPageBreaksBetweenFiles { get; set; }
    public string AdditionalHeadHtml { get; set; } = string.Empty;

    // Non-breaking additions to generate consistent base CSS for preview/PDF
    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; } = 24;
}