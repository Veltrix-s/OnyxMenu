using HarmonyLib;

namespace Nocturne;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MixUpOutfit))]
internal static class MushroomImmunityPatch
{
    public static bool Prefix(PlayerControl __instance) => !(NocturneConfig.MushroomImmune.Value && __instance != null && __instance.AmOwner);
}
