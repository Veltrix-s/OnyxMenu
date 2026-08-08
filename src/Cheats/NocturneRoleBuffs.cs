using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneRoleBuffs : MonoBehaviour
{
    private float auraAt;

    internal static bool NoCd => NocturneConfig.BuffNoCd.Value;
    internal static bool VentAny => NocturneConfig.BuffVentAny.Value;
    internal static bool Reach => NocturneConfig.BuffKillReach.Value;
    internal static bool KillAny => NocturneConfig.BuffKillAny.Value;

    internal static bool Alive(PlayerControl p) => p != null && p.Data != null && !p.Data.IsDead;

    internal static bool Host()
    {
        try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { return false; }
    }

    internal static bool Ventless()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!Alive(me)) return false;
        RoleBehaviour r = me.Data.Role;
        return r == null || !r.CanVent;
    }

    internal static bool Faded()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!Alive(me) || me.Data.Role == null) return false;
        try
        {
            PhantomRole ph = ((Il2CppObjectBase)(object)me.Data.Role).TryCast<PhantomRole>();
            return ph != null && ph.isInvisible;
        }
        catch { return false; }
    }

    internal static float VanillaDist()
    {
        try
        {
            int d = GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.KillDistance);
            return d <= 0 ? 1f : (d == 1 ? 1.8f : 2.5f);
        }
        catch { return 2.5f; }
    }

    internal static float Radius() => Reach ? NocturneConfig.BuffKillDist.Value : VanillaDist();

    internal static PlayerControl NearestValid(DetectiveRole role)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (role == null || !Alive(me)) return null;
        Vector2 p = me.GetTruePosition();
        PlayerControl best = null;
        float bd = float.MaxValue;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl t = e.Current;
            if (t == null || t == me || !Alive(t) || t.Data.Disconnected) continue;
            if (!role.IsValidTarget(t.Data)) continue;
            float d = Vector2.Distance(p, t.GetTruePosition());
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }

    internal static PlayerControl NearestKill(float max, bool anyone)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!Alive(me)) return null;
        Vector2 p = me.GetTruePosition();
        PlayerControl best = null;
        float bd = max;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl t = e.Current;
            if (t == null || t == me || !Alive(t) || t.inVent) continue;
            RoleBehaviour tr = t.Data.Role;
            if (!anyone && tr != null && (int)tr.TeamType == 1) continue;
            float d = Vector2.Distance(p, t.GetTruePosition());
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }

    public void Update()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || ShipStatus.Instance == null) return;

        if (VentAny && Ventless())
        {
            HudManager hud = HudManager.Instance;
            if (hud != null && hud.ImpostorVentButton != null && !hud.ImpostorVentButton.gameObject.activeSelf)
                hud.ImpostorVentButton.gameObject.SetActive(true);
        }

        if (NocturneConfig.BuffKillAura.Value) Aura(me);
    }

    private void Aura(PlayerControl me)
    {
        if (!Alive(me) || me.inVent || MeetingHud.Instance != null) return;
        RoleBehaviour r = me.Data.Role;
        if (r == null || !r.CanUseKillButton) return;
        if (Time.time < auraAt) return;
        if (me.killTimer > 0.05f && !(NocturneConfig.BuffNoKillCd.Value && Host())) return;

        PlayerControl t = NearestKill(Radius(), KillAny);
        if (t == null) return;
        auraAt = Time.time + 0.15f;
        try { me.CmdCheckMurder(t); } catch { }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
internal static class BuffVentUsePatch
{
    public static void Postfix(Vent __instance, NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse, ref float __result)
    {
        if (!NocturneRoleBuffs.VentAny || !NocturneRoleBuffs.Ventless()) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (pc == null || me == null || pc.PlayerId != me.PlayerId) return;

        float d = Vector2.Distance(me.GetTruePosition(), __instance.transform.position);
        __result = d;
        couldUse = true;
        canUse = d <= __instance.UsableDistance;
    }
}

[HarmonyPatch(typeof(ActionButton), nameof(ActionButton.SetCoolDown))]
internal static class BuffCooldownPatch
{
    public static void Prefix(ref float __0)
    {
        if (NocturneRoleBuffs.NoCd) __0 = 0f;
    }
}

[HarmonyPatch(typeof(PlayerControl), "CanMove", MethodType.Getter)]
internal static class BuffVentMovePatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (__result) return;
        if (__instance != PlayerControl.LocalPlayer || !__instance.inVent) return;
        if (__instance.Data == null || __instance.Data.IsDead) return;
        if (!NocturneConfig.BuffVentWalk.Value && !BuffVentSabPatch.Opening) return;
        __result = true;
    }
}

[HarmonyPatch(typeof(ImpostorRole), "FindClosestTarget")]
internal static class BuffReachPatch
{
    public static bool Prefix(ref PlayerControl __result)
    {
        bool any = NocturneRoleBuffs.KillAny;
        bool ghost = NocturneConfig.BuffVanishKill.Value && NocturneRoleBuffs.Faded();
        if (!NocturneRoleBuffs.Reach && !any && !ghost) return true;
        __result = NocturneRoleBuffs.NearestKill(NocturneRoleBuffs.Radius(), any);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetKillTimer))]
internal static class BuffKillCdPatch
{
    public static void Prefix(PlayerControl __instance, ref float __0)
    {
        if (__instance != PlayerControl.LocalPlayer) return;
        if (NocturneConfig.BuffNoKillCd.Value && NocturneRoleBuffs.Host()) __0 = 0f;
    }
}

[HarmonyPatch(typeof(ShapeshifterRole), "FixedUpdate")]
internal static class BuffShiftPatch
{
    public static void Postfix(ShapeshifterRole __instance)
    {
        if (!NocturneConfig.BuffSsForever.Value || __instance == null) return;
        if (__instance.Player != PlayerControl.LocalPlayer) return;
        __instance.durationSecondsRemaining = float.MaxValue;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift), new Type[] { typeof(PlayerControl), typeof(bool) })]
internal static class BuffShiftQuietPatch
{
    public static void Prefix(ref bool __1)
    {
        if (NocturneConfig.BuffSsQuiet.Value) __1 = false;
    }
}

[HarmonyPatch(typeof(EngineerRole), "FixedUpdate")]
internal static class BuffEngPatch
{
    public static void Postfix(EngineerRole __instance)
    {
        if (__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;
        if (NocturneConfig.BuffEngVent.Value) __instance.inVentTimeRemaining = float.MaxValue;
        if (!NocturneConfig.BuffEngCd.Value || __instance.cooldownSecondsRemaining <= 0f) return;

        __instance.cooldownSecondsRemaining = 0f;
        try
        {
            HudManager hud = HudManager.Instance;
            if (hud != null && hud.AbilityButton != null) hud.AbilityButton.SetCooldownFill(0f);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PhantomRole), "FixedUpdate")]
internal static class BuffPhantomPatch
{
    private static float full;

    public static void Postfix(PhantomRole __instance)
    {
        if (__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;

        if (__instance.isInvisible)
        {
            float left = __instance.durationSecondsRemaining;
            if (left > full) full = left;
            if (NocturneConfig.BuffPhVanish.Value && full > 0f && left < full)
                __instance.durationSecondsRemaining = full;
        }
        else full = 0f;
    }
}

[HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.Refresh))]
internal static class BuffVentSabPatch
{
    private static float openUntil;

    internal static bool Opening => Time.unscaledTime < openUntil;

    internal static void HoldOpen() => openUntil = Time.unscaledTime + 0.5f;

    public static void Postfix(SabotageButton __instance)
    {
        if (!NocturneConfig.BuffVentSab.Value || __instance == null) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!NocturneRoleBuffs.Alive(me) || !me.inVent) return;
        RoleBehaviour r = me.Data.Role;
        if (r == null || (int)r.TeamType != 1) return;
        try { __instance.SetEnabled(); } catch { }
    }
}

[HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.DoClick))]
internal static class BuffVentSabClickPatch
{
    public static bool Prefix()
    {
        if (!NocturneConfig.BuffVentSab.Value) return true;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!NocturneRoleBuffs.Alive(me) || !me.inVent) return true;
        RoleBehaviour r = me.Data.Role;
        if (r == null || (int)r.TeamType != 1) return true;

        HudManager hud = HudManager.Instance;
        if (hud == null) return true;

        try
        {
            BuffVentSabPatch.HoldOpen();
            MapOptions o = new MapOptions();
            o.Mode = MapOptions.Modes.Sabotage;
            o.AllowMovementWhileMapOpen = true;
            hud.ToggleMapVisible(o);
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(ScientistRole), "Update")]
internal static class BuffSciPatch
{
    public static void Postfix(ScientistRole __instance)
    {
        if (__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;
        if (NocturneConfig.BuffSciCd.Value) __instance.currentCooldown = 0f;
        if (NocturneConfig.BuffSciBat.Value) __instance.currentCharge = float.MaxValue;
    }
}

[HarmonyPatch(typeof(DetectiveRole), "FindClosestTarget")]
internal static class BuffDetPatch
{
    public static bool Prefix(DetectiveRole __instance, ref PlayerControl __result)
    {
        if (!NocturneConfig.BuffDetReach.Value) return true;
        __result = NocturneRoleBuffs.NearestValid(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(Ladder), "SetDestinationCooldown")]
internal static class BuffLadderPatch
{
    public static bool Prefix() => !NocturneConfig.BuffMapCd.Value;
}

[HarmonyPatch(typeof(global::Console), nameof(global::Console.CanUse))]
internal static class BuffTaskPatch
{
    public static void Prefix(global::Console __instance)
    {
        if (NocturneConfig.BuffImpTasks.Value && __instance != null) __instance.AllowImpostor = true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
internal static class BuffAutoReportPatch
{
    public static void Postfix(PlayerControl target)
    {
        if (!NocturneConfig.BuffAutoReport.Value) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (!NocturneRoleBuffs.Alive(me) || target == null || target.Data == null) return;
        try { me.CmdReportDeadBody(target.Data); } catch { }
    }
}
