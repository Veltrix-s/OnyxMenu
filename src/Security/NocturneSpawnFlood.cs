using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneSpawnFlood
{
    private const int Cap = 200;
    private const int UnownedPerFrame = 8;
    private const int UnownedCap = 64;
    private const float GraceSeconds = 5f;

    private static int frame = -1;
    private static int count;
    private static int unownedFrame = -1;
    private static int unownedInFrame;
    private static int unownedTotal;
    private static float graceUntil;

    internal static bool On => NocturneConfig.BlockFakeMeetings.Value;

    internal static void Grace()
    {
        graceUntil = Time.unscaledTime + GraceSeconds;
        unownedTotal = 0;
    }

    internal static bool OwnerOk(MessageReader reader)
    {
        InnerNetClient net = AmongUsClient.Instance;
        if (net == null) return true;

        int pos = reader.Position;
        int ownerId;
        try
        {
            reader.ReadPackedUInt32();
            ownerId = reader.ReadPackedInt32();
        }
        catch
        {
            reader.Position = pos;
            return true;
        }
        reader.Position = pos;

        if (ownerId <= 0 || net.FindClientById(ownerId) != null) return true;

        int f = Time.frameCount;
        if (f != unownedFrame)
        {
            unownedFrame = f;
            unownedInFrame = 0;
        }
        unownedInFrame++;
        unownedTotal++;
        return unownedInFrame <= UnownedPerFrame && unownedTotal <= UnownedCap;
    }

    internal static bool Allow()
    {
        if (Time.unscaledTime < graceUntil) return true;
        int f = Time.frameCount;
        if (f != frame)
        {
            frame = f;
            count = 0;
        }
        count++;
        return count <= Cap;
    }

    internal static Il2CppSystem.Collections.IEnumerator Drop()
    {
        NocturneSecurityNotify.Once("spawn", 1f, "Обрезан спавн-флуд", "Trimmed a spawn flood");
        return Empty().WrapToIl2Cpp();
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CoHandleSpawn))]
internal static class NocturneSpawnFloodPatch
{
    public static bool Prefix(MessageReader reader, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!NocturneSpawnFlood.On) return true;
        if (NocturneSpawnFlood.OwnerOk(reader) && NocturneSpawnFlood.Allow()) return true;
        __result = NocturneSpawnFlood.Drop();
        return false;
    }
}
