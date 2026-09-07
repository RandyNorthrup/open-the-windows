using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IStringCollection (wuapi.idl). Item is DISPID_VALUE; Count is 0x60020001.</summary>
[ComImport]
[Guid("EFF90582-2DDC-480F-A06D-60F3FBC362C3")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IStringCollection
{
    [DispId(0)]
    string this[int index] { get; }

    [DispId(0x60020001)]
    int Count { get; }
}
