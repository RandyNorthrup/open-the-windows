namespace OpenTheWindows.App.Services;

/// <summary>
/// Modal user prompts. Abstracted so view models can request a message,
/// a yes/no confirmation, or a typed confirmation without depending on WPF
/// <c>MessageBox</c> / window types, and so tests can script the answers.
/// </summary>
internal interface IDialogService
{
    /// <summary>Shows an informational message with a single dismiss button.</summary>
    void ShowMessage(string title, string message);

    /// <summary>
    /// Asks a yes/no question. Returns <see langword="true"/> only if the user
    /// explicitly confirms.
    /// </summary>
    bool Confirm(string title, string message);

    /// <summary>
    /// Gates a dangerous action behind exact re-typing of
    /// <paramref name="requiredText"/> (for example a Breaking tweak's id).
    /// Returns <see langword="true"/> only when the user typed the required
    /// text verbatim and confirmed.
    /// </summary>
    bool ConfirmTyped(string title, string message, string requiredText);
}
