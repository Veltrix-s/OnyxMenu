using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class NocturneKillTools
{
    private static bool Ready(out PlayerControl me)
    {
        me = PlayerControl.LocalPlayer;
        return me != null && me.Data != null
            && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && ShipStatus.Instance != null && MeetingHud.Instance == null;
    }

    private static bool Valid(PlayerControl pc)
        => pc != null && pc.Data != null && !pc.Data.Disconnected && !pc.Data.IsDead;

    private static bool Imp(PlayerControl pc)
    {
        try { return pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor; }
        catch { return false; }
    }

    private static void Murder(PlayerControl me, PlayerControl t)
    {
        try { me.RpcMurderPlayer(t, true); }
        catch { try { me.CmdCheckMurder(t); } catch { } }
    }

    internal static string KillOne(PlayerControl t)
    {
        if (!Ready(out PlayerControl me)) return Deny();
        if (!Valid(t) || t == me) return NocturneText.T("Нет цели.", "No target.");
        Murder(me, t);
        return NocturneText.T("Готово: ", "Done: ") + t.Data.PlayerName;
    }

    internal static string Telekill(PlayerControl t)
    {
        if (!Ready(out PlayerControl me)) return Deny();
        if (!Valid(t) || t == me || me.NetTransform == null) return NocturneText.T("Нет цели.", "No target.");
        Vector2 back = me.GetTruePosition();
        try { me.NetTransform.SnapTo(t.GetTruePosition()); } catch { }
        Murder(me, t);
        try { me.NetTransform.RpcSnapTo(back); } catch { }
        return NocturneText.T("Телекилл: ", "Telekill: ") + t.Data.PlayerName;
    }

    internal static string KillAll(int mode)
    {
        if (!Ready(out PlayerControl me)) return Deny();
        List<PlayerControl> pool = new List<PlayerControl>();
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (Valid(pc) && pc != me) pool.Add(pc);
        }
        catch { }

        int n = 0;
        foreach (PlayerControl pc in pool)
        {
            if (mode == 1 && Imp(pc)) continue;
            if (mode == 2 && !Imp(pc)) continue;
            Murder(me, pc);
            n++;
        }
        return NocturneText.T("Убрано: ", "Killed: ") + n;
    }

    private static string Deny() => NocturneText.T("Только хост, в матче.", "Host, in match only.");
}
