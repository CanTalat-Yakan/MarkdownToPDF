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

    // Debounce timer to hide progress a short moment after the preview stops changing
    private readonly DispatcherTimer _progressHideTimer;

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

        // Initialize debounce timer
        _progressHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _progressHideTimer.Tick += ProgressHideTimer_Tick;

        //_ = PickFilesAndLoadAsync();
    }

    private void ProgressHideTimer_Tick(object? sender, object e)
    {
        _progressHideTimer.Stop();
        // If we have any pages rendered, hide the progress UI as the preview is usable
        if (ViewModel.PreviewPages is { Count: > 0 })
        {
            HideProgressUI();
            UpdateActionVisibility();
        }
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

        if (e.PropertyName == nameof(WireframePageViewModel.CanExport))
        {
            // Refresh action buttons visibility when exportability changes
            _ = DispatcherQueue.TryEnqueue(UpdateActionVisibility);
        }

        if (e.PropertyName == nameof(WireframePageViewModel.TotalPagesExpected))
        {
            UpdateProgressBar();
        }
    }

    private void HideProgressUI()
    {
        CenterProgressBar.IsIndeterminate = false;
        CenterProgressBar.Visibility = Visibility.Collapsed;
        // Hide center status panel when we have pages, otherwise keep visible for instructions
        CenterStatusPanel.Visibility = (ViewModel.PreviewPages is { Count: > 0 }) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowIndeterminateProgress(string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
            ContentTextBlock.Text = message!;

        CenterProgressBar.IsIndeterminate = true;
        CenterProgressBar.Minimum = 0;
        CenterProgressBar.Maximum = 100;
        CenterStatusPanel.Visibility = Visibility.Visible;
        CenterProgressBar.Visibility = Visibility.Visible;
    }

    private void ShowDeterminateProgress(double percent, string? message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
            ContentTextBlock.Text = message!;

        CenterProgressBar.IsIndeterminate = false;
        CenterProgressBar.Minimum = 0;
        CenterProgressBar.Maximum = 100;
        CenterProgressBar.Value = percent;
        CenterStatusPanel.Visibility = Visibility.Visible;
        CenterProgressBar.Visibility = Visibility.Visible;
    }

    private void UpdateProgressBar()
    {
        // show percentage if we know expected total; otherwise indeterminate
        var expected = ViewModel.TotalPagesExpected;
        var current = Math.Max(0, ViewModel.TotalPages);

        if (expected > 0)
        {
            // If we've reached or exceeded expected pages, hide the progress UI
            if (current >= expected)
            {
                HideProgressUI();
            }
            else
            {
                var percent = Math.Clamp((double)current / expected * 100.0, 0, 100);
                ShowDeterminateProgress(percent, $"Loading preview... {Math.Round(percent)}%");
            }
        }
        else
        {
            // No known expected count -> show indeterminate only while we have no pages
            if ((ViewModel.PreviewPages?.Count ?? 0) == 0 && ViewModel.CanExport)
            {
                ShowIndeterminateProgress("Loading preview...");
            }
            else
            {
                // We already have some pages; hide progress regardless of percent accuracy
                HideProgressUI();
            }
        }

        // Ensure buttons reflect loading state
        UpdateActionVisibility();
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
        var isLoading = CenterProgressBar.Visibility == Visibility.Visible;

        // Bottom-right actions
        AddFilesInitialButton.Visibility = (!hasPages && !isLoading) ? Visibility.Visible : Visibility.Collapsed;
        ExportButton.Visibility = hasPages && ViewModel.CanExport ? Visibility.Visible : Visibility.Collapsed;

        // Toolbar trash icon
        ClearFilesButton.Visibility = hasPages ? Visibility.Visible : Visibility.Collapsed;

        // Center status panel logic:
        // 1) If progress is explicitly visible, keep the panel visible (do not override during loading)
        if (isLoading)
        {
            CenterStatusPanel.Visibility = Visibility.Visible;
            return;
        }

        // 2) If no pages, show instruction and hide progress
        if (!hasPages)
        {
            CenterStatusPanel.Visibility = Visibility.Visible;
            ContentTextBlock.Text = "No files selected. Click 'Add Files' to begin.";
            CenterProgressBar.Visibility = Visibility.Collapsed;
            CenterProgressBar.IsIndeterminate = true;
            return;
        }

        // 3) Otherwise hide center panel
        CenterStatusPanel.Visibility = Visibility.Collapsed;
    }

    private void PreviewPages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Called on UI thread because collection modifications happen there; if not, dispatch to UI thread.
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            UpdateActionVisibility();
            BuildHierarchyTree();

            // update progress when a page is added
            UpdateProgressBar();

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

            // Debounce hide of progress after the collection stabilizes
            try
            {
                _progressHideTimer.Stop();
                _progressHideTimer.Start();
            }
            catch { }

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
            CenterStatusPanel.Visibility = Visibility.Visible;
            CenterProgressBar.Visibility = Visibility.Collapsed;
            UpdateActionVisibility();
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
                ContentTextBlock.Text = "No files selected. Click 'Add Files' to begin.";
                CenterStatusPanel.Visibility = Visibility.Visible;
                CenterProgressBar.Visibility = Visibility.Collapsed;
                UpdateActionVisibility();
                return;
            }

            // Show order dialog before loading
            var orderDialog = new FileOrderDialog(files.Select(f => f.Path))
            {
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = ((FrameworkElement)this.Content).ActualTheme
            };
            var result = await orderDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                // User canceled ordering/generation
                ContentTextBlock.Text = "No files selected. Click 'Add Files' to begin.";
                CenterStatusPanel.Visibility = Visibility.Visible;
                CenterProgressBar.Visibility = Visibility.Collapsed;
                UpdateActionVisibility();
                return;
            }

            var orderedPaths = orderDialog.GetOrderedPaths();

            // Show loading progress immediately BEFORE starting heavy work
            ContentTextBlock.Text = "Loading preview...";
            CenterStatusPanel.Visibility = Visibility.Visible;
            CenterProgressBar.Visibility = Visibility.Visible;
            CenterProgressBar.IsIndeterminate = true;
            UpdateActionVisibility();

            // Stop any pending hide while we start loading
            _progressHideTimer.Stop();

            // Yield UI thread so the text/progress can render before we start work
            await Task.Yield();
            await Task.Delay(1);

            await ViewModel.LoadFromFilesAsync(orderedPaths);

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

            // build hierarchy once headings are available
            BuildHierarchyTree();

            // Debounce hide in case more pages continue to arrive after the async load
            _progressHideTimer.Stop();
            _progressHideTimer.Start();

            UpdateActionVisibility();
        }
        catch (Exception ex)
        {
            ContentTextBlock.Text = $"Unable to render preview. Error: {ex.Message}";
            CenterStatusPanel.Visibility = Visibility.Visible;
            CenterProgressBar.Visibility = Visibility.Collapsed;
            UpdateActionVisibility();
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.CanExport)
            {
                ContentTextBlock.Text = "Open one or more markdown files first.";
                CenterStatusPanel.Visibility = Visibility.Visible;
                CenterProgressBar.Visibility = Visibility.Collapsed;
                UpdateActionVisibility();
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
            CenterStatusPanel.Visibility = Visibility.Visible;
            CenterProgressBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ContentTextBlock.Text = $"Export failed. Error: {ex.Message}";
            CenterStatusPanel.Visibility = Visibility.Visible;
            CenterProgressBar.Visibility = Visibility.Collapsed;
        }
        finally
        {
            UpdateActionVisibility();
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
        UpdateActionVisibility();
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
