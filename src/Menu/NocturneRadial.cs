using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneRadial : MonoBehaviour
{
    private GUIStyle _label, _center, _sub, _hint;
    private float _openAt = -1f;
    private Vector2[] _pos = new Vector2[10];
    private float[] _siz = new float[10];
    private static Texture2D _disc, _glow;
    private static GUIStyle _texStyle;

    private static Color A(Color c, float a) => Ui.C(c.r, c.g, c.b, a);
    private static float Frac(float v) => v - Ui.Floor(v);

    private static void Tex(Texture2D t, Vector2 c, float r, Color col)
    {
        if (t == null) return;
        if (_texStyle == null) _texStyle = new GUIStyle();
        _texStyle.normal.background = t;
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(col.r, col.g, col.b, col.a * prev.a);
        GUI.Box(Ui.R(c.x - r, c.y - r, r * 2f, r * 2f), NocturneStyle.Blank, _texStyle);
        NocturneStyle.Tint = prev;
    }

    private static void Disc(Vector2 c, float r, Color col) => Tex(_disc, c, r, col);
    private static void Glow(Vector2 c, float r, Color col) => Tex(_glow, c, r, col);

    private static void Ring(Vector2 c, float r, Color col, float thick, int seg)
    {
        Vector2 prev = c + Ui.V(r, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var pt = c + Ui.V(Ui.Cos(a), Ui.Sin(a)) * r;
            DrawLine(prev, pt, col, thick);
            prev = pt;
        }
    }

    internal void DrawGui()
    {
        var key = NocturneConfig.RadialKey;
        bool open = !NocturneMenu.Rebinding && NocturneKeys.Held(key);
        if (!open)
        {
            _openAt = -1f;
            return;
        }
        if (_openAt < 0f) _openAt = Time.unscaledTime;

        Init();
        NocturnePalette p = NocturneStyle.Current;
        float sw = Screen.width, sh = Screen.height, tm = Time.unscaledTime;
        var c = Ui.V(sw * 0.5f, sh * 0.5f);
        Vector2 m = Event.current != null ? NocturneStyle.Mouse : Ui.V(-1f, -1f);
        float elapsed = tm - _openAt;
        float ease = 1f - Ui.Pow(1f - Ui.Clamp01(elapsed / 0.16f), 3f);
        float pulse = 0.5f + 0.5f * Ui.Sin(tm * 3.2f);

        NocturneStyle.Fill(Ui.R(0f, 0f, sw, sh), Ui.C(0f, 0f, 0f, 0.55f * ease));

        List<string> favs = NocturneQuick.FavIds();
        float hub = 62f * ease;
        float radius = Ui.Clamp(94f + favs.Count * 11f, 150f, 235f);

        Glow(c, (radius + 70f) * ease, A(p.Accent, 0.11f * ease));

        if (favs.Count == 0)
        {
            DrawHub(c, hub, p, pulse, tm, ease, true);
            _hint.normal.textColor = p.Muted;
            GUI.Label(Ui.R(c.x - 160f, c.y - 20f, 320f, 40f), NocturneText.T("Добавь фичи ★ в поиске меню", "Add favorites ★ in the menu search"), _hint);
            return;
        }

        Ring(c, radius * ease, A(p.Accent, 0.14f * ease), 1.5f, 64);

        for (int s = 0; s < 12; s++)
        {
            float seed = s * 0.61803399f;
            float ang = (s * 41f + tm * 7f) * Mathf.Deg2Rad;
            float rr = (radius + 26f + Frac(seed) * 70f) * ease;
            var sp = c + Ui.V(Ui.Cos(ang), Ui.Sin(ang)) * rr;
            float tw = 0.5f + 0.5f * Ui.Sin(tm * 2f + s);
            Disc(sp, 1.6f + Frac(seed) * 1.8f, A(s % 3 == 0 ? p.Accent : Ui.White, (0.06f + 0.12f * tw) * ease));
        }

        float popT = Ui.Clamp01(elapsed / 0.2f);
        float pu = popT - 1f;
        float pop = 1f + 2.2f * pu * pu * pu + 1.2f * pu * pu;
        if (_pos.Length < favs.Count)
        {
            _pos = new Vector2[favs.Count];
            _siz = new float[favs.Count];
        }
        Vector2[] pos = _pos;
        float[] siz = _siz;
        int hovered = -1;
        for (int i = 0; i < favs.Count; i++)
        {
            float ang = (-90f + i * 360f / favs.Count) * Mathf.Deg2Rad;
            pos[i] = c + Ui.V(Ui.Cos(ang), Ui.Sin(ang)) * radius * pop;
            siz[i] = Ui.Lerp(30f, 46f, Ui.Clamp01(pop));
            if (Vector2.Distance(pos[i], m) <= siz[i]) hovered = i;
        }

        for (int i = 0; i < favs.Count; i++)
            DrawLine(c, pos[i], A(p.Accent, hovered == i ? 0.55f : 0.16f), hovered == i ? 3f : 1.5f);

        for (int i = 0; i < favs.Count; i++)
        {
            QuickItem it = NocturneQuick.ById(favs[i]);
            if (it == null) continue;
            bool on = it.Cfg != null && it.Cfg.Value;
            bool hover = hovered == i;
            float r = hover ? siz[i] + 5f : siz[i];
            Vector2 bc = pos[i];

            if (on || hover)
                Glow(bc, r + 16f, A(p.Accent, (on ? 0.22f : 0.12f) + (hover ? 0.16f * pulse : 0f)));
            Disc(bc, r + 2.5f, hover ? Ui.White : (on ? A(p.Accent, 0.75f) : A(Ui.White, 0.16f)));
            Disc(bc, r, on ? A(p.Accent, 0.95f) : A(p.Button, 0.99f));

            _label.normal.textColor = on ? Ui.White : p.Text;
            _label.fontSize = hover ? 13 : 12;
            GUI.Label(Ui.R(bc.x - r + 8f, bc.y - r, r * 2f - 16f, r * 2f - 12f), it.Label, _label);
            Disc(Ui.V(bc.x, bc.y + r - 12f), 4f, on ? Ui.White : A(p.Muted, 0.6f));

            if (hover && Event.current != null && NocturneStyle.Is(EventType.MouseDown))
            {
                if (it.Cfg != null)
                    it.Cfg.Value = !it.Cfg.Value;
                Event.current.Use();
            }
        }

        DrawHub(c, hub, p, pulse, tm, ease, hovered < 0);

        if (hovered >= 0)
        {
            QuickItem it = NocturneQuick.ById(favs[hovered]);
            bool on = it != null && it.Cfg != null && it.Cfg.Value;
            _center.normal.textColor = p.Text;
            _center.fontSize = 14;
            GUI.Label(Ui.R(c.x - hub, c.y - 26f, hub * 2f, 26f), it != null ? it.Label : "", _center);
            var pill = Ui.R(c.x - 26f, c.y + 6f, 52f, 20f);
            NocturneStyle.FillRounded(pill, on ? A(p.Accent, 0.9f) : A(p.Button, 0.95f), 10);
            NocturneStyle.StrokeRounded(pill, on ? Ui.White : A(Ui.White, 0.12f), 10, 1);
            _sub.normal.textColor = on ? Ui.White : p.Muted;
            GUI.Label(pill, on ? NocturneText.T("ВКЛ", "ON") : NocturneText.T("ВЫКЛ", "OFF"), _sub);
        }
    }

    [HideFromIl2Cpp]
    private void DrawHub(Vector2 c, float hub, NocturnePalette p, float pulse, float tm, float ease, bool idle)
    {
        float rp = Frac(tm * 0.55f);
        Ring(c, hub + rp * 28f, A(p.Accent, (1f - rp) * 0.45f * ease), 2f, 48);
        Glow(c, hub + 24f, A(p.Accent, 0.16f + 0.09f * pulse));
        Disc(c, hub + 2.5f, A(p.Accent, 0.6f));
        Disc(c, hub, A(p.Window, 0.98f));

        if (idle)
        {
            Gem(Ui.V(c.x, c.y - 12f), 11f, A(p.Accent, 0.92f));
            _center.normal.textColor = p.Accent;
            _center.fontSize = 15;
            GUI.Label(Ui.R(c.x - hub, c.y + 8f, hub * 2f, 24f), "NOCTURNE", _center);
        }
    }

    private static void Gem(Vector2 c, float s, Color col)
    {
        Matrix4x4 prev = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        NocturneStyle.FillRounded(Ui.R(c.x - s, c.y - s, s * 2f, s * 2f), col, 3);
        NocturneStyle.StrokeRounded(Ui.R(c.x - s, c.y - s, s * 2f, s * 2f), A(Ui.White, 0.25f), 3, 1);
        GUI.matrix = prev;
    }

    private static void DrawLine(Vector2 a, Vector2 b, Color col, float thick)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        Matrix4x4 prev = GUI.matrix;
        GUIUtility.RotateAroundPivot(Ui.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        NocturneStyle.Fill(Ui.R(a.x, a.y - thick * 0.5f, len, thick), col);
        GUI.matrix = prev;
    }

    private void Init()
    {
        if (_disc == null) _disc = NocturneStyle.BuildDisc(128);
        if (_glow == null)
            _glow = NocturneStyle.BuildGlow(128);
        if (_label != null)
            return;
        _label = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 12, fontStyle = FontStyle.Bold };
        _label.normal.textColor = Ui.White;
        _center = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15, wordWrap = true };
        _center.normal.textColor = Ui.White;
        _sub = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11 };
        _sub.normal.textColor = Ui.White;
        _hint = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true };
        _hint.normal.textColor = Ui.White;
    }
}
