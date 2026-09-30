using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneJoinLevels
{
    private const uint MaxRaw = 9999u;
    private const byte TempId = 100;
    private const float SettleGap = 0.75f;

    private static readonly Dictionary<byte, uint> ByPlayerId = new Dictionary<byte, uint>();
    private static readonly Dictionary<int, uint> ByClientId = new Dictionary<int, uint>();
    private static readonly Dictionary<int, float> LoadedAt = new Dictionary<int, float>();

    private static bool ValidRaw(uint raw) => raw != uint.MaxValue && raw <= MaxRaw;

    internal static void Reset()
    {
        ByPlayerId.Clear();
        ByClientId.Clear();
        LoadedAt.Clear();
    }

    private static int OwnerOf(PlayerControl player)
    {
        return player != null ? player.OwnerId : -1;
    }

    private static bool BlankName(string name)
    {
        string v = (name ?? string.Empty).Trim();
        return v.Length == 0 || v == "??" || v == "???"
            || v.Equals("Player", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool OutfitReady(PlayerControl pc)
    {
        try
        {
            if (pc.Data.DefaultOutfit == null) return false;
            int col = pc.Data.DefaultOutfit.ColorId;
            if (col < 0) return false;
            return Palette.PlayerColors == null || col < Palette.PlayerColors.Length;
        }
        catch
        {
            return false;
        }
    }

    private static bool Loaded(PlayerControl pc)
    {
        return pc != null && pc.Data != null && !pc.Data.Disconnected
            && pc.PlayerId < TempId && !pc.Data.IsIncomplete
            && !BlankName(pc.Data.PlayerName) && OutfitReady(pc);
    }

    private static bool Settled(int clientId, PlayerControl pc)
    {
        if (clientId < 0)
            return false;

        if (!Loaded(pc))
        {
            LoadedAt.Remove(clientId);
            return false;
        }

        float now = Time.unscaledTime;
        if (!LoadedAt.TryGetValue(clientId, out float at))
        {
            LoadedAt[clientId] = now;
            return false;
        }
        return now - at >= SettleGap;
    }

    internal static bool Ready(ClientData c)
        => c != null && c.Id >= 0 && Settled(c.Id, c.Character);

    internal static void Remember(PlayerControl player, uint raw)
    {
        if (player == null || raw == 0u || !ValidRaw(raw))
            return;
        ByPlayerId[player.PlayerId] = raw;
        try
        {
            ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(player) : null;
            if (c != null && c.Id >= 0)
            {
                ByClientId[c.Id] = raw;
                PushToClient(c.Id, raw);
            }
        }
        catch { }
    }

    internal static void RememberRpc(PlayerControl player, uint raw) => Remember(player, raw);

    internal static void RememberClient(int clientId, uint raw)
    {
        if (clientId < 0 || raw == 0u || !ValidRaw(raw)) return;
        ByClientId[clientId] = raw;
        PushToClient(clientId, raw);
    }

    private static void PushToClient(int clientId, uint raw)
    {
        if (clientId < 0 || raw == 0u || !ValidRaw(raw)) return;
        try
        {
            ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.FindClientById(clientId) : null;
            if (c != null && c.PlayerLevel != raw) c.PlayerLevel = raw;
        }
        catch { }
    }

    internal static void RememberCurrent(PlayerControl player)
    {
        try
        {
            if (player != null && player.Data != null && !player.Data.IsIncomplete && ValidRaw(player.Data.PlayerLevel))
                Remember(player, player.Data.PlayerLevel);
        }
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
        try
        {
            if (ByPlayerId.TryGetValue(player.PlayerId, out raw)) return true;
        }
        catch { }
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
        uint live = 0u;
        bool haveLive = false;
        try
        {
            if (pc != null && pc.Data != null && !pc.Data.IsIncomplete && ValidRaw(pc.Data.PlayerLevel))
            {
                live = pc.Data.PlayerLevel;
                haveLive = true;
            }
        }
        catch { }

        if (haveLive && live > 0u)
        {
            raw = live;
            Remember(pc, raw);
            return true;
        }

        if (TryCache(pc, out raw) && raw > 0u)
            return true;
        if (clientId >= 0 && ByClientId.TryGetValue(clientId, out raw) && raw > 0u) return true;

        int owner = clientId >= 0 ? clientId : OwnerOf(pc);
        if (haveLive && Settled(owner, pc))
        {
            raw = live;
            return true;
        }

        raw = 0u;
        return false;
    }

    internal static string Display(int clientId, PlayerControl pc)
        => TryRaw(clientId, pc, out uint raw) ? (raw + 1u).ToString() : "?";

    internal static string Display(PlayerControl pc)
    {
        int id = -1;
        if (pc != null)
            id = pc.OwnerId;
        return Display(id, pc);
    }

    internal static string Display(ClientData c)
    {
        if (c == null)
            return "?";
        if (c.Character != null)
        {
            string viaChar = Display(c.Id, c.Character);
            if (viaChar != "?")
                return viaChar;
        }
        if (TryGet(c.Id, out uint raw) && raw > 0u) return (raw + 1u).ToString();
        try
        {
            if (!ValidRaw(c.PlayerLevel))
                return "?";
            if (c.PlayerLevel > 0u)
            {
                RememberClient(c.Id, c.PlayerLevel);
                return (c.PlayerLevel + 1u).ToString();
            }
            if (Settled(c.Id, c.Character))
                return "1";
        }
        catch { }
        return "?";
    }

    internal static bool TryLevel(int clientId, PlayerControl pc, out int level)
    {
        if (TryRaw(clientId, pc, out uint raw))
        {
            level = (int)(raw + 1u);
            return true;
        }
        level = 0;
        return false;
    }

    internal static void Inspect(InnerNetClient net, MessageReader reader)
    {
        if (net == null || reader == null || reader.Tag != 1 || !net.AmHost)
            return;

        MessageReader copy = null;
        MessageReader plat = null;
        try
        {
            copy = MessageReader.Get(reader);
            int gameId = copy.ReadInt32();
            if (gameId != net.GameId) return;
            int clientId = copy.ReadInt32();
            if (clientId == net.ClientId)
                return;

            copy.ReadInt32();
            copy.ReadString();
            plat = copy.ReadMessage();
            int platformId = plat.Tag;
            plat.ReadString();
            if (platformId == 4 || platformId == 9)
                plat.ReadUInt64();
            else if (platformId == 10)
                plat.ReadUInt64();

            uint level = copy.ReadPackedUInt32();
            RememberClient(clientId, level);
        }
        catch { }
        finally
        {
            try
            {
                plat?.Recycle();
            }
            catch { }
            try
            {
                copy?.Recycle();
            }
            catch { }
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleMessage))]
internal static class NocturneJoinLevelPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageReader reader)
    {
        try
        {
            NocturneJoinLevels.Inspect(__instance, reader);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class NocturneLevelResetPatch
{
    public static void Prefix()
    {
        try
        {
            NocturneJoinLevels.Reset();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
internal static class NocturneLevelJoinPushPatch
{
    public static void Postfix([HarmonyArgument(0)] ClientData client)
    {
        if (client == null || client.Id < 0 || client.PlayerLevel > 0u)
            return;
        try
        {
            if (NocturneJoinLevels.TryGet(client.Id, out uint raw) && raw > 0u) client.PlayerLevel = raw;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class NocturneLevelRpcPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 38 || __instance == null || reader == null)
            return;
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

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetLevel))]
internal static class NocturneLevelSetPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] uint level)
    {
        if (__instance == null || level == 0u)
            return;
        try
        {
            NocturneJoinLevels.Remember(__instance, level);
        }
        catch { }
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
            if (__instance.IsIncomplete)
                return;
            uint raw = __instance.PlayerLevel;
            if (raw == 0u)
                return;
            NocturneJoinLevels.RememberClient(__instance.ClientId, raw);
        }
        catch { }
    }
}
