using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class NocturneXmas
{
    private const float Step = 0.1f;
    private static readonly Dictionary<byte, float> _timers = new Dictionary<byte, float>();
    private static readonly List<byte> _ids = new List<byte>();
    private static readonly List<byte> _drop = new List<byte>();
    private static readonly HashSet<int> _used = new HashSet<int>();
    private static readonly List<int> _free = new List<int>();

    internal static bool Toggle(byte id)
    {
        if (_timers.Remove(id)) return false;
        _timers[id] = 0f;
        return true;
    }

    internal static bool IsCommand(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        switch (text.Trim().ToLowerInvariant())
        {
            case "/xmas":
            case "/tree":
            case "/ёлка":
            case "!xmas":
                return true;
            default:
                return false;
        }
    }

    internal static void Tick()
    {
        if (_timers.Count == 0 || AmongUsClient.Instance == null) return;
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) { _timers.Clear(); return; }
        float now = Time.realtimeSinceStartup;
        bool host = AmongUsClient.Instance.AmHost;

        _ids.Clear();
        foreach (byte id in _timers.Keys) _ids.Add(id);
        _drop.Clear();

        for (int i = 0; i < _ids.Count; i++)
        {
            byte id = _ids[i];
            if (now - _timers[id] < Step) continue;
            _timers[id] = now;

            PlayerControl p = Find(id);
            if (p == null || p.Data == null || p.Data.Disconnected) { _drop.Add(id); continue; }

            try
            {
                if (host)
                {
                    p.RpcSetColor((byte)Random.Range(0, MaxColor() + 1));
                }
                else if (p.AmOwner)
                {
                    int free = FreeColor(p);
                    if (free >= 0) p.CmdCheckColor((byte)free);
                }
            }
            catch { }
        }

        for (int i = 0; i < _drop.Count; i++) _timers.Remove(_drop[i]);
    }

    private static int FreeColor(PlayerControl self)
    {
        _used.Clear();
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl o = e.Current;
                if (o != null && o.Data != null && o != self && o.Data.DefaultOutfit != null)
                    _used.Add(o.Data.DefaultOutfit.ColorId);
            }
        }
        catch { }

        _free.Clear();
        int max = MaxColor();
        for (int i = 0; i <= max; i++)
            if (!_used.Contains(i)) _free.Add(i);
        return _free.Count == 0 ? -1 : _free[Random.Range(0, _free.Count)];
    }

    private static PlayerControl Find(byte id)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
                if (e.Current != null && e.Current.PlayerId == id) return e.Current;
        }
        catch { }
        return null;
    }

    private static int MaxColor()
    {
        try { if (Palette.PlayerColors != null) return Mathf.Max(0, Palette.PlayerColors.Length - 1); }
        catch { }
        return 18;
    }
}
