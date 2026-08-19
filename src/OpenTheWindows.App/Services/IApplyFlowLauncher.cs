using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.Services;

/// <summary>
/// Opens the modal review-and-apply dialog for a selection of entries. The UI
/// seam that a page view model calls; a fake records the request in tests.
/// </summary>
internal interface IApplyFlowLauncher
{
    /// <summary>Shows the review-and-apply dialog for <paramref name="entries"/> under <paramref name="options"/>.</summary>
    void Launch(string title, IReadOnlyList<TweakDefinition> entries, ApplyOptions options);
}
