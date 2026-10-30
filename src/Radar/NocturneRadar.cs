using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneRadar : MonoBehaviour
{
    private sealed class Mover
    {
        internal Vector2 Pos;
        internal Vector2 Vel;
        internal float At = -1f;
        internal Color Col;
        internal float ColAt = -99f;
    }

    private static RadarGeo _geo;
    private static int _geoMap = -999;
    private static float _geoRetry;

    private static readonly RadarFrame Frame = new RadarFrame();
    private static readonly Dictionary<byte, Mover> Movers = new Dictionary<byte, Mover>();

    private static Texture2D _tex;
    private static int _texMap = -999, _texKey, _texW, _texH;
    private static int _wantW, _wantH, _wantKey;
    private static float _wantAt;

    private static string _titleName;
    private static GUIContent _titleC;

    private static bool _drag;
    private static Vector2 _dragOff;
    private static float _rx, _ry;
    private static bool _sizing;
    private static float _sizeNow, _gripDx;

    private static DeadBody[] _bodies;

    private static float _lastDoorClick = -99f;
    private static int _lastDoorRoom = -1;

    internal void DrawGui()
    {
        if (!NocturneConfig.Radar.Value)
            return;
        if (ShipStatus.Instance == null || PlayerControl.LocalPlayer == null)
            return;
        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return;
        if (!Geometry())
            return;

        float sw = Screen.width, sh = Screen.height;
        float rs = Ui.Clamp(sh / 1080f, 0.85f, 2.2f);
        float aspect = _geo.Aspect;
        float maxSc = Ui.Max(0.6f, Ui.Min(4f, RadarView.MaxScale(aspect, rs, sw, sh)));
        float userSc = Ui.Clamp(_sizing ? _sizeNow : NocturneConfig.RadarSize.Value / 100f, 0.6f, maxSc);

        if (!_drag)
        {
            _rx = NocturneConfig.RadarX.Value;
            _ry = NocturneConfig.RadarY.Value;
        }
        RadarLayout b = RadarView.Layout(aspect, userSc, rs, ref _rx, ref _ry, sw, sh);
        var fit = new RadarFit(_geo, b.Box.m_Width, b.Box.m_Height);
        Patches.MouseShield.Cover(b.Box);

        Event e = NocturneStyle.Ev;
        if (e != null)
            Input(b, fit, e, rs, maxSc, userSc);
        if (e == null || !NocturneStyle.Painting)
            return;

        Color ac = NocturneStyle.Current.Accent;
        Gather();
        Bodies();

        string name = MapName();
        if (!ReferenceEquals(name, _titleName))
        {
            _titleName = name;
            _titleC = new GUIContent(name);
        }

        Frame.Title = _titleC;
        Frame.Locked = NocturneConfig.RadarLocked.Value;
        Frame.Fx = NocturneConfig.RadarFx.Value;
        Frame.Teleport = NocturneConfig.RadarTeleport.Value;
        Frame.Doors = CanDoors();
        Frame.Opacity = Ui.Clamp(NocturneConfig.RadarOpacity.Value / 100f, 0.3f, 1f);
        if (Frame.Doors)
            for (int i = 0; i < Frame.RoomCount; i++)
                if (Frame.Rooms[i].Door)
                    Frame.Rooms[i].Pinned = NocturneDoors.IsPinned(Frame.Rooms[i].Id);

        RadarView.Draw(b, fit, MapTexture(b, ac), Frame);
    }

    private static void Input(RadarLayout b, RadarFit fit, Event e, float rs, float maxSc, float userSc)
    {
        Vector2 m = NocturneStyle.Mouse;
        bool left = e.button == 0;
        bool locked = NocturneConfig.RadarLocked.Value;

        if (NocturneStyle.Is(EventType.MouseDown) && left && Ui.In(b.Lock, m))
        {
            NocturneConfig.RadarLocked.Value = !locked;
            _drag = false;
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseDown) && left && CanDoors() && TryClickDoor(b, fit, m))
        {
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseDown) && left && Ui.In(b.Grip, m))
        {
            _sizing = true;
            _sizeNow = userSc;
            _gripDx = Ui.Right(b.Box) - m.x;
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseDown) && left && !locked && Ui.In(b.Box, m))
        {
            _drag = true;
            _dragOff = Ui.V(m.x - b.Box.m_XMin, m.y - b.Box.m_YMin);
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseDrag) && _sizing)
        {
            _sizeNow = Ui.Clamp((m.x + _gripDx - b.Box.m_XMin) / (RadarView.BaseW * rs), 0.6f, maxSc);
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseDrag) && _drag)
        {
            _rx = m.x - _dragOff.x;
            _ry = m.y - _dragOff.y;
            e.Use();
        }
        else if (NocturneStyle.Is(EventType.MouseUp) && (_drag || _sizing))
        {
            if (_drag)
            {
                _drag = false;
                NocturneConfig.RadarX.Value = _rx;
                NocturneConfig.RadarY.Value = _ry;
            }
            if (_sizing)
            {
                _sizing = false;
                NocturneConfig.RadarSize.Value = Ui.Clamp(Ui.RoundToInt(_sizeNow * 100f), 60, 400);
            }
            e.Use();
        }
        else if (NocturneConfig.RadarTeleport.Value && NocturneStyle.Is(EventType.MouseDown) && e.button == 1)
        {
            Teleport(b, fit, e);
        }
    }

    private static bool CanDoors()
    {
        if (!NocturneConfig.RadarDoors.Value) return false;
        PlayerControl me = PlayerControl.LocalPlayer;
        return ShipStatus.Instance != null && MeetingHud.Instance == null
            && me != null && me.Data != null && me.Data.Role != null && me.Data.Role.IsImpostor;
    }

    private static bool TryClickDoor(RadarLayout b, RadarFit fit, Vector2 mp)
    {
        for (int i = 0; i < Frame.RoomCount; i++)
        {
            if (!Frame.Rooms[i].Door || !Ui.In(RadarView.DoorRect(b, fit, Frame.Rooms[i].Pos), mp))
                continue;

            int room = Frame.Rooms[i].Id;
            float now = Time.unscaledTime;
            if (_lastDoorRoom == room && now - _lastDoorClick < 0.35f)
            {
                NocturneDoors.TogglePin(room);
                _lastDoorClick = -99f;
            }
            else
            {
                NocturneDoors.CloseOne(room);
                _lastDoorClick = now;
                _lastDoorRoom = room;
            }
            return true;
        }
        return false;
    }

    private static void Teleport(RadarLayout b, RadarFit fit, Event e)
    {
        Vector2 m = NocturneStyle.Mouse;
        if (!Ui.In(b.Box, m)) return;
        Vector2 world = fit.World(Ui.V(m.x - b.Box.m_XMin, m.y - b.Box.m_YMin));
        PlayerControl.LocalPlayer.NetTransform.SnapTo(world);
        e.Use();
    }

    internal static string MapName()
    {
        switch (NocturneNav.CurrentMapId())
        {
            case 0:
            case 3:
                return "The Skeld";
            case 1:
                return "Mira HQ";
            case 2:
                return "Polus";
            case 4:
                return "Airship";
            case 5:
                return "Fungle";
            default:
                return NocturneText.T("Карта", "Map");
        }
    }

    private static bool Geometry()
    {
        int map = NocturneNav.CurrentMapId();
        if (_geo != null && _geoMap == map)
            return true;

        float now = Time.unscaledTime;
        if (now < _geoRetry)
            return false;
        _geoRetry = now + 0.5f;

        var g = new RadarGeo();
        Rooms(g);
        NocturneNav.Graph nav = NocturneNav.Current();
        if (nav != null)
        {
            for (int i = 0; i < nav.Pos.Length; i++)
                foreach (int nb in nav.Adj[i])
                {
                    if (nb <= i || nb >= nav.Pos.Length)
                        continue;
                    g.Roads.Add(nav.Pos[i]);
                    g.Roads.Add(nav.Pos[nb]);
                }
        }
        if (!g.Close())
            return false;

        _geo = g;
        _geoMap = map;
        _texW = 0;
        _bodies = null;
        Frame.Bodies.Clear();
        Movers.Clear();
        return true;
    }

    private static void Rooms(RadarGeo g)
    {
        Frame.RoomCount = 0;
        ShipStatus s = ShipStatus.Instance;
        if (s == null || s.AllRooms == null)
            return;

        var withDoors = new HashSet<int>();
        if (s.AllDoors != null)
            for (int i = 0; i < s.AllDoors.Length; i++)
            {
                OpenableDoor d = s.AllDoors[i];
                if (d != null)
                    withDoors.Add((int)d.Room);
            }

        var rooms = s.AllRooms;
        var list = new List<RadarRoom>(rooms.Length);
        for (int i = 0; i < rooms.Length; i++)
        {
            PlainShipRoom r = rooms[i];
            Collider2D c = r != null ? r.roomArea : null;
            if (c == null)
                continue;
            AddPoly(c, g);

            Bounds area = c.bounds;
            list.Add(new RadarRoom
            {
                Id = (int)r.RoomId,
                Pos = Ui.V(area.center.x, area.center.y),
                Name = RoomName(r.RoomId),
                Door = withDoors.Count == 0 || withDoors.Contains((int)r.RoomId),
            });
        }
        Frame.Rooms = list.ToArray();
        Frame.RoomCount = list.Count;
    }

    private static GUIContent RoomName(SystemTypes id)
    {
        if (id == SystemTypes.Hallway || id == SystemTypes.Outside)
            return null;
        if (!DestroyableSingleton<TranslationController>.InstanceExists)
            return null;
        string text = DestroyableSingleton<TranslationController>.Instance.GetString(id);
        return string.IsNullOrEmpty(text) ? null : new GUIContent(text);
    }

    private static void AddPoly(Collider2D c, RadarGeo g)
    {
        Transform t = c.transform;

        PolygonCollider2D poly = c.TryCast<PolygonCollider2D>();
        if (poly != null)
        {
            Vector2 off = poly.offset;
            var pts = poly.points;
            if (pts == null || pts.Length < 3)
                return;
            var world = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 wp = t.TransformPoint(Ui.V3(pts[i].x + off.x, pts[i].y + off.y, 0f));
                world[i] = Ui.V(wp.x, wp.y);
            }
            g.Polys.Add(world);
            return;
        }

        BoxCollider2D box = c.TryCast<BoxCollider2D>();
        if (box != null)
        {
            Vector2 hs = box.size * 0.5f;
            Vector2 off = box.offset;
            g.Polys.Add(new[]
            {
                Corner(t, off.x - hs.x, off.y - hs.y),
                Corner(t, off.x + hs.x, off.y - hs.y),
                Corner(t, off.x + hs.x, off.y + hs.y),
                Corner(t, off.x - hs.x, off.y + hs.y),
            });
        }
    }

    private static Vector2 Corner(Transform t, float x, float y)
    {
        Vector3 wp = t.TransformPoint(Ui.V3(x, y, 0f));
        return Ui.V(wp.x, wp.y);
    }

    private static void Gather()
    {
        Frame.Marks.Clear();
        PlayerControl me = PlayerControl.LocalPlayer;
        byte mine = me.PlayerId;
        float now = NocturneStyle.Now;
        var all = PlayerControl.AllPlayerControls;
        int count = all != null ? all.Count : 0;
        int alive = 0, total = 0;

        for (int i = 0; i < count; i++)
        {
            PlayerControl pc = all[i];
            if (pc == null || pc.Data == null)
                continue;

            total++;
            bool dead = pc.Data.IsDead;
            if (!dead)
                alive++;

            Vector2 w = pc.GetTruePosition();
            if (!Movers.TryGetValue(pc.PlayerId, out Mover m))
            {
                m = new Mover();
                Movers[pc.PlayerId] = m;
            }
            Follow(m, w, now);
            if (now - m.ColAt > 0.5f)
            {
                m.Col = PlayerColor(pc);
                m.ColAt = now;
            }

            Frame.Marks.Add(new RadarMark { Pos = w, Vel = dead ? Ui.Zero2 : m.Vel, Col = m.Col, Dead = dead, Me = pc.PlayerId == mine });
        }
        Frame.Alive = alive;
        Frame.Total = total;
    }

    private static void Follow(Mover m, Vector2 w, float now)
    {
        if (m.At < 0f)
        {
            m.Pos = w;
            m.At = now;
            return;
        }

        float dt = now - m.At;
        if (dt < 0.05f)
            return;

        float vx = (w.x - m.Pos.x) / dt, vy = (w.y - m.Pos.y) / dt;
        if (vx * vx + vy * vy > 900f)
        {
            vx = 0f;
            vy = 0f;
        }
        m.Vel = Ui.V(m.Vel.x + (vx - m.Vel.x) * 0.55f, m.Vel.y + (vy - m.Vel.y) * 0.55f);
        m.Pos = w;
        m.At = now;
    }

    private static void Bodies()
    {
        if (!NocturneConfig.RadarBodies.Value)
        {
            Frame.Bodies.Clear();
            _bodies = null;
            return;
        }

        DeadBody[] all = Utils.Bodies();
        if (ReferenceEquals(all, _bodies))
            return;

        _bodies = all;
        Frame.Bodies.Clear();
        if (all == null)
            return;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null)
                Frame.Bodies.Add(all[i].TruePosition);
    }

    private static Color PlayerColor(PlayerControl pc)
    {
        int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
        if (Palette.PlayerColors != null && id >= 0 && id < Palette.PlayerColors.Length)
        {
            Color32 c = Palette.PlayerColors[id];
            return Ui.C(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
        }
        return Ui.White;
    }

    private static Texture2D MapTexture(RadarLayout b, Color ac)
    {
        int w = Ui.RoundToInt(b.Box.m_Width), h = Ui.RoundToInt(b.Box.m_Height);
        int key = (b.Rad << 24) | ((int)(ac.r * 255f) << 16) | ((int)(ac.g * 255f) << 8) | (int)(ac.b * 255f);
        float now = NocturneStyle.Now;
        if (w != _wantW || h != _wantH || key != _wantKey)
        {
            _wantW = w;
            _wantH = h;
            _wantKey = key;
            _wantAt = now;
        }

        bool fresh = _texW == 0 || _texMap != _geoMap;
        bool stale = w != _texW || h != _texH || key != _texKey;
        if (fresh || (stale && !_sizing && now - _wantAt > 0.2f))
        {
            _tex = RadarMap.Bake(_tex, _geo, w, h, ac, b.Rad);
            _texMap = _geoMap;
            _texKey = key;
            _texW = w;
            _texH = h;
        }
        return _tex;
    }
}
