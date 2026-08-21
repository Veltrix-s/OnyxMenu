using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
        public int FixSys = -1;
        public float RepairAt;
    }

    private static readonly Dictionary<byte, St> States = new Dictionary<byte, St>();

    private const float Arrive = 0.30f;
    private const float WpDist = 0.45f;
    private const float Speed = 2.2f;
    private const float StuckMax = 0.9f;
    private const float IdleMin = 3f, IdleMax = 6f;
    private const float Reach = 0.55f;

    internal static void Reset() => States.Clear();

    private static bool Active()
    {
        try
        {
            bool tasks = NocturneConfig.DummyDoTasks.Value;
            bool fix = NocturneConfig.DummyFixSabotage.Value;
            return (tasks || fix) && ShipStatus.Instance != null
                && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
                && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;
        }
        catch { return false; }
    }

    private static bool _placed;
    private static int _placedMap = -999;

    internal static void Tick(IEnumerable<PlayerControl> bots)
    {
        if (!Active()) { _placed = false; return; }
        float now = Time.time;

        if (!_placed || _placedMap != NocturneNav.CurrentMapId())
        {
            _placed = true;
            _placedMap = NocturneNav.CurrentMapId();
            PlaceAtSpawn(bots);
        }

        foreach (PlayerControl pc in bots)
        {
            if (pc == null || pc.Data == null || pc.Data.IsDead) continue;
            Drive(pc, now);
        }
    }

    private static void PlaceAtSpawn(IEnumerable<PlayerControl> bots)
    {
        Vector2 c;
        try { c = ShipStatus.Instance.InitialSpawnCenter; }
        catch { try { c = ShipStatus.Instance.MeetingSpawnCenter; } catch { c = Vector2.zero; } }

        int i = 0;
        foreach (PlayerControl pc in bots)
        {
            if (pc == null) continue;
            float a = i * 0.7f;
            Vector2 spot = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.4f + 0.25f * i);
            Vector2 safe = NocturneNav.NearestWalkable(spot);
            try { pc.NetTransform.SnapTo(safe); } catch { try { ((Component)pc).transform.position = safe; } catch { } }
            States.Remove(pc.PlayerId);
            i++;
        }
    }

    private static void Drive(PlayerControl pc, float now)
    {
        if (!States.TryGetValue(pc.PlayerId, out St s)) { s = new St(); States[pc.PlayerId] = s; }

        if (NocturneDummyChat.TryReport(pc)) { Stop(pc, s); return; }

        if (now < s.IdleTo) { Stop(pc, s); return; }

        if (NocturneConfig.DummyFixSabotage.Value)
        {
            if (s.Fixing)
            {
                if (!Sabotaged(s.FixSys)) { s.Fixing = false; s.FixSys = -1; s.RepairAt = 0f; s.Path = null; }
            }
            else if (SabotageGoal(Pos(pc), out Vector2 fp, out int sys))
            {
                s.Fixing = true; s.FixSys = sys; s.RepairAt = 0f; s.Goal = fp; s.Path = null;
            }
        }

        bool tasks = NocturneConfig.DummyDoTasks.Value;
        if (!s.Fixing && !tasks) { Stop(pc, s); return; }

        if (s.Path == null || s.Wp >= s.Path.Count)
        {
            if (now < s.Repath) { Stop(pc, s); return; }
            Vector2 from = Pos(pc);
            Vector2 goal = s.Fixing ? s.Goal : PickConsole(from);
            if (goal == Vector2.zero) { s.Repath = now + 0.5f; return; }

            List<Vector2> path = NocturneNav.FindPath(from, goal);
            if (path == null || path.Count < 2) { s.Repath = now + 0.8f; return; }
            s.Path = path; s.Wp = 1;
            if (!s.Fixing) s.Goal = goal;
            s.Stuck = 0f; s.Last = from;
        }

        Vector2 cur = Pos(pc);
        if ((s.Goal - cur).magnitude <= Reach)
        {
            Stop(pc, s);
            if (s.Fixing)
            {
                if (s.RepairAt <= 0f) s.RepairAt = now + Random.Range(3f, 5f);
                else if (now >= s.RepairAt)
                {
                    Repair(s.FixSys);
                    s.Fixing = false; s.FixSys = -1; s.RepairAt = 0f; s.IdleTo = now + 1f; s.Path = null;
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
            if (s.Wp >= s.Path.Count) { s.Path = null; s.Repath = now + 0.15f; }
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
                if (s.Wp >= s.Path.Count) { s.Path = null; s.Repath = now + 0.2f; }
                return;
            }
        }
        else s.Stuck = 0f;

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
                try { pc.cosmetics.SetFlipX(dir.x < 0f); } catch { }
            }
        }
        catch { }

        if (!s.Running)
        {
            s.Running = true;
            try { pc.MyPhysics.Animations.PlayRunAnimation(); } catch { }
            try { pc.cosmetics.AnimateSkinRun(); } catch { }
        }
    }

    private static void Stop(PlayerControl pc, St s)
    {
        try { if (pc.MyPhysics != null && pc.MyPhysics.body != null) pc.MyPhysics.body.velocity = Vector2.zero; } catch { }
        if (s.Running)
        {
            s.Running = false;
            try { pc.MyPhysics.Animations.PlayIdleAnimation(); } catch { }
            try { pc.cosmetics.AnimateSkinIdle(); } catch { }
        }
    }

    private static Vector2 Pos(PlayerControl pc)
    {
        try { return pc.GetTruePosition(); } catch { return ((Component)pc).transform.position; }
    }

    private static readonly int[] Sabs = { 3, 8, 7, 14, 15 };

    private static ISystemType Sys(int id)
    {
        try { return ShipStatus.Instance != null && ShipStatus.Instance.Systems != null ? ShipStatus.Instance.Systems[(SystemTypes)id] : null; }
        catch { return null; }
    }

    private static bool Sabotaged(int id)
    {
        try
        {
            ISystemType sys = Sys(id);
            if (sys == null) return false;
            var act = ((Il2CppObjectBase)sys).TryCast<IActivatable>();
            if (act != null) return act.IsActive;
            if (id == 8)
            {
                var o = ((Il2CppObjectBase)sys).TryCast<LifeSuppSystemType>();
                return o != null && o.Countdown < 9000f;
            }
        }
        catch { }
        return false;
    }

    private static bool SabotageGoal(Vector2 from, out Vector2 fixPos, out int sys)
    {
        fixPos = Vector2.zero; sys = -1;
        try
        {
            for (int i = 0; i < Sabs.Length; i++)
            {
                if (!Sabotaged(Sabs[i])) continue;
                Vector2 room = Room(Sabs[i]);
                fixPos = room != Vector2.zero ? room : from;
                sys = Sabs[i];
                return true;
            }
        }
        catch { }
        return false;
    }

    private static Vector2 Room(int id)
    {
        try
        {
            var rooms = ShipStatus.Instance.FastRooms;
            if (rooms == null) return Vector2.zero;
            PlainShipRoom r = rooms[(SystemTypes)id];
            if (r == null || r.roomArea == null) return Vector2.zero;
            return ((Component)r.roomArea).transform.position;
        }
        catch { return Vector2.zero; }
    }

    private static void Repair(int id)
    {
        try
        {
            if (ShipStatus.Instance == null) return;
            ISystemType sys = Sys(id);
            switch (id)
            {
                case 7:
                {
                    var sw = ((Il2CppObjectBase)sys).TryCast<SwitchSystem>();
                    if (sw != null)
                    {
                        int actual = sw.ActualSwitches, exp = sw.ExpectedSwitches;
                        for (int i = 0; i < 5; i++)
                            if (((actual >> i) & 1) != ((exp >> i) & 1))
                                ShipStatus.Instance.RpcUpdateSystem((SystemTypes)7, (byte)i);
                    }
                    break;
                }
                case 8:
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)8, (byte)(64 | 0));
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)8, (byte)(64 | 1));
                    break;
                case 3:
                case 15:
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)id, (byte)(64 | 0));
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)id, (byte)(64 | 1));
                    break;
                case 14:
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)14, (byte)(16 | 0));
                    ShipStatus.Instance.RpcUpdateSystem((SystemTypes)14, (byte)(16 | 1));
                    break;
            }
        }
        catch { }
    }

    private static Vector2 PickConsole(Vector2 from)
    {
        try
        {
            ShipStatus ship = ShipStatus.Instance;
            if (ship == null) return Vector2.zero;
            var cons = ship.AllConsoles;
            int n = ((Il2CppArrayBase<Console>)(object)cons).Count;
            if (n <= 0) return Vector2.zero;

            for (int t = 0; t < 6; t++)
            {
                Console c = ((Il2CppArrayBase<Console>)(object)cons)[Random.Range(0, n)];
                if (c == null) continue;
                Vector2 p = ((Component)c).transform.position;
                if ((p - from).magnitude > 2.5f) return p;
            }
            Console any = ((Il2CppArrayBase<Console>)(object)cons)[Random.Range(0, n)];
            return any != null ? (Vector2)((Component)any).transform.position : Vector2.zero;
        }
        catch { return Vector2.zero; }
    }
}
