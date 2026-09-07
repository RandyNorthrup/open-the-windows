using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>ISearchResult (wuapi.idl). DispId 0x60020003 is Updates.</summary>
[ComImport]
[Guid("D40CFF62-E08C-4498-941A-01E25F0FD33C")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface ISearchResult
{
    [DispId(0x60020003)]
    IUpdateCollection Updates { get; }
}
