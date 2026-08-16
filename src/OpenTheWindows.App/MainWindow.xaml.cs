using System.Windows;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App;

/// <summary>Main window; view model injected by the container.</summary>
internal partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
    }
}
