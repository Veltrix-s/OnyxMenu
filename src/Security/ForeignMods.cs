using System;
using System.Collections.Generic;
using Hazel;
using HarmonyLib;

namespace Nocturne;

internal static class ForeignMods
{
    private const byte VanillaTop = 90;

    private static readonly Dictionary<byte, string> Seen = new Dictionary<byte, string>();
    private static readonly Dictionary<int, int> Pending = new Dictionary<int, int>();
    private static readonly HashSet<byte> Toasted = new HashSet<byte>();

    internal static int Count => Seen.Count;

    internal static string Name(byte pid) => Seen.TryGetValue(pid, out string s) ? s : null;

    internal static void Reset()
    {
        Seen.Clear();
        Pending.Clear();
        Toasted.Clear();
    }

    internal static void Inspect(PlayerControl src, byte callId, MessageReader reader)
    {
        if (callId == 242 || src == null || src == PlayerControl.LocalPlayer)
            return;

        byte pid = src.PlayerId;
        if (Seen.ContainsKey(pid))
            return;

        string known = KnownId(callId, reader);
        if (known != null)
        {
            Mark(src, pid, known);
            return;
        }

        if (callId < VanillaTop)
            return;

        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);

            if (callId == 212)
            {
                string info = copy.ReadString();
                if (copy.ReadBoolean())
                    Mark(src, pid, string.IsNullOrWhiteSpace(info) || info.Length > 32 ? "BanMod" : info);
                return;
            }

            string sig = copy.ReadString();
            byte claimed = copy.ReadByte();
            string ver = copy.ReadString();

            if (claimed == pid && !string.IsNullOrEmpty(sig) && sig.Length <= 24 && ver != null && ver.Length <= 16)
            {
                if (sig.IndexOf("MMC", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Mark(src, pid, "ModMenuCrew " + ver);
                    return;
                }
                if (LooksVersion(ver))
                {
                    Mark(src, pid, sig + " " + ver);
                    return;
                }
            }
        }
        catch
        {
        }
        finally { try { copy?.Recycle(); } catch { } }

        Unknown(src, pid, callId);
    }

    private static void Unknown(PlayerControl src, byte pid, byte callId)
    {
        int key = (pid << 8) | callId;
        Pending.TryGetValue(key, out int hits);
        hits++;
        Pending[key] = hits;

        if (hits < 2)
            return;

        Mark(src, pid, NocturneText.T("Неизвестный", "Unknown") + " (" + callId + ")");
    }

    private static string KnownId(byte callId, MessageReader reader)
    {
        switch (callId)
        {
            case 164:
                return Empty(reader) ? "SickoMenu" : null;
            case 103:
                return "ASKIN";
            case 144:
            case 145:
                return "Gaff Menu";
            case 150:
                return "BetterAmongUs";
            case 169:
                return NocturneText.T("Неизвестный", "Unknown");
            case 176:
                return "HostGuard";
            case 195:
            case 204:
                return "Polar Client";
            case 219:
            case 240:
                return "BanMod";
            case 250:
                return "KillNetwork";
            default:
                return null;
        }
    }

    private static bool Empty(MessageReader reader)
    {
        try
        {
            return reader.BytesRemaining == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Mark(PlayerControl src, byte pid, string mod)
    {
        Seen[pid] = mod;

        if (!NocturneConfig.ModHandshake.Value || !Toasted.Add(pid))
            return;

        string who = src.Data != null ? NocturneNameColor.Strip(src.Data.PlayerName) : "?";
        NocturneToast.Push(NocturneText.T("Чужой мод", "Foreign mod"), who + " · " + mod, 4f, NocturneNotifyKind.Warning);
    }

    private static bool LooksVersion(string s)
    {
        if (s.Length < 3 || s.Length > 16)
            return false;

        bool digit = false;
        bool dot = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
                digit = true;
            else if (c == '.')
                dot = true;
            else if (!char.IsLetter(c) && c != '-' && c != '_')
                return false;
        }
        return digit && dot;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class ForeignModsRpcPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (reader != null)
            ForeignMods.Inspect(__instance, callId, reader);
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class ForeignModsJoinPatch
{
    public static void Postfix() => ForeignMods.Reset();
}
