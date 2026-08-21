using HarmonyLib;

namespace Nocturne.Patches;

internal static class NocturneClassic
{
    internal static bool On => NocturneConfig.ClassicBody.Value;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.BodyType), MethodType.Getter)]
internal static class NocturneClassicBodyGetPatch
{
    public static bool Prefix(PlayerControl __instance, ref PlayerBodyTypes __result)
    {
        if (__instance == null) { __result = (PlayerBodyTypes)0; return false; }
        if (!NocturneClassic.On) return true;
        __result = PlayerBodyTypes.Classic;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetBodyType))]
internal static class NocturneClassicBodySetPatch
{
    public static void Prefix([HarmonyArgument(0)] ref PlayerBodyTypes bodyType)
    {
        if (NocturneClassic.On) bodyType = PlayerBodyTypes.Classic;
    }
}

[HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldClassicMode))]
internal static class NocturneClassicModePatch
{
    public static void Postfix(ref bool __result)
    {
        if (NocturneConfig.ClassicMode.Value) __result = true;
    }
}

[HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldClassicMainMenuMode))]
internal static class NocturneClassicMenuPatch
{
    public static void Postfix(ref bool __result)
    {
        if (NocturneConfig.ClassicMenu.Value) __result = true;
    }
}
