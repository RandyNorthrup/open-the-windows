using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IUpdateIdentity (wuapi.idl). UpdateID is 0x60020003.</summary>
[ComImport]
[Guid("46297823-9940-4C09-AED9-CD3EA6D05968")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IUpdateIdentity
{
    [DispId(0x60020003)]
    string UpdateID { get; }
}
