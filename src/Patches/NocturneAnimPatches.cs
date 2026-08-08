using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(HudManager), "Update")]
internal static class NocturneAnimTickPatch
{
    public static void Postfix() { try { NocturneAnimations.Tick(); } catch { } }
}

[HarmonyPatch(typeof(PlayerPhysics), "HandleAnimation")]
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
