namespace MarkdownToPDF.Models;

public sealed class ExportOptions
{
    public string OutputPath { get; set; } = string.Empty;
    public bool Landscape { get; set; } = false;
    public bool PrintBackground { get; set; } = true;
    public string PaperFormat { get; set; } = "A4";

    public bool ShowPageNumbers { get; set; } = false;
    public string PageNumberPosition { get; set; } = "BottomRight";
    public bool ShowPageNumberOnFirstPage { get; set; } = true;

    public double TopMarginMm { get; set; } = 25.4;
    public double RightMarginMm { get; set; } = 25.4;
    public double BottomMarginMm { get; set; } = 25.4;
    public double LeftMarginMm { get; set; } = 25.4;

    public int PreviewDestinationWidthPx { get; set; } = 794;
    public int PreviewDpi { get; set; } = 96;
}