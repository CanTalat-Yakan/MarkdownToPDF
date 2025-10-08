using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarkdownToPDF.Models;
using MarkdownToPDF.ViewModels;

namespace MarkdownToPDF.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly WireframePageViewModel _viewModel;

    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; }
    public bool UseAdvancedExtensions { get; set; }
    public bool UsePipeTables { get; set; }
    public bool UseAutoLinks { get; set; }
    public bool InsertPageBreaksBetweenFiles { get; set; }

    public string PaperFormat { get; set; } = "A4";
    public bool Landscape { get; set; }
    public bool PrintBackground { get; set; }

    // Converted to DependencyProperty so x:Bind (IsEnabled) updates when toggled
    public bool ShowPageNumbers
    {
        get => (bool)GetValue(ShowPageNumbersProperty);
        set => SetValue(ShowPageNumbersProperty, value);
    }

    public static readonly DependencyProperty ShowPageNumbersProperty =
        DependencyProperty.Register(
            nameof(ShowPageNumbers),
            typeof(bool),
            typeof(SettingsDialog),
            new PropertyMetadata(false));

    public string PageNumberPosition { get; set; } = "BottomRight";
    public double TopMarginMm { get; set; }
    public double RightMarginMm { get; set; }
    public double BottomMarginMm { get; set; }
    public double LeftMarginMm { get; set; }

    public SettingsDialog(WireframePageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        var formattingOptions = viewModel.Formatting;
        BaseFontFamily = formattingOptions.BaseFontFamily;
        BodyMarginPx = formattingOptions.BodyMarginPx;
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
        TopMarginMm = exportOptions.TopMarginMm;
        RightMarginMm = exportOptions.RightMarginMm;
        BottomMarginMm = exportOptions.BottomMarginMm;
        LeftMarginMm = exportOptions.LeftMarginMm;

        DataContext = this;
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var newFormatting = new FormattingOptions
            {
                UseAdvancedExtensions = UseAdvancedExtensions,
                UsePipeTables = UsePipeTables,
                UseAutoLinks = UseAutoLinks,
                InsertPageBreaksBetweenFiles = InsertPageBreaksBetweenFiles,
                BaseFontFamily = BaseFontFamily,
                BodyMarginPx = BodyMarginPx
            };

            var newExport = new ExportOptions
            {
                PaperFormat = PaperFormat,
                Landscape = Landscape,
                PrintBackground = PrintBackground,
                ShowPageNumbers = ShowPageNumbers,
                PageNumberPosition = PageNumberPosition,
                TopMarginMm = TopMarginMm,
                RightMarginMm = RightMarginMm,
                BottomMarginMm = BottomMarginMm,
                LeftMarginMm = LeftMarginMm,
                PreviewDestinationWidthPx = _viewModel.Export.PreviewDestinationWidthPx,
                PreviewDpi = _viewModel.Export.PreviewDpi,
                PreferCssPageSize = false
            };

            await _viewModel.ApplySettingsAsync(newFormatting, newExport);
        }
        catch (Exception ex)
        {
            _ = new ContentDialog
            {
                Title = "Apply Failed",
                Content = $"Error applying settings: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}