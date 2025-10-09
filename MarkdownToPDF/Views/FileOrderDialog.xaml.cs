using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MarkdownToPDF.Views;

public sealed partial class FileOrderDialog : ContentDialog
{
    private FrameworkElement? _rootForPointerHandler;

    public ObservableCollection<Models.MarkdownFileModel> Files { get; } = new();

    public FileOrderDialog(IEnumerable<string> filePaths)
    {
        this.InitializeComponent();
        foreach (var p in filePaths)
        {
            if (!string.IsNullOrWhiteSpace(p))
                Files.Add(new Models.MarkdownFileModel(p));
        }
        DataContext = this;

        Opened += Dialog_Opened;
        Closed += Dialog_Closed;
    }

    public IReadOnlyList<string> GetOrderedPaths() => Files.Select(f => f.FilePath).ToArray();

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (XamlRoot?.Content is FrameworkElement root)
        {
            _rootForPointerHandler = root;
            _rootForPointerHandler.PointerPressed += Root_PointerPressed;
        }
    }

    private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (_rootForPointerHandler is not null)
        {
            _rootForPointerHandler.PointerPressed -= Root_PointerPressed;
            _rootForPointerHandler = null;
        }
    }

    private void Root_PointerPressed(object? sender, PointerRoutedEventArgs e)
    {
        var original = e.OriginalSource as DependencyObject;
        var current = original;
        while (current is not null)
        {
            if (current == this)
                return;
            current = VisualTreeHelper.GetParent(current);
        }
        Hide();
    }
}
