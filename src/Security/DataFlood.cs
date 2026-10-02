using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class DataFlood
{
    private const float PhantomAfter = 3f;
    private const int PerSec = 40;

    private static readonly Dictionary<uint, (float first, float last)> seen = new Dictionary<uint, (float first, float last)>();
    private static readonly List<uint> stale = new List<uint>();

    private static Il2CppSystem.Collections.IEnumerator skip;
    private static float windowAt;
    private static float sweepAt;
    private static float graceUntil;
    private static float lastNote;
    private static int hits;

    internal static bool On => NocturneConfig.BlockDataFlood.Value;

    internal static void Reset()
    {
        seen.Clear();
        hits = 0;
        graceUntil = Time.unscaledTime + 5f;
    }

    internal static bool Check(InnerNetClient net, MessageReader reader)
    {
        if (reader.Tag != 1) return false;

        int pos = reader.Position;
        uint id;
        try
        {
            id = reader.ReadPackedUInt32();
        }
        catch
        {
            return true;
        }
        reader.Position = pos;

        if (id == 0) return true;
        if (net.allObjects.AllObjectsFast.ContainsKey(id) || net.DestroyedObjects.Contains(id))
            return false;

        float now = Time.unscaledTime;
        if (now >= sweepAt && seen.Count > 0)
            Sweep(net, now);

        if (seen.TryGetValue(id, out var t))
        {
            seen[id] = (t.first, now);
            if (now - t.first > PhantomAfter) return true;
        }
        else
        {
            if (seen.Count >= 1024) return true;
            seen[id] = (now, now);
        }

        if (now - windowAt >= 1f)
        {
            windowAt = now;
            hits = 0;
        }
        return ++hits > PerSec && now >= graceUntil;
    }

    internal static Il2CppSystem.Collections.IEnumerator Skip()
    {
        if (Time.unscaledTime - lastNote >= 1f)
        {
            lastNote = Time.unscaledTime;
            NocturneSecurityNotify.Fire("Обрезан DATA-флуд", "Trimmed a DATA flood");
        }
        return skip ??= Empty().WrapToIl2Cpp();
    }

    private static void Sweep(InnerNetClient net, float now)
    {
        sweepAt = now + 2f;
        stale.Clear();
        foreach (var kv in seen)
        {
            if (now - kv.Value.last > 10f || net.allObjects.AllObjectsFast.ContainsKey(kv.Key))
                stale.Add(kv.Key);
        }
        for (int i = 0; i < stale.Count; i++)
            seen.Remove(stale[i]);
    }

    private static IEnumerator Empty()
    {
        yield break;
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameDataInner))]
internal static class DataFloodPatch
{
    public static bool Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageReader reader, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (!DataFlood.On || !DataFlood.Check(__instance, reader)) return true;
        __result = DataFlood.Skip();
        return false;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class DataFloodJoinPatch
{
    public static void Prefix() => DataFlood.Reset();
}
