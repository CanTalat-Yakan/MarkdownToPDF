using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace MarkdownToPDF.Views;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    public MainWindow()
    {
        ViewModel = App.GetService<MainViewModel>();
        this.InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        // initial size - will be adjusted when WireframePage is ready
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 1400));
        CenterWindow();

        NavView.IsPaneOpen = false;
        NavView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

        NavFrame.Navigated += NavFrame_Navigated;

        var navService = App.GetService<IJsonNavigationService>() as JsonNavigationService;
        if (navService != null)
        {
            navService.Initialize(NavView, NavFrame, NavigationPageMappings.PageDictionary)
                .ConfigureDefaultPage(typeof(WireframePage))
                .ConfigureSettingsPage(typeof(SettingsPage))
                .ConfigureJsonFile("Assets/NavViewMenu/AppData.json")
                .ConfigureTitleBar(AppTitleBar);
        }
    }

    private void NavFrame_Navigated(object? sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // If navigated to the WireframePage, attempt to get its view model and adjust window size
        if (NavFrame.Content is WireframePage wf && wf.DataContext is WireframePageViewModel vm)
        {
            // initial adjust
            AdjustWindowSizeToPreview(vm);
            // subscribe to future changes
            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.PropertyChanged += Vm_PropertyChanged;
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is WireframePageViewModel vm &&
            (e.PropertyName == nameof(WireframePageViewModel.PagePreviewWidthPx) || e.PropertyName == nameof(WireframePageViewModel.PagePreviewHeightPx)))
        {
            AdjustWindowSizeToPreview(vm);
        }
    }

    private void AdjustWindowSizeToPreview(WireframePageViewModel vm)
    {
        try
        {
            // Get preview size from viewmodel
            int pageWidth = vm.PagePreviewWidthPx;
            int pageHeight = vm.PagePreviewHeightPx;

            // Add some padding to account for UI chrome, margins and toolbars
            const int horizontalPadding = 50; // left/right pane, margins, window chrome
            const int verticalPadding = 150;   // title bar, toolbar, bottom actions

            int desiredWidth = Math.Max(800, pageWidth + horizontalPadding);
            int desiredHeight = Math.Max(1300, pageHeight + verticalPadding);

            AppWindow.Resize(new SizeInt32(desiredWidth, desiredHeight));
            CenterWindow();
        }
        catch
        {
            // ignore resizing errors
        }
    }

    private void CenterWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (area == null) return;
        AppWindow.Move(new PointInt32((area.Value.Width - AppWindow.Size.Width) / 2, (area.Value.Height - AppWindow.Size.Height) / 2));
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        await App.Current.ThemeService.SetElementThemeWithoutSaveAsync();
    }

    private async void ClearFilesButton_Click(object sender, RoutedEventArgs e)
    {
        // Try to find the WireframePage in the navigation frame and call its clear method
        if (NavFrame.Content is WireframePage wf)
        {
            await wf.ClearCurrentFilesAndPickAsync();
            return;
        }

        // If current content isn't the wireframe page, attempt to navigate to it first then call the method
        NavFrame.Navigate(typeof(WireframePage));
        await Task.Delay(100); // let navigation complete
        if (NavFrame.Content is WireframePage wf2)
        {
            await wf2.ClearCurrentFilesAndPickAsync();
        }
    }
}
