using HarmonyLib;

namespace Nocturne;

internal static class NocturneComms
{
    internal static bool On => NocturneConfig.CommsBypass != null && NocturneConfig.CommsBypass.Value;
}

[HarmonyPatch(typeof(PlayerControl), "AreCommsAffected")]
internal static class NocturneCommsTaskPatch
{
    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (!__result || !NocturneComms.On) return;
        if (__instance != PlayerControl.LocalPlayer) return;
        __result = false;
    }
}

[HarmonyPatch(typeof(RoleBehaviour), "CommsSabotaged", MethodType.Getter)]
internal static class NocturneCommsRolePatch
{
    public static void Postfix(RoleBehaviour __instance, ref bool __result)
    {
        if (!__result || !NocturneComms.On) return;
        if (__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;
        __result = false;
    }
}
