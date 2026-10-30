using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(ZiplineConsole), nameof(ZiplineConsole.Use))]
internal static class NoZiplineCooldownPatch
{
    public static void Postfix(ZiplineConsole __instance)
    {
        if (!NocturneConfig.BuffMapCd.Value || __instance == null) return;

        __instance.CoolDown = 0f;
        if (__instance.destination != null) __instance.destination.CoolDown = 0f;
    }
}
