using System.Collections.Generic;

namespace Nocturne;

internal sealed class RecentEntry
{
    internal string Name;
    internal string Fc;
    internal string Puid;
    internal string Platform;
    internal string Raw;
    internal string Level;
    internal string Left;

    internal string Title;
    internal string Info;

    internal string Key
    {
        get
        {
            if (!string.IsNullOrEmpty(Fc)) return "fc:" + Fc;
            if (!string.IsNullOrEmpty(Puid)) return "puid:" + Puid;
            return "name:" + Name;
        }
    }

    internal void Seal()
    {
        Title = "<b>" + Name + "</b>";

        string s = Left;
        if (!string.IsNullOrEmpty(Level) && Level != "?") s += " · " + NocturneText.T("ур.", "lv.") + Level;
        if (!string.IsNullOrEmpty(Platform) && Platform != "?") s += " · " + Platform;
        if (!string.IsNullOrEmpty(Raw)) s += " · " + Raw;
        if (!string.IsNullOrEmpty(Fc)) s += " · FC " + Fc;
        Info = s;
    }
}

internal static class NocturneRecent
{
    private const int Max = 30;

    private static readonly List<RecentEntry> _list = new List<RecentEntry>();

    internal static int Count => _list.Count;
    internal static IReadOnlyList<RecentEntry> Entries => _list;
    internal static void Clear() => _list.Clear();

    internal static void Push(RecentEntry e)
    {
        if (e == null) return;
        e.Seal();

        string key = e.Key;
        for (int i = _list.Count - 1; i >= 0; i--)
            if (_list[i].Key == key) _list.RemoveAt(i);

        _list.Insert(0, e);
        if (_list.Count > Max) _list.RemoveRange(Max, _list.Count - Max);
    }
}
