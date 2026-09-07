using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>IUpdateSearcher (wuapi.idl). DispId 0x6002000D is Search.</summary>
[ComImport]
[Guid("8F45ABF1-F9AE-4B95-A933-F0F66E5056EA")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IUpdateSearcher
{
    [DispId(0x6002000D)]
    ISearchResult Search(string criteria);
}
