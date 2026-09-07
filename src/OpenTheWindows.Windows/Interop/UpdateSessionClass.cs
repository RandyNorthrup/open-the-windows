using System.Runtime.InteropServices;

namespace OpenTheWindows.Windows.Interop;

/// <summary>Microsoft.Update.Session coclass (CLSID_UpdateSession).</summary>
[ComImport]
[Guid(Wuapi.SessionClsid)]
internal sealed class UpdateSessionClass;
