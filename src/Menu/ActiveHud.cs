using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace Nocturne;

internal static class ActiveHud
{
    private sealed class Entry
    {
        internal ConfigEntry<bool>[] Cfg;
        internal string Ru;
        internal string En;
        internal Color Col;
        internal Color Tx;
        internal string Text;
        internal GUIContent Content;
        internal float TextW;
        internal float Alpha;
        internal float Slot;
        internal float Flash;
        internal int Count;
        internal int Shown;
        internal int Ver = -1;
        internal bool On;
        internal bool Listed;
    }

    private const int MaxRows = 14;
    private const int Steps = 32;
    private const float Row = 19f;
    private const float PadX = 12f;
    private const float PadY = 9f;
    private const float Head = 24f;

    private static readonly GUIContent Probe = new GUIContent();
    private static readonly GUIContent[] Nums = new GUIContent[100];
    private static readonly GUIContent[] Plus = new GUIContent[100];
    private static readonly GUIContent[] Titles = new GUIContent[Steps];
    private static readonly string[] Shades = new string[Steps];
    private static readonly GUIStyle[] Glosses = new GUIStyle[41];
    private static readonly StringBuilder Sb = new StringBuilder(640);

    private static Entry[] _all;
    private static Entry[] _ord;
    private static int _n;
    private static int _on;
    private static int _more;

    private static GUIStyle _txt;
    private static GUIStyle _txtR;
    private static GUIStyle _ttl;
    private static GUIStyle _pill;
    private static GUIStyle _line;
    private static GUIContent _idle;
    private static string _title;
    private static float _tw;
    private static Color _tAcc;
    private static bool _tRu;
    private static bool _ready;

    private static int _frame = -1;
    private static int _ver;
    private static bool _ru;
    private static float _sc = 1f;
    private static float _poll;
    private static float _pa;
    private static float _pw;
    private static float _ph;

    private static bool _drag;
    private static Vector2 _off;
    private static float _ax = -1f;
    private static float _ay = -1f;
    private static bool _seen;
    private static bool _hov;
    private static Rect _box;

    internal static void Draw()
    {
        if (!NocturneConfig.HudActive.Value)
        {
            _seen = false;
            return;
        }

        bool want = (LobbyBehaviour.Instance != null || ShipStatus.Instance != null)
            && IntroCutscene.Instance == null && ExileController.Instance == null;
        if (!want && _pa <= 0.004f)
        {
            _seen = false;
            return;
        }

        if (_all == null) Build();
        if (_frame != Time.frameCount)
        {
            _frame = Time.frameCount;
            Step(want);
        }
        if (_pa <= 0.004f)
        {
            _seen = false;
            return;
        }

        if (!_drag)
        {
            _ax = NocturneConfig.HudActiveX.Value;
            _ay = NocturneConfig.HudActiveY.Value;
        }
        float ax = _ax < 0f ? Screen.width - 14f * _sc : _ax;
        float ay = _ay < 0f ? 168f * _sc : _ay;
        bool right = ax > Screen.width * 0.5f;
        var box = new Rect(
            Mathf.Round(Mathf.Clamp(right ? ax - _pw : ax, 0f, Mathf.Max(0f, Screen.width - _pw))),
            Mathf.Round(Mathf.Clamp(ay, 0f, Mathf.Max(0f, Screen.height - _ph))),
            _pw,
            _ph);

        _box = box;
        _seen = true;
        if (!NocturneStyle.Painting) return;

        Event ev = Event.current;
        _hov = ev != null && NocturneMenu.Opened && Reach(box).Contains(ev.mousePosition);
        Paint(box, right);
    }

    internal static void Drag()
    {
        if (!_seen || !NocturneMenu.Opened)
        {
            _drag = false;
            return;
        }

        if (_drag && !Input.GetMouseButton(0))
        {
            _drag = false;
            NocturneConfig.HudActiveX.Value = _ax;
            NocturneConfig.HudActiveY.Value = _ay;
            return;
        }

        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDown && e.button == 0 && Reach(_box).Contains(e.mousePosition))
        {
            _drag = true;
            _off = e.mousePosition - new Vector2(_box.x, _box.y);
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseDrag)
        {
            float nx = Mathf.Clamp(e.mousePosition.x - _off.x, 0f, Mathf.Max(0f, Screen.width - _box.width));
            float ny = Mathf.Clamp(e.mousePosition.y - _off.y, 0f, Mathf.Max(0f, Screen.height - _box.height));
            _ax = nx + _box.width * 0.5f > Screen.width * 0.5f ? nx + _box.width : nx;
            _ay = ny;
            e.Use();
        }
        else if (_drag && e.type == EventType.MouseUp)
        {
            _drag = false;
            NocturneConfig.HudActiveX.Value = _ax;
            NocturneConfig.HudActiveY.Value = _ay;
            e.Use();
        }
    }

    private static Rect Reach(Rect box)
    {
        float slop = Mathf.Max(8f, 6f * _sc);
        float w = Mathf.Max(box.width + slop * 2f, 64f);
        float h = Mathf.Max(box.height + slop * 2f, 48f);
        return new Rect(box.x + (box.width - w) * 0.5f, box.y + (box.height - h) * 0.5f, w, h);
    }

    private static void Step(bool want)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = 1f - Mathf.Exp(-15f * dt);

        float sc = Mathf.Clamp(Screen.height / 1080f, 0.85f, 2.2f) * Mathf.Clamp(NocturneConfig.HudActiveSize.Value, 60, 180) / 100f;
        bool ru = NocturneText.IsRussian;
        if (_txt == null || ru != _ru || Mathf.Abs(sc - _sc) > 0.001f)
        {
            _sc = sc;
            _ru = ru;
            _ver++;
            Restyle();
        }

        if (Time.unscaledTime >= _poll)
        {
            _poll = Time.unscaledTime + 0.12f;
            Poll();
        }

        int on = 0;
        float widest = 0f;
        for (int i = 0; i < _n; i++)
        {
            Entry e = _ord[i];
            e.Alpha = Mathf.MoveTowards(e.Alpha, e.On ? 1f : 0f, dt * (e.On ? 4.5f : 5.5f));
            e.Slot = Mathf.Abs(i - e.Slot) < 0.004f ? i : e.Slot + (i - e.Slot) * k;
            e.Flash = Mathf.Max(0f, e.Flash - dt * 1.3f);

            if (!e.On && e.Alpha <= 0f)
            {
                e.Listed = false;
                Array.Copy(_ord, i + 1, _ord, i, _n - i - 1);
                _n--;
                i--;
                continue;
            }

            if (e.Ver != _ver || (e.On && e.Cfg.Length > 1 && e.Count != e.Shown)) Retext(e);
            if (!e.On) continue;

            on++;
            if (i < MaxRows && e.TextW > widest) widest = e.TextW;
        }
        _on = on;
        _more = Mathf.Max(0, on - MaxRows);

        int rows = on == 0 ? 1 : Mathf.Min(on, MaxRows) + (_more > 0 ? 1 : 0);
        float headW = _tw + (PadX * 2f + 4f + 13f + 6f + 12f + 14f + 36f) * _sc;
        float rowW = widest + (PadX * 2f + 4f + 13f + 6f) * _sc;
        float wt = Mathf.Clamp(Mathf.Max(headW, rowW), 170f * _sc, 340f * _sc);
        float ht = (PadY * 2f + Head + 6f + rows * Row) * _sc;

        bool show = want && (on > 0 || NocturneMenu.Opened);
        _pa = Mathf.MoveTowards(_pa, show ? 1f : 0f, dt * 5f);
        if (_pa <= 0.01f || _pw <= 0f)
        {
            _pw = wt;
            _ph = ht;
        }
        else
        {
            _pw += (wt - _pw) * k;
            _ph += (ht - _ph) * k;
        }
    }

    private static void Poll()
    {
        int added = 0;
        for (int i = 0; i < _all.Length; i++)
        {
            Entry e = _all[i];
            int c = 0;
            for (int j = 0; j < e.Cfg.Length; j++)
                if (e.Cfg[j].Value) c++;

            e.Count = c;
            bool on = c > 0;
            if (on == e.On) continue;

            e.On = on;
            if (!on || e.Listed) continue;

            e.Listed = true;
            e.Slot = _n;
            e.Alpha = -0.2f * added++;
            e.Flash = 1f;
            _ord[_n++] = e;
        }
    }

    private static void Retext(Entry e)
    {
        string name = _ru ? e.Ru : e.En;
        e.Text = e.Cfg.Length > 1 && e.Count > 1 ? name + "  ×" + e.Count : name;
        e.Shown = e.Count;
        e.Ver = _ver;
        e.TextW = Measure(_txt, e.Text);
        e.Content ??= new GUIContent();
        e.Content.text = e.Text;
    }

    private static void Paint(Rect box, bool right)
    {
        NocturnePalette p = NocturneStyle.Current;
        Color acc = p.Accent;
        float sc = _sc;
        float fg = _pa;
        float bg = _pa * Mathf.Clamp(NocturneConfig.HudActiveOpacity.Value, 30, 100) / 100f;
        bool menu = NocturneMenu.Opened;
        bool lite = NocturneStyle.Lite;
        int rad = Mathf.RoundToInt(11f * sc);
        float ins = (PadX + 2f) * sc;
        float now = NocturneStyle.Now;
        Color prev = GUI.color;

        if (!_ready || acc != _tAcc || _ru != _tRu) Palette(acc);

        Color tone = Color.Lerp(p.Window, acc, 0.05f);
        NocturneStyle.Glow(new Rect(box.x - 18f * sc, box.y - 10f * sc, box.width + 36f * sc, box.height + 30f * sc), new Color(0f, 0f, 0f, 0.42f * bg));
        NocturneStyle.FillRounded(box, new Color(tone.r, tone.g, tone.b, 0.9f * bg), rad);
        if (!lite)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.075f * bg);
            GUI.Box(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, (box.height - 2f) * 0.7f), GUIContent.none, Gloss(rad));
            GUI.color = prev;
        }
        if (_hov || _drag)
            NocturneStyle.FillRounded(box, new Color(acc.r, acc.g, acc.b, (_drag ? 0.11f : 0.06f) * fg), rad);
        NocturneStyle.StrokeRounded(box, new Color(acc.r, acc.g, acc.b, (_drag ? 0.95f : _hov ? 0.85f : menu ? 0.6f : 0.34f) * fg), rad, 1);

        float hy = box.y + PadY * sc;
        float hh = Head * sc;
        float ic = 13f * sc;
        float block = ic + 6f * sc + _tw;
        float x0 = right ? box.xMax - ins - block : box.x + ins;
        float wx = x0 + ic + 6f * sc;
        float pulse = 0.55f + 0.45f * MathF.Sin(now * 2.4f);
        var bolt = new Rect(x0, hy + (hh - ic) * 0.5f, ic, ic);
        var halo = new Rect(bolt.x - 8f * sc, bolt.y - 8f * sc, ic + 16f * sc, ic + 16f * sc);
        NocturneStyle.Glow(halo, new Color(acc.r, acc.g, acc.b, 0.3f * pulse * fg));
        NocturneIcons.Draw(NocturneIcon.Bolt, bolt, new Color(acc.r, acc.g, acc.b, fg));

        GUI.color = new Color(1f, 1f, 1f, fg);
        GUI.Label(new Rect(wx, hy, _tw + 2f, hh), Titles[lite ? 0 : (int)(now * 12f) % Steps], _ttl);

        float ly = hy + hh + 1f * sc;
        var line = new Rect(box.x + ins, ly, box.width - ins * 2f, 1f);
        GUI.color = new Color(acc.r, acc.g, acc.b, 0.5f * fg);
        GUI.Box(line, GUIContent.none, _line);
        if (!lite)
        {
            float u = Mathf.Repeat(now * 12f - 7f, Steps) / Steps;
            float gx0 = Mathf.Max(line.x, wx + u * _tw - _tw * 0.17f);
            float gx1 = Mathf.Min(line.xMax, wx + u * _tw + _tw * 0.17f);
            if (gx1 - gx0 > 2f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f * MathF.Sin(u * MathF.PI) * fg);
                GUI.Box(new Rect(gx0, ly, gx1 - gx0, 1f), GUIContent.none, _line);
            }
        }

        float pw = (_on > 9 ? 36f : 30f) * sc;
        float pt = 16f * sc;
        float dot = Mathf.Max(4f, Mathf.Round(6f * sc));
        var pill = new Rect(right ? box.x + ins : box.xMax - ins - pw, hy + (hh - pt) * 0.5f, pw, pt);
        GUI.color = prev;
        var led = new Rect(pill.x + 8f * sc, pill.y + (pt - dot) * 0.5f, dot, dot);
        NocturneStyle.FillRounded(pill, new Color(acc.r, acc.g, acc.b, 0.16f * fg), Mathf.RoundToInt(pt * 0.5f));
        NocturneStyle.FillRounded(led, new Color(acc.r, acc.g, acc.b, (0.55f + 0.45f * pulse) * fg), Mathf.RoundToInt(dot * 0.5f));
        Color soft = Color.Lerp(acc, Color.white, 0.4f);
        GUI.color = new Color(soft.r, soft.g, soft.b, fg);
        GUI.Label(new Rect(pill.x + 14f * sc, pill.y, pw - 18f * sc, pt), Num(_on), _pill);

        GUI.color = prev;
        if (menu)
        {
            float gx = right ? pill.xMax + 7f * sc : pill.x - 14f * sc;
            float gy = hy + (hh - 6f * sc) * 0.5f;
            for (int c = 0; c < 3; c++)
                for (int r = 0; r < 2; r++)
                    NocturneStyle.Fill(new Rect(gx + c * 4f * sc, gy + r * 4f * sc, 2f * sc, 2f * sc), new Color(1f, 1f, 1f, 0.38f * fg));
        }

        float top = box.y + (PadY + Head + 6f) * sc;
        float rh = Row * sc;
        float lab = 13f * sc;
        float inner = box.width - ins * 2f - lab;
        GUIStyle st = right ? _txtR : _txt;
        for (int i = 0; i < _n && i < MaxRows; i++)
        {
            Entry e = _ord[i];
            float a = Mathf.Clamp01(e.Alpha);
            if (a <= 0.01f) continue;

            float k = a * a * (3f - 2f * a);
            float ry = Mathf.Round(top + e.Slot * rh);
            float dx = (1f - k) * 12f * sc * (right ? 1f : -1f);

            GUI.color = prev;
            if (e.Flash > 0.01f)
            {
                var wash = new Rect(box.x + ins - 6f * sc, ry + 1f, box.width - ins * 2f + 12f * sc, rh - 2f);
                NocturneStyle.FillRounded(wash, new Color(e.Col.r, e.Col.g, e.Col.b, 0.22f * e.Flash * fg), 6);
            }

            float ds = Mathf.Round(dot * (1f + 0.6f * e.Flash));
            float cx = right ? box.xMax - ins - dot * 0.5f + dx : box.x + ins + dot * 0.5f + dx;
            var mark = new Rect(Mathf.Round(cx - ds * 0.5f), Mathf.Round(ry + (rh - ds) * 0.5f), ds, ds);
            NocturneStyle.FillRounded(mark, new Color(e.Col.r, e.Col.g, e.Col.b, k * fg), Mathf.RoundToInt(ds * 0.5f));

            GUI.color = new Color(e.Tx.r, e.Tx.g, e.Tx.b, k * fg);
            GUI.Label(new Rect(right ? box.x + ins + dx : box.x + ins + lab + dx, ry, inner, rh), e.Content, st);
        }

        if (_more > 0)
        {
            GUI.color = new Color(0.70f, 0.72f, 0.76f, fg);
            GUI.Label(new Rect(right ? box.x + ins : box.x + ins + lab, top + MaxRows * rh, inner, rh), More(_more), st);
        }
        else if (_on == 0 && menu)
        {
            GUI.color = new Color(0.62f, 0.64f, 0.68f, fg);
            GUI.Label(new Rect(right ? box.x + ins : box.x + ins + lab, top, inner, rh), _idle, st);
        }
        GUI.color = prev;
    }

    private static void Palette(Color acc)
    {
        _tAcc = acc;
        _tRu = _ru;
        _ready = true;

        Color.RGBToHSV(acc, out float h, out float s, out float v);
        if (s > 0.25f) s = Mathf.Max(s, 0.72f);
        v = Mathf.Max(v, 0.88f);

        for (int i = 0; i < Steps; i++)
        {
            float w = 0.5f + 0.5f * MathF.Sin(i * MathF.PI * 2f / Steps);
            float spark = w * w * w * w * w * w;
            float deep = (1f - w) * (1f - w) * (1f - w);
            float hue = Mathf.Repeat(h + (w - 0.5f) * 0.14f, 1f);
            float sat = Mathf.Lerp(Mathf.Lerp(s, 1f, deep * 0.35f), s * 0.5f, spark);
            float val = Mathf.Lerp(Mathf.Lerp(v, v * 0.86f, deep), 1f, spark);
            Shades[i] = Hex(Color.HSVToRGB(hue, Mathf.Clamp01(sat), Mathf.Clamp01(val)));
        }

        for (int p = 0; p < Steps; p++)
        {
            Sb.Clear();
            for (int c = 0; c < _title.Length; c++)
            {
                if (_title[c] == ' ')
                {
                    Sb.Append(' ');
                    continue;
                }
                Sb.Append("<color=#").Append(Shades[((p - c * 2) % Steps + Steps) % Steps]).Append('>').Append(_title[c]).Append("</color>");
            }
            if (Titles[p] == null) Titles[p] = new GUIContent();
            Titles[p].text = Sb.ToString();
        }
    }

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }

    private static GUIStyle Gloss(int rad)
    {
        rad = Mathf.Clamp(rad, 2, 40);
        GUIStyle s = Glosses[rad];
        if (s != null) return s;

        int w = rad * 2 + 2;
        int h = rad * 2 + 24;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float fade = y < rad ? 1f : y >= h - rad ? 0f : 1f - (y - rad + 0.5f) / (h - rad * 2);
            float dy = y < rad ? rad - y - 0.5f : 0f;
            for (int x = 0; x < w; x++)
            {
                float dx = x < rad ? rad - x - 0.5f : x >= w - rad ? x + 0.5f - (w - rad) : 0f;
                float cover = dx > 0f && dy > 0f ? Mathf.Clamp01(rad - MathF.Sqrt(dx * dx + dy * dy) + 0.5f) : 1f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * cover * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply();

        s = new GUIStyle();
        s.normal.background = t;
        s.border = NocturneStyle.Offset(rad, rad, rad, rad);
        return Glosses[rad] = s;
    }

    private static Texture2D Bump(int w)
    {
        var t = new Texture2D(w, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[w];
        for (int i = 0; i < w; i++)
        {
            float s = MathF.Sin((i + 0.5f) / w * MathF.PI);
            px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(MathF.Pow(s, 0.8f) * 255f));
        }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }

    private static void Restyle()
    {
        if (_txt == null)
        {
            _txt = Make(FontStyle.Normal, TextAnchor.MiddleLeft);
            _txtR = Make(FontStyle.Normal, TextAnchor.MiddleRight);
            _ttl = Make(FontStyle.Bold, TextAnchor.MiddleLeft);
            _ttl.richText = true;
            _pill = Make(FontStyle.Bold, TextAnchor.MiddleCenter);
            _line = new GUIStyle();
            _line.normal.background = Bump(64);
            _idle = new GUIContent();
        }

        int size = Mathf.RoundToInt(13f * _sc);
        _txt.fontSize = size;
        _txtR.fontSize = size;
        size = Mathf.RoundToInt(11f * _sc);
        _ttl.fontSize = size;
        _pill.fontSize = size;

        _title = NocturneText.T("АКТИВНЫЕ ФУНКЦИИ", "ACTIVE FUNCTIONS");
        _idle.text = NocturneText.T("всё выключено", "all off");
        _tw = Measure(_ttl, _title);
    }

    private static GUIStyle Make(FontStyle fs, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontStyle = fs,
            alignment = anchor,
            wordWrap = false,
            clipping = TextClipping.Clip,
        };
        s.normal.textColor = Color.white;
        s.padding = NocturneStyle.Offset(0, 0, 0, 0);
        return s;
    }

    private static float Measure(GUIStyle st, string s)
    {
        Probe.text = s;
        return st.CalcSize(Probe).x;
    }

    private static GUIContent Num(int n)
    {
        n = Mathf.Clamp(n, 0, 99);
        return Nums[n] ??= new GUIContent(n.ToString());
    }

    private static GUIContent More(int n)
    {
        n = Mathf.Clamp(n, 0, 99);
        return Plus[n] ??= new GUIContent("+" + n);
    }

    private static Color Tint(string section)
    {
        switch (section)
        {
            case "Visual":
                return new Color(0.36f, 0.78f, 1f);
            case "Player":
            case "Cheats":
                return new Color(1f, 0.69f, 0.25f);
            case "Buffs":
                return new Color(1f, 0.40f, 0.44f);
            case "Host":
            case "AutoHost":
                return new Color(1f, 0.84f, 0.35f);
            case "Guard":
                return new Color(0.33f, 0.84f, 0.53f);
            case "Spoof":
            case "Account":
            case "Advanced":
            case "Privacy":
                return new Color(0.69f, 0.52f, 1f);
            case "Lobby":
            case "Cosmetics":
                return new Color(1f, 0.50f, 0.77f);
            default:
                return new Color(0.80f, 0.83f, 0.88f);
        }
    }

    private static void Add(List<Entry> list, string ru, string en, params ConfigEntry<bool>[] cfg)
    {
        if ((bool)cfg[0].DefaultValue) return;
        Color col = Tint(cfg[0].Definition.Section);
        list.Add(new Entry { Cfg = cfg, Ru = ru, En = en, Col = col, Tx = Color.Lerp(new Color(0.95f, 0.95f, 0.94f), col, 0.22f) });
    }

    private static void Build()
    {
        var list = new List<Entry>(128);
        foreach (QuickItem q in NocturneQuick.Items)
            Add(list, q.Ru, q.En, q.Cfg);

        Add(list, "Таймер Гадюки", "Viper timer", NocturneConfig.ViperTimer);
        Add(list, "Видеть Фантомов", "See phantoms", NocturneConfig.SeePhantoms);
        Add(list, "Видеть щиты", "See shields", NocturneConfig.SeeProtections);
        Add(list, "Обход связи", "Comms bypass", NocturneConfig.CommsBypass);
        Add(list, "Чат над головой", "Overhead chat", NocturneConfig.OverheadChat);
        Add(list, "Наклон мира", "World tilt", NocturneConfig.WorldTilt);
        Add(list, "Классический вид", "Classic look", NocturneConfig.ClassicBody);
        Add(list, "Скольжение", "Glide walk", NocturneConfig.WalkNoAnim);
        Add(list, "Муравей", "Ant walk", NocturneConfig.AntWalk);
        Add(list, "Перетаскивание себя", "Self drag", NocturneConfig.SelfDrag);
        Add(list, "Авто-задания", "Auto tasks", NocturneConfig.AutoTasks);
        Add(list, "Авто-починка саботажа", "Auto-fix sabotage", NocturneConfig.SabAutoFix);
        Add(list, "Задания издалека", "Far tasks", NocturneConfig.ConsoleReach);
        Add(list, "Без деконтаминации", "Skip decon", NocturneConfig.SkipDecon);
        Add(list, "Иммунитет к грибам", "Mushroom immunity", NocturneConfig.MushroomImmune);
        Add(list, "Ники при грибах", "Mixup names", NocturneConfig.MixupNames);
        Add(list, "Ранний голос", "Early vote", NocturneConfig.EarlyVote);
        Add(list, "Слив таймера прятек", "H&S timer drain", NocturneConfig.HnsDrain);
        Add(list, "Авто вент-ТП", "Auto vent TP", NocturneConfig.VentTpAuto);
        Add(list, "Ловушка предов", "Impostor trap", NocturneConfig.ImpTrap);
        Add(list, "Щит всем", "Shield everyone", NocturneConfig.ShieldAll);
        Add(list, "Тихий Фантом", "Silent phantom", NocturneConfig.PhantomNoVanish);
        Add(list, "Иммунитет к голосам", "Vote immunity", NocturneConfig.VoteImmune);
        Add(list, "Без репортов", "No reports", NocturneConfig.NoReports);
        Add(list, "Глушилка камер", "Camera jam", NocturneConfig.CameraJam);
        Add(list, "Кастом сервер", "Custom server", NocturneConfig.CustomServerEnabled);
        Add(list, "Спам голосов", "Vote spam", NocturneConfig.VoteSpam);
        Add(list, "Спам дыма", "Smoke spam", NocturneConfig.SmokeSpam);
        Add(list, "Цвет морфа", "Morph color", NocturneConfig.FakeMorphColor);
        Add(list, "Вечные грибы", "Endless mushrooms", NocturneConfig.SabInfMushroom);
        Add(list, "Кастом сикеры", "Custom seekers", NocturneConfig.HideAndSeekTwoSeekers);
        Add(list, "Пред без форы", "Seeker no head start", NocturneConfig.SeekerInstantStart);
        Add(list, "Одинаковые цвета", "Duplicate colors", NocturneConfig.AllowDuplicateColors);
        Add(list, "Один цвет всем", "One color for all", NocturneConfig.ColorAll);
        Add(list, "Перехват цвета", "Color snipe", NocturneConfig.SnipeColor);
        Add(list, "4 импостера", "4 impostors", NocturneConfig.FourImpostors);
        Add(list, "Снятые лимиты", "Unlocked limits", NocturneConfig.LooseHostOptions);
        Add(list, "Клоны по ЛКМ", "Clone mode", NocturneConfig.LobbyCloneMode);
        Add(list, "Сетевые клоны", "Net clones", NocturneConfig.NetCloneMode);
        Add(list, "Спуф ID устройства", "Spoof device ID", NocturneConfig.SpoofDeviceId);
        Add(list, "Спуф френд-кода", "Spoof friend code", NocturneConfig.SpoofFriendCodeEnabled);
        Add(list, "Чат без лимита длины", "Unlimited chat length", NocturneConfig.UnlimitedChatLength);
        Add(list, "Чат без задержки", "No chat cooldown", NocturneConfig.SkipChatCooldown);
        Add(list, "Без кулдаунов", "No cooldowns", NocturneConfig.BuffNoCd);
        Add(list, "Вент любому", "Vent any role", NocturneConfig.BuffVentAny);
        Add(list, "Ходьба в венте", "Vent walk", NocturneConfig.BuffVentWalk);
        Add(list, "Вент-сеть", "Vent network", NocturneConfig.VentNetwork);
        Add(list, "Дальность киллов", "Kill reach", NocturneConfig.BuffKillReach);
        Add(list, "Килл любого", "Kill anyone", NocturneConfig.BuffKillAny);
        Add(list, "Килл-аура", "Kill aura", NocturneConfig.BuffKillAura);
        Add(list, "Вент после килла", "Vent after kill", NocturneConfig.AutoVentKill);
        Add(list, "Тело в вент", "Body to vent", NocturneConfig.BodyToVent);
        Add(list, "Киллы без КД", "No kill cooldown", NocturneConfig.BuffNoKillCd);
        Add(list, "Килл в невидимости", "Kill while vanished", NocturneConfig.BuffVanishKill);
        Add(list, "Вечная невидимость", "Endless vanish", NocturneConfig.BuffPhVanish);
        Add(list, "Саботаж из вента", "Sabotage in vent", NocturneConfig.BuffVentSab);
        Add(list, "Вечный морф", "Endless shapeshift", NocturneConfig.BuffSsForever);
        Add(list, "Мульти-саботаж", "Multi-sabotage", NocturneConfig.MultiSabotage);

        Add(list, "Бафы ролей", "Role buffs",
            NocturneConfig.BuffEngVent, NocturneConfig.BuffEngCd, NocturneConfig.BuffSciBat, NocturneConfig.BuffSciCd,
            NocturneConfig.BuffDetReach, NocturneConfig.BuffDetCd, NocturneConfig.BuffTrackReach, NocturneConfig.BuffTrackCd,
            NocturneConfig.BuffTrackTime, NocturneConfig.BuffTrackLive, NocturneConfig.BuffSgFresh, NocturneConfig.BuffSgComms,
            NocturneConfig.BuffSgReach, NocturneConfig.BuffJudgeNoTasks, NocturneConfig.BuffImpTasks, NocturneConfig.BuffMapCd,
            NocturneConfig.BuffSsQuiet, NocturneConfig.BuffSsDead, NocturneConfig.BuffAutoReport);
        Add(list, "Блок саботажей", "Sabotage blocks",
            NocturneConfig.BlockLights, NocturneConfig.BlockReactor, NocturneConfig.BlockLaboratory, NocturneConfig.BlockOxygen,
            NocturneConfig.BlockComms, NocturneConfig.BlockHeli, NocturneConfig.BlockMushroom);
        Add(list, "Защита и фильтры", "Guard extras",
            NocturneConfig.VentTpProtect, NocturneConfig.ZiplineProtect,
            NocturneConfig.BlockFakeMeetings,
            NocturneConfig.AntiBanHost, NocturneConfig.AccessWhitelistOnly, NocturneConfig.AccessNickBanEnabled,
            NocturneConfig.AccessPlatformBanEnabled, NocturneConfig.MinLevelEnabled, NocturneConfig.MaxLevelEnabled,
            NocturneConfig.ColorReservationsEnabled);

        _all = list.ToArray();
        _ord = new Entry[_all.Length];
    }
}
