namespace MarkdownToPDF.Models;

public sealed class ExportOptions
{
    public string OutputPath { get; set; } = string.Empty;
    public bool Landscape { get; set; }
    public bool PrintBackground { get; set; } = true;
    public bool PreferCssPageSize { get; set; } = true;
    public string PaperFormat { get; set; } = "A4";
    public bool ShowPageNumbers { get; set; }

    // Word-like defaults: 25.4 mm (1 inch)
    public double TopMarginMm { get; set; } = 25.4;
    public double RightMarginMm { get; set; } = 25.4;
    public double BottomMarginMm { get; set; } = 25.4;
    public double LeftMarginMm { get; set; } = 25.4;

    // Preview-related
    public int PreviewDestinationWidthPx { get; set; } = 794;
    public int PreviewDpi { get; set; } = 96;
}