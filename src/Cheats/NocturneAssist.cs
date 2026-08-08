using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(global::Console), nameof(global::Console.CanUse))]
internal static class ConsoleReachPatch
{
    public static void Postfix(global::Console __instance, NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse, ref float __result)
    {
        if (!NocturneConfig.ConsoleReach.Value || __instance == null || !couldUse || canUse) return;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || pc == null || pc.PlayerId != me.PlayerId) return;

        float d = Vector2.Distance(me.GetTruePosition(), (Vector2)__instance.transform.position);
        if (d > NocturneConfig.ConsoleDist.Value) return;
        canUse = true;
        __result = d;
    }
}

[HarmonyPatch(typeof(DeconSystem), nameof(DeconSystem.Deteriorate))]
internal static class SkipDeconPatch
{
    public static void Postfix(DeconSystem __instance)
    {
        if (!NocturneConfig.SkipDecon.Value || __instance == null) return;
        try { if (__instance.UpperDoor != null) __instance.UpperDoor.SetDoorway(true); } catch { }
        try { if (__instance.LowerDoor != null) __instance.LowerDoor.SetDoorway(true); } catch { }
    }
}

[HarmonyPatch(typeof(SpawnInMinigame), nameof(SpawnInMinigame.Begin))]
internal static class AirshipSpawnPatch
{
    public static void Postfix(SpawnInMinigame __instance)
    {
        if (!NocturneConfig.AirshipSpawn.Value || __instance == null) return;
        try
        {
            var locs = __instance.Locations;
            if (locs == null || locs.Length == 0) return;
            int i = Mathf.Clamp(NocturneConfig.AirshipSpawnId.Value, 0, locs.Length - 1);
            __instance.SpawnAt(locs[i]);
        }
        catch { }
    }
}
