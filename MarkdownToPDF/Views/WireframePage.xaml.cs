using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using WinRT.Interop;
using MarkdownToPDF.Models;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Collections.Specialized;

namespace MarkdownToPDF.Views;

public sealed partial class WireframePage : Page
{
    private WireframePageViewModel ViewModel => (WireframePageViewModel)DataContext;

    private double PageSlotHeightPortrait => 1123 + 32;
    private double PageSlotHeightLandscape => 794 + 32;
    private double CurrentPageSlotHeight => ViewModel.Export.Landscape ? PageSlotHeightLandscape : PageSlotHeightPortrait;

    // threshold in pixels to show scroll-to-top button
    private const double ShowScrollToTopThreshold = 200.0;

    public WireframePage()
    {
        InitializeComponent();
        DataContext = new WireframePageViewModel(
            App.GetService<IMarkdownService>(),
            App.GetService<IPdfService>());

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += WireframePage_Loaded;

        // Track PreviewPages changes to update UI buttons
        if (ViewModel.PreviewPages != null)
            ViewModel.PreviewPages.CollectionChanged += PreviewPages_CollectionChanged;

        _ = PickFilesAndLoadAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WireframePageViewModel.HeadingInfos))
        {
            BuildHierarchyTree();
            return;
        }

        if (e.PropertyName == nameof(WireframePageViewModel.CurrentPage))
        {
            // Update the PageInputBox to reflect the current page unless user is editing it
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!PageInputBox.IsFocusEngaged)
                    {
                        PageInputBox.Text = ViewModel.CurrentPage.ToString();
                    }
                }
                catch { }
            });
        }
    }

    private void BuildHierarchyTree()
    {
        if (ViewModel.HeadingInfos is null || ViewModel.HeadingInfos.Count == 0)
        {
            HierarchyTree.RootNodes.Clear();
            return;
        }

        HierarchyTree.RootNodes.Clear();

        // We expect HeadingInfo.Level: 1=H2, 2=H3, ... based on model comment
        var stack = new Stack<TreeViewNode>();
        foreach (var h in ViewModel.HeadingInfos)
        {
            var node = new TreeViewNode
            {
                Content = h,
                IsExpanded = false
            };

            if (stack.Count == 0)
            {
                HierarchyTree.RootNodes.Add(node);
                stack.Push(node);
                continue;
            }

            // Pop to a parent with level < current level
            while (stack.Count > 0)
            {
                var topHeading = (HeadingInfo)stack.Peek().Content;
                if (topHeading.Level < h.Level) break;
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                HierarchyTree.RootNodes.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }
    }

    private async void WireframePage_Loaded(object sender, RoutedEventArgs e)
    {
        // Do not automatically open file picker on startup anymore.
        // Just set the initial UI state based on whether there are preview pages already.
        BuildHierarchyTree();

        UpdateActionVisibility();

        // Ensure page input is initialized
        PageInputBox.Text = ViewModel.CurrentPage.ToString();
    }

    private void UpdateActionVisibility()
    {
        // If there are preview pages, show the action panel (Save + Re-add)
        var hasPages = ViewModel.PreviewPages is not null && ViewModel.PreviewPages.Count > 0;
        AddFilesInitialButton.Visibility = hasPages ? Visibility.Collapsed : Visibility.Visible;

        // Content text visibility
        ContentTextBlock.Visibility = hasPages ? Visibility.Collapsed : Visibility.Visible;
        if (!hasPages)
        {
            ContentTextBlock.Text = "No files selected. Click 'Add Files' to begin.";
        }
    }

    private void PreviewPages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Called on UI thread because collection modifications happen there; if not, dispatch to UI thread.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            UpdateActionVisibility();
            BuildHierarchyTree();

            // When previews change (reload, clear, etc.) ensure the scroll-to-top button is hidden
            try
            {
                ScrollToTopButton.Visibility = Visibility.Collapsed;
                // also reset scroll position to top to avoid stale offset
                PreviewScrollViewer.ChangeView(null, 0, null, false);
            }
            catch
            {
                // ignore any exceptions when changing view
            }

            // Update page input to reflect new current page
            try
            {
                if (!PageInputBox.IsFocusEngaged)
                    PageInputBox.Text = ViewModel.CurrentPage.ToString();
            }
            catch { }
        });
    }

    private async void AddFilesInitialButton_Click(object sender, RoutedEventArgs e)
    {
        await PickFilesAndLoadAsync();
    }

    private async void ReaddButton_Click(object sender, RoutedEventArgs e)
    {
        await PickFilesAndLoadAsync();
    }

    // Handler for toolbar ClearFiles button
    private async void ClearFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Clear current files
            await ViewModel.LoadFromFilesAsync(Array.Empty<string>());
            UpdateActionVisibility();
            // Prompt user to pick new files
            await PickFilesAndLoadAsync();
        }
        catch (Exception ex)
        {
            ContentTextBlock.Text = $"Unable to clear/load files: {ex.Message}";
            ContentTextBlock.Visibility = Visibility.Visible;
        }
    }

    // Allow external callers (like MainWindow) to clear current files and immediately reopen the file picker
    public async Task ClearCurrentFilesAndPickAsync()
    {
        // Clear current files by loading an empty set
        await ViewModel.LoadFromFilesAsync(Array.Empty<string>());
        // Update UI immediately
        UpdateActionVisibility();
        // Open picker to allow picking new files
        await PickFilesAndLoadAsync();
    }

    private async Task PickFilesAndLoadAsync()
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

            // ensure UI resets: hide scroll-to-top and scroll to top after loading
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    ScrollToTopButton.Visibility = Visibility.Collapsed;
                    PreviewScrollViewer.ChangeView(null, 0, null, false);
                }
                catch { }

                // Update the page input after loading
                try { if (!PageInputBox.IsFocusEngaged) PageInputBox.Text = ViewModel.CurrentPage.ToString(); } catch { }
            });

            ContentTextBlock.Visibility = Visibility.Collapsed;

            // build hierarchy once headings are available
            BuildHierarchyTree();
            UpdateActionVisibility();
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
            XamlRoot = this.Content.XamlRoot,
            // Ensure the dialog opens with the current theme of the host
            RequestedTheme = ((FrameworkElement)this.Content).ActualTheme
        };
        await dlg.ShowAsync();
        // After settings, headings might change. Rebuild preview already happens via ViewModel.
        BuildHierarchyTree();
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

        // Show or hide the scroll-to-top button depending on vertical offset
        if (offset > ShowScrollToTopThreshold)
        {
            if (ScrollToTopButton.Visibility != Visibility.Visible)
                ScrollToTopButton.Visibility = Visibility.Visible;
        }
        else
        {
            if (ScrollToTopButton.Visibility != Visibility.Collapsed)
                ScrollToTopButton.Visibility = Visibility.Collapsed;
        }
    }

    private void ScrollToTopButton_Click(object sender, RoutedEventArgs e)
    {
        // Scroll back to top of the preview
        PreviewScrollViewer.ChangeView(null, 0, null, false);
        ScrollToTopButton.Visibility = Visibility.Collapsed;
    }

    private void PageInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        ApplyPageInputBoxNavigation();
    }

    private void ApplyPageInputBoxNavigation()
    {
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

    private void HierarchyButton_Click(object sender, RoutedEventArgs e)
    {
        HierarchySplitView.IsPaneOpen = !HierarchySplitView.IsPaneOpen;
    }

    private void HierarchyTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // Support both RootNodes (TreeViewNode) and ItemsSource (HeadingInfo) use-cases
        if (args.InvokedItem is HeadingInfo hi)
        {
            if (hi.Page > 0)
            {
                PageInputBox.Text = hi.Page.ToString();
                ApplyPageInputBoxNavigation();
            }
            return;
        }
        if (args.InvokedItem is TreeViewNode node && node.Content is HeadingInfo hi2)
        {
            if (hi2.Page > 0)
            {
                PageInputBox.Text = hi2.Page.ToString();
                ApplyPageInputBoxNavigation();
            }
        }
    }
}
