using System.Globalization;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.App.ViewModels;

/// <summary>One autostart that changed since the baseline, as shown on the Startup page.</summary>
internal sealed class PersistenceChangeViewModel
{
    /// <summary>Projects a change.</summary>
    public PersistenceChangeViewModel(PersistenceChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ChangeLabel = change.Change switch
        {
            PersistenceChangeKind.Added => "New",
            PersistenceChangeKind.Removed => "Gone",
            _ => "Changed",
        };
        KindLabel = change.Item.Kind switch
        {
            PersistenceKind.Service => "Service",
            PersistenceKind.ScheduledTask => "Scheduled task",
            PersistenceKind.RunKey => "Run key",
            _ => "Startup folder",
        };
        Key = change.Item.Key;
        Detail = change.Change == PersistenceChangeKind.Changed
            ? string.Create(CultureInfo.CurrentCulture, $"was: {change.Before!.Detail}{Environment.NewLine}now: {change.After!.Detail}")
            : change.Item.Detail;
    }

    /// <summary>New, Gone or Changed.</summary>
    public string ChangeLabel { get; }

    /// <summary>The kind of autostart.</summary>
    public string KindLabel { get; }

    /// <summary>Its identity (service name, task path, Run value, file path).</summary>
    public string Key { get; }

    /// <summary>What it runs (before and after for a change).</summary>
    public string Detail { get; }

    /// <summary>Spoken by screen readers for the list item.</summary>
    public override string ToString() => string.Create(CultureInfo.CurrentCulture, $"{ChangeLabel} {KindLabel}, {Key}");
}
