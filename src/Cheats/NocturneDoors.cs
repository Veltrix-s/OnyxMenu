using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class NocturneDoors
{
    private static readonly HashSet<int> _pinned = new HashSet<int>();
    private static readonly HashSet<int> _seen = new HashSet<int>();
    private static float _next;

    public static bool KeepOpen;
    public static bool HasPins => _pinned.Count > 0;

    public static void CloseAll() => ForEachRoom(Close);
    public static void PinAll() => ForEachRoom(r => { Close(r); _pinned.Add(r); });
    public static void UnpinAll() => _pinned.Clear();

    public static bool IsPinned(int room) => _pinned.Contains(room);
    public static void CloseOne(int room) => Close(room);

    public static bool TogglePin(int room)
    {
        if (IsDecon(room)) return false;
        if (_pinned.Remove(room)) return false;
        Close(room);
        _pinned.Add(room);
        return true;
    }

    public static void OpenAll()
    {
        ShipStatus ss = ShipStatus.Instance;
        if (ss == null || ss.AllDoors == null) return;
        try { foreach (OpenableDoor d in ss.AllDoors) OpenDoor(ss, d); }
        catch { }
    }

    public static void Tick()
    {
        ShipStatus ss = ShipStatus.Instance;
        if (ss == null) return;
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.7f;

        if (KeepOpen) { OpenClosed(ss); return; }
        if (_pinned.Count == 0) return;
        foreach (int r in _pinned) Close(r);
    }

    private static void ForEachRoom(Action<int> act)
    {
        ShipStatus ss = ShipStatus.Instance;
        if (ss == null || ss.AllDoors == null) return;
        _seen.Clear();
        try
        {
            foreach (OpenableDoor d in ss.AllDoors)
            {
                if (d == null) continue;
                int r = (int)d.Room;
                if (_seen.Add(r)) act(r);
            }
        }
        catch { }
    }

    private static void OpenClosed(ShipStatus ss)
    {
        if (ss.AllDoors == null) return;
        try
        {
            foreach (OpenableDoor d in ss.AllDoors)
            {
                if (d == null || IsDecon((int)d.Room)) continue;
                PlainDoor pd = d.TryCast<PlainDoor>();
                if (pd != null && pd.Open) continue;
                OpenDoor(ss, d);
            }
        }
        catch { }
    }

    private static void OpenDoor(ShipStatus ss, OpenableDoor d)
    {
        if (d == null || IsDecon((int)d.Room)) return;
        try
        {
            ss.RpcUpdateSystem(SystemTypes.Doors, (byte)(d.Id | 64));
            d.SetDoorway(true);
        }
        catch { }
    }

    private static void Close(int r)
    {
        if (IsDecon(r)) return;
        try { ShipStatus.Instance.RpcCloseDoorsOfType((SystemTypes)r); }
        catch { }
    }

    private static bool IsDecon(int r) =>
        r == (int)SystemTypes.Decontamination || r == (int)SystemTypes.Decontamination2 || r == (int)SystemTypes.Decontamination3;
}
