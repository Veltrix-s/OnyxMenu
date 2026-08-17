using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nocturne;

internal static class TaskTools
{
    private static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    private static bool Ready(PlayerControl pc)
        => Host && ShipStatus.Instance != null && pc != null && pc.Data != null;

    private static void Send(PlayerControl pc, byte[] ids)
    {
        try { pc.Data.RpcSetTasks((Il2CppStructArray<byte>)ids); } catch { }
    }

    internal static string Clear(PlayerControl pc)
    {
        if (!Ready(pc)) return NocturneText.T("Только хост, в матче.", "Host only, in match.");
        Send(pc, Array.Empty<byte>());
        return NocturneText.T("Задания обнулены: ", "Tasks cleared: ") + Name(pc);
    }

    internal static string Flood(PlayerControl pc)
    {
        if (!Ready(pc)) return NocturneText.T("Только хост, в матче.", "Host only, in match.");
        var ids = new byte[255];
        for (int i = 0; i < ids.Length; i++) ids[i] = (byte)i;
        Send(pc, ids);
        return NocturneText.T("Завален заданиями: ", "Flooded with tasks: ") + Name(pc);
    }

    internal static string Normal(PlayerControl pc)
    {
        if (!Ready(pc)) return NocturneText.T("Только хост, в матче.", "Host only, in match.");

        ShipStatus s = ShipStatus.Instance;
        var ids = new List<byte>();
        Take(s.CommonTasks, Count(NocturneLobbySettings.Common(), 1), ids);
        Take(s.ShortTasks, Count(NocturneLobbySettings.Short(), 3), ids);
        Take(s.LongTasks, Count(NocturneLobbySettings.Long(), 1), ids);

        if (ids.Count == 0) return NocturneText.T("Нет заданий на карте.", "No tasks on this map.");
        Send(pc, ids.ToArray());
        return NocturneText.T("Выдан набор заданий: ", "Task set given: ") + Name(pc);
    }

    private static int Count(int fromOptions, int fallback)
        => fromOptions > 0 ? fromOptions : fallback;

    private static void Take(Il2CppReferenceArray<NormalPlayerTask> pool, int count, List<byte> into)
    {
        if (pool == null) return;
        int n = 0;
        for (int i = 0; i < pool.Length && n < count; i++)
        {
            NormalPlayerTask t = pool[i];
            if (t == null) continue;
            into.Add((byte)t.Index);
            n++;
        }
    }

    private static string Name(PlayerControl pc)
        => pc.Data != null ? NocturneNameColor.Strip(pc.Data.PlayerName) : "?";
}
