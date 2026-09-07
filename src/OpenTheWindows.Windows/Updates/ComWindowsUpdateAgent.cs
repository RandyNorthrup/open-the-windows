using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows.Interop;

namespace OpenTheWindows.Windows.Updates;

/// <summary>Windows Update Agent over the typed wuapi IDispatch interfaces.</summary>
[SupportedOSPlatform("windows")]
internal sealed class ComWindowsUpdateAgent : IWindowsUpdateAgent
{
    /// <inheritdoc />
    public IReadOnlyList<DiscoveredUpdate> Search(string criteria)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(criteria);
        IUpdateCollection updates = CreateSearcher().Search(criteria).Updates;
        List<DiscoveredUpdate> list = [];
        for (int i = 0; i < updates.Count; i++)
        {
            list.Add(Read(updates[i]));
        }

        return list;
    }

    /// <inheritdoc />
    public void SetHidden(string identity, bool hidden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        string criteria = string.Create(CultureInfo.InvariantCulture, $"UpdateID='{identity}'");
        IUpdateCollection updates = CreateSearcher().Search(criteria).Updates;
        if (updates.Count == 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"Windows Update Agent has no update with identity '{identity}'."));
        }

        updates[0].IsHidden = hidden;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:Recognized reflection pattern",
        Justification = "Activates a fixed in-box COM server (Microsoft.Update.Session) by CLSID; the type is an OS COM class, not managed code the trimmer can remove.")]
    private static IUpdateSearcher CreateSearcher()
    {
        Type type = Type.GetTypeFromCLSID(new Guid(Wuapi.SessionClsid))
            ?? throw new InvalidOperationException("The Windows Update Agent COM session is not registered.");
        var session = (IUpdateSession)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("The Windows Update Agent COM session could not be created."));
        return session.CreateUpdateSearcher();
    }

    private static DiscoveredUpdate Read(IUpdate update)
    {
        IStringCollection kb = update.KBArticleIDs;
        List<string> ids = [];
        for (int i = 0; i < kb.Count; i++)
        {
            string article = kb[i];
            if (!string.IsNullOrWhiteSpace(article))
            {
                ids.Add(article);
            }
        }

        return new DiscoveredUpdate(update.Identity.UpdateID, update.Title, ids, update.IsHidden);
    }
}
