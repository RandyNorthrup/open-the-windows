using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw users [--json]</c>: list the real user profiles on the machine (from the
/// ProfileList) that an all-users profile application would target, with whether
/// each per-user hive is currently loaded. Read-only; exit 0 unless the command errors.
/// </summary>
internal static class UsersCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var json = new Option<bool>("--json") { Description = "Emit the user list as JSON on stdout (stable schema)." };
        var command = new Command("users", "List the user hives an all-users apply would target (read-only).");
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            IReadOnlyList<UserProfile> users = services.CreateUserHiveEnumerator().Enumerate();

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(users, CliJsonContext.Default.IReadOnlyListUserProfile));
                return ExitCodes.Success;
            }

            if (users.Count == 0)
            {
                stdout.WriteLine("No user profiles found.");
                return ExitCodes.Success;
            }

            foreach (UserProfile user in users)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{user.Sid}  {(user.HiveLoaded ? "loaded    " : "not loaded")}  {user.ProfilePath}"));
            }

            return ExitCodes.Success;
        });

        return command;
    }
}
