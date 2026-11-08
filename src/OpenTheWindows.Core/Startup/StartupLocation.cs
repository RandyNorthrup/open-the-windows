namespace OpenTheWindows.Core.Startup;

/// <summary>Where Windows finds an app to start at sign-in (the places Task Manager's Startup apps lists).</summary>
public enum StartupLocation
{
    /// <summary><c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run</c> (every user).</summary>
    MachineRun = 0,

    /// <summary><c>HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run</c> (32-bit apps, every user).</summary>
    MachineRun32 = 1,

    /// <summary>The signed-in user's <c>SOFTWARE\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
    UserRun = 2,

    /// <summary>The all-users Startup folder under <c>%ProgramData%</c>.</summary>
    MachineStartupFolder = 3,

    /// <summary>The signed-in user's Startup folder under their <c>AppData</c>.</summary>
    UserStartupFolder = 4,
}
