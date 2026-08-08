using System.Collections.Generic;
using HarmonyLib;

namespace Nocturne;

[HarmonyPatch(typeof(PlayerPhysics), "FixedUpdate")]
internal static class NocturneSeeThrough
{
    private static readonly Dictionary<byte, float> faded = new Dictionary<byte, float>();

    internal static void Reset() => faded.Clear();

    internal static void RestoreAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.cosmetics == null) continue;
                if (faded.TryGetValue(pc.PlayerId, out float a))
                {
                    pc.invisibilityAlpha = a;
                    try { pc.cosmetics.SetPhantomRoleAlpha(1f); } catch { }
                    ShowName(pc, true);
                }
            }
        }
        catch { }
        faded.Clear();
    }

    private static void Postfix(PlayerPhysics __instance)
    {
        bool vents = NocturneConfig.SeeVents.Value;
        bool ghosts = NocturneConfig.SeeGhosts.Value;
        if (!vents && !ghosts) { if (faded.Count > 0) RestoreAll(); return; }
        if (ShipStatus.Instance == null) return;

        try
        {
            PlayerControl p = __instance.myPlayer;
            PlayerControl me = PlayerControl.LocalPlayer;
            if (p == null || me == null || p.Data == null || me.Data == null) return;
            if (p.cosmetics == null || p == me || me.Data.IsDead) return;

            if (vents && p.inVent)
            {
                if (!faded.ContainsKey(p.PlayerId)) faded[p.PlayerId] = p.invisibilityAlpha;
                if (!p.Visible)
                {
                    p.Visible = true;
                    p.invisibilityAlpha = 0.5f;
                    p.cosmetics.SetPhantomRoleAlpha(0.5f);
                    ShowName(p, true);
                }
            }
            else if (faded.TryGetValue(p.PlayerId, out float a))
            {
                faded.Remove(p.PlayerId);
                p.invisibilityAlpha = a;
                p.cosmetics.SetPhantomRoleAlpha(1f);
                ShowName(p, true);
            }

            if (ghosts && p.Data.IsDead) p.Visible = true;
        }
        catch { }
    }

    private static void ShowName(PlayerControl p, bool on)
    {
        try
        {
            var t = p.cosmetics.nameText;
            if (t != null) t.gameObject.SetActive(on);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShipStatus), "OnEnable")]
internal static class NocturneSeeThroughReset
{
    private static void Postfix() => NocturneSeeThrough.Reset();
}

[HarmonyPatch(typeof(MeetingHud), "Start")]
internal static class NocturneSeeThroughMeetingReset
{
    private static void Postfix() => NocturneSeeThrough.RestoreAll();
}

[HarmonyPatch(typeof(PlayerControl), "CalculatedAlpha", MethodType.Getter)]
internal static class NocturneSeePhantoms
{
    private static void Postfix(PlayerControl __instance, ref float __result)
    {
        if (NocturneConfig.SeePhantoms == null || !NocturneConfig.SeePhantoms.Value) return;
        if (__instance == null || __instance == PlayerControl.LocalPlayer) return;
        if (__result >= 0.5f) return;
        __result = 0.5f;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TurnOnProtection))]
internal static class NocturneSeeProtections
{
    private static void Prefix(ref bool visible)
    {
        if (NocturneConfig.SeeProtections != null && NocturneConfig.SeeProtections.Value) visible = true;
    }
}
