using System.CommandLine;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// The options shared by every profile command (scan and apply): the profile
/// name, an optional catalogue override directory, the Draft toggle and the
/// single-entry <c>--only</c> selector. Composed by each command's option set so
/// the declarations live in one place.
/// </summary>
internal sealed class CommonProfileOptions
{
    public Option<string> Profile { get; } = CommandSupport.ProfileOption();

    public Option<string?> CatalogDir { get; } = CommandSupport.CatalogDirOption();

    public Option<bool> IncludeDraft { get; } = CommandSupport.IncludeDraftOption();

    public Option<string?> Only { get; } = CommandSupport.OnlyOption();

    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(Profile);
        command.Options.Add(CatalogDir);
        command.Options.Add(IncludeDraft);
        command.Options.Add(Only);
    }
}
