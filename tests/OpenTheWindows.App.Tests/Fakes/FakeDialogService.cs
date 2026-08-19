using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>
/// Scriptable <see cref="IDialogService"/> for view-model tests: records prompts
/// and returns the answers the test set, so confirmation-gated flows can be
/// driven both ways without a UI.
/// </summary>
internal sealed class FakeDialogService : IDialogService
{
    /// <summary>The answer <see cref="Confirm"/> returns.</summary>
    public bool ConfirmResult { get; set; }

    /// <summary>The answer <see cref="ConfirmTyped"/> returns.</summary>
    public bool ConfirmTypedResult { get; set; }

    /// <summary>Every message shown via <see cref="ShowMessage"/>.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>How many times <see cref="Confirm"/> was called.</summary>
    public int ConfirmCount { get; private set; }

    /// <summary>How many times <see cref="ConfirmTyped"/> was called.</summary>
    public int ConfirmTypedCount { get; private set; }

    /// <summary>The <c>requiredText</c> of the last <see cref="ConfirmTyped"/> call.</summary>
    public string? LastTypedRequired { get; private set; }

    /// <inheritdoc />
    public void ShowMessage(string title, string message) => Messages.Add(message);

    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        ConfirmCount++;
        return ConfirmResult;
    }

    /// <inheritdoc />
    public bool ConfirmTyped(string title, string message, string requiredText)
    {
        ConfirmTypedCount++;
        LastTypedRequired = requiredText;
        return ConfirmTypedResult;
    }
}
