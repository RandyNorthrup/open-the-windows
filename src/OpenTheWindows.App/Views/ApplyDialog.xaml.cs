using System.Windows;

namespace OpenTheWindows.App.Views;

/// <summary>
/// Modal review-and-apply dialog. The stages and commands live on the
/// <c>ApplyFlowViewModel</c>; the code-behind only closes the window.
/// </summary>
internal partial class ApplyDialog : Window
{
    /// <summary>Initialises the dialog.</summary>
    public ApplyDialog() => InitializeComponent();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
