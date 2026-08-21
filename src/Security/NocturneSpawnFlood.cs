using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneSpawnFlood
{
    private const int Cap = 200;
    private const float GraceSeconds = 5f;

    private static int frame = -1;
    private static int count;
    private static float lastNote;
    private static float graceUntil;

    internal static bool On => NocturneConfig.BlockFakeMeetings.Value;

    internal static void Grace() => graceUntil = Time.unscaledTime + GraceSeconds;

    internal static bool Allow()
    {
        if (Time.unscaledTime < graceUntil) return true;
        int f = Time.frameCount;
        if (f != frame) { frame = f; count = 0; }
        count++;
        return count <= Cap;
    }

    internal static Il2CppSystem.Collections.IEnumerator Drop()
    {
        if (Time.unscaledTime - lastNote >= 1f)
        {
            lastNote = Time.unscaledTime;
            NocturneSecurityNotify.Fire("Обрезан спавн-флуд", "Trimmed a spawn flood");
        }
        return Empty().WrapToIl2Cpp();
    }

    private static IEnumerator Empty() { yield break; }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CoHandleSpawn))]
internal static class NocturneSpawnFloodPatch
{
    public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!NocturneSpawnFlood.On || NocturneSpawnFlood.Allow()) return true;
        __result = NocturneSpawnFlood.Drop();
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class NocturneSpawnGraceJoinPatch
{
    public static void Prefix() => NocturneSpawnFlood.Grace();
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneSpawnGraceLobbyPatch
{
    public static void Prefix() => NocturneSpawnFlood.Grace();
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NocturneSpawnGraceShipPatch
{
    public static void Prefix() => NocturneSpawnFlood.Grace();
}
