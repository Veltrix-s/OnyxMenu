using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneJoinDetector : MonoBehaviour
{
    private const float ScanInterval = 0.5f;
    private const float PendingTimeout = 2.5f;

    private static readonly string[] SusTokens =
    {
        "menu", "mod", "cheat", "hack", "inject", "trainer", "aimbot", "godmode",
        "esp", "exploit", "njord", "meow", "sus", "aura", "xenon", "nexus",
    };

    private readonly HashSet<int> _known = new HashSet<int>();
    private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
    private readonly List<int> _live = new List<int>();
    private readonly List<int> _stale = new List<int>();
    private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
    private readonly Dictionary<int, RecentEntry> _seen = new Dictionary<int, RecentEntry>();
    private bool _primed;
    private float _next;

    private static bool DetectOn => NocturneConfig.JoinDetect.Value;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + ScanInterval;
        ModHandshake.Tick();
        Scan();
    }

    private void Forget()
    {
        if (_known.Count == 0 && _pending.Count == 0 && _names.Count == 0 && _seen.Count == 0 && !_primed) return;
        _known.Clear();
        _pending.Clear();
        _names.Clear();
        _seen.Clear();
        _primed = false;
    }

    private void Scan()
    {
        InnerNetClient net = Net();
        if (net == null || net.allClients == null)
        {
            Forget();
            return;
        }

        float now = Time.realtimeSinceStartup;
        bool priming = !_primed;
        _live.Clear();

        var cursor = net.allClients.GetEnumerator();
        while (cursor.MoveNext())
        {
            ClientData client = cursor.Current;
            if (client == null || client.Id < 0) continue;
            _live.Add(client.Id);
            _names[client.Id] = ResolveName(client);
            if (client.Id != net.ClientId && (!_seen.TryGetValue(client.Id, out RecentEntry rec) || rec.Level == "?"))
                _seen[client.Id] = Snapshot(client);

            if (priming) { _known.Add(client.Id); continue; }
            if (_known.Contains(client.Id)) continue;
            if (client.Id == net.ClientId) { _known.Add(client.Id); continue; }

            if (!_pending.ContainsKey(client.Id)) _pending[client.Id] = now;
            bool hasChar = client.Character != null && client.Character.Data != null;
            if (hasChar || now - _pending[client.Id] >= PendingTimeout)
            {
                AnnounceJoin(client);
                _known.Add(client.Id);
                _pending.Remove(client.Id);
            }
        }

        if (priming) _primed = true;
        PruneDeparted(_live.Contains(net.ClientId));
    }

    private void PruneDeparted(bool selfPresent)
    {
        _stale.Clear();
        foreach (int id in _known)
            if (!_live.Contains(id)) _stale.Add(id);
        for (int i = 0; i < _stale.Count; i++)
        {
            int id = _stale[i];
            if (selfPresent) { AnnounceLeave(id); Remember(id); }
            _known.Remove(id);
            _names.Remove(id);
            _seen.Remove(id);
        }

        _stale.Clear();
        foreach (var pair in _pending)
            if (!_live.Contains(pair.Key)) _stale.Add(pair.Key);
        for (int i = 0; i < _stale.Count; i++) _pending.Remove(_stale[i]);
    }

    private static RecentEntry Snapshot(ClientData client)
    {
        var e = new RecentEntry
        {
            Name = ResolveName(client),
            Level = ResolveLevel(client),
            Platform = "?",
            Raw = string.Empty,
            Fc = string.Empty,
            Puid = string.Empty,
        };

        try
        {
            if (client.PlatformData != null)
            {
                e.Platform = PlatformLabel(client.PlatformData.Platform);
                e.Raw = CleanRaw(client.PlatformData.PlatformName);
            }
        }
        catch { }
        try { e.Fc = client.FriendCode ?? string.Empty; } catch { }
        try { e.Puid = client.ProductUserId ?? string.Empty; } catch { }
        return e;
    }

    private void Remember(int id)
    {
        if (!_seen.TryGetValue(id, out RecentEntry e)) return;
        if (_names.TryGetValue(id, out string n)) e.Name = n;
        e.Left = System.DateTime.Now.ToString("HH:mm");
        NocturneRecent.Push(e);
    }

    private static void AnnounceJoin(ClientData client)
    {
        string name = ResolveName(client);
        string level = ResolveLevel(client);

        string platform = "?";
        string raw = string.Empty;
        try
        {
            if (client.PlatformData != null)
            {
                platform = PlatformLabel(client.PlatformData.Platform);
                raw = CleanRaw(client.PlatformData.PlatformName);
            }
        }
        catch { }

        bool sus = IsSuspicious(raw);
        string tail = $"{NocturneText.T("Ур.", "Lv.")}{level} · {platform}";
        if (raw.Length > 0) tail += $" · {raw}";

        bool known = false;
        try
        {
            InnerNetClient net = AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
            bool self = net != null && client.Id == net.ClientId;
            if (!self && NocturneConfig.NotifyKnownPlayer.Value
                && NocturneNameHistory.KnownBefore(client, out string since, out _))
            {
                known = true;
                tail += NocturneText.T($" · знаком с {since}", $" · known since {since}");
            }
        }
        catch { }

        string fc = string.Empty, puid = string.Empty;
        try { fc = client.FriendCode ?? string.Empty; } catch { }
        try { puid = client.ProductUserId ?? string.Empty; } catch { }
        string extra = string.Empty;
        if (fc.Length > 0) extra += " · FC " + fc;
        if (puid.Length > 0) extra += " · PUID " + puid;

        bool botHide = false;
        try
        {
            bool showBots = NocturneConfig.ShowBotJoins.Value;
            if (!showBots && NocturneAccess.IsBotPlatform(raw)) botHide = true;
        }
        catch { }

        NocturneNotifyKind kind = sus ? NocturneNotifyKind.Danger : known ? NocturneNotifyKind.Warning : NocturneNotifyKind.Info;
        string mark = sus ? "⚠ " : known ? "☺ ＋ " : "＋ ";
        if (DetectOn && !botHide) NocturneToast.Push(mark + name, tail, 4.5f, kind);
        NocturneEventLog.Add(mark + name + " " + NocturneText.T("зашёл", "joined") + " · " + tail + extra, kind, NocturneEventCat.Join);
    }

    private void AnnounceLeave(int id)
    {
        string name = _names.TryGetValue(id, out string n) ? n : NocturneText.T("Игрок", "Player");
        if (DetectOn) NocturneToast.Push("－ " + name, NocturneText.T("вышел", "left"), 3.5f, NocturneNotifyKind.Warning);
        NocturneEventLog.Add("－ " + name + " " + NocturneText.T("вышел", "left"), NocturneNotifyKind.Warning, NocturneEventCat.Join);
    }

    private static string ResolveName(ClientData client)
    {
        try
        {
            if (client.Character != null && client.Character.Data != null && !string.IsNullOrWhiteSpace(client.Character.Data.PlayerName))
                return Trim(client.Character.Data.PlayerName, 22);
        }
        catch { }

        try
        {
            if (client.PlatformData != null && !string.IsNullOrWhiteSpace(client.PlatformData.PlatformName))
                return Trim(client.PlatformData.PlatformName, 22);
        }
        catch { }

        return NocturneText.T("Игрок", "Player");
    }

    private static string ResolveLevel(ClientData client)
    {
        return Patches.NocturneJoinLevels.Display(client);
    }

    private static string CleanRaw(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string raw = value.Replace("\r", " ").Replace("\n", " ").Trim();

        int pipe = raw.IndexOf('|');
        if (pipe > 0) raw = raw.Substring(0, pipe).TrimEnd();

        if (raw.Equals("TESTNAME", System.StringComparison.OrdinalIgnoreCase)) return string.Empty;

        return Trim(raw, 24);
    }

    private static bool IsSuspicious(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        string lower = raw.ToLowerInvariant();
        for (int i = 0; i < SusTokens.Length; i++)
            if (lower.Contains(SusTokens[i])) return true;
        return false;
    }

    private static string Trim(string value, int max)
    {
        string clean = value.Trim();
        return clean.Length <= max ? clean : clean.Substring(0, max - 1).TrimEnd() + "…";
    }

    private static InnerNetClient Net()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
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
}
