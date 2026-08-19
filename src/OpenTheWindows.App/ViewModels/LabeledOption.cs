namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// A selectable option with a display <paramref name="Label"/> and its bound
/// <paramref name="Value"/>. Used for the level dial and the risk filter so a
/// combo box can show localised text while binding to the domain enum (or
/// <see langword="null"/> for an "all" filter).
/// </summary>
/// <param name="Value">The bound value (a boxed enum, or null).</param>
/// <param name="Label">The localised display text.</param>
internal sealed record LabeledOption(object? Value, string Label);
