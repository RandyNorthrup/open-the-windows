using System.Windows;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.App.Views;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IApplyFlowLauncher"/> that builds a fresh <see cref="ApplyFlowViewModel"/>
/// per launch, starts its plan on a background thread and shows the modal
/// <see cref="ApplyDialog"/> owned by the main window while the plan loads.
/// </summary>
internal sealed class ApplyFlowLauncher : IApplyFlowLauncher
{
    private readonly Func<ApplyFlowViewModel> _flowFactory;

    /// <summary>Creates the launcher over a factory that yields a fresh flow view model.</summary>
    public ApplyFlowLauncher(Func<ApplyFlowViewModel> flowFactory)
    {
        ArgumentNullException.ThrowIfNull(flowFactory);
        _flowFactory = flowFactory;
    }

    /// <inheritdoc />
    public void Launch(string title, IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ApplyFlowViewModel flow = _flowFactory();

        // StartAsync never faults (a planning error lands the flow in its Failed stage), and
        // its continuation runs on the dialog's message loop, so the dialog opens at once
        // with a progress bar and fills in when the plan is ready.
        _ = flow.StartAsync(title, entries, options);
        ApplyDialog dialog = new()
        {
            DataContext = flow,
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }
}
