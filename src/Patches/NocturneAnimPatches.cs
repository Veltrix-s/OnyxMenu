using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class NocturneAnimTickPatch
{
    public static void Postfix() { try { NocturneAnimations.Tick(); } catch { } }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
internal static class NocturneMoonWalkPatch
{
    public static bool Prefix(PlayerPhysics __instance)
    {
        if (!NocturneConfig.MoonWalk.Value || __instance == null || !__instance.AmOwner) return HarmonyControl.Continue;
        __instance.ResetAnimState();
        return HarmonyControl.SkipOriginal;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
internal static class NocturneAnimForcePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        try
        {
            if (!NocturneAnimations.ClimbHeld) return;
            if (__instance == null || __instance.myPlayer == null || __instance.myPlayer != PlayerControl.LocalPlayer) return;
            NocturneAnimations.ForceClimb(__instance.Animations);
        }
        catch { }
    }
}
