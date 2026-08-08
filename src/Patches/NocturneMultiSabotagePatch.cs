using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(SabotageSystemType), "get_AnyActive")]
internal static class NocturneMultiSabotageActivePatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!(NocturneConfig.MultiSabotage?.Value ?? false)) return true;
        try
        {
            AmongUsClient c = AmongUsClient.Instance;
            if (c != null && c.AmHost) return true;
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.Role == null || !me.Data.Role.IsImpostor) return true;
            __result = false;
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(SabotageSystemType), "UpdateSystem")]
internal static class NocturneMultiSabotageTimerPatch
{
    public static void Prefix(SabotageSystemType __instance, [HarmonyArgument(0)] PlayerControl player)
    {
        if (!(NocturneConfig.MultiSabotage?.Value ?? false)) return;
        try
        {
            if (__instance != null && player != null && player.AmOwner) __instance.Timer = 0f;
        }
        catch { }
    }
}
