using System.Collections.ObjectModel;
using System.Drawing.Text;

namespace MarkdownToPDF.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly WireframePageViewModel _viewModel;

    public string BaseFontFamily { get; set; } = "Segoe UI";
    public ObservableCollection<string> FontFamilies { get; } = new();

    public double BodyMarginPx { get; set; }
    public double BodyFontSizePx { get; set; }
    public string BodyTextAlignment { get; set; } = "Justify";
    public bool UseAdvancedExtensions { get; set; }
    public bool UsePipeTables { get; set; }
    public bool UseAutoLinks { get; set; }
    public bool InsertPageBreaksBetweenFiles { get; set; }

    public string PaperFormat { get; set; } = "A4";
    public bool Landscape { get; set; }
    public bool PrintBackground { get; set; }

    public bool ShowPageNumbers
    {
        get => (bool)GetValue(ShowPageNumbersProperty);
        set => SetValue(ShowPageNumbersProperty, value);
    }
    public static readonly DependencyProperty ShowPageNumbersProperty =
        DependencyProperty.Register(nameof(ShowPageNumbers), typeof(bool), typeof(SettingsDialog), new PropertyMetadata(false));

    public bool ShowPageNumberOnFirstPage { get; set; } = true;

    public string PageNumberPosition { get; set; } = "BottomRight";
    public double TopMarginMm { get; set; }
    public double RightMarginMm { get; set; }
    public double BottomMarginMm { get; set; }
    public double LeftMarginMm { get; set; }

    public string HeaderNumberingPattern { get; set; } = "1.1.1";

    public bool AddTableOfContents
    {
        get => (bool)GetValue(AddTableOfContentsProperty);
        set => SetValue(AddTableOfContentsProperty, value);
    }
    public static readonly DependencyProperty AddTableOfContentsProperty =
        DependencyProperty.Register(
            nameof(AddTableOfContents),
            typeof(bool),
            typeof(SettingsDialog),
            new PropertyMetadata(false));

    public bool IndentTableOfContents { get; set; }
    public string TableOfContentsBulletStyle { get; set; } = "-";
    public string TableOfContentsHeaderText { get; set; } = "Table of Contents";
    public bool TableOfContentsAfterFirstFile { get; set; }

    public SettingsDialog(WireframePageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        var formattingOptions = viewModel.Formatting;
        BaseFontFamily = ExtractFirstFamily(formattingOptions.BaseFontFamily);
        BodyMarginPx = formattingOptions.BodyMarginPx;
        BodyFontSizePx = formattingOptions.BodyFontSizePx;
        BodyTextAlignment = formattingOptions.BodyTextAlignment;
        UseAdvancedExtensions = formattingOptions.UseAdvancedExtensions;
        UsePipeTables = formattingOptions.UsePipeTables;
        UseAutoLinks = formattingOptions.UseAutoLinks;
        InsertPageBreaksBetweenFiles = formattingOptions.InsertPageBreaksBetweenFiles;

        var exportOptions = viewModel.Export;
        PaperFormat = exportOptions.PaperFormat;
        Landscape = exportOptions.Landscape;
        PrintBackground = exportOptions.PrintBackground;
        ShowPageNumbers = exportOptions.ShowPageNumbers;
        PageNumberPosition = exportOptions.PageNumberPosition;
        ShowPageNumberOnFirstPage = exportOptions.ShowPageNumberOnFirstPage;
        TopMarginMm = exportOptions.TopMarginMm;
        RightMarginMm = exportOptions.RightMarginMm;
        BottomMarginMm = exportOptions.BottomMarginMm;
        LeftMarginMm = exportOptions.LeftMarginMm;

        HeaderNumberingPattern = formattingOptions.HeaderNumberingPattern;
        AddTableOfContents = formattingOptions.AddTableOfContents;
        IndentTableOfContents = formattingOptions.IndentTableOfContents;
        TableOfContentsBulletStyle = formattingOptions.TableOfContentsBulletStyle;
        TableOfContentsHeaderText = formattingOptions.TableOfContentsHeaderText;
        TableOfContentsAfterFirstFile = formattingOptions.TableOfContentsAfterFirstFile;

        DataContext = this;
        LoadFonts();
    }

    private void LoadFonts()
    {
        try
        {
            using var installed = new InstalledFontCollection();
            var names = installed.Families
                .Select(f => f.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
                FontFamilies.Add(n);
        }
        catch
        {
            string[] fallback =
            {
                "Segoe UI","Calibri","Arial","Times New Roman","Georgia",
                "Cambria","Consolas","Courier New","Cascadia Mono","Verdana",
                "Tahoma","Trebuchet MS"
            };
            foreach (var f in fallback.Distinct())
                FontFamilies.Add(f);
        }
        if (!FontFamilies.Contains(BaseFontFamily))
            FontFamilies.Insert(0, BaseFontFamily);
    }

    private static string ExtractFirstFamily(string cssValue)
    {
        if (string.IsNullOrWhiteSpace(cssValue))
            return "Segoe UI";
        var first = cssValue.Split(',')[0].Trim().Trim('\'', '"');
        return string.IsNullOrWhiteSpace(first) ? "Segoe UI" : first;
    }

    private static string BuildCssFontStack(string primary)
    {
        if (string.IsNullOrWhiteSpace(primary))
            primary = "Segoe UI";
        var lower = primary.ToLowerInvariant();
        if (lower.Contains("mono") || lower.Contains("consolas") || lower.Contains("courier"))
            return $"{primary}, Consolas, 'Courier New', monospace";
        return $"{primary}, 'Segoe UI', Arial, Helvetica, sans-serif";
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var root = XamlRoot;

        var trimmedPattern = HeaderNumberingPattern?.Trim() ?? string.Empty;
        bool enableNumbering = !string.IsNullOrWhiteSpace(trimmedPattern);

        var newFormatting = new FormattingOptions
        {
            UseAdvancedExtensions = UseAdvancedExtensions,
            UsePipeTables = UsePipeTables,
            UseAutoLinks = UseAutoLinks,
            InsertPageBreaksBetweenFiles = InsertPageBreaksBetweenFiles,
            BaseFontFamily = BuildCssFontStack(BaseFontFamily),
            BodyMarginPx = BodyMarginPx,
            BodyFontSizePx = BodyFontSizePx,
            BodyTextAlignment = BodyTextAlignment,
            AddHeaderNumbering = enableNumbering,
            HeaderNumberingPattern = enableNumbering ? trimmedPattern : string.Empty,
            AddTableOfContents = AddTableOfContents,
            IndentTableOfContents = IndentTableOfContents,
            TableOfContentsBulletStyle = TableOfContentsBulletStyle,
            TableOfContentsHeaderText = TableOfContentsHeaderText,
            TableOfContentsAfterFirstFile = TableOfContentsAfterFirstFile
        };

        var newExport = new ExportOptions
        {
            PaperFormat = PaperFormat,
            Landscape = Landscape,
            PrintBackground = PrintBackground,
            ShowPageNumbers = ShowPageNumbers,
            PageNumberPosition = PageNumberPosition,
            ShowPageNumberOnFirstPage = ShowPageNumberOnFirstPage,
            TopMarginMm = TopMarginMm,
            RightMarginMm = RightMarginMm,
            BottomMarginMm = BottomMarginMm,
            LeftMarginMm = LeftMarginMm,
            PreviewDestinationWidthPx = _viewModel.Export.PreviewDestinationWidthPx,
            PreviewDpi = _viewModel.Export.PreviewDpi
        };

        Hide();
        try
        {
            await _viewModel.ApplySettingsAsync(newFormatting, newExport);
        }
        catch (Exception ex)
        {
            _ = new ContentDialog
            {
                Title = "Apply Failed",
                Content = $"Error applying settings: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = root
            }.ShowAsync();
        }
    }
}