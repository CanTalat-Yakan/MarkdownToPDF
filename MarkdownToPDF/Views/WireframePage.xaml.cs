using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MarkdownToPDF.Views;

public sealed partial class WireframePage : Page
{
    private WireframePageViewModel ViewModel => (WireframePageViewModel)DataContext;

    private double PageSlotHeightPortrait => 1123 + 32;
    private double PageSlotHeightLandscape => 794 + 32;
    private double CurrentPageSlotHeight => ViewModel.Export.Landscape ? PageSlotHeightLandscape : PageSlotHeightPortrait;

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

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog(ViewModel)
        {
            XamlRoot = this.Content.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private void PreviewScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (ViewModel.PreviewPages.Count == 0) return;

        var sv = (ScrollViewer)sender;
        var offset = sv.VerticalOffset;
        var centerOffset = offset + sv.ViewportHeight / 2.0;
        int index = (int)Math.Floor(centerOffset / CurrentPageSlotHeight);

        if (index < 0) index = 0;
        if (index >= ViewModel.PreviewPages.Count) index = ViewModel.PreviewPages.Count - 1;

        ViewModel.CurrentPage = index + 1;
    }

    private void PageInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        if (ViewModel.TotalPages == 0) return;

        var text = PageInputBox.Text.Trim();
        if (int.TryParse(text, out var requested))
        {
            if (requested < 1) requested = 1;
            if (requested > ViewModel.TotalPages) requested = ViewModel.TotalPages;
            ScrollToPage(requested);
        }
        else
        {
            PageInputBox.Text = ViewModel.CurrentPage.ToString();
        }
    }

    private void ScrollToPage(int pageNumber)
    {
        var index = pageNumber - 1;
        if (index < 0) index = 0;
        if (index >= ViewModel.TotalPages) index = ViewModel.TotalPages - 1;

        var targetOffset = index * CurrentPageSlotHeight;
        PreviewScrollViewer.ChangeView(null, targetOffset, null, false);
        PageInputBox.Text = (index + 1).ToString();
    }
}
