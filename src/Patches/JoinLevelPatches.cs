using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;

namespace Nocturne.Patches;

internal static class NocturneJoinLevels
{
    private const uint MaxRaw = 9999u;
    private static readonly Dictionary<byte, uint> ByPlayerId = new Dictionary<byte, uint>();
    private static readonly Dictionary<int, uint> ByClientId = new Dictionary<int, uint>();

    private static bool ValidRaw(uint raw) => raw != uint.MaxValue && raw <= MaxRaw;

    internal static void Remember(PlayerControl player, uint raw)
    {
        if (player == null || !ValidRaw(raw)) return;
        try { ByPlayerId[player.PlayerId] = raw; } catch { }
        try
        {
            ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(player) : null;
            if (c != null && c.Id >= 0) ByClientId[c.Id] = raw;
        }
        catch { }
    }

    internal static void RememberRpc(PlayerControl player, uint raw) => Remember(player, raw);

    internal static void RememberClient(int clientId, uint raw)
    {
        if (clientId < 0 || !ValidRaw(raw)) return;
        ByClientId[clientId] = raw;
    }

    internal static void RememberCurrent(PlayerControl player)
    {
        try { if (player != null && player.Data != null && player.Data.PlayerLevel > 0u && ValidRaw(player.Data.PlayerLevel)) Remember(player, player.Data.PlayerLevel); }
        catch { }
    }

    internal static bool TryGet(int clientId, out uint raw)
    {
        raw = 0u;
        return clientId >= 0 && ByClientId.TryGetValue(clientId, out raw);
    }

    private static bool TryCache(PlayerControl player, out uint raw)
    {
        raw = 0u;
        if (player == null) return false;
        try { if (ByPlayerId.TryGetValue(player.PlayerId, out raw)) return true; } catch { }
        try
        {
            ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(player) : null;
            if (c != null && ByClientId.TryGetValue(c.Id, out raw)) return true;
        }
        catch { }
        return false;
    }

    internal static bool TryRaw(int clientId, PlayerControl pc, out uint raw)
    {
        try
        {
            if (pc != null && pc.Data != null)
            {
                uint d = pc.Data.PlayerLevel;
                if (ValidRaw(d))
                {
                    if (d > 0u) { Remember(pc, d); raw = d; return true; }
                    if (TryCache(pc, out raw)) return true;
                    raw = 0u; return true;
                }
            }
        }
        catch { }

        if (TryCache(pc, out raw)) return true;
        if (clientId >= 0 && ByClientId.TryGetValue(clientId, out raw)) return true;

        raw = 0u;
        return false;
    }

    internal static string Display(int clientId, PlayerControl pc)
        => TryRaw(clientId, pc, out uint raw) ? (raw + 1u).ToString() : "?";

    internal static string Display(PlayerControl pc)
    {
        int id = -1;
        try { if (pc != null) id = pc.OwnerId; } catch { }
        return Display(id, pc);
    }

    internal static string Display(ClientData c)
    {
        if (c == null) return "?";
        if (c.Character != null)
        {
            string viaChar = Display(c.Id, c.Character);
            if (viaChar != "?") return viaChar;
        }
        if (TryGet(c.Id, out uint raw)) return (raw + 1u).ToString();
        try { if (c.PlayerLevel > 0u && ValidRaw(c.PlayerLevel)) { RememberClient(c.Id, c.PlayerLevel); return (c.PlayerLevel + 1u).ToString(); } }
        catch { }
        return "?";
    }

    internal static bool TryLevel(int clientId, PlayerControl pc, out int level)
    {
        if (TryRaw(clientId, pc, out uint raw)) { level = (int)(raw + 1u); return true; }
        level = 0;
        return false;
    }

    internal static void Inspect(InnerNetClient net, MessageReader reader)
    {
        if (net == null || reader == null || reader.Tag != 1 || !net.AmHost) return;

        MessageReader copy = null;
        MessageReader plat = null;
        try
        {
            copy = MessageReader.Get(reader);
            int gameId = copy.ReadInt32();
            if (gameId != net.GameId) return;
            int clientId = copy.ReadInt32();
            if (clientId == net.ClientId) return;

            copy.ReadInt32();
            copy.ReadString();
            plat = copy.ReadMessage();
            int platformId = plat.Tag;
            plat.ReadString();
            if (platformId == 4 || platformId == 9) plat.ReadUInt64();
            else if (platformId == 10) plat.ReadUInt64();

            uint level = copy.ReadPackedUInt32();
            RememberClient(clientId, level);
        }
        catch { }
        finally
        {
            try { plat?.Recycle(); } catch { }
            try { copy?.Recycle(); } catch { }
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), "HandleMessage")]
internal static class NocturneJoinLevelPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageReader reader)
    {
        try { NocturneJoinLevels.Inspect(__instance, reader); }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class NocturneLevelRpcPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 38 || __instance == null || reader == null) return;
        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);
            uint raw = copy.ReadPackedUInt32();
            NocturneJoinLevels.RememberRpc(__instance, raw);
        }
        catch { }
        finally { try { copy?.Recycle(); } catch { } }
    }
}

[HarmonyPatch(typeof(PlayerControl), "SetLevel")]
internal static class NocturneLevelSetPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] uint level)
    {
        if (__instance == null || level == 0u) return;
        try { NocturneJoinLevels.Remember(__instance, level); } catch { }
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
internal static class NocturneLevelInfoPatch
{
    public static void Postfix(NetworkedPlayerInfo __instance)
    {
        if (__instance == null) return;
        try
        {
            if (__instance.IsIncomplete) return;
            uint raw = __instance.PlayerLevel;
            if (raw == 0u) return;
            NocturneJoinLevels.RememberClient(__instance.ClientId, raw);
        }
        catch { }
    }
}
