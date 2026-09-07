using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IUpdate (wuapi.idl). Title is DISPID_VALUE; IsHidden is 0x60020011.</summary>
[ComImport]
[Guid("6A92B07A-D821-4682-B423-5C805022CC4D")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IUpdate
{
    [DispId(0)]
    string Title { get; }

    [DispId(0x6002000C)]
    IUpdateIdentity Identity { get; }

    [DispId(0x60020011)]
    bool IsHidden { get; set; }

    [DispId(0x60020028)]
    IStringCollection KBArticleIDs { get; }
}
