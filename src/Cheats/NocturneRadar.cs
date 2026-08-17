using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneRadar : MonoBehaviour
{
    private const float W = 236f, H = 212f, Pad = 2f, Head = 22f;

    private static GUIStyle _title;
    private static Texture2D _dot;
    private static readonly Dictionary<byte, List<(Vector2 w, float t)>> _trail = new Dictionary<byte, List<(Vector2, float)>>();

    private static Vector2 _min, _max;
    private static int _boundsMap = -999;

    private static float _sc = 1f, _al = 0.93f;
    private static bool _drag;
    private static Vector2 _dragOff;
    private static float _rx, _ry;

    internal void DrawGui()
    {
        if (!NocturneConfig.Radar.Value) return;
        if (ShipStatus.Instance == null || PlayerControl.LocalPlayer == null) return;
        if (MeetingHud.Instance != null || ExileController.Instance != null) return;
        if (!Bounds()) return;

        float userSc = Mathf.Clamp((NocturneConfig.RadarSize.Value) / 100f, 0.6f, 1.8f);
        _sc = userSc * Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.2f);
        _al = Mathf.Clamp((NocturneConfig.RadarOpacity.Value) / 100f, 0.3f, 1f);
        float w = W * _sc, h = H * _sc;

        if (!_drag) { _rx = NocturneConfig.RadarX.Value; _ry = NocturneConfig.RadarY.Value; }
        _rx = Mathf.Clamp(_rx, 0f, Mathf.Max(0f, Screen.width - w));
        _ry = Mathf.Clamp(_ry, 0f, Mathf.Max(0f, Screen.height - h));

        var box = new Rect(_rx, _ry, w, h);
        float pad = Pad * _sc, head = Head * _sc;
        var inner = new Rect(box.x + pad, box.y + head + 2f * _sc, box.width - 2f * pad, box.height - head - 2f * _sc - pad);
        var lockRect = new Rect(box.xMax - head + 1f * _sc, box.y + 3f * _sc, head - 6f * _sc, head - 6f * _sc);
        bool locked = NocturneConfig.RadarLocked.Value;

        Event e = Event.current;
        if (e != null)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && lockRect.Contains(e.mousePosition))
            {
                NocturneConfig.RadarLocked.Value = !locked;
                _drag = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && CanDoors() && TryClickDoor(inner, e.mousePosition))
            {
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && !locked && box.Contains(e.mousePosition))
            {
                _drag = true;
                _dragOff = new Vector2(e.mousePosition.x - box.x, e.mousePosition.y - box.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _drag)
            {
                _rx = Mathf.Clamp(e.mousePosition.x - _dragOff.x, 0f, Mathf.Max(0f, Screen.width - w));
                _ry = Mathf.Clamp(e.mousePosition.y - _dragOff.y, 0f, Mathf.Max(0f, Screen.height - h));
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _drag)
            {
                _drag = false;
                NocturneConfig.RadarX.Value = _rx;
                NocturneConfig.RadarY.Value = _ry;
                e.Use();
            }
            else if (NocturneConfig.RadarTeleport.Value && e.type == EventType.MouseDown && e.button == 1)
            {
                Teleport(inner);
            }
        }
        if (e == null || e.type != EventType.Repaint) return;

        EnsureStyle();
        NocturnePalette p = NocturneStyle.Current;

        NocturneStyle.FillRounded(box, A(p.Window, 0.93f), 12);
        NocturneStyle.FillRounded(new Rect(box.x, box.y, box.width, head + 6f * _sc), A(p.Accent, 0.10f), 12);
        NocturneStyle.StrokeRounded(box, A(p.Accent, 0.55f), 12, 1);
        NocturneStyle.Fill(new Rect(box.x, box.y + head + 3f * _sc, box.width, 1f), A(p.Accent, 0.35f));
        _title.fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * _sc));
        _title.normal.textColor = A(p.Accent, 1f);
        GUI.Label(new Rect(box.x, box.y + 3f * _sc, box.width, head), "◎  " + MapName(), _title);

        bool lockHover = lockRect.Contains(e.mousePosition);
        Color lockCol = locked ? new Color(0.96f, 0.32f, 0.32f, 1f)
            : (lockHover ? new Color(0.75f, 0.80f, 0.86f, 1f) : new Color(0.5f, 0.55f, 0.62f, 1f));
        NocturneStyle.FillRounded(lockRect, A(new Color(0.10f, 0.10f, 0.13f), locked ? 0.9f : 0.55f), 4);
        NocturneStyle.StrokeRounded(lockRect, A(lockCol, 0.85f), 4, 1);
        DrawLock(lockRect, lockCol);

        NocturneStyle.FillRounded(inner, A(new Color(0.24f, 0.26f, 0.30f), 0.55f), 6);

        NocturneNav.Graph g = NocturneNav.Current();
        if (g != null) { try { Skeleton(g, inner); } catch { } }
        Players(inner);
        if (NocturneConfig.RadarBodies.Value) Bodies(inner);
        if (CanDoors()) DrawDoors(inner);
    }

    private static readonly List<(SystemTypes room, Vector2 center)> _doorRooms = new List<(SystemTypes, Vector2)>();
    private static int _doorMap = -999;

    private static bool CanDoors()
    {
        if (!NocturneConfig.RadarDoors.Value) return false;
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            return ShipStatus.Instance != null && MeetingHud.Instance == null
                && me != null && me.Data != null && me.Data.Role != null && me.Data.Role.IsImpostor;
        }
        catch { return false; }
    }

    private static List<(SystemTypes room, Vector2 center)> DoorRooms()
    {
        int map = NocturneNav.CurrentMapId();
        if (_doorMap != map) BuildDoorRooms(map);
        return _doorRooms;
    }

    private static void BuildDoorRooms(int map)
    {
        _doorMap = map;
        _doorRooms.Clear();
        ShipStatus s = ShipStatus.Instance;
        if (s == null || s.AllRooms == null) return;

        var withDoors = new HashSet<int>();
        try
        {
            if (s.AllDoors != null)
                for (int i = 0; i < s.AllDoors.Length; i++)
                {
                    OpenableDoor d = s.AllDoors[i];
                    if (d != null) withDoors.Add((int)d.Room);
                }
        }
        catch { }

        var rooms = s.AllRooms;
        for (int i = 0; i < rooms.Length; i++)
        {
            PlainShipRoom r = rooms[i];
            if (r == null || r.roomArea == null) continue;
            if (withDoors.Count > 0 && !withDoors.Contains((int)r.RoomId)) continue;
            Bounds b = r.roomArea.bounds;
            _doorRooms.Add((r.RoomId, new Vector2(b.center.x, b.center.y)));
        }
    }

    private static float _lastDoorClick = -99f;
    private static SystemTypes _lastDoorRoom = (SystemTypes)255;

    private static bool TryClickDoor(Rect r, Vector2 mp)
    {
        var list = DoorRooms();
        float sz = 15f * _sc;
        for (int i = 0; i < list.Count; i++)
        {
            Vector2 sp = Map(list[i].center, r);
            if (new Rect(sp.x - sz * 0.5f, sp.y - sz * 0.5f, sz, sz).Contains(mp))
            {
                SystemTypes room = list[i].room;
                float now = Time.unscaledTime;
                if (_lastDoorRoom == room && now - _lastDoorClick < 0.35f)
                {
                    NocturneDoors.TogglePin((int)room);
                    _lastDoorClick = -99f;
                }
                else
                {
                    NocturneDoors.CloseOne((int)room);
                    _lastDoorClick = now;
                    _lastDoorRoom = room;
                }
                return true;
            }
        }
        return false;
    }

    private static void DrawDoors(Rect r)
    {
        var list = DoorRooms();
        if (list.Count == 0) return;
        Event e = Event.current;
        Vector2 mp = e != null ? e.mousePosition : new Vector2(-999f, -999f);
        float sz = 15f * _sc;

        for (int i = 0; i < list.Count; i++)
        {
            Vector2 sp = Map(list[i].center, r);
            var rect = new Rect(sp.x - sz * 0.5f, sp.y - sz * 0.5f, sz, sz);
            bool hover = rect.Contains(mp);
            bool pinned = NocturneDoors.IsPinned((int)list[i].room);
            Color edge = pinned ? new Color(0.96f, 0.30f, 0.30f, 1f)
                : (hover ? new Color(1f, 0.55f, 0.42f, 1f) : new Color(0.96f, 0.74f, 0.34f, 1f));
            Color fillCol = pinned ? new Color(0.22f, 0.06f, 0.06f) : new Color(0.10f, 0.10f, 0.13f);

            NocturneStyle.FillRounded(rect, A(fillCol, hover ? 0.95f : 0.8f), 3);
            NocturneStyle.StrokeRounded(rect, A(edge, 1f), 3, 1);
            NocturneStyle.Fill(new Rect(sp.x - 0.7f * _sc, rect.y + 2.5f * _sc, 1.4f * _sc, rect.height - 5f * _sc), A(edge, 1f));
            float k = 1.4f * _sc;
            NocturneStyle.Fill(new Rect(sp.x + 1.6f * _sc, sp.y - k * 0.5f, k, k), A(edge, 1f));
        }
    }

    private static void DrawLock(Rect r, Color col)
    {
        float bw = r.width * 0.62f;
        float bx = r.center.x - bw * 0.5f;
        float bodyTop = r.y + r.height * 0.46f;
        float bodyH = r.yMax - 2f * _sc - bodyTop;
        if (bodyH < 1f) return;
        NocturneStyle.FillRounded(new Rect(bx, bodyTop, bw, bodyH), A(col, 1f), 2);

        float shW = bw * 0.72f;
        float shX = r.center.x - shW * 0.5f;
        float shTop = r.y + 2.5f * _sc;
        float th = Mathf.Max(1f, 1.2f * _sc);
        float shH = bodyTop - shTop;
        if (shH < 1f) return;
        NocturneStyle.Fill(new Rect(shX, shTop, th, shH), A(col, 1f));
        NocturneStyle.Fill(new Rect(shX + shW - th, shTop, th, shH), A(col, 1f));
        NocturneStyle.Fill(new Rect(shX, shTop, shW, th), A(col, 1f));
    }

    private static void EnsureStyle()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
    }

    private static string MapName()
    {
        switch (NocturneNav.CurrentMapId())
        {
            case 0: case 3: return "The Skeld";
            case 1: return "Mira HQ";
            case 2: return "Polus";
            case 4: return "Airship";
            case 5: return "Fungle";
            default: return NocturneText.T("Карта", "Map");
        }
    }

    private static bool Bounds()
    {
        int map = NocturneNav.CurrentMapId();
        if (map == _boundsMap && _min.x <= _max.x) return true;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        ShipStatus s = ShipStatus.Instance;
        if (s != null && s.AllRooms != null)
        {
            var rooms = s.AllRooms;
            for (int i = 0; i < rooms.Length; i++)
            {
                Collider2D c = rooms[i] != null ? rooms[i].roomArea : null;
                if (c == null) continue;
                Bounds b = c.bounds;
                Grow(ref min, ref max, new Vector2(b.min.x, b.min.y));
                Grow(ref min, ref max, new Vector2(b.max.x, b.max.y));
            }
        }

        if (min.x > max.x)
        {
            NocturneNav.Graph g = NocturneNav.Current();
            if (g != null && g.Pos.Length > 0) { foreach (Vector2 v in g.Pos) Grow(ref min, ref max, v); }
            else return false;
        }

        if (min.x > max.x) return false;
        Vector2 m = (max - min) * 0.015f + Vector2.one * 0.3f;
        _min = min - m; _max = max + m; _boundsMap = map;
        _trail.Clear();
        return true;
    }

    private static void Grow(ref Vector2 min, ref Vector2 max, Vector2 v)
    {
        if (v.x < min.x) min.x = v.x;
        if (v.y < min.y) min.y = v.y;
        if (v.x > max.x) max.x = v.x;
        if (v.y > max.y) max.y = v.y;
    }

    private static Vector2 Map(Vector2 w, Rect r)
    {
        float tx = (w.x - _min.x) / Mathf.Max(0.01f, _max.x - _min.x);
        float ty = (w.y - _min.y) / Mathf.Max(0.01f, _max.y - _min.y);
        return new Vector2(r.x + tx * r.width, r.y + (1f - ty) * r.height);
    }

    private static Vector2[] _skel;
    private static Color32[] _skelPx;
    private static Texture2D _skelTex;
    private static GUIStyle _skelStyle;
    private static int _skelMap = -999, _skelW, _skelH;
    private static float _skelAt;

    private static void Skeleton(NocturneNav.Graph g, Rect r)
    {
        int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, 1024);
        int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, 1024);
        int map = NocturneNav.CurrentMapId();

        bool dirty = _skelTex == null || map != _skelMap;
        bool resized = w != _skelW || h != _skelH;
        if (dirty || (resized && Time.unscaledTime - _skelAt > 0.1f))
        {
            BuildSkelTex(g, w, h);
            _skelMap = map; _skelW = w; _skelH = h; _skelAt = Time.unscaledTime;
        }
        if (_skelTex == null) return;

        if (_skelStyle == null) _skelStyle = new GUIStyle();
        _skelStyle.normal.background = _skelTex;
        Color prev = GUI.color;
        GUI.color = A(new Color(0.80f, 0.85f, 0.96f), 0.55f);
        GUI.Box(r, GUIContent.none, _skelStyle);
        GUI.color = prev;
    }

    private static void BuildSkelTex(NocturneNav.Graph g, int w, int h)
    {
        if (_skelTex == null || _skelTex.width != w || _skelTex.height != h)
        {
            if (_skelTex != null) Object.Destroy(_skelTex);
            _skelTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _skelPx = new Color32[w * h];
        }

        PaintMap(g, w, h);
        if (FitToContent(w, h)) PaintMap(g, w, h);

        _skelTex.SetPixels32(_skelPx);
        _skelTex.Apply();
    }

    private static void PaintMap(NocturneNav.Graph g, int w, int h)
    {
        System.Array.Clear(_skelPx, 0, _skelPx.Length);

        int n = g.Pos.Length;
        if (_skel == null || _skel.Length < n) _skel = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float tx = (g.Pos[i].x - _min.x) / Mathf.Max(0.01f, _max.x - _min.x);
            float ty = (g.Pos[i].y - _min.y) / Mathf.Max(0.01f, _max.y - _min.y);
            _skel[i] = new Vector2(tx * w, ty * h);
        }

        Color32 fillCol = new Color32(255, 255, 255, 120);
        Color32 edgeCol = new Color32(255, 255, 255, 255);
        Color32 corrCol = new Color32(255, 255, 255, 150);
        AddRoomFills(_skelPx, w, h, fillCol, edgeCol);

        int thick = Mathf.Max(2, Mathf.RoundToInt(2.4f * _sc));
        for (int i = 0; i < n; i++)
            foreach (int nb in g.Adj[i])
            {
                if (nb <= i || nb >= n) continue;
                Stamp(_skelPx, w, h, _skel[i], _skel[nb], thick, corrCol);
            }
    }

    private static bool FitToContent(int w, int h)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (_skelPx[row + x].a == 0) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX < minX || maxY < minY) return false;

        float mL = minX / (float)w, mR = 1f - (maxX + 1) / (float)w;
        float mB = minY / (float)h, mT = 1f - (maxY + 1) / (float)h;
        if (mL < 0.02f && mR < 0.02f && mB < 0.02f && mT < 0.02f) return false;

        float rx = Mathf.Max(0.01f, _max.x - _min.x), ry = Mathf.Max(0.01f, _max.y - _min.y);
        Vector2 nmin = new Vector2(_min.x + minX / (float)w * rx, _min.y + minY / (float)h * ry);
        Vector2 nmax = new Vector2(_min.x + (maxX + 1) / (float)w * rx, _min.y + (maxY + 1) / (float)h * ry);
        Vector2 pad = (nmax - nmin) * 0.008f;
        _min = nmin - pad; _max = nmax + pad;
        return true;
    }

    private static void Stamp(Color32[] px, int w, int h, Vector2 a, Vector2 b, int thick, Color32 col)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.5f) return;
        int steps = Mathf.CeilToInt(len);
        float sx = dx / steps, sy = dy / steps;
        int half = thick / 2;
        for (int s = 0; s <= steps; s++)
        {
            int cx = Mathf.RoundToInt(a.x + sx * s);
            int cy = Mathf.RoundToInt(a.y + sy * s);
            for (int oy = -half; oy <= half; oy++)
            {
                int y = cy + oy;
                if (y < 0 || y >= h) continue;
                int row = y * w;
                for (int ox = -half; ox <= half; ox++)
                {
                    int x = cx + ox;
                    if (x < 0 || x >= w) continue;
                    px[row + x] = col;
                }
            }
        }
    }

    private static readonly List<Vector2> _fpoly = new List<Vector2>(48);

    private static void AddRoomFills(Color32[] px, int w, int h, Color32 fill, Color32 edge)
    {
        ShipStatus s = ShipStatus.Instance;
        if (s == null || s.AllRooms == null) return;
        var rooms = s.AllRooms;
        for (int ri = 0; ri < rooms.Length; ri++)
        {
            PlainShipRoom room = rooms[ri];
            Collider2D c = room != null ? room.roomArea : null;
            if (c == null) continue;
            try { FillCollider(c, px, w, h, fill, edge); } catch { }
        }
    }

    private static void FillCollider(Collider2D c, Color32[] px, int w, int h, Color32 fill, Color32 edge)
    {
        Transform t = c.transform;

        PolygonCollider2D poly = c.TryCast<PolygonCollider2D>();
        if (poly != null)
        {
            Vector2 off = poly.offset;
            var pts = poly.points;
            if (pts == null || pts.Length < 3) return;
            _fpoly.Clear();
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 wp = t.TransformPoint(new Vector3(pts[i].x + off.x, pts[i].y + off.y, 0f));
                _fpoly.Add(WorldToPix(new Vector2(wp.x, wp.y), w, h));
            }
            FillPoly(px, w, h, _fpoly, fill);
            Outline(px, w, h, edge);
            return;
        }

        BoxCollider2D box = c.TryCast<BoxCollider2D>();
        if (box != null)
        {
            Vector2 hs = box.size * 0.5f;
            Vector2 off = box.offset;
            _fpoly.Clear();
            _fpoly.Add(WorldToPix(Corner(t, off.x - hs.x, off.y - hs.y), w, h));
            _fpoly.Add(WorldToPix(Corner(t, off.x + hs.x, off.y - hs.y), w, h));
            _fpoly.Add(WorldToPix(Corner(t, off.x + hs.x, off.y + hs.y), w, h));
            _fpoly.Add(WorldToPix(Corner(t, off.x - hs.x, off.y + hs.y), w, h));
            FillPoly(px, w, h, _fpoly, fill);
            Outline(px, w, h, edge);
        }
    }

    private static void Outline(Color32[] px, int w, int h, Color32 edge)
    {
        int et = Mathf.Max(1, Mathf.RoundToInt(1.3f * _sc));
        for (int i = 0; i < _fpoly.Count; i++)
            Stamp(px, w, h, _fpoly[i], _fpoly[(i + 1) % _fpoly.Count], et, edge);
    }

    private static Vector2 Corner(Transform t, float x, float y)
    {
        Vector3 wp = t.TransformPoint(new Vector3(x, y, 0f));
        return new Vector2(wp.x, wp.y);
    }

    private static Vector2 WorldToPix(Vector2 world, int w, int h)
    {
        float tx = (world.x - _min.x) / Mathf.Max(0.01f, _max.x - _min.x);
        float ty = (world.y - _min.y) / Mathf.Max(0.01f, _max.y - _min.y);
        return new Vector2(tx * w, ty * h);
    }

    private static readonly List<float> _fx = new List<float>(16);

    private static void FillPoly(Color32[] px, int w, int h, List<Vector2> poly, Color32 col)
    {
        int n = poly.Count;
        if (n < 3) return;

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++) { if (poly[i].y < minY) minY = poly[i].y; if (poly[i].y > maxY) maxY = poly[i].y; }
        int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, h - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, h - 1);

        for (int y = y0; y <= y1; y++)
        {
            float yc = y + 0.5f;
            _fx.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                if ((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc))
                    _fx.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
            }
            if (_fx.Count < 2) continue;
            _fx.Sort();
            int row = y * w;
            for (int i = 0; i + 1 < _fx.Count; i += 2)
            {
                int xa = Mathf.Clamp(Mathf.RoundToInt(_fx[i]), 0, w - 1);
                int xb = Mathf.Clamp(Mathf.RoundToInt(_fx[i + 1]), 0, w - 1);
                for (int x = xa; x <= xb; x++) px[row + x] = col;
            }
        }
    }

    private static void Players(Rect r)
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null) continue;
                bool dead = pc.Data.IsDead;
                Vector2 w = pc.GetTruePosition();
                Vector2 sp = Map(w, r);
                Color col = PlayerColor(pc);

                if (!dead) { Track(pc.PlayerId, w); Trail(pc.PlayerId, r, col); }
                else col.a = 0.4f;

                if (pc == me) DrawDot(sp, (15f + pulse * 5f) * _sc, A(NocturneStyle.Current.Accent, 0.22f));
                float d = (pc == me ? 11f : 8.5f) * _sc;
                DrawDot(sp, d + 3f * _sc, A(Color.black, 0.6f));
                DrawDot(sp, d, A(col, col.a));
            }
        }
        catch { }
    }

    private static void Track(byte id, Vector2 w)
    {
        if (!_trail.TryGetValue(id, out var list)) { list = new List<(Vector2, float)>(); _trail[id] = list; }
        float now = Time.unscaledTime;
        if (list.Count == 0 || now - list[list.Count - 1].t > 0.06f) list.Add((w, now));
        while (list.Count > 0 && now - list[0].t > 0.5f) list.RemoveAt(0);
    }

    private static void Trail(byte id, Rect r, Color col)
    {
        if (!_trail.TryGetValue(id, out var list) || list.Count < 2) return;
        for (int i = 1; i < list.Count; i++)
            Line(Map(list[i - 1].w, r), Map(list[i].w, r), A(col, (float)i / list.Count * 0.45f * col.a), 2f * _sc);
    }

    private static Texture2D DotTex()
    {
        if (_dot != null) return _dot;
        int s = 32;
        _dot = new Texture2D(s, s, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        float rad = s / 2f - 1f, c = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((rad - dist) / 1.5f) * 255f));
            }
        _dot.SetPixels32(px);
        _dot.Apply();
        return _dot;
    }

    private static GUIStyle _dotStyle;

    private static void DrawDot(Vector2 c, float size, Color col)
    {
        if (_dotStyle == null) _dotStyle = new GUIStyle();
        _dotStyle.normal.background = DotTex();
        Color prev = GUI.color;
        GUI.color = new Color(col.r, col.g, col.b, col.a * prev.a);
        GUI.Box(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), GUIContent.none, _dotStyle);
        GUI.color = prev;
    }

    private static DeadBody[] _bodies;
    private static float _bodiesAt = -99f;

    private static void Bodies(Rect r)
    {
        try
        {
            if (_bodies == null || Time.unscaledTime - _bodiesAt > 0.5f)
            {
                _bodiesAt = Time.unscaledTime;
                _bodies = Object.FindObjectsOfType<DeadBody>();
            }
            if (_bodies == null) return;
            for (int i = 0; i < _bodies.Length; i++)
            {
                DeadBody b = _bodies[i];
                if (b == null) continue;
                Vector2 sp = Map(b.TruePosition, r);
                DrawDot(sp, 11f * _sc, A(new Color(0.6f, 0.1f, 0.1f, 1f), 0.85f));
                DrawDot(sp, 9f * _sc, A(new Color(0.9f, 0.25f, 0.25f, 1f), 0.95f));
                float k = 3f * _sc;
                Line(new Vector2(sp.x - k, sp.y - k), new Vector2(sp.x + k, sp.y + k), A(Color.white, 1f), 1.6f * _sc);
                Line(new Vector2(sp.x - k, sp.y + k), new Vector2(sp.x + k, sp.y - k), A(Color.white, 1f), 1.6f * _sc);
            }
        }
        catch { }
    }

    private static void Teleport(Rect r)
    {
        Event e = Event.current;
        if (!r.Contains(e.mousePosition)) return;
        float tx = (e.mousePosition.x - r.x) / r.width;
        float ty = 1f - (e.mousePosition.y - r.y) / r.height;
        Vector2 world = new Vector2(_min.x + tx * (_max.x - _min.x), _min.y + ty * (_max.y - _min.y));
        try { PlayerControl.LocalPlayer.NetTransform.SnapTo(world); } catch { }
        e.Use();
    }

    private static Color PlayerColor(PlayerControl pc)
    {
        try
        {
            int id = pc.Data.DefaultOutfit != null ? pc.Data.DefaultOutfit.ColorId : 0;
            if (Palette.PlayerColors != null && id >= 0 && id < Palette.PlayerColors.Length)
            {
                Color32 c = Palette.PlayerColors[id];
                return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
            }
        }
        catch { }
        return Color.white;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a * _al);

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        NocturneStyle.Fill(new Rect(a.x, a.y - w * 0.5f, len, w), col);
        GUI.matrix = m;
    }
}
