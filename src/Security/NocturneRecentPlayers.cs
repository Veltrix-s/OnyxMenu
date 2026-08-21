using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal sealed class RecentRow
{
    internal string Name;
    internal string Code;
    internal string Puid;
}

internal static class NocturneRecentPlayers
{
    private static readonly List<RecentRow> _rows = new List<RecentRow>();
    private static float _next;

    internal static List<RecentRow> Rows
    {
        get { Refresh(); return _rows; }
    }

    private static void Refresh()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1f;

        _rows.Clear();
        FriendsListManager m = FriendsListManager.Instance;
        if (m == null || m.RecentlyPlayedWith == null) return;

        var e = m.RecentlyPlayedWith.GetEnumerator();
        while (e.MoveNext())
        {
            var p = e.Current;
            if (p == null) continue;
            string fc = p.FriendCode ?? string.Empty;
            string puid = p.Puid ?? string.Empty;
            if (fc.Length == 0 && puid.Length == 0) continue;
            _rows.Add(new RecentRow { Name = p.PlayerName ?? "?", Code = fc, Puid = puid });
        }
    }

    internal static void Ban(RecentRow r)
    {
        if (r == null) return;
        NocturneAccess.AddBan(r.Name, r.Code, r.Puid);
        NocturneToast.Push(NocturneText.T("Бан-лист", "Ban list"), r.Name, 2.2f, NocturneNotifyKind.Danger);
    }

    internal static void White(RecentRow r)
    {
        if (r == null) return;
        NocturneAccess.AddWhite(r.Name, r.Code, r.Puid);
        NocturneToast.Push(NocturneText.T("Вайтлист", "Whitelist"), r.Name, 2.2f, NocturneNotifyKind.Success);
    }

    internal static void Clear()
    {
        FriendsListManager m = FriendsListManager.Instance;
        if (m != null) m.ClearRecentlyPlayed();
        _rows.Clear();
        _next = 0f;
    }
}
