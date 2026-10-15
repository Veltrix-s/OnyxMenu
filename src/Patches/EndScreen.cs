using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
internal static class EndScreen
{
    internal static EndGameManager Live;

    internal static bool Shown => Live != null;

    public static void Postfix(EndGameManager __instance) => Live = __instance;
}
