using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

public sealed class NocturneTracers : MonoBehaviour
{
    private GUIStyle _cd;
    private GUIStyle _cdShadow;
    private GUIStyle _tag;

    private struct TagLine
    {
        public int Dist;
        public string Name;
        public string Text;
        public float NameAt;
    }

    private readonly Dictionary<byte, TagLine> _tags = new Dictionary<byte, TagLine>();

    internal void DrawGui()
    {
        if (!NocturneStyle.Painting) return;
        bool tracers = NocturneConfig.Tracers.Value;
        bool timers = NocturneConfig.KillTimers.Value;
        bool esp = NocturneConfig.EspBoxes.Value
            && (NocturneConfig.EspSeeImps.Value || NocturneConfig.EspSeeAngels.Value || NocturneConfig.EspSeeCrew.Value);
        bool arrows = NocturneConfig.TaskArrows.Value;
        if (_lit.Count > 0 && (!arrows || ShipStatus.Instance == null))
        {
            if (ShipStatus.Instance != null)
                ReleaseArrows();
            else
                _lit.Clear();
        }
        if (!tracers && !timers && !esp && !arrows)
            return;
        if (MeetingHud.Instance != null || ExileController.Instance != null) return;
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        Camera cam = Camera.main;
        if (me == null || cam == null)
            return;

        _shield = Patches.MouseShield.Count;
        Vector2 from = tracers || arrows ? ToScreen(cam, me.GetTruePosition()) : default;

        if (arrows && ShipStatus.Instance != null)
            TaskArrows(cam, me, from);

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
                Color deadCol = SideOn ? SideCol(NocturneConfig.EspDeadColor.Value) : Ui.C(1f, 0.84f, 0.2f, 0.9f);
                DeadBody[] bodies = Utils.Bodies();
                for (int i = 0; i < bodies.Length; i++)
                {
                    DeadBody d = bodies[i];
                    if (d != null)
                        Line(from, ToScreen(cam, d.transform.position), deadCol, 2.5f);
                }
            }
        }

        if (timers && ShipStatus.Instance != null)
        {
            if (_cd == null)
                BuildTimerStyles();
            var it = PlayerControl.AllPlayerControls.GetEnumerator();
            while (it.MoveNext())
            {
                PlayerControl p = it.Current;
                if (p == null || !Patches.KillTimers.TryGet(p.PlayerId, out float sec)) continue;
                if (p == me || !Patches.KillTimers.Killer(p)) continue;
                DrawTimer(ToScreen(cam, p.GetTruePosition()), sec);
            }
        }

        if (esp)
        {
            bool names = NocturneConfig.EspNames.Value;
            bool seeDead = NocturneConfig.EspDead.Value;
            Vector2 mePos = me.GetTruePosition();
            float ppu = (ToScreen(cam, mePos) - ToScreen(cam, mePos + Ui.Up2)).magnitude;
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
                if (!SideShown(p)) continue;

                Vector2 w = p.GetTruePosition();
                Vector2 sp = ToScreen(cam, w + Ui.Up2 * 0.45f);
                if (sp.x < -margin || sp.x > sw + margin || sp.y < -margin || sp.y > sh + margin) continue;

                Color col = EspColor(p, dead);
                Box(sp, hw, hh, col);

                if (names)
                    Tag(Ui.V(sp.x, sp.y - hh - 12f), TagText(p, Ui.RoundToInt(Vector2.Distance(w, mePos))), col);
            }
        }
    }

    private sealed class Lit
    {
        internal NormalPlayerTask Task;
        internal ArrowBehaviour Arrow;
        internal Color Was;
        internal Vector2 Pos;
        internal bool Cloned;
    }

    private readonly Dictionary<int, Lit> _lit = new Dictionary<int, Lit>(16);
    private readonly List<int> _gone = new List<int>(8);
    private static readonly string[] Metres = new string[256];
    private static readonly Color ArrowCol = Ui.C(0.42f, 0.92f, 0.55f, 1f);
    private float _taskAt = -99f;

    private void TaskArrows(Camera cam, PlayerControl me, Vector2 from)
    {
        if (Time.unscaledTime - _taskAt > 0.5f)
        {
            _taskAt = Time.unscaledTime;
            LightArrows(me);
        }
        if (_lit.Count == 0) return;

        Vector2 mePos = me.GetTruePosition();
        foreach (KeyValuePair<int, Lit> kv in _lit)
        {
            Lit l = kv.Value;
            ArrowBehaviour a = l.Arrow;
            if (a == null || !a.gameObject.activeInHierarchy) continue;
            SpriteRenderer img = a.image;
            if (img == null || !img.enabled) continue;

            Vector3 wp = a.transform.position;
            Vector2 sp = ToScreen(cam, Ui.V(wp.x, wp.y));
            float dx = from.x - sp.x, dy = from.y - sp.y;
            float len = Ui.Sqrt(dx * dx + dy * dy);
            if (len > 1f)
            {
                sp.x += dx / len * 26f;
                sp.y += dy / len * 26f;
            }
            float ex = l.Pos.x - mePos.x, ey = l.Pos.y - mePos.y;
            int m = Ui.Clamp(Ui.RoundToInt(Ui.Sqrt(ex * ex + ey * ey)), 0, Metres.Length - 1);
            Tag(sp, Metres[m] ??= m + "m", ArrowCol);
        }
    }

    private void LightArrows(PlayerControl me)
    {
        if (me.myTasks == null) return;

        Vector2 mePos = me.GetTruePosition();
        ArrowBehaviour donor = null;
        for (int i = 0; i < me.myTasks.Count && donor == null; i++)
        {
            PlayerTask pt = me.myTasks[i];
            NormalPlayerTask n = pt != null ? pt.TryCast<NormalPlayerTask>() : null;
            if (n != null && n.Arrow != null && !(_lit.TryGetValue(n.GetInstanceID(), out Lit own) && own.Cloned))
                donor = n.Arrow;
        }

        for (int i = 0; i < me.myTasks.Count; i++)
        {
            PlayerTask pt = me.myTasks[i];
            if (pt == null || pt.IsComplete) continue;
            NormalPlayerTask n = pt.TryCast<NormalPlayerTask>();
            if (n == null) continue;

            var locs = n.FindValidConsolesPositions();
            if (locs == null || locs.Count == 0) continue;

            int id = n.GetInstanceID();
            if (!_lit.TryGetValue(id, out Lit lit))
            {
                lit = new Lit { Task = n };
                _lit[id] = lit;
            }
            if (lit.Arrow == null)
            {
                lit.Arrow = n.Arrow;
                if (lit.Arrow == null && donor != null)
                {
                    lit.Arrow = Object.Instantiate(donor, n.transform);
                    lit.Cloned = true;
                    n.Arrow = lit.Arrow;
                }
                if (lit.Arrow == null) continue;
            }

            Vector2 pos = locs[0];
            float best = (pos.x - mePos.x) * (pos.x - mePos.x) + (pos.y - mePos.y) * (pos.y - mePos.y);
            for (int k = 1; k < locs.Count; k++)
            {
                Vector2 c = locs[k];
                float d2 = (c.x - mePos.x) * (c.x - mePos.x) + (c.y - mePos.y) * (c.y - mePos.y);
                if (d2 < best)
                {
                    best = d2;
                    pos = c;
                }
            }

            lit.Pos = pos;
            ArrowBehaviour a = lit.Arrow;
            a.target = Ui.V3(pos.x, pos.y, 0f);
            if (!n.arrowSuspended && !a.gameObject.activeSelf)
                a.gameObject.SetActive(true);
            if (a.image != null)
            {
                if (lit.Was.a == 0f)
                    lit.Was = a.image.color;
                a.image.color = ArrowCol;
            }
        }

        _gone.Clear();
        foreach (KeyValuePair<int, Lit> kv in _lit)
        {
            Lit l = kv.Value;
            if (l.Task == null || l.Task.IsComplete || l.Arrow == null)
                _gone.Add(kv.Key);
        }
        for (int i = 0; i < _gone.Count; i++)
        {
            Lit l = _lit[_gone[i]];
            if (l.Arrow != null && l.Task != null)
                Restore(l);
            _lit.Remove(_gone[i]);
        }
    }

    private static void Restore(Lit l)
    {
        ArrowBehaviour a = l.Arrow;
        if (l.Cloned)
        {
            if (l.Task != null)
                l.Task.Arrow = null;
            Object.Destroy(a.gameObject);
            return;
        }
        if (a.image != null && l.Was.a > 0f)
            a.image.color = l.Was;
        a.gameObject.SetActive(l.Task != null && l.Task.taskStep > 0 && !l.Task.IsComplete);
    }

    private void ReleaseArrows()
    {
        foreach (KeyValuePair<int, Lit> kv in _lit)
            if (kv.Value.Arrow != null)
                Restore(kv.Value);
        _lit.Clear();
    }

    private static void Box(Vector2 c, float hw, float hh, Color col)
    {
        float t = 2f;
        float x = c.x - hw, y = c.y - hh, w = hw * 2f, h = hh * 2f;
        float br = Ui.Min(hw, hh) * 0.6f;
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(col.r, col.g, col.b, col.a * prev.a);
        NocturneStyle.FillRaw(Ui.R(x, y, br, t));
        NocturneStyle.FillRaw(Ui.R(x + w - br, y, br, t));
        NocturneStyle.FillRaw(Ui.R(x, y + h - t, br, t));
        NocturneStyle.FillRaw(Ui.R(x + w - br, y + h - t, br, t));
        NocturneStyle.FillRaw(Ui.R(x, y, t, br));
        NocturneStyle.FillRaw(Ui.R(x, y + h - br, t, br));
        NocturneStyle.FillRaw(Ui.R(x + w - t, y, t, br));
        NocturneStyle.FillRaw(Ui.R(x + w - t, y + h - br, t, br));
        NocturneStyle.Tint = prev;
    }

    private string TagText(PlayerControl p, int dist)
    {
        byte pid = p.PlayerId;
        bool dirty = !_tags.TryGetValue(pid, out TagLine t);

        float now = Time.unscaledTime;
        if (t.Name == null || now >= t.NameAt)
        {
            string nm = p.Data.PlayerName;
            if (string.IsNullOrEmpty(nm)) nm = "?";
            t.NameAt = now + 0.5f;
            dirty = true;
            if (nm != t.Name)
            {
                t.Name = nm;
                t.Text = null;
            }
        }

        if (t.Text == null || t.Dist != dist)
        {
            t.Dist = dist;
            t.Text = t.Name + "  " + dist + "m";
            dirty = true;
        }

        if (dirty)
            _tags[pid] = t;
        return t.Text;
    }

    private void Tag(Vector2 sp, string txt, Color col)
    {
        if (_tag == null)
            _tag = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        var r = Ui.R(sp.x - 90f, sp.y - 8f, 180f, 16f);
        _tag.normal.textColor = Ui.C(0f, 0f, 0f, 0.85f);
        GUI.Label(Ui.R(r.m_XMin + 1f, r.m_YMin + 1f, r.m_Width, r.m_Height), txt, _tag);
        _tag.normal.textColor = col;
        GUI.Label(r, txt, _tag);
    }

    private void DrawTimer(Vector2 sp, float sec)
    {
        bool ready = sec <= 0.05f;
        string txt = ready ? NocturneText.T("ГОТОВ", "READY") : sec.ToString("0.0");
        var r = Ui.R(sp.x - 42f, sp.y - 66f, 84f, 20f);

        _cdShadow.normal.textColor = Ui.C(0f, 0f, 0f, 0.8f);
        GUI.Label(Ui.R(r.m_XMin + 1f, r.m_YMin + 1f, r.m_Width, r.m_Height), txt, _cdShadow);
        _cd.normal.textColor = ready ? Ui.C(0.92f, 0.32f, 0.36f, 1f) : Ui.C(1f, 0.86f, 0.42f, 1f);
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

    internal static Vector2 ToScreen(Camera cam, Vector2 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(Ui.V3(world.x, world.y, 0f));
        return Ui.V(sp.x, Screen.height - sp.y);
    }

    private static Color BodyColor(PlayerControl p)
    {
        int id = p.CurrentOutfit.ColorId;
        if (id >= 0 && id < Palette.PlayerColors.Length)
        {
            Color c = Palette.PlayerColors[id];
            c.a = 0.9f;
            return c;
        }
        return Ui.C(1f, 1f, 1f, 0.85f);
    }

    private static bool SideOn => NocturneConfig.EspSideColors.Value;

    internal static int Side(PlayerControl p)
    {
        if (p == null || p.Data == null || p.Data.Role == null) return 0;
        if (p.Data.Role.IsImpostor)
            return 1;
        if (p.Data.Role.Role == RoleTypes.GuardianAngel)
            return 2;
        return 0;
    }

    private static bool SideShown(PlayerControl p)
    {
        switch (Side(p))
        {
            case 1:
                return NocturneConfig.EspSeeImps.Value;
            case 2:
                return NocturneConfig.EspSeeAngels.Value;
            default:
                return NocturneConfig.EspSeeCrew.Value;
        }
    }

    internal static readonly Color[] SideCols =
    {
        Ui.C(1f, 1f, 1f, 0.9f),
        Ui.C(1f, 0.24f, 0.24f, 0.9f),
        Ui.C(1f, 0.84f, 0.2f, 0.9f),
        Ui.C(0.34f, 0.9f, 0.44f, 0.9f),
        Ui.C(0.34f, 0.85f, 0.95f, 0.9f),
        Ui.C(0.4f, 0.6f, 1f, 0.9f),
        Ui.C(1f, 0.6f, 0.2f, 0.9f),
        Ui.C(0.72f, 0.46f, 1f, 0.9f),
        Ui.C(1f, 0.5f, 0.72f, 0.9f),
        Ui.C(0.1f, 0.1f, 0.1f, 0.95f),
    };
    private static readonly string[] SideRu = { "Белый", "Красный", "Жёлтый", "Зелёный", "Голубой", "Синий", "Оранж.", "Фиолет.", "Розовый", "Чёрный" };
    private static readonly string[] SideEn = { "White", "Red", "Yellow", "Green", "Cyan", "Blue", "Orange", "Purple", "Pink", "Black" };

    internal static int SideCount => SideCols.Length;
    internal static Color SideCol(int i) => SideCols[Ui.Clamp(i, 0, SideCols.Length - 1)];
    internal static string SideName(int i) => (NocturneText.IsRussian ? SideRu : SideEn)[Ui.Clamp(i, 0, SideCols.Length - 1)];

    private static Color EspColor(PlayerControl p, bool dead)
    {
        if (SideOn)
        {
            if (dead) return SideCol(NocturneConfig.EspDeadColor.Value);
            bool imp = p != null && p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor;
            return SideCol(imp ? NocturneConfig.EspImpColor.Value : NocturneConfig.EspCrewColor.Value);
        }
        Color b = BodyColor(p);
        if (dead) b.a *= 0.5f;
        return b;
    }

    private static int _shield;
    private static readonly float[] Cuts = new float[32];

    private static void Line(Vector2 a, Vector2 b, Color col, float w)
    {
        float dx = b.x - a.x, dy = b.y - a.y;
        if (dx * dx + dy * dy < 4f) return;

        int n = 0;
        for (int i = 0; i < _shield && n < Cuts.Length; i++)
        {
            Rect r = Patches.MouseShield.At(i);
            float t0 = 0f, t1 = 1f;
            if (!Slab(a.x, dx, r.m_XMin, r.m_XMin + r.m_Width, ref t0, ref t1) || !Slab(a.y, dy, r.m_YMin, r.m_YMin + r.m_Height, ref t0, ref t1))
                continue;

            int k = n;
            while (k > 0 && Cuts[k - 2] > t0)
            {
                Cuts[k] = Cuts[k - 2];
                Cuts[k + 1] = Cuts[k - 1];
                k -= 2;
            }
            Cuts[k] = t0;
            Cuts[k + 1] = t1;
            n += 2;
        }

        float at = 0f;
        for (int i = 0; i < n; i += 2)
        {
            if (Cuts[i] > at) Seg(a, dx, dy, at, Cuts[i], col, w);
            if (Cuts[i + 1] > at) at = Cuts[i + 1];
        }
        if (at < 1f) Seg(a, dx, dy, at, 1f, col, w);
    }

    private static bool Slab(float p, float d, float lo, float hi, ref float t0, ref float t1)
    {
        if (Ui.Abs(d) < 1e-4f)
            return p >= lo && p <= hi;

        float u = (lo - p) / d, v = (hi - p) / d;
        if (u > v)
        {
            float s = u;
            u = v;
            v = s;
        }
        if (u > t0) t0 = u;
        if (v < t1) t1 = v;
        return t0 < t1;
    }

    private static void Seg(Vector2 a, float dx, float dy, float t0, float t1, Color col, float w)
    {
        float len = Ui.Sqrt(dx * dx + dy * dy) * (t1 - t0);
        if (len < 2f) return;

        Vector2 p = Ui.V(a.x + dx * t0, a.y + dy * t0);
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(Ui.Atan2(dy, dx) * Mathf.Rad2Deg, p);
        NocturneStyle.Fill(Ui.R(p.x, p.y - w * 0.5f, len, w), col);
        GUI.matrix = m;
    }
}
