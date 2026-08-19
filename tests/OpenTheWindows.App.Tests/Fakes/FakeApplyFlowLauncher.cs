using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Records review-and-apply launches so a page test can assert what it would apply.</summary>
internal sealed class FakeApplyFlowLauncher : IApplyFlowLauncher
{
    /// <summary>How many times <see cref="Launch"/> was called.</summary>
    public int LaunchCount { get; private set; }

    /// <summary>The title of the last launch.</summary>
    public string? LastTitle { get; private set; }

    /// <summary>The entries of the last launch.</summary>
    public IReadOnlyList<TweakDefinition>? LastEntries { get; private set; }

    /// <summary>The options of the last launch.</summary>
    public ApplyOptions? LastOptions { get; private set; }

    /// <inheritdoc />
    public void Launch(string title, IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        LaunchCount++;
        LastTitle = title;
        LastEntries = entries;
        LastOptions = options;
    }
}
