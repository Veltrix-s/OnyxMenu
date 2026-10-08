using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class Roster : MonoBehaviour
{
    private const float ScanInterval = 0.35f;
    private const float JoinSettle = 0.70f;
    private const float JoinRetry = 0.45f;
    private const float JoinMaxWait = 7f;

    private static readonly string[] SusTokens =
    {
        "menu", "mod", "cheat", "hack", "inject", "trainer", "aimbot", "godmode",
        "esp", "exploit", "njord", "meow", "sus", "aura", "xenon", "nexus",
    };

    private readonly HashSet<int> _known = new HashSet<int>();
    private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _recheck = new Dictionary<int, float>();
    private readonly List<int> _live = new List<int>();
    private readonly List<int> _stale = new List<int>();
    private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
    private readonly Dictionary<int, RecentEntry> _seen = new Dictionary<int, RecentEntry>();
    private bool _primed;
    private float _next;

    private static bool DetectOn => NocturneConfig.JoinDetect.Value;

    public void FixedUpdate()
    {
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + ScanInterval;
        Scan();
    }

    private void Forget()
    {
        if (_known.Count == 0 && _pending.Count == 0 && _names.Count == 0 && _seen.Count == 0 && !_primed) return;
        _known.Clear();
        _pending.Clear();
        _recheck.Clear();
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
            Patches.NocturneJoinLevels.RememberCurrent(client.Character);
            _names[client.Id] = ResolveName(client);
            if (client.Id != net.ClientId && (!_seen.TryGetValue(client.Id, out RecentEntry rec) || rec.Level == "?"))
                _seen[client.Id] = Snapshot(client);

            if (priming)
            {
                _known.Add(client.Id);
                continue;
            }
            if (_known.Contains(client.Id)) continue;
            if (client.Id == net.ClientId)
            {
                _known.Add(client.Id);
                continue;
            }

            if (!_pending.ContainsKey(client.Id))
            {
                _pending[client.Id] = now;
                _recheck[client.Id] = now + JoinSettle;
                continue;
            }
            if (_recheck.TryGetValue(client.Id, out float at) && now < at) continue;
            if (!Patches.NocturneJoinLevels.Ready(client) && now - _pending[client.Id] < JoinMaxWait)
            {
                _recheck[client.Id] = now + JoinRetry;
                continue;
            }

            Announce(client);
            LogClient(client);
            _known.Add(client.Id);
            _pending.Remove(client.Id);
            _recheck.Remove(client.Id);
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
            if (selfPresent)
            {
                AnnounceLeave(id);
                Remember(id);
            }
            _known.Remove(id);
            _names.Remove(id);
            _seen.Remove(id);
        }

        _stale.Clear();
        foreach (var pair in _pending)
            if (!_live.Contains(pair.Key)) _stale.Add(pair.Key);
        for (int i = 0; i < _stale.Count; i++)
        {
            _pending.Remove(_stale[i]);
            _recheck.Remove(_stale[i]);
        }
    }

    private static RecentEntry Snapshot(ClientData client)
    {
        var pd = client.PlatformData;
        return new RecentEntry
        {
            Name = ResolveName(client),
            Level = Patches.NocturneJoinLevels.Display(client),
            Platform = pd != null ? Utils.Platform(pd.Platform) : "?",
            Raw = pd != null ? CleanRaw(pd.PlatformName) : string.Empty,
            Fc = client.FriendCode ?? string.Empty,
            Puid = client.ProductUserId ?? string.Empty,
        };
    }

    private void Remember(int id)
    {
        if (!_seen.TryGetValue(id, out RecentEntry e)) return;
        if (_names.TryGetValue(id, out string n)) e.Name = n;
        e.Left = System.DateTime.Now.ToString("HH:mm");
        NocturneRecent.Push(e);
    }

    private static void Announce(ClientData client)
    {
        RecentEntry e = Snapshot(client);
        string tail = $"{NocturneText.T("Ур.", "Lv.")}{e.Level} · {e.Platform}";
        if (e.Raw.Length > 0)
            tail += $" · {e.Raw}";

        bool known = false;
        InnerNetClient net = Net();
        if (net != null && client.Id != net.ClientId && NocturneConfig.NotifyKnownPlayer.Value)
        {
            if (NocturneNameHistory.KnownBefore(client, out string since, out _))
            {
                known = true;
                tail += NocturneText.T($" · знаком с {since}", $" · known since {since}");
            }
        }

        string extra = string.Empty;
        if (e.Fc.Length > 0) extra += " · FC " + e.Fc;
        if (e.Puid.Length > 0) extra += " · PUID " + e.Puid;

        bool sus = IsSuspicious(e.Raw);
        bool botHide = !NocturneConfig.ShowBotJoins.Value && NocturneAccess.IsBotPlatform(e.Raw);

        NocturneNotifyKind kind = sus ? NocturneNotifyKind.Danger : known ? NocturneNotifyKind.Warning : NocturneNotifyKind.Info;
        string mark = sus ? "⚠ " : known ? "☺ ＋ " : "＋ ";
        if (DetectOn && !botHide)
            NocturneToast.Push(mark + e.Name, tail, 4.5f, kind);
        NocturneEventLog.Add(mark + e.Name + " " + NocturneText.T("зашёл", "joined") + " · " + tail + extra, kind, NocturneEventCat.Join);
    }

    private void AnnounceLeave(int id)
    {
        string name = _names.TryGetValue(id, out string n) ? n : NocturneText.T("Игрок", "Player");
        NocturneEventLog.Add("－ " + name + " " + NocturneText.T("вышел", "left"), NocturneNotifyKind.Warning, NocturneEventCat.Join);
    }

    private static void LogClient(ClientData c)
    {
        var pd = c.PlatformData;
        int tag = pd != null ? (int)pd.Platform : 0;
        string platform = pd != null ? pd.Platform.ToString() : "-";
        string raw = pd != null ? pd.PlatformName ?? string.Empty : string.Empty;
        string name = string.IsNullOrWhiteSpace(c.PlayerName) ? "-" : c.PlayerName;
        string level = Patches.NocturneJoinLevels.Display(c);
        if (level == "?") level = "-";

        NocturnePlugin.Logger?.LogInfo($"client info: id={c.Id}, player='{Trim(name, 64)}', platformTag={tag}, platform='{Trim(platform, 48)}', rawPlatformName='{Trim(raw, 64)}', level={level}, friendCode='{Trim(c.FriendCode, 64)}', productUserId='{Trim(c.ProductUserId, 128)}'");
    }

    private static string ResolveName(ClientData client)
    {
        PlayerControl pc = client.Character;
        if (pc != null && pc.Data != null && !string.IsNullOrWhiteSpace(pc.Data.PlayerName))
            return Trim(pc.Data.PlayerName, 22);

        var pd = client.PlatformData;
        if (pd != null && !string.IsNullOrWhiteSpace(pd.PlatformName))
            return Trim(pd.PlatformName, 22);

        return NocturneText.T("Игрок", "Player");
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
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= max ? clean : clean.Substring(0, max - 1).TrimEnd() + "…";
    }

    private static InnerNetClient Net()
    {
        return AmongUsClient.Instance == null ? null : AmongUsClient.Instance;
    }
}
