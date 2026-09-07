using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IUpdateSession (wuapi.idl). DispId 0x60020004 is CreateUpdateSearcher.</summary>
[ComImport]
[Guid("816858A4-260D-4260-933A-2585F1ABC76B")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
[CoClass(typeof(UpdateSessionClass))]
internal interface IUpdateSession
{
    [DispId(0x60020004)]
    IUpdateSearcher CreateUpdateSearcher();
}
