using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneJoinLogger : MonoBehaviour
{
    private readonly HashSet<int> _seen = new HashSet<int>();
    private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
    private readonly List<int> _live = new List<int>();
    private readonly List<int> _stale = new List<int>();
    private bool _primed;
    private float _next;

    public void Update()
    {
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 0.5f;

        InnerNetClient net = Net();
        if (net == null || net.allClients == null) { Forget(); return; }

        float now = Time.realtimeSinceStartup;
        bool priming = !_primed;
        _live.Clear();

        var e = net.allClients.GetEnumerator();
        while (e.MoveNext())
        {
            ClientData c = e.Current;
            if (c == null || c.Id < 0) continue;
            _live.Add(c.Id);
            try { Patches.NocturneJoinLevels.RememberCurrent(c.Character); } catch { }

            if (priming) { _seen.Add(c.Id); continue; }
            if (_seen.Contains(c.Id)) continue;
            if (c.Id == net.ClientId) { _seen.Add(c.Id); continue; }

            if (!_pending.ContainsKey(c.Id)) _pending[c.Id] = now;
            bool ready = c.Character != null && c.Character.Data != null
                && (Patches.NocturneJoinLevels.TryGet(c.Id, out _) || ValidLevel(c.Character.Data.PlayerLevel));
            if (ready || now - _pending[c.Id] >= 2.5f)
            {
                Log(c);
                _seen.Add(c.Id);
                _pending.Remove(c.Id);
            }
        }

        if (priming) _primed = true;
        Prune();
    }

    private void Forget()
    {
        if (_seen.Count == 0 && _pending.Count == 0 && !_primed) return;
        _seen.Clear();
        _pending.Clear();
        _primed = false;
    }

    private void Prune()
    {
        _stale.Clear();
        foreach (int id in _seen) if (!_live.Contains(id)) _stale.Add(id);
        for (int i = 0; i < _stale.Count; i++) _seen.Remove(_stale[i]);

        _stale.Clear();
        foreach (var kv in _pending) if (!_live.Contains(kv.Key)) _stale.Add(kv.Key);
        for (int i = 0; i < _stale.Count; i++) _pending.Remove(_stale[i]);
    }

    private static void Log(ClientData c)
    {
        int tag = 0;
        string platform = "-";
        string raw = string.Empty;
        try
        {
            if (c.PlatformData != null)
            {
                tag = (int)c.PlatformData.Platform;
                platform = c.PlatformData.Platform.ToString();
                raw = c.PlatformData.PlatformName ?? string.Empty;
            }
        }
        catch { }

        string name = "-";
        try { if (!string.IsNullOrWhiteSpace(c.PlayerName)) name = c.PlayerName; } catch { }

        string level = Patches.NocturneJoinLevels.Display(c);
        if (level == "?") level = "-";

        string fc = string.Empty;
        string puid = string.Empty;
        try { fc = c.FriendCode ?? string.Empty; } catch { }
        try { puid = c.ProductUserId ?? string.Empty; } catch { }

        NocturnePlugin.Logger?.LogInfo($"client info: id={c.Id}, player='{Trim(name, 64)}', platformTag={tag}, platform='{Trim(platform, 48)}', rawPlatformName='{Trim(raw, 64)}', level={level}, friendCode='{Trim(fc, 64)}', productUserId='{Trim(puid, 128)}'");
    }

    private static bool ValidLevel(uint raw) => raw != uint.MaxValue && raw <= 9999u;

    private static string Trim(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static InnerNetClient Net()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }
}
