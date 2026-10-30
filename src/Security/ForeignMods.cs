using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace Nocturne;

internal static class ForeignMods
{
    private const byte VanillaTop = 90;

    private const string Chocoo = "ChocooMenu";
    private const string Sicko = "SickoMenu User";

    private static readonly Dictionary<int, string> Seen = new Dictionary<int, string>();
    private static readonly Dictionary<long, int> Pending = new Dictionary<long, int>();
    private static readonly HashSet<int> Toasted = new HashSet<int>();
    private static readonly HashSet<int> Punished = new HashSet<int>();

    internal static bool Acting => On(NocturneConfig.GuardChocooAction.Value) || On(NocturneConfig.GuardSickoAction.Value);

    internal static string Name(int clientId) => Seen.TryGetValue(clientId, out string s) ? s : null;

    internal static void Reset()
    {
        Seen.Clear();
        Pending.Clear();
        Toasted.Clear();
        Punished.Clear();
    }

    internal static void Inspect(PlayerControl src, byte callId, MessageReader reader)
    {
        if (callId == 242 || src == null || src == PlayerControl.LocalPlayer)
            return;

        int cid = src.OwnerId;
        if (Seen.ContainsKey(cid))
            return;

        string known = KnownId(callId);
        if (known != null)
        {
            Mark(src, cid, known);
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
                    Mark(src, cid, string.IsNullOrWhiteSpace(info) || info.Length > 32 ? "BanMod" : info);
                return;
            }

            if (callId == 121)
            {
                if (copy.ReadString() == "CHOCOO_PING")
                    Mark(src, cid, Chocoo);
                else
                    Unknown(src, cid, callId);
                return;
            }

            string sig = copy.ReadString();
            byte claimed = copy.ReadByte();
            string ver = copy.ReadString();

            if (claimed == src.PlayerId && !string.IsNullOrEmpty(sig) && sig.Length <= 24 && ver != null && ver.Length <= 16)
            {
                if (sig.IndexOf("MMC", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Mark(src, cid, "ModMenuCrew " + ver);
                    return;
                }
                if (LooksVersion(ver))
                {
                    Mark(src, cid, sig + " " + ver);
                    return;
                }
            }
        }
        catch
        {
        }
        finally { copy?.Recycle(); }

        Unknown(src, cid, callId);
    }

    private static void Unknown(PlayerControl src, int cid, byte callId)
    {
        long key = ((long)cid << 8) | callId;
        Pending.TryGetValue(key, out int hits);
        hits++;
        Pending[key] = hits;

        if (hits < 2)
            return;

        Mark(src, cid, NocturneText.T("Неизвестный", "Unknown") + " (" + callId + ")");
    }

    private static string KnownId(byte callId)
    {
        switch (callId)
        {
            case 164:
                return Sicko;
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

    private static void Mark(PlayerControl src, int cid, string mod)
    {
        Seen[cid] = mod;
        Punish(src, cid, mod);

        if (!NocturneConfig.ModHandshake.Value || !Toasted.Add(cid))
            return;

        string who = src.Data != null ? NocturneNameColor.Strip(src.Data.PlayerName) : "?";
        NocturneToast.Push(NocturneText.T("Чужой мод", "Foreign mod"), who + " · " + mod, 4f, NocturneNotifyKind.Warning);
    }

    internal static void Recheck(ClientData c)
    {
        if (c != null && c.Character != null && !NocturneAccess.OnCooldown(c.Id) && Seen.TryGetValue(c.Id, out string mod))
            Punish(c.Character, c.Id, mod);
    }

    private static bool On(string action) => !string.IsNullOrEmpty(action) && !string.Equals(action, "Null", StringComparison.OrdinalIgnoreCase);

    private static void Punish(PlayerControl src, int cid, string mod)
    {
        string action = mod == Chocoo ? NocturneConfig.GuardChocooAction.Value
            : mod == Sicko ? NocturneConfig.GuardSickoAction.Value
            : null;
        if (!On(action) || src == null || Punished.Contains(cid))
            return;

        var net = AmongUsClient.Instance;
        bool warn = string.Equals(action, "Warn", StringComparison.OrdinalIgnoreCase);
        if (net == null || (!warn && !net.AmHost) || NocturneAccess.IsWhite(NocturneAccess.FindClient(net, cid)))
            return;

        Punished.Add(cid);
        string who = src.Data != null ? NocturneNameColor.Strip(src.Data.PlayerName) : "#" + cid;
        NocturneAccess.Act(net, cid, action, who, mod == Chocoo ? NocturneText.T("меню ChocooMenu", "ChocooMenu") : NocturneText.T("юзер SickoMenu", "SickoMenu user"));
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
[HarmonyPriority(Priority.Low)]
internal static class ForeignModsRpcPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (reader != null)
            ForeignMods.Inspect(__instance, callId, reader);
    }
}
