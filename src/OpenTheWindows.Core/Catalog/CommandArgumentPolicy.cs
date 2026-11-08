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
            return Guid.TryParseExact(args[1], "D", out _);
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
