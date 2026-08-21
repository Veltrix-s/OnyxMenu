using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal sealed class AccessEntry
{
    internal string Name;
    internal string Code;
    internal string Puid;
}

internal static class NocturneAccess
{
    private static readonly List<AccessEntry> Bans = new List<AccessEntry>();
    private static readonly List<AccessEntry> Whites = new List<AccessEntry>();
    private static readonly List<string> NickBans = new List<string>();
    private static readonly List<string> PlatformBans = new List<string>();
    private static readonly Dictionary<int, float> ActedAt = new Dictionary<int, float>();
    private const float ActCooldown = 6f;
    private static bool _loaded;

    private static string NocturneDir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne");
    private static string BanTxt => Path.Combine(NocturneDir, "BanList.txt");
    private static string WhiteTxt => Path.Combine(NocturneDir, "WhiteList.txt");
    private static string NickTxt => Path.Combine(NocturneDir, "NickBanList.txt");
    private static string PlatformTxt => Path.Combine(NocturneDir, "PlatformBanList.txt");

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Parse(NocturneConfig.BanList?.Value, Bans);
        Parse(NocturneConfig.WhiteList?.Value, Whites);
        ParseNicks(NocturneConfig.NickBanList?.Value, NickBans);
        ParsePlatforms(NocturneConfig.PlatformBanList?.Value, PlatformBans);

        string fmt = NocturneText.T("# Nocturne — формат: Имя | FriendCode | PUID", "# Nocturne — format: Name | FriendCode | PUID");
        RefreshHeader(BanTxt, fmt);
        RefreshHeader(WhiteTxt, fmt);
        RefreshHeader(NickTxt, NocturneText.T("# Nocturne — по одному нику в строке", "# Nocturne — one nick per line"));
        RefreshHeader(PlatformTxt, NocturneText.T("# Nocturne — по одному имени платформы в строке", "# Nocturne — one platform name per line"));
    }

    private static void ParsePlatforms(string csv, List<string> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            string n = item.Trim();
            if (n.Length > 0 && !HasPlatform(into, n)) into.Add(n);
        }
    }

    private static void ParseNicks(string csv, List<string> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            string n = item.Trim();
            if (n.Length > 0 && !HasNick(into, n)) into.Add(n);
        }
    }

    private static void Parse(string csv, List<AccessEntry> into)
    {
        into.Clear();
        if (string.IsNullOrWhiteSpace(csv)) return;
        foreach (string item in csv.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            string[] p = item.Split('|');
            string name = p.Length >= 1 ? p[0].Trim() : string.Empty;
            string code = p.Length >= 2 ? p[1].Trim() : string.Empty;
            string puid = p.Length >= 3 ? p[2].Trim() : string.Empty;
            if ((code.Length > 0 || puid.Length > 0) && !Has(into, code, puid))
                into.Add(new AccessEntry { Name = name, Code = code, Puid = puid });
        }
    }

    private static void Persist(BepInEx.Configuration.ConfigEntry<string> entry, List<AccessEntry> list, string txtPath)
    {
        if (entry != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Esc(list[i].Name)).Append('|').Append(Esc(list[i].Code)).Append('|').Append(Esc(list[i].Puid));
            }
            entry.Value = sb.ToString();
        }

        WriteTxt(txtPath, list);
    }

    private static void WriteTxt(string path, List<AccessEntry> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            sb.AppendLine(NocturneText.T("# Nocturne — формат: Имя | FriendCode | PUID", "# Nocturne — format: Name | FriendCode | PUID"));
            for (int i = 0; i < list.Count; i++)
                sb.Append(list[i].Name).Append(" | ").Append(list[i].Code).Append(" | ").Append(list[i].Puid).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private static void RefreshHeader(string path, string header)
    {
        try
        {
            if (!File.Exists(path)) return;
            var lines = new List<string>(File.ReadAllLines(path));
            if (lines.Count > 0 && lines[0].StartsWith("# Nocturne", System.StringComparison.Ordinal) && lines[0] != header)
            {
                lines[0] = header;
                File.WriteAllText(path, string.Join("\n", lines) + "\n");
            }
        }
        catch { }
    }

    private static string Esc(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(';', ' ').Replace('|', ' ').Trim();

    private static bool Has(List<AccessEntry> list, string code, string puid)
    {
        for (int i = 0; i < list.Count; i++)
        {
            AccessEntry e = list[i];
            if (!string.IsNullOrEmpty(code) && string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(puid) && string.Equals(e.Puid, puid, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasNick(List<string> list, string name)
    {
        string key = NickKey(name);
        if (key.Length == 0) return false;
        for (int i = 0; i < list.Count; i++)
            if (NickKey(list[i]) == key) return true;
        return false;
    }

    private static string NickKey(string name)
    {
        string s = NocturneNameColor.Strip(name);
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    private static bool HasPlatform(List<string> list, string raw)
    {
        string key = PlatformKey(raw);
        if (key.Length == 0) return false;
        for (int i = 0; i < list.Count; i++)
            if (PlatformKey(list[i]) == key) return true;
        return false;
    }

    private static string PlatformKey(string raw)
    {
        string s = NocturneNameColor.Strip(raw);
        return string.IsNullOrEmpty(s) ? string.Empty : s.Trim().ToLowerInvariant();
    }

    internal static int BanCount { get { EnsureLoaded(); return Bans.Count; } }
    internal static int WhiteCount { get { EnsureLoaded(); return Whites.Count; } }
    internal static int NickBanCount { get { EnsureLoaded(); return NickBans.Count; } }
    internal static int PlatformBanCount { get { EnsureLoaded(); return PlatformBans.Count; } }
    internal static IReadOnlyList<AccessEntry> BanEntries { get { EnsureLoaded(); return Bans; } }
    internal static IReadOnlyList<AccessEntry> WhiteEntries { get { EnsureLoaded(); return Whites; } }
    internal static IReadOnlyList<string> NickBanEntries { get { EnsureLoaded(); return NickBans; } }
    internal static IReadOnlyList<string> PlatformBanEntries { get { EnsureLoaded(); return PlatformBans; } }

    internal static bool IsBanned(string fc, string puid) { EnsureLoaded(); return Has(Bans, fc, puid); }
    internal static bool IsWhite(string fc, string puid) { EnsureLoaded(); return Has(Whites, fc, puid); }
    internal static bool IsNickBanned(string name) { EnsureLoaded(); return HasNick(NickBans, name); }
    internal static bool IsPlatformBanned(string raw) { EnsureLoaded(); return HasPlatform(PlatformBans, raw); }

    internal static bool IsBotPlatform(string raw)
        => !string.IsNullOrEmpty(raw) && raw.IndexOf("bot", System.StringComparison.OrdinalIgnoreCase) >= 0;

    internal static void AddBan(string name, string fc, string puid)
    {
        EnsureLoaded();
        if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(Bans, fc, puid)) return;
        Bans.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
        Persist(NocturneConfig.BanList, Bans, BanTxt);
    }

    internal static void AddWhite(string name, string fc, string puid)
    {
        EnsureLoaded();
        if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(Whites, fc, puid)) return;
        Whites.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
        Persist(NocturneConfig.WhiteList, Whites, WhiteTxt);
    }

    internal static void RemoveBan(string key) { EnsureLoaded(); if (RemoveByKey(Bans, key)) Persist(NocturneConfig.BanList, Bans, BanTxt); }
    internal static void RemoveWhite(string key) { EnsureLoaded(); if (RemoveByKey(Whites, key)) Persist(NocturneConfig.WhiteList, Whites, WhiteTxt); }

    private static bool RemoveByKey(List<AccessEntry> list, string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        for (int i = list.Count - 1; i >= 0; i--)
            if (string.Equals(list[i].Code, key, StringComparison.OrdinalIgnoreCase) || string.Equals(list[i].Puid, key, StringComparison.OrdinalIgnoreCase))
            { list.RemoveAt(i); return true; }
        return false;
    }

    internal static void ClearBans() { EnsureLoaded(); Bans.Clear(); Persist(NocturneConfig.BanList, Bans, BanTxt); }
    internal static void ClearWhites() { EnsureLoaded(); Whites.Clear(); Persist(NocturneConfig.WhiteList, Whites, WhiteTxt); }

    internal static void AddNickBan(string name)
    {
        EnsureLoaded();
        string clean = NocturneNameColor.Strip(name).Trim();
        if (clean.Length == 0 || HasNick(NickBans, clean)) return;
        NickBans.Add(clean);
        PersistNicks();
    }

    internal static void RemoveNickBan(string name)
    {
        EnsureLoaded();
        string key = NickKey(name);
        for (int i = NickBans.Count - 1; i >= 0; i--)
            if (NickKey(NickBans[i]) == key) { NickBans.RemoveAt(i); PersistNicks(); return; }
    }

    internal static void ClearNickBans() { EnsureLoaded(); NickBans.Clear(); PersistNicks(); }

    internal static void AddPlatformBan(string raw)
    {
        EnsureLoaded();
        string clean = NocturneNameColor.Strip(raw).Trim();
        if (clean.Length == 0 || HasPlatform(PlatformBans, clean)) return;
        PlatformBans.Add(clean);
        PersistPlatforms();
    }

    internal static void RemovePlatformBan(string raw)
    {
        EnsureLoaded();
        string key = PlatformKey(raw);
        for (int i = PlatformBans.Count - 1; i >= 0; i--)
            if (PlatformKey(PlatformBans[i]) == key) { PlatformBans.RemoveAt(i); PersistPlatforms(); return; }
    }

    internal static void ClearPlatformBans() { EnsureLoaded(); PlatformBans.Clear(); PersistPlatforms(); }

    private static void PersistPlatforms()
    {
        if (NocturneConfig.PlatformBanList != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < PlatformBans.Count; i++) { if (i > 0) sb.Append(';'); sb.Append(Esc(PlatformBans[i])); }
            NocturneConfig.PlatformBanList.Value = sb.ToString();
        }

        try
        {
            Directory.CreateDirectory(NocturneDir);
            var sb2 = new StringBuilder();
            sb2.AppendLine(NocturneText.T("# Nocturne — по одному имени платформы в строке", "# Nocturne — one platform name per line"));
            for (int i = 0; i < PlatformBans.Count; i++) sb2.Append(PlatformBans[i]).Append('\n');
            File.WriteAllText(PlatformTxt, sb2.ToString());
        }
        catch { }
    }

    private static void PersistNicks()
    {
        if (NocturneConfig.NickBanList != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < NickBans.Count; i++) { if (i > 0) sb.Append(';'); sb.Append(Esc(NickBans[i])); }
            NocturneConfig.NickBanList.Value = sb.ToString();
        }

        try
        {
            Directory.CreateDirectory(NocturneDir);
            var sb2 = new StringBuilder();
            sb2.AppendLine(NocturneText.T("# Nocturne — по одному нику в строке", "# Nocturne — one nick per line"));
            for (int i = 0; i < NickBans.Count; i++) sb2.Append(NickBans[i]).Append('\n');
            File.WriteAllText(NickTxt, sb2.ToString());
        }
        catch { }
    }

    internal static void ImportTxt()
    {
        EnsureLoaded();
        int b = ImportFile(BanTxt, Bans);
        int w = ImportFile(WhiteTxt, Whites);
        Persist(NocturneConfig.BanList, Bans, BanTxt);
        Persist(NocturneConfig.WhiteList, Whites, WhiteTxt);
        NocturneToast.Push(NocturneText.T("Импорт TXT", "Import TXT"), NocturneText.T($"Бан: {b} · Вайт: {w}", $"Ban: {b} · White: {w}"), 2.5f, NocturneNotifyKind.Success);
    }

    private static int ImportFile(string path, List<AccessEntry> list)
    {
        try
        {
            if (!File.Exists(path)) return list.Count;
            list.Clear();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('|');
                string name, fc, puid;
                if (p.Length == 1) { name = string.Empty; fc = p[0].Trim(); puid = string.Empty; }
                else { name = p[0].Trim(); fc = p.Length >= 2 ? p[1].Trim() : string.Empty; puid = p.Length >= 3 ? p[2].Trim() : string.Empty; }
                if ((fc.Length > 0 || puid.Length > 0) && !Has(list, fc, puid))
                    list.Add(new AccessEntry { Name = name, Code = fc, Puid = puid });
            }
        }
        catch { }
        return list.Count;
    }

    internal static void Kick(InnerNetClient net, int clientId, bool ban)
    {
        try
        {
            if (net == null || !net.AmHost) return;
            if (clientId < 0 || clientId == net.ClientId || clientId == net.HostId) return;
            net.KickPlayer(clientId, ban);
        }
        catch { }
    }

    internal static void Act(InnerNetClient net, int clientId, string action, string who, string reason)
    {
        switch ((action ?? "Null").Trim().ToLowerInvariant())
        {
            case "warn":
                NocturneToast.Push(NocturneText.T("Защита", "Protection"), $"{who}: {reason}", 3f, NocturneNotifyKind.Warning);
                break;
            case "kick":
                Kick(net, clientId, false);
                NocturneToast.Push(NocturneText.T("Кик", "Kick"), $"{who}: {reason}", 3f, NocturneNotifyKind.Warning);
                break;
            case "ban":
                ClientData c = FindClient(net, clientId);
                if (c != null) AddBan(SafeName(c), SafeFc(c), SafePuid(c));
                Kick(net, clientId, true);
                NocturneToast.Push(NocturneText.T("Бан", "Ban"), $"{who}: {reason}", 3f, NocturneNotifyKind.Danger);
                break;
        }
    }

    internal static void BanClient(InnerNetClient net, ClientData c)
    {
        if (c == null) return;
        AddBan(SafeName(c), SafeFc(c), SafePuid(c));
        Kick(net, c.Id, true);
        NocturneToast.Push(NocturneText.T("Бан-лист", "Ban list"), SafeName(c), 2.5f, NocturneNotifyKind.Danger);
    }

    internal static void WhiteClient(ClientData c)
    {
        if (c == null) return;
        AddWhite(SafeName(c), SafeFc(c), SafePuid(c));
        NocturneToast.Push(NocturneText.T("Вайтлист", "Whitelist"), SafeName(c), 2.5f, NocturneNotifyKind.Success);
    }

    internal static void NickBanClient(InnerNetClient net, ClientData c)
    {
        if (c == null) return;
        AddNickBan(SafeName(c));
        Kick(net, c.Id, true);
        NocturneToast.Push(NocturneText.T("Ник-бан", "Nick ban"), SafeName(c), 2.5f, NocturneNotifyKind.Danger);
    }


    internal static void Enforce(InnerNetClient net, ClientData client)
    {
        try
        {
            if (net == null || !net.AmHost || client == null) return;
            if (client.Id < 0 || client.Id == net.ClientId || client.Id == net.HostId) return;

            string fc = SafeFc(client);
            string puid = SafePuid(client);
            if (IsWhite(fc, puid)) return;
            if (OnCooldown(client.Id)) return;

            if (NocturneConfig.AccessBanEnabled.Value && IsBanned(fc, puid))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                NocturneToast.Push(NocturneText.T("Бан-лист", "Ban list"), SafeName(client), 3f, NocturneNotifyKind.Danger);
                return;
            }

            if (NocturneConfig.AccessNickBanEnabled.Value && IsNickBanned(SafeName(client)))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                NocturneToast.Push(NocturneText.T("Ник-бан", "Nick ban"), SafeName(client), 3f, NocturneNotifyKind.Danger);
                return;
            }

            if (NocturneConfig.AccessPlatformBanEnabled.Value && IsPlatformBanned(SafePlatform(client)))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                NocturneToast.Push(NocturneText.T("Платформ-бан", "Platform ban"), SafeName(client), 3f, NocturneNotifyKind.Danger);
                return;
            }

            if (IsBotPlatform(SafePlatform(client)))
            {
                Touch(client.Id);
                Kick(net, client.Id, true);
                NocturneToast.Push(NocturneText.T("Бот", "Bot"), SafeName(client), 3f, NocturneNotifyKind.Danger);
                return;
            }

            if (NocturneConfig.AccessWhitelistOnly.Value && WhiteCount > 0 && !IsWhite(fc, puid))
            {
                Touch(client.Id);
                Kick(net, client.Id, false);
                NocturneToast.Push(NocturneText.T("Не в вайтлисте", "Not whitelisted"), SafeName(client), 3f, NocturneNotifyKind.Warning);
                return;
            }

            if (TryLevel(client, out int lvl))
            {
                if (NocturneConfig.MinLevelEnabled.Value && lvl < NocturneConfig.MinLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, NocturneConfig.MinLevelAction.Value, SafeName(client), NocturneText.T($"ур.{lvl} < {NocturneConfig.MinLevel.Value}", $"lvl {lvl} < {NocturneConfig.MinLevel.Value}"));
                    return;
                }

                if (NocturneConfig.MaxLevelEnabled.Value && lvl > NocturneConfig.MaxLevel.Value)
                {
                    Touch(client.Id);
                    Act(net, client.Id, NocturneConfig.MaxLevelAction.Value, SafeName(client), NocturneText.T($"ур.{lvl} > {NocturneConfig.MaxLevel.Value}", $"lvl {lvl} > {NocturneConfig.MaxLevel.Value}"));
                }
            }
        }
        catch { }
    }

    private static bool OnCooldown(int id) => ActedAt.TryGetValue(id, out float t) && Time.realtimeSinceStartup - t < ActCooldown;
    private static void Touch(int id) => ActedAt[id] = Time.realtimeSinceStartup;

    internal static ClientData FindClient(InnerNetClient net, int clientId)
    {
        try
        {
            if (net == null || net.allClients == null) return null;
            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c != null && c.Id == clientId) return c;
            }
        }
        catch { }
        return null;
    }

    internal static string ClientName(InnerNetClient net, int clientId)
    {
        ClientData c = FindClient(net, clientId);
        return c != null ? SafeName(c) : "#" + clientId;
    }

    internal static string SafeName(ClientData c)
    {
        try { if (c != null && !string.IsNullOrWhiteSpace(c.PlayerName)) return c.PlayerName.Trim(); }
        catch { }
        return c != null ? "#" + c.Id : "?";
    }

    private static string SafeFc(ClientData c)
    {
        try { return c.FriendCode ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafePuid(ClientData c)
    {
        try { return c.ProductUserId ?? string.Empty; }
        catch { return string.Empty; }
    }

    internal static string SafePlatform(ClientData c)
    {
        try { return c != null && c.PlatformData != null ? (c.PlatformData.PlatformName ?? string.Empty).Trim() : string.Empty; }
        catch { return string.Empty; }
    }

    private static bool TryLevel(ClientData c, out int level)
    {
        return Patches.NocturneJoinLevels.TryLevel(c != null ? c.Id : -1, c != null ? c.Character : null, out level);
    }
}
