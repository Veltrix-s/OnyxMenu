using System.Collections;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneMorph
{
    private const ushort Shifter = 5;
    private const float Gap = 0.45f;

    private static bool Ready()
    {
        try { return AmongUsClient.Instance != null && ShipStatus.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost; }
        catch { return false; }
    }

    private static bool Valid(PlayerControl pc) => pc != null && pc.Data != null && !pc.Data.Disconnected;

    internal static string Into(PlayerControl victim, PlayerControl target)
    {
        if (!Ready()) return NocturneText.T("хост, в матче", "host, in match");
        if (!Valid(victim) || !Valid(target)) return NocturneText.T("нет цели", "no target");

        var list = new List<PlayerControl> { victim };
        Run(list, target);
        return NocturneText.T("морф в " + Nm(target), "morph into " + Nm(target));
    }

    internal static string IntoAll(PlayerControl target)
    {
        if (!Ready()) return NocturneText.T("хост, в матче", "host, in match");
        if (!Valid(target)) return NocturneText.T("нет цели", "no target");

        var list = new List<PlayerControl>();
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext()) { PlayerControl pc = e.Current; if (Valid(pc) && pc != target) list.Add(pc); }
        }
        catch { }
        if (list.Count == 0) return NocturneText.T("нет игроков", "no players");
        Run(list, target);
        return NocturneText.T($"морфит {list.Count} в {Nm(target)}", $"morphing {list.Count} into {Nm(target)}");
    }

    internal static string RevertAll()
    {
        if (!Ready()) return NocturneText.T("хост, в матче", "host, in match");

        var list = new List<PlayerControl>();
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl pc = e.Current;
                if (Valid(pc) && pc.shapeshiftTargetPlayerId != 255) list.Add(pc);
            }
        }
        catch { }
        if (list.Count == 0) return NocturneText.T("нет морфов", "no morphs");
        Run(list, null);
        return NocturneText.T($"сброс: {list.Count}", $"revert: {list.Count}");
    }

    private static void Run(List<PlayerControl> vics, PlayerControl into)
    {
        try { ((MonoBehaviour)AmongUsClient.Instance).StartCoroutine(Co(vics, into).WrapToIl2Cpp()); }
        catch { }
    }

    private static IEnumerator Co(List<PlayerControl> vics, PlayerControl into)
    {
        var roles = new Dictionary<byte, ushort>();
        foreach (PlayerControl vic in vics)
        {
            if (!Valid(vic)) continue;
            ushort orig = (ushort)(int)vic.Data.RoleType;
            roles[vic.PlayerId] = orig;
            if (orig != Shifter) SetRole(vic, Shifter);
        }

        yield return new WaitForSeconds(Gap);

        foreach (PlayerControl vic in vics)
        {
            if (!Valid(vic)) continue;
            Shift(vic, into != null ? into : vic);
        }

        yield return new WaitForSeconds(Gap);

        foreach (PlayerControl vic in vics)
        {
            if (!Valid(vic)) continue;
            if (roles.TryGetValue(vic.PlayerId, out ushort orig) && orig != Shifter) SetRole(vic, orig);
        }
    }

    private static void SetRole(PlayerControl pc, ushort role)
    {
        NocturneForceRoles.Assign(pc, (RoleTypes)role);
    }

    private static void Shift(PlayerControl pc, PlayerControl target)
    {
        try { pc.RpcShapeshift(target, false); } catch { }
    }

    private static string Nm(PlayerControl p)
    {
        try { return NocturneNameColor.Strip(p.Data.PlayerName ?? "?").Trim(); }
        catch { return "?"; }
    }
}
