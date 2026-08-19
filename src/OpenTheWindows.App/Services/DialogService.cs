using System.Windows;
using OpenTheWindows.App.Views;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IDialogService"/> over WPF <see cref="MessageBox"/> and the
/// <see cref="TypedConfirmationDialog"/>. The production implementation; tests
/// script answers through a fake.
/// </summary>
internal sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public void ShowMessage(string title, string message) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <inheritdoc />
    public bool Confirm(string title, string message) => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    /// <inheritdoc />
    public bool ConfirmTyped(string title, string message, string requiredText)
    {
        TypedConfirmationDialog dialog = new(title, message, requiredText)
        {
            Owner = Application.Current?.MainWindow,
        };
        return dialog.ShowDialog() == true;
    }
}
