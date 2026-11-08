using System.Globalization;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// The exact argument shapes a catalogue <see cref="CommandAction"/> may pass to
/// each allow-listed executable. An allow-listed binary is not enough on its own:
/// <c>powercfg</c>, <c>fsutil</c> and <c>auditpol</c> each have verbs that do far
/// more than a tweak needs, so every argument vector (apply and revert) must match
/// one of the shapes below or the action is refused.
/// </summary>
public static class CommandArgumentPolicy
{
    private const string Powercfg = "powercfg.exe";
    private const string Fsutil = "fsutil.exe";
    private const string Auditpol = "auditpol.exe";
    private const string SubcategoryPrefix = "/subcategory:";
    private const int VerbAndValue = 2;
    private const int VerbTypeAndValue = 3;
    private const int FsutilBehaviorArgs = 4;
    private const int AuditpolMinimumArgs = 3;

    private static readonly HashSet<string> PowercfgSchemeVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "/setactive", "-setactive", "/duplicatescheme", "-duplicatescheme", "/delete", "-delete",
    };

    private static readonly HashSet<string> PowercfgDuplicateVerbs = new(StringComparer.OrdinalIgnoreCase) { "/duplicatescheme", "-duplicatescheme" };

    private static readonly HashSet<string> PowercfgDeleteVerbs = new(StringComparer.OrdinalIgnoreCase) { "/delete", "-delete" };

    /// <summary>
    /// The schemes and the hidden template Windows ships (Balanced, High performance, Power saver,
    /// Ultimate Performance). A catalogue command may activate or copy them but never delete one or
    /// copy over one: deleting the hidden Ultimate Performance template removes it from the PC.
    /// </summary>
    private static readonly HashSet<Guid> BuiltInSchemes =
    [
        new("381b4222-f694-41f0-9685-ff5bb260df2e"),
        new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        new("a1841308-3541-4fab-bc81-f71556f20b4a"),
        new("e9a42b02-d5df-448d-aa00-03f14749eb61"),
    ];

    private static readonly HashSet<string> PowercfgHibernateVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "/hibernate", "-hibernate", "/h", "-h",
    };

    private static readonly HashSet<string> HibernateSwitches = new(StringComparer.OrdinalIgnoreCase) { "on", "off" };
    private static readonly HashSet<string> HibernateTypeFlags = new(StringComparer.OrdinalIgnoreCase) { "/type", "-type" };
    private static readonly HashSet<string> HibernateTypes = new(StringComparer.OrdinalIgnoreCase) { "reduced", "full" };
    private static readonly HashSet<string> Disable8Dot3Values = new(StringComparer.Ordinal) { "0", "1", "2", "3" };

    private static readonly HashSet<string> AuditpolSwitches = new(StringComparer.OrdinalIgnoreCase)
    {
        "/success:enable", "/success:disable", "/failure:enable", "/failure:disable",
    };

    /// <summary>
    /// Returns why running <paramref name="executable"/> with
    /// <paramref name="arguments"/> is refused, or <see langword="null"/> when the
    /// pair is on the allow-list and matches a permitted shape.
    /// </summary>
    public static string? Refusal(string executable, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!CommandAction.AllowedExecutables.Contains(executable))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Executable '{executable}' is not on the command allow-list.");
        }

        bool permitted = executable.ToUpperInvariant() switch
        {
            "POWERCFG.EXE" => IsPowercfg(arguments),
            "FSUTIL.EXE" => IsFsutil(arguments),
            "AUDITPOL.EXE" => IsAuditpol(arguments),
            _ => false,
        };

        return permitted
            ? null
            : string.Create(CultureInfo.InvariantCulture,
                $"Arguments '{string.Join(' ', arguments)}' are not a permitted form for '{executable}'.");
    }

    /// <summary>The executables this policy recognises, matching <see cref="CommandAction.AllowedExecutables"/>.</summary>
    public static IReadOnlyList<string> Executables { get; } = [Powercfg, Fsutil, Auditpol];

    private static bool IsPowercfg(IReadOnlyList<string> args)
    {
        if (args.Count == VerbAndValue && PowercfgSchemeVerbs.Contains(args[0]))
        {
            return Guid.TryParseExact(args[1], "D", out Guid scheme)
                && !(PowercfgDeleteVerbs.Contains(args[0]) && BuiltInSchemes.Contains(scheme));
        }

        if (args.Count == VerbTypeAndValue && PowercfgDuplicateVerbs.Contains(args[0]))
        {
            // A copy into a scheme GUID of the catalogue's own, so it can be activated and deleted again.
            return Guid.TryParseExact(args[1], "D", out _)
                && Guid.TryParseExact(args[2], "D", out Guid destination)
                && !BuiltInSchemes.Contains(destination);
        }

        if (args.Count == VerbAndValue && PowercfgHibernateVerbs.Contains(args[0]))
        {
            return HibernateSwitches.Contains(args[1]);
        }

        return args.Count == VerbTypeAndValue
            && PowercfgHibernateVerbs.Contains(args[0])
            && HibernateTypeFlags.Contains(args[1])
            && HibernateTypes.Contains(args[2]);
    }

    private static bool IsFsutil(IReadOnlyList<string> args)
        => args.Count == FsutilBehaviorArgs
            && string.Equals(args[0], "behavior", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[1], "set", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[2], "disable8dot3", StringComparison.OrdinalIgnoreCase)
            && Disable8Dot3Values.Contains(args[3]);

    private static bool IsAuditpol(IReadOnlyList<string> args)
    {
        if (args.Count < AuditpolMinimumArgs
            || !string.Equals(args[0], "/set", StringComparison.OrdinalIgnoreCase)
            || !args[1].StartsWith(SubcategoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string subcategory = args[1][SubcategoryPrefix.Length..];
        bool nameOk = subcategory.Length > 0 && subcategory.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '/' or '-');
        return nameOk && args.Skip(VerbAndValue).All(AuditpolSwitches.Contains);
    }
}
