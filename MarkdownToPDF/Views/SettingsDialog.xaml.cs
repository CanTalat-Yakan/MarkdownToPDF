using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MarkdownToPDF.Models;
using MarkdownToPDF.ViewModels;

namespace MarkdownToPDF.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly WireframePageViewModel _vm;

    // Formatting draft
    public string BaseFontFamily { get; set; } = "Segoe UI, sans-serif";
    public double BodyMarginPx { get; set; }
    public bool UseAdvancedExtensions { get; set; }
    public bool UsePipeTables { get; set; }
    public bool UseAutoLinks { get; set; }
    public bool InsertPageBreaksBetweenFiles { get; set; }

    // Export draft
    public string PaperFormat { get; set; } = "A4";
    public bool Landscape { get; set; }
    public bool PrintBackground { get; set; }
    public bool PreferCssPageSize { get; set; }
    public bool UseCssPageMargins { get; set; }
    public bool ShowPageNumbers { get; set; }
    public double TopMarginMm { get; set; }
    public double RightMarginMm { get; set; }
    public double BottomMarginMm { get; set; }
    public double LeftMarginMm { get; set; }

    public SettingsDialog(WireframePageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        // Seed from current options
        var f = vm.Formatting;
        BaseFontFamily = f.BaseFontFamily;
        BodyMarginPx = f.BodyMarginPx;
        UseAdvancedExtensions = f.UseAdvancedExtensions;
        UsePipeTables = f.UsePipeTables;
        UseAutoLinks = f.UseAutoLinks;
        InsertPageBreaksBetweenFiles = f.InsertPageBreaksBetweenFiles;

        var e = vm.Export;
        PaperFormat = e.PaperFormat;
        Landscape = e.Landscape;
        PrintBackground = e.PrintBackground;
        PreferCssPageSize = e.PreferCssPageSize;
        ShowPageNumbers = e.ShowPageNumbers;
        TopMarginMm = e.TopMarginMm;
        RightMarginMm = e.RightMarginMm;
        BottomMarginMm = e.BottomMarginMm;
        LeftMarginMm = e.LeftMarginMm;

        DataContext = this;
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            // Build new option objects
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
                PreferCssPageSize = PreferCssPageSize,
                ShowPageNumbers = ShowPageNumbers,
                TopMarginMm = TopMarginMm,
                RightMarginMm = RightMarginMm,
                BottomMarginMm = BottomMarginMm,
                LeftMarginMm = LeftMarginMm,

                // Preserve unchanged preview settings
                PreviewDestinationWidthPx = _vm.Export.PreviewDestinationWidthPx,
                PreviewDpi = _vm.Export.PreviewDpi
            };

            await _vm.ApplySettingsAsync(newFormatting, newExport);
        }
        catch (Exception ex)
        {
            // Simple inline error notice
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