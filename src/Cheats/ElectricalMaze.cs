using System;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Nocturne;

internal static class ElectricalMaze
{
    internal struct Gate
    {
        internal StaticDoor Door;
        internal Vector2 Pos;
        internal string Label;
        internal bool Open;
        internal bool Initial;
        internal bool Saved;
    }

    private static ShipStatus _ship;
    private static ElectricalDoors _system;
    private static float _next;
    private static int _frame = -1;

    internal static Gate[] Gates { get; private set; } = Array.Empty<Gate>();
    internal static Rect Bounds { get; private set; }
    internal static bool HasSaved { get; private set; }
    internal static bool CanEdit => _ship != null && _ship == ShipStatus.Instance
        && _system != null && Gates.Length > 0 && LobbyBehaviour.Instance == null && MeetingHud.Instance == null;

    internal static void Refresh()
    {
        int frame = Time.frameCount;
        if (_frame == frame) return;
        _frame = frame;

        ShipStatus ship = ShipStatus.Instance;
        if (ship == null || ship != _ship)
        {
            _ship = ship;
            _system = null;
            Gates = Array.Empty<Gate>();
            HasSaved = false;
            _next = 0f;
        }
        if (ship == null || NocturneNav.CurrentMapId() != 4)
            return;

        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + (_system == null ? 1f : 0.15f);

        if (_system == null)
        {
            Gates = Array.Empty<Gate>();
            HasSaved = false;
            var systems = ship.Systems;
            if (systems == null)
                return;

            foreach (var entry in systems)
            {
                ElectricalDoors maze;
                try
                {
                    maze = ((Il2CppObjectBase)entry.Value).TryCast<ElectricalDoors>();
                }
                catch
                {
                    continue;
                }
                if (maze == null)
                    continue;
                var doors = maze.Doors;
                if (doors == null || doors.Length == 0)
                    return;

                var gates = new Gate[doors.Length];
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < doors.Length; i++)
                {
                    StaticDoor door = doors[i];
                    if (door == null)
                        return;
                    Vector2 pos = door.transform.position;
                    bool open = door.IsOpen;
                    gates[i] = new Gate
                    {
                        Door = door,
                        Pos = pos,
                        Label = (i + 1).ToString(),
                        Open = open,
                        Initial = open
                    };
                    min = Vector2.Min(min, pos);
                    max = Vector2.Max(max, pos);
                }
                NocturnePlugin.Logger.LogInfo($"Electrical maze: system={entry.Key}, doors={doors.Length}");
                for (int i = 0; i < gates.Length; i++)
                    NocturnePlugin.Logger.LogInfo($"Electrical maze [{i}]: {gates[i].Door.name}, pos={gates[i].Pos}, open={gates[i].Open}");
                _system = maze;
                Gates = gates;
                Bounds = new Rect(min.x - 1f, min.y - 1f, max.x - min.x + 2f, max.y - min.y + 2f);
                break;
            }
        }

        for (int i = 0; i < Gates.Length; i++)
            if (Gates[i].Door != null)
                Gates[i].Open = Gates[i].Door.IsOpen;
    }

    internal static void Toggle(int index)
    {
        if (!CanEdit || index < 0 || index >= Gates.Length)
            return;
        StaticDoor door = Gates[index].Door;
        if (door != null)
            SetDoor(index, !door.IsOpen);
    }

    internal static void SetAll(bool open)
    {
        if (!CanEdit) return;
        for (int i = 0; i < Gates.Length; i++)
            SetDoor(i, open);
    }

    internal static void Save()
    {
        if (!CanEdit)
            return;
        for (int i = 0; i < Gates.Length; i++)
        {
            StaticDoor door = Gates[i].Door;
            if (door != null)
                Gates[i].Saved = door.IsOpen;
        }
        HasSaved = true;
    }

    internal static void Restore(bool saved)
    {
        if (!CanEdit || (saved && !HasSaved))
            return;
        for (int i = 0; i < Gates.Length; i++)
            SetDoor(i, saved ? Gates[i].Saved : Gates[i].Initial);
    }

    private static void SetDoor(int index, bool open)
    {
        StaticDoor door = Gates[index].Door;
        if (door == null || door.IsOpen == open)
            return;
        door.SetOpen(open);
        Gates[index].Open = open;
        if (Utils.Host)
            _system.IsDirty = true;
    }
}
