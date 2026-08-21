using System.Collections.Generic;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturnePet
{
    private const float Speed = 5f;
    private const float RpcDelay = 0.20f;
    private const float AnimDelay = 0.55f;
    private const float PaintGap = 0.35f;
    private const int PaintMax = 400;

    private static Vector2 _hand;
    private static float _elapsed;
    private static float _anim;
    private static byte _target = 255;
    private static bool _drag;

    private static readonly List<Vector2> _pts = new List<Vector2>();
    private static int _pi;

    private static readonly List<Collider2D> _roomAreas = new List<Collider2D>();
    private static readonly List<string> _roomNames = new List<string>();
    private static float _roomsAt;
    private static int _room;

    internal static bool On { get; private set; }
    internal static bool Manual { get; private set; }
    internal static bool Paint { get; private set; }
    internal static Vector2 Joy;

    internal static int PaintCount => _pts.Count;

    internal static bool IsTarget(byte pid) => On && !Manual && !Paint && _target == pid;

    internal static string TogglePaint()
    {
        if (!HasPet(PlayerControl.LocalPlayer)) return NocturneText.T("Нет пета.", "No pet.");
        if (On && Paint) { Stop(); return NocturneText.T("Роспись: выкл", "Paint: off"); }
        Paint = true;
        Manual = false;
        On = true;
        _pi = 0;
        _anim = 0f;
        return NocturneText.T("Роспись: рисуй мышью при закрытом меню", "Paint: draw with mouse while menu is closed");
    }

    internal static string ClearPaint()
    {
        _pts.Clear();
        _pi = 0;
        return NocturneText.T("Точки очищены.", "Points cleared.");
    }

    private static void RefreshRooms()
    {
        _roomAreas.Clear();
        _roomNames.Clear();
        try
        {
            ShipStatus ss = ShipStatus.Instance;
            if (ss == null) return;
            foreach (var r in ss.AllRooms)
            {
                if (r == null || r.roomArea == null) continue;
                _roomAreas.Add(r.roomArea);
                string n;
                try { n = TranslationController.Instance.GetString(r.RoomId); } catch { n = r.RoomId.ToString(); }
                _roomNames.Add(n);
            }
        }
        catch { _roomAreas.Clear(); _roomNames.Clear(); }
    }

    private static void EnsureRooms()
    {
        try
        {
            if (ShipStatus.Instance == null)
            {
                if (_roomNames.Count > 0) { _roomAreas.Clear(); _roomNames.Clear(); }
                return;
            }
            if (_roomNames.Count > 0 && Time.unscaledTime - _roomsAt < 1f) return;
            _roomsAt = Time.unscaledTime;
            RefreshRooms();
        }
        catch { }
    }

    internal static string RoomName()
    {
        EnsureRooms();
        if (_roomNames.Count == 0) return "-";
        _room = Mathf.Clamp(_room, 0, _roomNames.Count - 1);
        return _roomNames[_room];
    }

    internal static void RoomStep(int d)
    {
        EnsureRooms();
        int n = _roomNames.Count;
        if (n == 0) return;
        _room = ((_room + d) % n + n) % n;
    }

    internal static string FillRoom()
    {
        if (!HasPet(PlayerControl.LocalPlayer)) return NocturneText.T("Нет пета.", "No pet.");
        RefreshRooms();
        if (_roomAreas.Count == 0) return NocturneText.T("Нет комнат (не в матче?).", "No rooms (not in match?).");
        _room = Mathf.Clamp(_room, 0, _roomAreas.Count - 1);
        Collider2D area = _roomAreas[_room];
        if (area == null) return NocturneText.T("Нет комнаты.", "No room.");

        _pts.Clear();
        _pi = 0;
        try
        {
            Bounds bb = area.bounds;
            for (float px = bb.min.x; px <= bb.max.x && _pts.Count < PaintMax; px += 0.5f)
                for (float py = bb.min.y; py <= bb.max.y && _pts.Count < PaintMax; py += 0.5f)
                {
                    Vector2 p = new Vector2(px, py);
                    bool inside;
                    try { inside = area.OverlapPoint(p); } catch { inside = true; }
                    if (inside) _pts.Add(p);
                }
        }
        catch { }

        if (_pts.Count == 0) return NocturneText.T("Пусто.", "Empty.");
        Paint = true;
        Manual = false;
        On = true;
        _anim = 0f;
        return NocturneText.T("Заливка: ", "Filling: ") + _roomNames[_room] + $" ({_pts.Count})";
    }

    internal static string Grab(PlayerControl pc)
    {
        if (pc == null || pc.Data == null) return NocturneText.T("Нет цели.", "No target.");
        if (!HasPet(PlayerControl.LocalPlayer)) return NocturneText.T("Нет пета.", "No pet.");
        _target = pc.PlayerId;
        Manual = false;
        On = true;
        _anim = 0f;
        return NocturneText.T("Глажу: ", "Petting: ") + NocturneNameColor.Strip(pc.Data.PlayerName);
    }

    internal static string ToggleManual()
    {
        if (!HasPet(PlayerControl.LocalPlayer)) return NocturneText.T("Нет пета.", "No pet.");
        if (On && Manual) { Stop(); return NocturneText.T("Ручное: выкл", "Manual: off"); }
        Manual = true;
        On = true;
        _hand = Vector2.zero;
        _anim = 0f;
        return NocturneText.T("Ручное: вкл", "Manual: on");
    }

    internal static string Stop2()
    {
        Stop();
        return NocturneText.T("Стоп.", "Stopped.");
    }

    internal static void Stop()
    {
        On = false;
        Manual = false;
        Paint = false;
        _hand = Vector2.zero;
        _target = 255;
        Joy = Vector2.zero;
        _anim = 0f;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.cosmetics == null) return;
        me.moveable = true;
        if (me.MyPhysics != null && me.MyPhysics.body != null) me.MyPhysics.body.velocity = Vector2.zero;
        try { if (me.cosmetics.PettingHand != null) me.cosmetics.PettingHand.StopPetting(); } catch { }
        try { if (me.cosmetics.CurrentPet != null) me.cosmetics.CurrentPet.SetGettingPet(false, Vector2.zero); } catch { }
        try { if (me.NetTransform != null && MeetingHud.Instance == null) me.NetTransform.RpcSnapTo(me.GetTruePosition()); } catch { }
    }

    private static bool HasPet(PlayerControl me)
    {
        try { return me != null && me.cosmetics != null && me.cosmetics.CurrentPet != null; }
        catch { return false; }
    }

    internal static void Tick()
    {
        if (!On) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (!HasPet(me) || me.MyPhysics == null) { Stop(); return; }
        if (me.Data == null || me.Data.IsDead) { Stop(); return; }

        Vector2 petPos;
        if (Paint)
        {
            me.moveable = true;
            RecordStroke();
            if (_pts.Count == 0) { try { me.cosmetics.CurrentPet.SetGettingPet(false, Vector2.zero); } catch { } return; }
            if (_pi >= _pts.Count) _pi = 0;
            petPos = _pts[_pi];
        }
        else if (Manual)
        {
            me.moveable = true;
            _hand += Joy * Speed * Time.deltaTime;
            petPos = (Vector2)me.transform.position + _hand;
        }
        else
        {
            PlayerControl t = ById(_target);
            if (t == null || t.Data == null || t.Data.Disconnected) { Stop(); return; }
            me.moveable = false;
            if (me.MyPhysics.body != null) me.MyPhysics.body.velocity = Vector2.zero;
            petPos = t.transform.position;
            try { petPos.y -= me.cosmetics.currentPet.yOffset * 2f; } catch { }
        }

        try { me.cosmetics.CurrentPet.SetGettingPet(true, petPos); } catch { }

        _anim += Time.deltaTime;
        if (_anim >= AnimDelay)
        {
            _anim = 0f;
            try { if (me.cosmetics.PettingHand != null) me.cosmetics.PettingHand.StartPet(me.cosmetics.currentPet); } catch { }
        }

        _elapsed += Time.deltaTime;
        if (_elapsed < RpcDelay) return;
        _elapsed = 0f;
        if (Paint) _pi++;

        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            MessageWriter w = net.StartRpcImmediately(((InnerNetObject)me.MyPhysics).NetId, (byte)49, SendOption.Reliable, -1);
            NetHelpers.WriteVector2(me.GetTruePosition(), w);
            NetHelpers.WriteVector2(petPos, w);
            net.FinishRpcImmediately(w);
        }
        catch { }
    }

    private static void RecordStroke()
    {
        if (NocturneMenu.Opened || _pts.Count >= PaintMax) return;
        if (!Input.GetMouseButton(0) || Camera.main == null) return;

        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = new Vector2(w.x, w.y);
        if (_pts.Count == 0 || Vector2.Distance(_pts[_pts.Count - 1], p) >= PaintGap)
            _pts.Add(p);
    }

    private static PlayerControl ById(byte id)
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                if (pc != null && pc.PlayerId == id) return pc;
        }
        catch { }
        return null;
    }

    internal static void DrawJoystick(Event e)
    {
        if (!On || !Manual || e == null) return;

        const float r = 45f, kr = 16f;
        var center = new Vector2(75f, Screen.height - 75f);
        Vector2 mp = e.mousePosition;

        if (e.type == EventType.MouseDown && Vector2.Distance(mp, center) <= r + 15f) _drag = true;
        else if (e.type == EventType.MouseUp) _drag = false;

        Vector2 knob = center;
        if (_drag)
        {
            Vector2 d = Vector2.ClampMagnitude(mp - center, r);
            knob = center + d;
            Joy = new Vector2(d.x / r, -d.y / r);
        }
        else Joy = Vector2.zero;

        NocturneStyle.FillRounded(new Rect(center.x - r, center.y - r, r * 2f, r * 2f), new Color(0.05f, 0.05f, 0.07f, 0.55f), (int)r);
        NocturneStyle.StrokeRounded(new Rect(center.x - r, center.y - r, r * 2f, r * 2f), new Color(1f, 1f, 1f, 0.12f), (int)r, 1);
        Color acc = NocturneStyle.Current.Accent;
        NocturneStyle.FillRounded(new Rect(knob.x - kr, knob.y - kr, kr * 2f, kr * 2f), acc, (int)kr);

        if (GUI.Button(new Rect(center.x - 30f, center.y + r + 6f, 60f, 20f), NocturneText.T("ЦЕНТР", "CENTER")))
            _hand = Vector2.zero;
    }

    internal static void DrawPaint(Event e)
    {
        if (!On || !Paint || e == null || e.type != EventType.Repaint || NocturneMenu.Opened) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Color acc = NocturneStyle.Current.Accent;
        for (int i = 0; i < _pts.Count; i++)
        {
            Vector3 s = cam.WorldToScreenPoint(_pts[i]);
            if (s.z <= 0f) continue;
            NocturneStyle.FillRounded(new Rect(s.x - 4f, Screen.height - s.y - 4f, 8f, 8f), acc, 4);
        }

        Vector2 m = e.mousePosition;
        Rect box = new Rect(m.x - 18f, m.y - 18f, 36f, 36f);
        NocturneStyle.FillRounded(box, new Color(acc.r, acc.g, acc.b, 0.15f), 6);
        NocturneStyle.StrokeRounded(box, acc, 6, 2);
    }
}
