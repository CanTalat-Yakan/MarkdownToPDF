namespace MarkdownToPDF.Models;

public sealed class FormattingOptions
{
    public bool UseAdvancedExtensions { get; set; } = true;
    public bool UsePipeTables { get; set; } = true;
    public bool UseAutoLinks { get; set; } = true;
    public bool InsertPageBreaksBetweenFiles { get; set; } = false;
    public string AdditionalHeadHtml { get; set; } = string.Empty;

    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; } = 0;
    public double BodyFontSizePx { get; set; } = 12;
    public string BodyTextAlignment { get; set; } = "Justify";

    public bool AddHeaderNumbering { get; set; } = false;
    public string HeaderNumberingPattern { get; set; } = "1.1.1.";

    public bool AddTableOfContents { get; set; } = false;
    public bool IndentTableOfContents { get; set; } = true;
    public string TableOfContentsBulletStyle { get; set; } = "-";
    public string TableOfContentsHeaderText { get; set; } = "Table of Contents";
    public bool TableOfContentsAfterFirstFile { get; set; } = true;
}