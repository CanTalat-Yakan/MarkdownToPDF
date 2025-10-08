namespace MarkdownToPDF.Models;

public sealed class FormattingOptions
{
    public bool UseAdvancedExtensions { get; set; }
    public bool UsePipeTables { get; set; }
    public bool UseAutoLinks { get; set; }
    public bool InsertPageBreaksBetweenFiles { get; set; }
    public string AdditionalHeadHtml { get; set; } = string.Empty;

    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; } = 24;
    public double BodyFontSizePx { get; set; } = 12;
    public string BodyTextAlignment { get; set; } = "Justify";
}