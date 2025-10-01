using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MarkdownToPDF.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using MarkdownToPDF.ViewModels;

namespace MarkdownToPDF.Views;

public sealed partial class WireframePage : Page
{
    private WireframePageViewModel ViewModel => (WireframePageViewModel)DataContext;

    public WireframePage()
    {
        InitializeComponent();
        DataContext = new WireframePageViewModel(
            App.GetService<IMarkdownService>(),
            App.GetService<IPdfService>());

        Loaded += WireframePage_Loaded;
    }

    private async void WireframePage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMarkdownOnInitAsync();
    }

    private async Task LoadMarkdownOnInitAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".md");
            picker.FileTypeFilter.Add(".markdown");
            InitializeWithWindow.Initialize(picker, App.Hwnd);

            // Allow selecting multiple markdown files
            var files = await picker.PickMultipleFilesAsync();
            if (files is null || files.Count == 0)
            {
                ContentTextBlock.Text = "No files selected.";
                ContentTextBlock.Visibility = Visibility.Visible;
                return;
            }

            await ViewModel.LoadFromFilesAsync(files.Select(f => f.Path).ToArray());

            ContentTextBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ContentTextBlock.Text = $"Unable to render preview. Error: {ex.Message}";
            ContentTextBlock.Visibility = Visibility.Visible;
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.CanExport)
            {
                ContentTextBlock.Text = "Open one or more markdown files first.";
                ContentTextBlock.Visibility = Visibility.Visible;
                return;
            }

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(ViewModel.CurrentMarkdownFileName ?? "Document")
            };
            savePicker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });
            InitializeWithWindow.Initialize(savePicker, App.Hwnd);

            var targetFile = await savePicker.PickSaveFileAsync();
            if (targetFile is null)
                return;

            await ViewModel.ExportToAsync(targetFile.Path);

            ContentTextBlock.Text = $"Exported to: {targetFile.Path}";
            ContentTextBlock.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ContentTextBlock.Text = $"Export failed. Error: {ex.Message}";
            ContentTextBlock.Visibility = Visibility.Visible;
        }
    }
}
