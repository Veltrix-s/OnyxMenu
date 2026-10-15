using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneDummyAI
{
    private sealed class St
    {
        public List<Vector2> Path;
        public int Wp;
        public float IdleTo;
        public Vector2 Goal;
        public bool Running;
        public Vector2 Last;
        public float Stuck;
        public float Repath;
        public bool Fixing;
        public SystemTypes FixSys = NoSys;
        public float RepairAt;
    }

    private static readonly Dictionary<byte, St> States = new Dictionary<byte, St>();

    private const float Arrive = 0.30f;
    private const float WpDist = 0.45f;
    private const float Speed = 2.2f;
    private const float StuckMax = 0.9f;
    private const float IdleMin = 3f, IdleMax = 6f;
    private const float Reach = 0.55f;
    private const SystemTypes NoSys = (SystemTypes)255;

    internal static void Reset() => States.Clear();

    private static bool Active()
    {
        bool tasks = NocturneConfig.DummyDoTasks.Value;
        bool fix = NocturneConfig.DummyFixSabotage.Value;
        return (tasks || fix) && ShipStatus.Instance != null
            && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
            && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;
    }

    private static bool _placed;
    private static int _placedMap = -999;
    private static SystemTypes _sab = NoSys;
    private static Vector2 _sabRoom;

    internal static void Tick(IEnumerable<PlayerControl> bots)
    {
        if (!Active())
        {
            _placed = false;
            return;
        }
        float now = Time.time;

        if (!_placed || _placedMap != NocturneNav.CurrentMapId())
        {
            _placed = true;
            _placedMap = NocturneNav.CurrentMapId();
            PlaceAtSpawn(bots);
        }

        _sab = NoSys;
        if (NocturneConfig.DummyFixSabotage.Value)
        {
            for (int i = 0; i < Sabs.Length; i++)
            {
                if (!Sabotaged(Sabs[i])) continue;
                _sab = Sabs[i];
                _sabRoom = Room(_sab);
                break;
            }
        }

        foreach (PlayerControl pc in bots)
        {
            if (pc == null || pc.Data == null || pc.Data.IsDead)
                continue;
            Drive(pc, now);
        }
    }

    private static void PlaceAtSpawn(IEnumerable<PlayerControl> bots)
    {
        Vector2 c;
        try
        {
            c = ShipStatus.Instance.InitialSpawnCenter;
        }
        catch { try { c = ShipStatus.Instance.MeetingSpawnCenter; } catch { c = Ui.Zero2; } }

        int i = 0;
        foreach (PlayerControl pc in bots)
        {
            if (pc == null)
                continue;
            float a = i * 0.7f;
            Vector2 spot = c + Ui.V(Ui.Cos(a), Ui.Sin(a)) * (0.4f + 0.25f * i);
            Vector2 safe = NocturneNav.NearestWalkable(spot);
            try
            {
                pc.NetTransform.SnapTo(safe);
            }
            catch { try { pc.transform.position = safe; } catch { } }
            States.Remove(pc.PlayerId);
            i++;
        }
    }

    private static void Drive(PlayerControl pc, float now)
    {
        if (!States.TryGetValue(pc.PlayerId, out St s))
        {
            s = new St();
            States[pc.PlayerId] = s;
        }

        if (NocturneDummyChat.TryReport(pc))
        {
            Stop(pc, s);
            return;
        }

        if (now < s.IdleTo)
        {
            Stop(pc, s);
            return;
        }

        if (NocturneConfig.DummyFixSabotage.Value)
        {
            if (s.Fixing)
            {
                if (!Sabotaged(s.FixSys))
                {
                    s.Fixing = false;
                    s.FixSys = NoSys;
                    s.RepairAt = 0f;
                    s.Path = null;
                }
            }
            else if (_sab != NoSys)
            {
                s.Fixing = true;
                s.FixSys = _sab;
                s.RepairAt = 0f;
                s.Goal = _sabRoom != Ui.Zero2 ? _sabRoom : Pos(pc);
                s.Path = null;
            }
        }

        bool tasks = NocturneConfig.DummyDoTasks.Value;
        if (!s.Fixing && !tasks)
        {
            Stop(pc, s);
            return;
        }

        if (s.Path == null || s.Wp >= s.Path.Count)
        {
            if (now < s.Repath)
            {
                Stop(pc, s);
                return;
            }
            Vector2 from = Pos(pc);
            Vector2 goal = s.Fixing ? s.Goal : PickConsole(from);
            if (goal == Ui.Zero2)
            {
                s.Repath = now + 0.5f;
                return;
            }

            List<Vector2> path = NocturneNav.FindPath(from, goal);
            if (path == null || path.Count < 2)
            {
                s.Repath = now + 0.8f;
                return;
            }
            s.Path = path;
            s.Wp = 1;
            if (!s.Fixing) s.Goal = goal;
            s.Stuck = 0f;
            s.Last = from;
        }

        Vector2 cur = Pos(pc);
        if ((s.Goal - cur).magnitude <= Reach)
        {
            Stop(pc, s);
            if (s.Fixing)
            {
                if (s.RepairAt <= 0f)
                    s.RepairAt = now + Random.Range(3f, 5f);
                else if (now >= s.RepairAt)
                {
                    Repair(s.FixSys);
                    s.Fixing = false;
                    s.FixSys = NoSys;
                    s.RepairAt = 0f;
                    s.IdleTo = now + 1f;
                    s.Path = null;
                }
                return;
            }
            s.IdleTo = now + Random.Range(IdleMin, IdleMax);
            s.Path = null;
            return;
        }

        bool last = s.Wp >= s.Path.Count - 1;
        Vector2 tgt = s.Path[s.Wp];
        Vector2 diff = tgt - cur;
        if (diff.magnitude <= (last ? Arrive : WpDist))
        {
            s.Wp++;
            s.Stuck = 0f;
            if (s.Wp >= s.Path.Count)
            {
                s.Path = null;
                s.Repath = now + 0.15f;
            }
            return;
        }

        float moved = (cur - s.Last).magnitude;
        s.Last = cur;
        if (moved < 0.012f)
        {
            s.Stuck += Time.fixedDeltaTime;
            if (s.Stuck > StuckMax)
            {
                s.Stuck = 0f;
                s.Wp++;
                if (s.Wp >= s.Path.Count)
                {
                    s.Path = null;
                    s.Repath = now + 0.2f;
                }
                return;
            }
        }
        else
            s.Stuck = 0f;

        Move(pc, diff.normalized, s);
    }

    private static void Move(PlayerControl pc, Vector2 dir, St s)
    {
        try
        {
            var phys = pc.MyPhysics;
            if (phys != null && phys.body != null)
            {
                phys.body.velocity = dir * Speed;
                pc.cosmetics.SetFlipX(dir.x < 0f);
            }
        }
        catch { }

        if (!s.Running)
        {
            s.Running = true;
            try
            {
                pc.MyPhysics.Animations.PlayRunAnimation();
            }
            catch { }
            try
            {
                pc.cosmetics.AnimateSkinRun();
            }
            catch { }
        }
    }

    private static void Stop(PlayerControl pc, St s)
    {
        if (pc.MyPhysics != null && pc.MyPhysics.body != null)
            pc.MyPhysics.body.velocity = Ui.Zero2;
        if (s.Running)
        {
            s.Running = false;
            try
            {
                pc.MyPhysics.Animations.PlayIdleAnimation();
            }
            catch { }
            try
            {
                pc.cosmetics.AnimateSkinIdle();
            }
            catch { }
        }
    }

    private static Vector2 Pos(PlayerControl pc)
    {
        try
        {
            return pc.GetTruePosition();
        }
        catch
        {
            return pc.transform.position;
        }
    }

    private static readonly SystemTypes[] Sabs =
    {
        SystemTypes.Reactor, SystemTypes.LifeSupp, SystemTypes.Electrical, SystemTypes.Comms, SystemTypes.Laboratory,
    };

    private static ISystemType Sys(SystemTypes id)
    {
        var all = ShipStatus.Instance != null ? ShipStatus.Instance.Systems : null;
        return all != null && all.ContainsKey(id) ? all[id] : null;
    }

    private static bool Sabotaged(SystemTypes id)
    {
        try
        {
            ISystemType sys = Sys(id);
            if (sys == null) return false;
            var act = sys.TryCast<IActivatable>();
            if (act != null) return act.IsActive;
            if (id == SystemTypes.LifeSupp)
            {
                var o = sys.TryCast<LifeSuppSystemType>();
                return o != null && o.Countdown < 9000f;
            }
        }
        catch { }
        return false;
    }

    private static Vector2 Room(SystemTypes id)
    {
        try
        {
            var rooms = ShipStatus.Instance.FastRooms;
            if (rooms == null)
                return Ui.Zero2;
            PlainShipRoom r = rooms[id];
            if (r == null || r.roomArea == null) return Ui.Zero2;
            return r.roomArea.transform.position;
        }
        catch
        {
            return Ui.Zero2;
        }
    }

    private static void Repair(SystemTypes id)
    {
        try
        {
            if (ShipStatus.Instance == null) return;
            ISystemType sys = Sys(id);
            switch (id)
            {
                case SystemTypes.Electrical:
                {
                    var sw = sys.TryCast<SwitchSystem>();
                    if (sw != null)
                    {
                        int actual = sw.ActualSwitches, exp = sw.ExpectedSwitches;
                        for (int i = 0; i < 5; i++)
                            if (((actual >> i) & 1) != ((exp >> i) & 1))
                                ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Electrical, (byte)i);
                    }
                    break;
                }
                case SystemTypes.LifeSupp:
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 64);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 64 | 1);
                    break;
                case SystemTypes.Reactor:
                case SystemTypes.Laboratory:
                    ShipStatus.Instance.RpcUpdateSystem(id, 64);
                    ShipStatus.Instance.RpcUpdateSystem(id, 64 | 1);
                    break;
                case SystemTypes.Comms:
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16 | 1);
                    break;
            }
        }
        catch { }
    }

    private static Vector2 PickConsole(Vector2 from)
    {
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null)
            return Ui.Zero2;
        var cons = ship.AllConsoles;
        int n = cons.Count;
        if (n <= 0) return Ui.Zero2;

        for (int t = 0; t < 6; t++)
        {
            Console c = cons[Random.Range(0, n)];
            if (c == null)
                continue;
            Vector2 p = c.transform.position;
            if ((p - from).magnitude > 2.5f) return p;
        }
        Console any = cons[Random.Range(0, n)];
        return any != null ? (Vector2)any.transform.position : Ui.Zero2;
    }
}
