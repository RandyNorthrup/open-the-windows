using System.Windows;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.App.Views;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IApplyFlowLauncher"/> that builds a fresh <see cref="ApplyFlowViewModel"/>
/// per launch, primes it with the plan and shows the modal
/// <see cref="ApplyDialog"/> owned by the main window.
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
        flow.Start(title, entries, options);
        ApplyDialog dialog = new()
        {
            DataContext = flow,
            Owner = Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }
}
