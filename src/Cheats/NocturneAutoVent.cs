using System;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneAutoVent : MonoBehaviour
{
    private static float _at = -1f;
    private static byte _pk = 255;
    private static float _pkAt;

    internal static bool On => NocturneConfig.AutoVentKill.Value;
    internal static bool BodyOn => NocturneConfig.BodyToVent.Value;

    internal static void Arm() { if (On) _at = Time.time + 0.25f; }

    internal static bool KillToVent(PlayerControl killer, PlayerControl target)
    {
        try
        {
            if (!BodyOn || _pk != 255) return false;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
            if (killer == null || killer != PlayerControl.LocalPlayer) return false;
            if (target == null || target.Data == null || target.Data.IsDead) return false;
            if (ShipStatus.Instance == null || MeetingHud.Instance != null) return false;

            int idx = NearestIndex(target.GetTruePosition());
            if (idx < 0) return false;

            try { NocturneVentTp.Send(target, idx); } catch { }
            _pk = target.PlayerId;
            _pkAt = Time.time + 0.4f;
            return true;
        }
        catch { return false; }
    }

    public void Update()
    {
        if (_pk != 255 && Time.time >= _pkAt)
        {
            byte id = _pk;
            _pk = 255;
            DoKill(id);
        }

        if (_at < 0f || Time.time < _at) return;
        _at = -1f;
        Escape();
    }

    private static void DoKill(byte id)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            PlayerControl t = ById(id);
            if (me == null || t == null || t.Data == null || t.Data.IsDead) return;
            me.RpcMurderPlayer(t, true);
        }
        catch { }
    }

    private static PlayerControl ById(byte id)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
                if (e.Current != null && e.Current.PlayerId == id) return e.Current;
        }
        catch { }
        return null;
    }

    private static void Escape()
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.IsDead || me.MyPhysics == null) return;
            if (ShipStatus.Instance == null || MeetingHud.Instance != null || me.inVent) return;

            Vent best = Nearest(me.GetTruePosition());
            if (best == null) return;

            try { me.NetTransform.RpcSnapTo(best.transform.position); } catch { }
            try { me.MyPhysics.RpcEnterVent(best.Id); } catch { }
        }
        catch { }
    }

    private static int NearestIndex(Vector2 pos)
    {
        int best = -1;
        float bd = float.MaxValue;
        try
        {
            var vents = ShipStatus.Instance.AllVents;
            if (vents == null) return -1;
            for (int i = 0; i < vents.Count; i++)
            {
                Vent v = vents[i];
                if (v == null) continue;
                float d = Vector2.Distance(pos, v.transform.position);
                if (d < bd) { bd = d; best = i; }
            }
        }
        catch { }
        return best;
    }

    private static Vent Nearest(Vector2 pos)
    {
        Vent best = null;
        float bd = float.MaxValue;
        try
        {
            var vents = ShipStatus.Instance.AllVents;
            if (vents == null) return null;
            for (int i = 0; i < vents.Count; i++)
            {
                Vent v = vents[i];
                if (v == null) continue;
                float d = Vector2.Distance(pos, v.transform.position);
                if (d < bd) { bd = d; best = v; }
            }
        }
        catch { }
        return best;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer), new Type[] { typeof(PlayerControl), typeof(MurderResultFlags) })]
internal static class NocturneAutoVentKillPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        try
        {
            if (!NocturneAutoVent.On || __instance == null || __instance != PlayerControl.LocalPlayer) return;
            NocturneAutoVent.Arm();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
internal static class NocturneBodyToVentPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        try { return !NocturneAutoVent.KillToVent(__instance, target); }
        catch { return true; }
    }
}
