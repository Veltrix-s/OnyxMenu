using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

public sealed class NocturneTracers : MonoBehaviour
{
    private GUIStyle _cd;
    private GUIStyle _cdShadow;
    private GUIStyle _tag;

    public void Update()
    {
        if (NocturneConfig.KillTimers.Value && ShipStatus.Instance != null && MeetingHud.Instance == null)
            Patches.KillTimers.Tick(Time.deltaTime);
    }

    internal void DrawGui()
    {
        if (Event.current.type != EventType.Repaint) return;
        bool tracers = NocturneConfig.Tracers.Value;
        bool timers = NocturneConfig.KillTimers.Value;
        bool esp = NocturneConfig.EspBoxes.Value;
        bool arrows = NocturneConfig.TaskArrows.Value;
        if (!tracers && !timers && !esp && !arrows) return;
        if (MeetingHud.Instance != null) return;
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        Camera cam = Camera.main;
        if (me == null || cam == null) return;

        Vector2 from = ToScreen(cam, me.GetTruePosition());

        if (arrows && ShipStatus.Instance != null) DrawTaskArrows(cam, me, from);

        if (tracers)
        {
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
                Line(from, ToScreen(cam, p.GetTruePosition()), EspColor(p, false), 2.5f);
            }

            if (NocturneConfig.TracerBodies.Value && ShipStatus.Instance != null)
            {
                Color deadCol = SideOn ? SideCol(NocturneConfig.EspDeadColor.Value) : new Color(1f, 0.84f, 0.2f, 0.9f);
                DeadBody[] bodies = Bodies();
                for (int i = 0; i < bodies.Length; i++)
                {
                    DeadBody d = bodies[i];
                    if (d != null) Line(from, ToScreen(cam, d.transform.position), deadCol, 2.5f);
                }
            }
        }

        if (timers && ShipStatus.Instance != null)
        {
            if (_cd == null) BuildTimerStyles();
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || !Patches.KillTimers.Killer(p)) continue;
                if (Patches.KillTimers.TryGet(p.PlayerId, out float sec))
                    DrawTimer(ToScreen(cam, p.GetTruePosition()), sec);
            }
        }

        if (esp)
        {
            bool names = NocturneConfig.EspNames.Value;
            bool seeDead = NocturneConfig.EspDead.Value;
            Vector2 mePos = me.GetTruePosition();
            float ppu = (ToScreen(cam, mePos) - ToScreen(cam, mePos + Vector2.up)).magnitude;
            if (ppu < 4f) ppu = 42f;
            float hw = ppu * 0.42f, hh = ppu * 0.62f;
            float margin = hh + 60f, sw = Screen.width, sh = Screen.height;

            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || p == me || p.Data == null || p.Data.Disconnected) continue;
                bool dead = p.Data.IsDead;
                if (dead && !seeDead) continue;

                Vector2 w = p.GetTruePosition();
                Vector2 sp = ToScreen(cam, w + Vector2.up * 0.45f);
                if (sp.x < -margin || sp.x > sw + margin || sp.y < -margin || sp.y > sh + margin) continue;

                Color col = EspColor(p, dead);
                Box(sp, hw, hh, col);

                if (names)
                {
                    float dist = Vector2.Distance(w, mePos);
                    string nm = p.Data.PlayerName;
                    if (string.IsNullOrEmpty(nm)) nm = "?";
                    Tag(new Vector2(sp.x, sp.y - hh - 12f), nm + "  " + Mathf.RoundToInt(dist) + "m", col);
                }
            }
        }
    }

    private readonly List<Vector2> _taskTargets = new List<Vector2>(16);
    private float _taskAt = -99f;

    private void DrawTaskArrows(Camera cam, PlayerControl me, Vector2 from)
    {
        if (Time.unscaledTime - _taskAt > 0.5f) { _taskAt = Time.unscaledTime; RebuildTaskTargets(me); }
        if (_taskTargets.Count == 0) return;

        Color col = new Color(0.42f, 0.92f, 0.55f, 0.9f);
        Vector2 mePos = me.GetTruePosition();
        for (int i = 0; i < _taskTargets.Count; i++)
        {
            Vector2 d = ToScreen(cam, _taskTargets[i]) - from;
            if (d.sqrMagnitude < 4f) continue;
            d.Normalize();
            Vector2 tip = from + d * 92f;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Arrow(tip, ang, 15f, col);
            Tag(tip + d * 22f, Mathf.RoundToInt(Vector2.Distance(_taskTargets[i], mePos)) + "m", col);
        }
    }

    private void RebuildTaskTargets(PlayerControl me)
    {
        _taskTargets.Clear();
        try
        {
            if (me.myTasks == null) return;
            for (int i = 0; i < me.myTasks.Count; i++)
            {
                PlayerTask pt = me.myTasks[i];
                if (pt == null || pt.IsComplete) continue;
                NormalPlayerTask n = pt.TryCast<NormalPlayerTask>();
                if (n == null || n.Locations == null || n.Locations.Count == 0) continue;
                int step = Mathf.Clamp(n.taskStep, 0, n.Locations.Count - 1);
                _taskTargets.Add(n.Locations[step]);
            }
        }
        catch { }
    }

    private void Arrow(Vector2 tip, float angDeg, float len, Color col)
    {
        float a = angDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Line(tip - dir * len, tip, col, 3.5f);
        float bl = (angDeg + 150f) * Mathf.Deg2Rad, br = (angDeg - 150f) * Mathf.Deg2Rad;
        Line(tip, tip + new Vector2(Mathf.Cos(bl), Mathf.Sin(bl)) * len * 0.7f, col, 3.5f);
        Line(tip, tip + new Vector2(Mathf.Cos(br), Mathf.Sin(br)) * len * 0.7f, col, 3.5f);
    }

    private static void Box(Vector2 c, float hw, float hh, Color col)
    {
        float t = 2f;
        float x = c.x - hw, y = c.y - hh, w = hw * 2f, h = hh * 2f;
        float br = Mathf.Min(hw, hh) * 0.6f;
        Color prev = GUI.color;
        GUI.color = new Color(col.r, col.g, col.b, col.a * prev.a);
        NocturneStyle.FillRaw(new Rect(x, y, br, t));
        NocturneStyle.FillRaw(new Rect(x + w - br, y, br, t));
        NocturneStyle.FillRaw(new Rect(x, y + h - t, br, t));
        NocturneStyle.FillRaw(new Rect(x + w - br, y + h - t, br, t));
        NocturneStyle.FillRaw(new Rect(x, y, t, br));
        NocturneStyle.FillRaw(new Rect(x, y + h - br, t, br));
        NocturneStyle.FillRaw(new Rect(x + w - t, y, t, br));
        NocturneStyle.FillRaw(new Rect(x + w - t, y + h - br, t, br));
        GUI.color = prev;
    }

    private void Tag(Vector2 sp, string txt, Color col)
    {
        if (_tag == null)
            _tag = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var r = new Rect(sp.x - 90f, sp.y - 8f, 180f, 16f);
        _tag.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), txt, _tag);
        _tag.normal.textColor = col;
        GUI.Label(r, txt, _tag);
    }

    private void DrawTimer(Vector2 sp, float sec)
    {
        bool ready = sec <= 0.05f;
        string txt = ready ? "ГОТОВ" : sec.ToString("0.0");
        var r = new Rect(sp.x - 42f, sp.y - 66f, 84f, 20f);

        _cdShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), txt, _cdShadow);
        _cd.normal.textColor = ready ? new Color(0.92f, 0.32f, 0.36f) : new Color(1f, 0.86f, 0.42f);
        GUI.Label(r, txt, _cd);
    }

    private void BuildTimerStyles()
    {
        _cd = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        _cdShadow = new GUIStyle(_cd);
    }

    private static DeadBody[] _bodies;
    private static float _bodiesAt = -99f;

    private static DeadBody[] Bodies()
    {
        if (_bodies == null || Time.unscaledTime - _bodiesAt > 0.5f)
        {
            _bodiesAt = Time.unscaledTime;
            _bodies = Object.FindObjectsOfType<DeadBody>();
        }
        return _bodies;
    }

    private static Vector2 ToScreen(Camera cam, Vector2 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    private static Color BodyColor(PlayerControl p)
    {
        try
        {
            int id = p.CurrentOutfit.ColorId;
            if (id >= 0 && id < Palette.PlayerColors.Length)
            {
                Color c = Palette.PlayerColors[id];
                c.a = 0.9f;
                return c;
            }
        }
        catch { }
        return new Color(1f, 1f, 1f, 0.85f);
    }

    private static bool SideOn => NocturneConfig.EspSideColors != null && NocturneConfig.EspSideColors.Value;

    internal static readonly Color[] SideCols =
    {
        new Color(1f, 1f, 1f, 0.9f),
        new Color(1f, 0.24f, 0.24f, 0.9f),
        new Color(1f, 0.84f, 0.2f, 0.9f),
        new Color(0.34f, 0.9f, 0.44f, 0.9f),
        new Color(0.34f, 0.85f, 0.95f, 0.9f),
        new Color(0.4f, 0.6f, 1f, 0.9f),
        new Color(1f, 0.6f, 0.2f, 0.9f),
        new Color(0.72f, 0.46f, 1f, 0.9f),
        new Color(1f, 0.5f, 0.72f, 0.9f),
        new Color(0.1f, 0.1f, 0.1f, 0.95f),
    };
    private static readonly string[] SideRu = { "Белый", "Красный", "Жёлтый", "Зелёный", "Голубой", "Синий", "Оранж.", "Фиолет.", "Розовый", "Чёрный" };
    private static readonly string[] SideEn = { "White", "Red", "Yellow", "Green", "Cyan", "Blue", "Orange", "Purple", "Pink", "Black" };

    internal static int SideCount => SideCols.Length;
    internal static Color SideCol(int i) => SideCols[Mathf.Clamp(i, 0, SideCols.Length - 1)];
    internal static string SideName(int i) => (NocturneText.IsRussian ? SideRu : SideEn)[Mathf.Clamp(i, 0, SideCols.Length - 1)];

    private static Color EspColor(PlayerControl p, bool dead)
    {
        if (SideOn)
        {
            if (dead) return SideCol(NocturneConfig.EspDeadColor.Value);
            bool imp = false;
            try { imp = p != null && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor; } catch { }
            return SideCol(imp ? NocturneConfig.EspImpColor.Value : NocturneConfig.EspCrewColor.Value);
        }
        Color b = BodyColor(p);
        if (dead) b.a *= 0.5f;
        return b;
    }

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 2f) return;

        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, a);
        NocturneStyle.Fill(new Rect(a.x, a.y - w * 0.5f, len, w), col);
        GUI.matrix = m;
    }
}
