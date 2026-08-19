using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace OpenTheWindows.App.Views;

/// <summary>
/// Modal dialog that gates a dangerous (Breaking) action behind exact re-typing
/// of a required token (the tweak id). <see cref="Window.DialogResult"/> is
/// <see langword="true"/> only after the user typed the token verbatim and
/// pressed Confirm.
/// </summary>
internal partial class TypedConfirmationDialog : Window
{
    private readonly string _requiredText;

    /// <summary>Builds the dialog for a specific confirmation token.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="message">The consequence explanation shown to the user.</param>
    /// <param name="requiredText">The exact text the user must type to enable Confirm.</param>
    public TypedConfirmationDialog(string title, string message, string requiredText)
    {
        ArgumentException.ThrowIfNullOrEmpty(requiredText);
        _requiredText = requiredText;
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InstructionText.Text = string.Create(
            CultureInfo.CurrentCulture, $"Type {requiredText} to confirm.");
    }

    private void ConfirmationInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        ConfirmButton.IsEnabled = string.Equals(ConfirmationInput.Text, _requiredText, StringComparison.Ordinal);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
