using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IUpdateCollection (wuapi.idl).</summary>
[ComImport]
[Guid("07F7438C-7709-4CA5-B518-91279288134E")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IUpdateCollection
{
    [DispId(0)]
    IUpdate this[int index] { get; }

    [DispId(0x60020001)]
    int Count { get; }
}
