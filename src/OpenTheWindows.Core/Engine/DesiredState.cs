using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>One action of one entry, paired with its owning entry for context.</summary>
/// <param name="Entry">The catalogue entry the action belongs to.</param>
/// <param name="Action">The desired state change.</param>
public sealed record DesiredState(TweakDefinition Entry, ITweakAction Action);
