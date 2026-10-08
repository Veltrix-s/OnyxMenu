using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public enum NocturneNotifyKind
{
    Info, Success, Warning, Danger
}

internal static class NocturneToast
{
    private sealed class Entry
    {
        internal string Title;
        internal string Detail;
        internal NocturneNotifyKind Kind;
        internal int Count = 1;
        internal float Life;
        internal float Left;
        internal float In;
        internal float Out;
        internal float Pulse;
        internal float Flash = 1f;
        internal float Y;
        internal float H;
        internal int Lines;
        internal float Block;
        internal int Ver = -1;
        internal bool Multi;
        internal int Icon = -1;
        internal string Key;
        internal GUIContent Head;
        internal GUIContent Body;
    }

    private const float EnterSec = 0.5f;
    private const float ExitSec = 0.3f;

    private static readonly List<Entry> Entries = new List<Entry>(8);
    private static readonly GUIContent Probe = new GUIContent();
    private static readonly GUIContent[] Mult = new GUIContent[100];
    private static readonly GUIStyle[] Glass = new GUIStyle[41];
    private static readonly GUIStyle[] Wash = new GUIStyle[41];

    private static GUIStyle _head;
    private static GUIStyle _body;
    private static GUIStyle _num;
    private static GUIStyle _bar;
    private static float _sc = -1f;
    private static float _lh;
    private static float _hh;
    private static float _tw;
    private static float _rise = -1f;
    private static int _ver;

    internal static void Push(string text, float life = 3.6f) => Push(text, null, life, NocturneNotifyKind.Info);

    internal static void Push(string title, string detail, float life, NocturneNotifyKind kind, NocturneIcon? icon = null, string key = null)
    {
        if (!NocturneConfig.Toasts.Value) return;

        string t = Clean(title, 120);
        string d = Clean(detail, 160);
        if (t.Length == 0)
        {
            t = d;
            d = string.Empty;
        }
        if (t.Length == 0) return;

        float dur = Span(life);
        int ic = icon.HasValue ? (int)icon.Value : -1;

        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry old = Entries[i];
            if (key != null ? old.Key != key : (old.Kind != kind || old.Title != t || old.Detail != d)) continue;

            if (old.Title == t && old.Detail == d)
            {
                if (old.Count < 99) old.Count++;
                old.Pulse = 1f;
            }
            else
            {
                old.Title = t;
                old.Detail = d;
                old.Count = 1;
                old.Ver = 0;
            }
            old.Kind = kind;
            old.Icon = ic;
            old.Life = old.Left = dur;
            old.Flash = 0.7f;
            if (i != Entries.Count - 1)
            {
                Entries.RemoveAt(i);
                Entries.Add(old);
            }
            return;
        }

        int live = 0, burst = 0;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry old = Entries[i];
            if (old.In < 0.3f) burst++;
            if (old.Left <= ExitSec) continue;
            if (++live >= 5)
                old.Left = ExitSec;
        }
        if (Entries.Count == 0) _rise = -1f;
        while (Entries.Count >= 10)
            Entries.RemoveAt(0);

        Entries.Add(new Entry { Title = t, Detail = d, Kind = kind, Icon = ic, Key = key, Life = dur, Left = dur, In = -0.2f * burst });
    }

    internal static float Span(float life) => MathF.Min(MathF.Max(life, 1.25f) * 1.25f + 0.4f, 12f);

    internal static void Demo()
    {
        Push(NocturneText.T("Nocturne на связи ✓", "Nocturne is live ✓"));
        Push(NocturneText.T("Вайтлист", "Whitelist"), "Player42", 4f, NocturneNotifyKind.Success);
        Push(NocturneText.T("Знакомый игрок", "Known player"),
            NocturneText.T("Уже был у тебя в лобби, ник менялся 3 раза: Alpha → Bravo → Charlie", "Seen in your lobby before, changed nick 3 times: Alpha → Bravo → Charlie"),
            4.5f, NocturneNotifyKind.Warning);
        Push(NocturneText.T("Бан", "Ban"), NocturneText.T("Player42: чат-флуд", "Player42: chat flood"), 5f, NocturneNotifyKind.Danger);
    }

    internal static void Draw()
    {
        if (Entries.Count == 0) return;
        if (!NocturneStyle.Painting) return;

        float sw = Screen.width;
        float sh = Screen.height;
        float rs = MathF.Min(MathF.Max(sh / 1080f, 0.85f), 2.2f);
        float sc = rs * MathF.Min(MathF.Max(NocturneConfig.HudScale.Value, 0.6f), 2f);
        if (_head == null || MathF.Abs(sc - _sc) > 0.004f)
            Restyle(sc);

        float dt = NocturneStyle.Dt;
        float k = 1f - MathF.Exp(-14f * dt);
        Step(dt);
        if (Entries.Count == 0) return;

        float want = ShipStatus.Instance != null ? 392f : 92f;
        _rise = _rise < 0f ? want : _rise + (want - _rise) * k;

        float edge = 18f * rs;
        float w = MathF.Min(352f * sc, sw - edge * 2f);
        float x0 = sw - edge - w;
        float away = w + edge + 24f * sc;
        float bottom = sh - _rise * rs;
        float gap = 9f * sc;
        Color acc = NocturneStyle.Current.Accent;
        _tw = w - 76f * sc;

        float stack = 0f;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry e = Entries[i];
            bool fresh = e.Ver < 0;
            if (e.Ver != _ver || e.Multi != (e.Count > 1))
                Fit(e, sc);
            e.Y = fresh ? stack : e.Y + (stack - e.Y) * k;
            stack += (e.H + gap) * (1f - e.Out);

            float u = e.In - 1f;
            float slide = 1f + u * u * (2f * u + 1f);
            float a = Sat(e.In * 3f) * (1f - e.Out);
            float x = MathF.Round(x0 + (1f - slide) * away + e.Out * e.Out * away);
            if (a < 0.01f || x >= sw) continue;

            Card(e, x, MathF.Round(bottom - e.Y - e.H), w, a, sc, acc);
        }
    }

    private static void Step(float dt)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry e = Entries[i];
            e.Left -= dt;
            e.In = MathF.Min(1f, e.In + dt / EnterSec);
            e.Pulse = MathF.Max(0f, e.Pulse - dt * 3.5f);
            e.Flash = MathF.Max(0f, e.Flash - dt * 1.6f);
            if (e.Left > ExitSec)
                e.Out = MathF.Max(0f, e.Out - dt * 6f);
            else
                e.Out += dt / ExitSec;

            if (e.Out >= 1f) Entries.RemoveAt(i);
        }
    }

    private static void Fit(Entry e, float sc)
    {
        e.Ver = _ver;
        e.Multi = e.Count > 1;
        e.Head ??= new GUIContent();
        e.Body ??= new GUIContent();

        float hw = e.Multi ? _tw - 44f * sc : _tw;
        string s = e.Title;
        Probe.text = s;
        if (_head.CalcSize(Probe).x > hw)
            s = Cut(s, _head, hw, 0f);
        e.Head.text = s;

        float block = _hh;
        if (e.Detail.Length == 0)
            e.Lines = 0;
        else
        {
            s = e.Detail;
            Probe.text = s;
            int lines = (int)(_body.CalcHeight(Probe, _tw) / _lh + 0.5f);
            if (lines > 2)
            {
                s = Cut(s, _body, _tw, _lh * 2f + 1f);
                lines = 2;
            }
            e.Body.text = lines == 2 ? Balance(s) : s;
            e.Lines = lines;
            block += 2f * sc + lines * _lh;
        }

        e.Block = block;
        e.H = MathF.Round(30f * sc + MathF.Max(36f * sc, block));
    }

    private static string Cut(string s, GUIStyle st, float w, float maxH)
    {
        int lo = 0, hi = s.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            Probe.text = s.Substring(0, mid) + "…";
            bool fits = maxH > 0f ? st.CalcHeight(Probe, w) <= maxH : st.CalcSize(Probe).x <= w;
            if (fits)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo > 0 ? s.Substring(0, lo).TrimEnd() + "…" : "…";
    }

    private static string Balance(string s)
    {
        int best = -1;
        float gap = float.MaxValue;
        for (int i = s.IndexOf(' '); i > 0; i = s.IndexOf(' ', i + 1))
        {
            Probe.text = s.Substring(0, i);
            float a = _body.CalcSize(Probe).x;
            Probe.text = s.Substring(i + 1);
            float b = _body.CalcSize(Probe).x;
            if (a > _tw - 1f || b > _tw - 1f) continue;
            if (MathF.Abs(a - b) < gap)
            {
                gap = MathF.Abs(a - b);
                best = i;
            }
        }
        return best < 0 ? s : s.Substring(0, best) + "\n" + s.Substring(best + 1);
    }

    private static void Card(Entry e, float x, float y, float w, float a, float sc, Color acc)
    {
        Color k = e.Kind switch
        {
            NocturneNotifyKind.Success => Ui.C(0.22f, 0.82f, 0.38f, 1f),
            NocturneNotifyKind.Danger => Ui.C(0.93f, 0.30f, 0.30f, 1f),
            NocturneNotifyKind.Warning => Ui.C(0.97f, 0.74f, 0.14f, 1f),
            _ => acc,
        };
        Color hi = Ui.C(k.r + (1f - k.r) * 0.3f, k.g + (1f - k.g) * 0.3f, k.b + (1f - k.b) * 0.3f, 1f);
        float h = e.H;
        int rad = (int)(14f * sc + 0.5f);
        var r = Ui.R(x, y, w, h);

        NocturneStyle.Tint = Ui.C(1f, 1f, 1f, a);
        NocturneStyle.Glow(Ui.R(x - 18f * sc, y - 9f * sc, w + 36f * sc, h + 32f * sc), Ui.C(k.r * 0.2f, k.g * 0.2f, k.b * 0.2f, 0.55f));
        if (NocturneStyle.Lite)
            NocturneStyle.Fill(r, Ui.C(0.065f, 0.065f, 0.072f, 0.96f));
        else
        {
            GUI.Box(r, NocturneStyle.Blank, Bake(Glass, rad, false));
            NocturneStyle.Tint = Ui.C(k.r, k.g, k.b, a * 0.2f);
            GUI.Box(r, NocturneStyle.Blank, Bake(Wash, rad, true));
            NocturneStyle.Tint = Ui.C(1f, 1f, 1f, a);
        }
        if (e.Flash > 0.01f)
            NocturneStyle.FillRounded(r, Ui.C(1f, 1f, 1f, 0.07f * e.Flash), rad);
        NocturneStyle.StrokeRounded(r, Ui.C(k.r, k.g, k.b, 0.38f + 0.45f * e.Flash), rad, 1);

        float ts = 36f * sc;
        float ch = MathF.Max(ts, e.Block);
        float top = y + 12f * sc;
        int tr = (int)(11f * sc + 0.5f);
        var tile = Ui.R(x + 14f * sc, MathF.Round(top + (ch - ts) * 0.5f), ts, ts);
        NocturneStyle.FillRounded(tile, Ui.C(k.r, k.g, k.b, 0.18f), tr);
        NocturneStyle.StrokeRounded(tile, Ui.C(k.r, k.g, k.b, 0.5f), tr, 1);
        float isz = 22f * sc * (1f + 0.35f * e.Flash * e.Flash);
        NocturneIcon icon = e.Icon >= 0 ? (NocturneIcon)e.Icon : e.Kind switch
        {
            NocturneNotifyKind.Success => NocturneIcon.Check,
            NocturneNotifyKind.Warning => NocturneIcon.Warn,
            NocturneNotifyKind.Danger => NocturneIcon.Error,
            _ => NocturneIcon.Info,
        };
        NocturneIcons.Draw(icon, Ui.R(tile.m_XMin + (ts - isz) * 0.5f, tile.m_YMin + (ts - isz) * 0.5f, isz, isz), hi);

        float tx = x + 62f * sc;
        float ty = MathF.Round(top + (ch - e.Block) * 0.5f);
        GUI.Label(Ui.R(tx, ty, e.Multi ? _tw - 44f * sc : _tw, _hh), e.Head, _head);
        if (e.Lines > 0)
            GUI.Label(Ui.R(tx, MathF.Round(ty + _hh + 2f * sc), _tw, e.Lines * _lh + 2f), e.Body, _body);

        float bx = x + 14f * sc;
        float bw = w - 28f * sc;
        float bh = MathF.Max(3f, MathF.Round(3f * sc));
        float by = MathF.Round(y + h - 7f * sc - bh);
        NocturneStyle.Fill(Ui.R(bx, by, bw, bh), Ui.C(1f, 1f, 1f, 0.09f));
        float fw = MathF.Round(bw * Sat(e.Left / e.Life));
        if (fw >= 1f)
        {
            NocturneStyle.Tint = Ui.C(k.r, k.g, k.b, a);
            GUI.Box(Ui.R(bx, by, fw, bh), NocturneStyle.Blank, _bar);
            if (fw >= 8f * sc)
                NocturneStyle.Glow(Ui.R(bx + fw - 9f * sc, by - 8f * sc, 18f * sc, bh + 16f * sc), Ui.C(hi.r, hi.g, hi.b, 0.6f));
        }

        if (!e.Multi) return;
        float g = 1f + 0.3f * e.Pulse;
        float pw = 34f * sc * g;
        float ph = 20f * sc * g;
        float cx = x + w - 31f * sc;
        float cy = ty + _hh * 0.5f;
        var pill = Ui.R(cx - pw * 0.5f, cy - ph * 0.5f, pw, ph);
        NocturneStyle.FillRounded(pill, Ui.C(k.r, k.g, k.b, 0.22f), (int)(ph * 0.5f + 0.5f));
        NocturneStyle.Tint = Ui.C(hi.r, hi.g, hi.b, a);
        GUI.Label(pill, Mult[e.Count] ??= new GUIContent("×" + e.Count), _num);
    }

    private static GUIStyle Bake(GUIStyle[] cache, int rad, bool wash)
    {
        rad = rad < 4 ? 4 : rad > 40 ? 40 : rad;
        GUIStyle s = cache[rad];
        if (s != null) return s;

        int w = wash ? rad * 2 + 64 : rad * 2 + 2;
        int h = wash ? rad * 2 + 2 : rad * 2 + 40;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (h - 1 - y + 0.5f) / h;
            float lum = 0.05f + 0.045f * (1f - v);
            byte lo = (byte)(lum * 255f + 0.5f);
            float sy = y + 0.5f;
            float dy = sy < rad ? rad - sy : sy > h - rad ? sy - (h - rad) : 0f;
            for (int x = 0; x < w; x++)
            {
                float sx = x + 0.5f;
                float dx = sx < rad ? rad - sx : sx > w - rad ? sx - (w - rad) : 0f;
                float cover = Sat(rad - MathF.Sqrt(dx * dx + dy * dy) + 0.5f);
                if (wash)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(cover * MathF.Pow(1f - sx / w, 1.8f) * 255f));
                else
                    px[y * w + x] = new Color32(lo, lo, (byte)(lo + 2), (byte)(cover * 245f));
            }
        }
        t.SetPixels32(px);
        t.Apply();

        s = new GUIStyle();
        s.normal.background = t;
        s.border = NocturneStyle.Offset(rad, rad, rad, rad);
        return cache[rad] = s;
    }

    private static void Restyle(float sc)
    {
        if (_head == null)
        {
            _head = Make(FontStyle.Bold, TextAnchor.MiddleLeft, false, Ui.C(0.97f, 0.97f, 0.98f, 1f));
            _body = Make(FontStyle.Normal, TextAnchor.UpperLeft, true, Ui.C(0.76f, 0.77f, 0.81f, 1f));
            _num = Make(FontStyle.Bold, TextAnchor.MiddleCenter, false, Ui.White);

            var t = new Texture2D(64, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[64];
            for (int i = 0; i < 64; i++)
            {
                float u = (i + 0.5f) / 64f;
                px[i] = new Color32(255, 255, 255, (byte)((0.35f + 0.65f * u * MathF.Sqrt(u)) * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            _bar = new GUIStyle();
            _bar.normal.background = t;
        }
        _sc = sc;
        _head.fontSize = (int)(17f * sc + 0.5f);
        _body.fontSize = (int)(14f * sc + 0.5f);
        _num.fontSize = (int)(13f * sc + 0.5f);
        Probe.text = "Ag";
        _lh = MathF.Max(_body.CalcHeight(Probe, 1000f), 1f);
        _hh = MathF.Max(_head.CalcHeight(Probe, 1000f), 1f);
        _ver++;
    }

    private static GUIStyle Make(FontStyle fs, TextAnchor an, bool wrap, Color col)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontStyle = fs,
            alignment = an,
            wordWrap = wrap,
            richText = true,
            clipping = TextClipping.Clip,
        };
        s.normal.textColor = col;
        s.padding = NocturneStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static float Sat(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private static string Clean(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string s = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
