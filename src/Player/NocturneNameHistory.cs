using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneNameHistory
{
    private sealed class Rec
    {
        internal string Fc;
        internal string Nick;
        internal List<string> Nicks;
        internal string Level = "?";
        internal string Puid = "";
        internal string Platform = "?";
        internal string Raw = "";
        internal string First;
        internal string Last;
    }

    private const string Sep = "══════════════════════════════════════";

    private const float NickWriteCd = 3f;

    private static readonly Dictionary<string, Rec> Cache = new Dictionary<string, Rec>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _priorFcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> _lastFlip = new Dictionary<string, float>();
    private static bool _loaded;

    private static string HistoryTxt => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "PlayerHistory.txt");

    internal static string RecordJoin(ClientData c)
    {
        if (c == null) return null;
        string nick = ResolveNick(c);
        if (nick.Length == 0 || nick == "???" || nick == "Игрок") return null;
        string fc = SafeFc(c);
        if (fc.Length == 0) return null;

        EnsureLoaded();
        string key = fc.ToLowerInvariant();
        string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        string level = ResolveLevel(c);
        string puid = SafePuid(c);
        ResolvePlatform(c, out string platform, out string raw);

        if (Cache.TryGetValue(key, out Rec r))
        {
            bool changed = false;
            string prev = null;
            if (!string.Equals(r.Nick, nick, StringComparison.Ordinal))
            {
                float tnow = Time.realtimeSinceStartup;
                if (_lastFlip.TryGetValue(key, out float lf) && tnow - lf < NickWriteCd) { _lastFlip[key] = tnow; return null; }
                _lastFlip[key] = tnow;

                prev = r.Nick;
                if (!r.Nicks.Contains(nick)) r.Nicks.Insert(0, nick);
                r.Nick = nick;
                r.Last = now;
                changed = true;
            }
            if (level != "?" && level != r.Level) { r.Level = level; changed = true; }
            if (puid.Length > 0 && puid != r.Puid) { r.Puid = puid; changed = true; }
            if (platform != "?" && platform != r.Platform) { r.Platform = platform; changed = true; }
            if (raw.Length > 0 && raw != r.Raw) { r.Raw = raw; changed = true; }
            if (changed) Save();
            return prev;
        }

        Cache[key] = new Rec { Fc = fc, Nick = nick, Nicks = new List<string> { nick }, Level = level, Puid = puid, Platform = platform, Raw = raw, First = now, Last = now };
        Save();
        return null;
    }

    internal static string NickOf(ClientData c) => ResolveNick(c);

    internal static bool KnownBefore(ClientData c, out string since, out string knownNick)
    {
        since = string.Empty;
        knownNick = string.Empty;
        string fc = SafeFc(c);
        if (fc.Length == 0) return false;

        EnsureLoaded();
        string key = fc.ToLowerInvariant();
        if (!_priorFcs.Contains(key)) return false;
        if (!Cache.TryGetValue(key, out Rec r)) return false;

        since = ShortDate(r.First);
        knownNick = r.Nick ?? string.Empty;
        return true;
    }

    private static string ShortDate(string iso)
    {
        if (string.IsNullOrEmpty(iso) || iso.Length < 10) return "?";
        string d = iso.Substring(0, 10);
        return d.Length == 10 ? d.Substring(8, 2) + "." + d.Substring(5, 2) + "." + d.Substring(0, 4) : d;
    }

    internal static int KnownNickCount(PlayerControl p)
    {
        string fc = SafeFc(p);
        if (fc.Length == 0) return 0;
        EnsureLoaded();
        return Cache.TryGetValue(fc.ToLowerInvariant(), out Rec r) ? r.Nicks.Count : 0;
    }

    private static string ResolveNick(ClientData c)
    {
        try
        {
            if (c.Character != null && c.Character.Data != null && !string.IsNullOrWhiteSpace(c.Character.Data.PlayerName))
                return NocturneNameColor.Strip(c.Character.Data.PlayerName).Trim();
        }
        catch { }
        try { if (!string.IsNullOrWhiteSpace(c.PlayerName)) return NocturneNameColor.Strip(c.PlayerName).Trim(); }
        catch { }
        return string.Empty;
    }

    private static string ResolveLevel(ClientData c) => Patches.NocturneJoinLevels.Display(c);

    private static void ResolvePlatform(ClientData c, out string plat, out string raw)
    {
        plat = "?";
        raw = "";
        try
        {
            if (c.PlatformData != null)
            {
                plat = PlatformLabel(c.PlatformData.Platform);
                raw = Clean(c.PlatformData.PlatformName);
            }
        }
        catch { }
    }

    private static string SafeFc(ClientData c) { try { return (c.FriendCode ?? string.Empty).Trim(); } catch { return string.Empty; } }
    private static string SafeFc(PlayerControl p) { try { return p != null && p.Data != null ? (p.Data.FriendCode ?? string.Empty).Trim() : string.Empty; } catch { return string.Empty; } }
    private static string SafePuid(ClientData c) { try { return (c.ProductUserId ?? string.Empty).Trim(); } catch { return string.Empty; } }

    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace("\r", " ").Replace("\n", " ").Replace("·", " ").Trim();
        return s.Length <= 40 ? s : s.Substring(0, 40);
    }

    private static string PlatformLabel(Platforms platform)
    {
        return (int)platform switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS Store",
            5 => "Itch.io",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
            112 => "Starlight",
            _ => "Unknown",
        };
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Load();
        foreach (string k in Cache.Keys) _priorFcs.Add(k);
    }

    private static void Load()
    {
        Cache.Clear();
        try
        {
            if (!File.Exists(HistoryTxt)) return;
            string[] lines = File.ReadAllLines(HistoryTxt, Encoding.UTF8);
            bool block = false;
            for (int i = 0; i < lines.Length; i++) if (lines[i].StartsWith("Name: ", StringComparison.Ordinal)) { block = true; break; }
            if (block) LoadBlocks(lines);
            else LoadLegacy(lines);
        }
        catch { }
    }

    private static void LoadBlocks(string[] lines)
    {
        Rec cur = null;
        foreach (string raw in lines)
        {
            string line = raw.Replace("\r", "");
            if (line.StartsWith("Name: ", StringComparison.Ordinal))
            {
                Commit(cur);
                cur = new Rec { Nick = line.Substring(6).Trim(), Nicks = new List<string>() };
                if (cur.Nick.Length > 0) cur.Nicks.Add(cur.Nick);
                continue;
            }
            if (cur == null) continue;
            if (line.StartsWith("Aliases: ", StringComparison.Ordinal))
            {
                string a = line.Substring(9).Trim();
                if (a.Length > 0 && a != "—")
                    foreach (string part in a.Split('·')) { string n = part.Trim(); if (n.Length > 0 && !cur.Nicks.Contains(n)) cur.Nicks.Add(n); }
            }
            else if (line.StartsWith("Level: ", StringComparison.Ordinal)) cur.Level = line.Substring(7).Trim();
            else if (line.StartsWith("FriendCode: ", StringComparison.Ordinal)) cur.Fc = line.Substring(12).Trim();
            else if (line.StartsWith("PUID: ", StringComparison.Ordinal)) { string v = line.Substring(6).Trim(); cur.Puid = v == "—" ? "" : v; }
            else if (line.StartsWith("Platform: ", StringComparison.Ordinal))
            {
                string v = line.Substring(10).Trim();
                int idx = v.IndexOf(" · ", StringComparison.Ordinal);
                if (idx >= 0) { cur.Platform = v.Substring(0, idx).Trim(); cur.Raw = v.Substring(idx + 3).Trim(); }
                else cur.Platform = v;
            }
            else if (line.StartsWith("First seen: ", StringComparison.Ordinal)) cur.First = line.Substring(12).Trim();
            else if (line.StartsWith("Last seen: ", StringComparison.Ordinal)) cur.Last = line.Substring(11).Trim();
        }
        Commit(cur);
    }

    private static void Commit(Rec r)
    {
        if (r == null) return;
        string fc = (r.Fc ?? string.Empty).Trim();
        if (fc.Length == 0) return;
        if (r.Nicks == null) r.Nicks = new List<string>();
        if (r.Nicks.Count == 0 && !string.IsNullOrEmpty(r.Nick)) r.Nicks.Add(r.Nick);
        r.Fc = fc;
        Cache[fc.ToLowerInvariant()] = r;
    }

    private static void LoadLegacy(string[] lines)
    {
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            string[] p = line.Split('|');
            if (p.Length < 5) continue;
            string fc = p[0].Trim();
            if (fc.Length == 0) continue;

            var nicks = new List<string>();
            foreach (string part in p[2].Split(';')) { string n = part.Trim(); if (n.Length > 0) nicks.Add(n); }
            if (nicks.Count == 0 && p[1].Trim().Length > 0) nicks.Add(p[1].Trim());

            Cache[fc.ToLowerInvariant()] = new Rec { Fc = fc, Nick = p[1].Trim(), Nicks = nicks, First = p[3].Trim(), Last = p[4].Trim() };
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryTxt));
            var sb = new StringBuilder();
            sb.Append("# Nocturne player history\n");
            var ordered = new List<Rec>(Cache.Values);
            ordered.Sort((a, b) => string.CompareOrdinal(b.Last ?? "", a.Last ?? ""));
            foreach (Rec r in ordered)
            {
                sb.Append(Sep).Append('\n');
                sb.Append("Name: ").Append(r.Nick).Append('\n');
                string aliases = r.Nicks.Count > 1 ? string.Join(" · ", r.Nicks.GetRange(1, r.Nicks.Count - 1)) : "—";
                sb.Append("Aliases: ").Append(aliases).Append('\n');
                sb.Append("Level: ").Append(r.Level).Append('\n');
                sb.Append("FriendCode: ").Append(r.Fc).Append('\n');
                sb.Append("PUID: ").Append(r.Puid.Length > 0 ? r.Puid : "—").Append('\n');
                string plat = r.Platform;
                if (r.Raw.Length > 0) plat += " · " + r.Raw;
                sb.Append("Platform: ").Append(plat).Append('\n');
                sb.Append("First seen: ").Append(r.First).Append('\n');
                sb.Append("Last seen: ").Append(r.Last).Append('\n');
            }
            sb.Append(Sep).Append('\n');
            File.WriteAllText(HistoryTxt, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
}

public sealed class NocturneHistoryTracker : MonoBehaviour
{
    private const float NickToastCd = 4f;

    private float _next;
    private int _room = -1;
    private readonly HashSet<int> _greeted = new HashSet<int>();
    private readonly Dictionary<string, float> _lastNick = new Dictionary<string, float>();

    public void Update()
    {
        if (!NocturneConfig.NameHistory.Value) return;
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 0.6f;

        try
        {
            InnerNetClient net = AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
            if (net == null || net.allClients == null) return;

            if (net.GameId != _room) { _room = net.GameId; _greeted.Clear(); _lastNick.Clear(); }
            bool greet = NocturneConfig.NotifyKnownPlayer.Value;

            var e = net.allClients.GetEnumerator();
            while (e.MoveNext())
            {
                ClientData c = e.Current;
                if (c == null || c.Id < 0) continue;

                if (greet && c.Id != net.ClientId && _greeted.Add(c.Id) && NocturneNameHistory.KnownBefore(c, out string since, out string oldNick))
                    Greet(c, since, oldNick);

                string prev = NocturneNameHistory.RecordJoin(c);
                if (prev != null)
                {
                    string fc = c.FriendCode;
                    string key = string.IsNullOrEmpty(fc) ? "id" + c.Id : fc.ToLowerInvariant();
                    float now = Time.realtimeSinceStartup;
                    if (!_lastNick.TryGetValue(key, out float lt) || now - lt >= NickToastCd)
                    {
                        _lastNick[key] = now;
                        NocturneToast.Push(NocturneText.T("Смена ника", "Nick change"), $"{prev} → {NocturneNameHistory.NickOf(c)}", 4f, NocturneNotifyKind.Info);
                    }
                }
            }
        }
        catch { }
    }

    private static void Greet(ClientData c, string since, string oldNick)
    {
        string nick = NocturneNameHistory.NickOf(c);
        if (nick.Length == 0) return;

        string detail = oldNick.Length > 0 && !string.Equals(oldNick, nick, StringComparison.Ordinal)
            ? NocturneText.T($"{nick} (был {oldNick}) · с {since}", $"{nick} (was {oldNick}) · since {since}")
            : NocturneText.T($"{nick} · с {since}", $"{nick} · since {since}");

        bool joinToast = NocturneConfig.JoinDetect.Value;
        if (!joinToast) NocturneToast.Push(NocturneText.T("Знакомый игрок", "Known player"), detail, 4.5f, NocturneNotifyKind.Warning);
        try { NocturneEventLog.Add("☺ " + NocturneText.T("Знакомый: ", "Known: ") + detail, NocturneNotifyKind.Warning, NocturneEventCat.Join); } catch { }
    }
}
