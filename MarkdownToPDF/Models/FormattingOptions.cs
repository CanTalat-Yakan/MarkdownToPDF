namespace MarkdownToPDF.Models;

public sealed class FormattingOptions
{
    public bool UseAdvancedExtensions { get; set; } = true;
    public bool UsePipeTables { get; set; } = true;
    public bool UseAutoLinks { get; set; } = true;
    public bool InsertPageBreaksBetweenFiles { get; set; } = false;
    public string BaseHeadHtml { get; set; }
    public string HeadHtml { get; set; } =
        """
        <style>
            h1 { text-align: center; }
            h2 { margin-top: 2.2em; }
            h2, h3, h4, h5, h6, pre, code { text-align: left; margin-bottom: -0.5em;  }
            img { max-width:100%; }
            pre { overflow:auto; }
            table { border-collapse: collapse; border-spacing: 0; width: calc(100% - 1px); }
            table, th, td { border: 1px solid #aaaaaa; }
            table th { white-space:nowrap; }
            th, td { padding: 4px 6px; vertical-align: top; }
            thead th { background: #f6f6f6; }
            tbody tr:nth-child(even) td { background:#f6f6f6; }
            td, th { word-break: break-word; }
            a, a:visited { color:#000; text-decoration: underline; }
        </style>
        """;

    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; } = 0;
    public double BodyFontSizePx { get; set; } = 12;
    public string BodyTextAlignment { get; set; } = "Justify";

    public bool AddHeaderNumbering { get; set; } = false;
    public string HeaderNumberingPattern { get; set; } = "1.1.1.";

    public bool AddTableOfContents { get; set; } = false;
    public string TableOfContentsHeaderText { get; set; } = "Table of Contents";
    public bool TableOfContentsAfterFirstFile { get; set; } = false;
    public string TableOfContentsLeaders { get; set; } = "Dotted";
}