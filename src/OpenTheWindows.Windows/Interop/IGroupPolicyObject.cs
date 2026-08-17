using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>
/// The Local Group Policy object COM interface (<c>userenv.dll</c>). Only the
/// three methods the product uses are given real signatures —
/// <see cref="OpenLocalMachineGPO"/>, <see cref="Save"/> and
/// <see cref="GetRegistryKey"/>; the earlier v-table slots are declared as
/// parameterless placeholders purely to preserve method ordering (they are
/// never invoked). Writing policy values through this object and saving them
/// records them in the Local GPO's <c>Registry.pol</c>, so they survive
/// <c>gpupdate</c>.
/// </summary>
[ComImport]
[Guid("EA502723-A23D-11d1-A7D3-0000F87571E3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGroupPolicyObject
{
    /// <summary>Placeholder for v-table slot 0 (New); never called.</summary>
    void New();

    /// <summary>Placeholder for v-table slot 1 (OpenDSGPO); never called.</summary>
    void OpenDSGPO();

    /// <summary>Loads the local machine GPO with the given open flags.</summary>
    void OpenLocalMachineGPO(uint flags);

    /// <summary>Placeholder for v-table slot 3 (OpenRemoteMachineGPO); never called.</summary>
    void OpenRemoteMachineGPO();

    /// <summary>Saves the registry policy changes, optionally registering the client-side extension.</summary>
    void Save(
        [MarshalAs(UnmanagedType.Bool)] bool machine,
        [MarshalAs(UnmanagedType.Bool)] bool add,
        in Guid extension,
        in Guid snapin);

    /// <summary>Placeholder for v-table slot 5 (Delete); never called.</summary>
    void Delete();

    /// <summary>Placeholder for v-table slot 6 (GetName); never called.</summary>
    void GetName();

    /// <summary>Placeholder for v-table slot 7 (GetDisplayName); never called.</summary>
    void GetDisplayName();

    /// <summary>Placeholder for v-table slot 8 (SetDisplayName); never called.</summary>
    void SetDisplayName();

    /// <summary>Placeholder for v-table slot 9 (GetPath); never called.</summary>
    void GetPath();

    /// <summary>Placeholder for v-table slot 10 (GetDSPath); never called.</summary>
    void GetDSPath();

    /// <summary>Placeholder for v-table slot 11 (GetFileSysPath); never called.</summary>
    void GetFileSysPath();

    /// <summary>Returns an open registry key handle for the requested section (machine = 2, user = 1).</summary>
    nint GetRegistryKey(uint section);
}
