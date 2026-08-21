using System.Collections.Generic;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneJail
{
    private const float TickDelay = 0.5f;

    private static readonly HashSet<byte> _targets = new HashSet<byte>();
    private static readonly List<Collider2D> _rooms = new List<Collider2D>();
    private static readonly List<string> _roomNames = new List<string>();
    private static readonly Dictionary<byte, ushort> _seq = new Dictionary<byte, ushort>();
    private static float _roomsAt;
    private static int _cell;
    private static float _nextAt;

    internal static int TargetCount => _targets.Count;
    internal static bool IsTarget(byte pid) => _targets.Contains(pid);
    internal static void ToggleTarget(byte pid) { if (!_targets.Remove(pid)) _targets.Add(pid); }
    internal static void ClearTargets() => _targets.Clear();

    internal static void SelectAll()
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl pc = e.Current;
                if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
                if (pc == PlayerControl.LocalPlayer) continue;
                _targets.Add(pc.PlayerId);
            }
        }
        catch { }
    }

    internal static string CellName()
    {
        EnsureRooms();
        if (_roomNames.Count == 0) return "-";
        _cell = Mathf.Clamp(_cell, 0, _roomNames.Count - 1);
        return _roomNames[_cell];
    }

    internal static void CellStep(int d)
    {
        EnsureRooms();
        int n = _roomNames.Count;
        if (n == 0) return;
        _cell = ((_cell + d) % n + n) % n;
    }

    private static void RefreshRooms()
    {
        _rooms.Clear();
        _roomNames.Clear();
        try
        {
            ShipStatus ss = ShipStatus.Instance;
            if (ss == null) return;
            foreach (var r in ss.AllRooms)
            {
                if (r == null || r.roomArea == null) continue;
                _rooms.Add(r.roomArea);
                string n;
                try { n = TranslationController.Instance.GetString(r.RoomId); } catch { n = r.RoomId.ToString(); }
                _roomNames.Add(n);
            }
        }
        catch { _rooms.Clear(); _roomNames.Clear(); }
    }

    private static void EnsureRooms()
    {
        try
        {
            if (ShipStatus.Instance == null)
            {
                if (_roomNames.Count > 0) { _rooms.Clear(); _roomNames.Clear(); }
                return;
            }
            if (_roomNames.Count > 0 && Time.unscaledTime - _roomsAt < 1f) return;
            _roomsAt = Time.unscaledTime;
            RefreshRooms();
        }
        catch { }
    }

    private static int NearestVent(Vector2 pos)
    {
        int best = -1;
        float bd = float.MaxValue;
        try
        {
            var vents = ShipStatus.Instance.AllVents;
            if (vents == null) return -1;
            for (int i = 0; i < vents.Count; i++)
            {
                Vent v = vents[i];
                if (v == null) continue;
                float d = Vector2.Distance(pos, v.transform.position);
                if (d < bd) { bd = d; best = i; }
            }
        }
        catch { }
        return best;
    }

    internal static void Tick()
    {
        if (_targets.Count == 0) return;
        EnsureRooms();
        if (_rooms.Count == 0) return;

        float now = Time.unscaledTime;
        if (now < _nextAt) return;
        _nextAt = now + TickDelay;

        _cell = Mathf.Clamp(_cell, 0, _rooms.Count - 1);
        Collider2D cell = _rooms[_cell];
        if (cell == null) return;
        int ventId = NearestVent((Vector2)cell.bounds.center);
        if (ventId < 0) return;

        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl pc = e.Current;
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead) continue;
                if (!_targets.Contains(pc.PlayerId)) continue;
                if (cell.OverlapPoint(pc.GetTruePosition())) continue;
                SendToVent(pc, ventId);
            }
        }
        catch { }
    }

    private static ushort Next(byte pid)
    {
        _seq.TryGetValue(pid, out ushort v);
        if (v < 10000) v = 10000;
        v++;
        _seq[pid] = v;
        return v;
    }

    private static bool Push(InnerNetClient net, PlayerControl target, byte vent, byte op)
    {
        MessageWriter body = null, w = null;
        try
        {
            body = MessageWriter.Get(SendOption.None);
            body.Write(Next(target.PlayerId));
            body.Write(op);
            body.Write(vent);

            w = net.StartRpcImmediately(((InnerNetObject)ShipStatus.Instance).NetId, 35, SendOption.Reliable, target.OwnerId);
            if (w == null) return false;
            w.Write((byte)SystemTypes.Ventilation);
            w.WriteNetObject(target);
            w.Write(body, false);
            net.FinishRpcImmediately(w);
            return true;
        }
        catch { return false; }
        finally { try { body?.Recycle(); } catch { } }
    }

    private static void SendToVent(PlayerControl target, int ventId)
    {
        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null || target == null || target.MyPhysics == null) return;
            byte vent = (byte)ventId;

            if (net.AmHost)
            {
                target.MyPhysics.RpcBootFromVent(ventId);
                return;
            }

            if (!Push(net, target, vent, 2)) return;
            Push(net, target, vent, 5);
        }
        catch { }
    }

    internal static void Reset()
    {
        _targets.Clear();
        _seq.Clear();
        _nextAt = 0f;
    }
}

[HarmonyLib.HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneJailReset
{
    public static void Postfix() => NocturneJail.Reset();
}
