using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal sealed class PlayerList
{
    internal const float W = 300f;
    private const float Row = 38f;
    private const float Top = 48f;

    internal float Now;
    internal float Dt = 1f / 60f;
    internal float Alpha = 1f;
    internal int Current = -1;
    internal bool Playing;
    internal Color Accent;
    internal Color Bg;
    internal List<string> Names;
    internal List<float> Times;
    internal Vector2 Scroll;
    internal string Title = string.Empty;
    internal string Empty = string.Empty;

    internal int Pick = -1;
    internal bool Close, Refresh;

    private readonly float[] _hov = new float[2];
    private readonly GUIContent _probe = new GUIContent();
    private string[] _nums = new string[0];
    private string[] _names = new string[0];
    private string[] _times = new string[0];
    private float[] _timeFor = new float[0];
    private string[] _namesFor = new string[0];
    private float[] _wide = new float[0];
    private bool[] _wideOn = new bool[0];

    internal static float HeightFor(int count) => Top + (count == 0 ? 74f : Math.Min(count * Row, 304f) + 10f);

    internal void Draw()
    {
        PlayerGfx.Ensure();
        Pick = -1;
        Close = Refresh = false;

        Event ev = Event.current;
        bool paint = ev.type == EventType.Repaint;
        Vector2 mouse = ev.mousePosition;
        float a = Alpha;
        int n = Names.Count;
        Fit(n);

        if (paint)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.70f * a);
            GUI.Label(Ui.R(18f, 12f, W - 110f, 24f), Title, PlayerGfx.Head);
        }

        Rect re = Ui.R(W - 78f, 10f, 28f, 28f);
        Rect cl = Ui.R(W - 42f, 10f, 28f, 28f);
        Button(re, NocturneIcon.Refresh, 0, mouse, paint);
        Button(cl, NocturneIcon.Close, 1, mouse, paint);
        if (GUI.Button(re, PlayerGfx.Blank, PlayerGfx.None))
            Refresh = true;
        if (GUI.Button(cl, PlayerGfx.Blank, PlayerGfx.None))
            Close = true;

        if (n == 0)
        {
            if (paint)
            {
                GUI.color = Ui.C(1f, 1f, 1f, 0.55f * a);
                GUI.Label(Ui.R(18f, Top + 8f, W - 36f, 40f), Empty, PlayerGfx.Line);
            }
        }
        else
            Rows(n, paint, a);

        GUI.DragWindow(Ui.R(0f, 0f, W - 84f, Top));
    }

    private void Rows(int n, bool paint, float a)
    {
        Event ev = Event.current;
        float full = n * Row;
        float view = Math.Min(full, 304f);
        Rect area = Ui.R(0f, Top, W, view);
        if (ev.type == EventType.ScrollWheel && Ui.In(area, ev.mousePosition))
        {
            Scroll.y += ev.delta.y * 24f;
            ev.Use();
        }
        Scroll.y = Ui.Clamp(Scroll.y, 0f, Ui.Max(0f, full - view));

        GUI.BeginGroup(area);
        Vector2 mouse = ev.mousePosition;
        for (int i = 0; i < n; i++)
        {
            float y = i * Row - Scroll.y;
            if (y + Row < 0f || y > view)
                continue;
            Rect r = Ui.R(10f, y + 1f, W - 20f, Row - 2f);
            float top = Ui.Max(r.m_YMin, 0f);
            Rect hit = Ui.R(r.m_XMin, top, r.m_Width, Ui.Min(Ui.Bottom(r), view) - top);
            if (paint)
                Item(i, r, Ui.In(hit, mouse), a);
            if (GUI.Button(hit, PlayerGfx.Blank, PlayerGfx.None))
                Pick = i;
        }
        GUI.EndGroup();

        if (paint && full > view)
        {
            float len = Ui.Max(24f, view * view / full);
            GUI.color = Ui.C(1f, 1f, 1f, 0.22f * a);
            GUI.Box(Ui.R(W - 6f, Top + 4f + (view - 8f - len) * Scroll.y / (full - view), 3f, len), PlayerGfx.Blank, PlayerGfx.Pill);
        }
    }

    private void Item(int i, Rect r, bool over, float a)
    {
        bool on = i == Current;
        if (on)
        {
            GUI.color = Ui.C(Accent.r, Accent.g, Accent.b, 0.22f * a);
            GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
        }
        else if (over)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.07f * a);
            GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
        }

        float cy = r.m_YMin + r.m_Height * 0.5f;
        if (on && Playing)
        {
            GUI.color = Ui.C(Accent.r, Accent.g, Accent.b, a);
            for (int k = 0; k < 3; k++)
            {
                float h = 4f + 9f * (0.5f + 0.5f * Ui.Sin(Now * (5f + k * 1.7f) + k * 1.3f));
                GUI.Box(Ui.R(r.m_XMin + 12f + k * 5f, cy + 6f - h, 3f, h), PlayerGfx.Blank, PlayerGfx.Flat);
            }
        }
        else if (on)
        {
            GUI.color = Ui.C(Accent.r, Accent.g, Accent.b, a);
            GUI.Box(Ui.R(r.m_XMin + 10f, cy - 8f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Play));
        }
        else
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.38f * a);
            GUI.Label(Ui.R(r.m_XMin + 4f, r.m_YMin, 26f, r.m_Height), _nums[i], PlayerGfx.Tiny);
        }

        GUI.color = Ui.C(1f, 1f, 1f, (on ? 1f : 0.82f) * a);
        Rect lab = Ui.R(r.m_XMin + 34f, r.m_YMin, r.m_Width - 34f - 52f, r.m_Height);
        GUIStyle st = on ? PlayerGfx.RowOn : PlayerGfx.Row;
        float wide = Wide(i, on, st);
        if (wide <= lab.m_Width)
            GUI.Label(lab, _names[i], st);
        else
        {
            GUI.BeginGroup(lab);
            GUI.Label(Ui.R(-Slide(wide - lab.m_Width + 6f, i), 0f, wide + 8f, lab.m_Height), _names[i], st);
            GUI.EndGroup();
        }
        GUI.color = Ui.C(1f, 1f, 1f, 0.45f * a);
        GUI.Label(Ui.R(Ui.Right(r) - 50f, r.m_YMin, 44f, r.m_Height), _times[i], PlayerGfx.TinyRight);
    }

    private float Wide(int i, bool on, GUIStyle st)
    {
        if (_wide[i] <= 0f || _wideOn[i] != on)
        {
            _probe.text = _names[i];
            _wide[i] = st.CalcSize(_probe).x;
            _wideOn[i] = on;
        }
        return _wide[i];
    }

    private float Slide(float over, int i)
    {
        float travel = over / 36f;
        float t = (Now + i * 0.61f) % (travel + 3.2f);
        if (t < 1.6f)
            return 0f;
        if (t < 1.6f + travel)
            return (t - 1.6f) * 36f;
        t -= 1.6f + travel;
        if (t < 1.2f)
            return over;
        float k = Ui.Clamp01((t - 1.2f) / 0.4f);
        return over * (1f - k * k * (3f - 2f * k));
    }

    private void Fit(int n)
    {
        if (_nums.Length < n)
        {
            int old = _nums.Length;
            Array.Resize(ref _nums, n);
            Array.Resize(ref _names, n);
            Array.Resize(ref _times, n);
            Array.Resize(ref _timeFor, n);
            Array.Resize(ref _namesFor, n);
            Array.Resize(ref _wide, n);
            Array.Resize(ref _wideOn, n);
            for (int i = old; i < n; i++)
            {
                _nums[i] = (i + 1).ToString("D2");
                _times[i] = "--:--";
                _timeFor[i] = -1f;
            }
        }
        for (int i = 0; i < n; i++)
        {
            string raw = Names[i];
            if (!ReferenceEquals(raw, _namesFor[i]))
            {
                _namesFor[i] = raw;
                _names[i] = PlayerGfx.Clean(raw);
                _wide[i] = 0f;
            }
            float t = i < Times.Count ? Times[i] : 0f;
            if (t != _timeFor[i])
            {
                _timeFor[i] = t;
                int s = (int)t;
                _times[i] = t > 0f ? (s / 60) + ":" + (s % 60).ToString("D2") : "--:--";
            }
        }
    }

    private void Button(Rect r, NocturneIcon icon, int slot, Vector2 mouse, bool paint)
    {
        if (!paint)
            return;
        float hv = Ui.MoveTowards(_hov[slot], Ui.In(r, mouse) ? 1f : 0f, Dt * 8f);
        _hov[slot] = hv;
        Vector2 c = Ui.Mid(r);
        GUI.color = Ui.C(1f, 1f, 1f, (0.08f + 0.10f * hv) * Alpha);
        GUI.Box(Ui.R(c.x - 14f, c.y - 14f, 28f, 28f), PlayerGfx.Blank, PlayerGfx.Dot);
        GUI.color = Ui.C(1f, 1f, 1f, (0.75f + 0.25f * hv) * Alpha);
        GUI.Box(Ui.R(c.x - 8f, c.y - 8f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(icon));
    }
}

internal sealed class PlayerSearch
{
    internal const float W = 300f;
    private const float Top = 48f;
    private const float Row = 30f;
    private const int Limit = 60;

    internal float Now;
    internal float Dt = 1f / 60f;
    internal float Alpha = 1f;
    internal Color Accent;
    internal string Title = string.Empty;
    internal string Placeholder = string.Empty;
    internal string Go = string.Empty;
    internal string Recents = string.Empty;
    internal bool Ready;
    internal List<string> Recent;
    internal string Text = string.Empty;
    internal bool Focus;

    internal int Pick = -1;
    internal bool Close, Submit;

    private readonly float[] _hov = new float[3];
    private readonly GUIContent _probe = new GUIContent();
    private string _probeFor;
    private float _probeW;

    internal static float HeightFor(int recents) => recents == 0 ? 144f : 164f + recents * Row;

    private static bool Filled(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsWhiteSpace(s[i]))
                return true;
        }
        return false;
    }

    internal void Draw()
    {
        PlayerGfx.Ensure();
        Pick = -1;
        Close = Submit = false;

        Event ev = Event.current;
        bool paint = ev.type == EventType.Repaint;
        Vector2 mouse = ev.mousePosition;
        float a = Alpha;
        int n = Recent.Count;

        Rect cl = Ui.R(W - 42f, 10f, 28f, 28f);
        Rect fld = Ui.R(14f, Top + 2f, W - 28f, 34f);
        Rect go = Ui.R(14f, Top + 46f, W - 28f, 34f);

        if (ev.type == EventType.MouseDown && ev.button == 0)
            Focus = Ui.In(fld, mouse);
        bool can = Ready && Filled(Text);

        if (paint)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.70f * a);
            GUI.Label(Ui.R(18f, 12f, W - 70f, 24f), Title, PlayerGfx.Head);
            CloseButton(cl, mouse);
            Field(fld, a);
            float hv = Ui.MoveTowards(_hov[1], can && Ui.In(go, mouse) ? 1f : 0f, Dt * 8f);
            _hov[1] = hv;
            GUI.color = Ui.C(Accent.r, Accent.g, Accent.b, (can ? 0.34f + 0.16f * hv : 0.12f) * a);
            GUI.Box(go, PlayerGfx.Blank, PlayerGfx.Round8);
            GUI.color = Ui.C(1f, 1f, 1f, (can ? 1f : 0.45f) * a);
            GUI.Label(Ui.R(go.m_XMin, go.m_YMin - 2f, go.m_Width, go.m_Height), Go, PlayerGfx.Press);
            if (n > 0)
            {
                GUI.color = Ui.C(1f, 1f, 1f, 0.38f * a);
                GUI.Label(Ui.R(18f, 134f, W - 36f, 18f), Recents, PlayerGfx.Tiny);
            }
        }

        if (GUI.Button(cl, PlayerGfx.Blank, PlayerGfx.None))
            Close = true;
        if (GUI.Button(go, PlayerGfx.Blank, PlayerGfx.None) && can)
            Submit = true;

        for (int i = 0; i < n; i++)
        {
            Rect r = Ui.R(10f, 154f + i * Row, W - 20f, Row - 2f);
            if (paint)
            {
                if (Ui.In(r, mouse))
                {
                    GUI.color = Ui.C(1f, 1f, 1f, 0.07f * a);
                    GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
                }
                GUI.color = Ui.C(1f, 1f, 1f, 0.40f * a);
                GUI.Box(Ui.R(r.m_XMin + 8f, r.m_YMin + (r.m_Height - 14f) * 0.5f, 14f, 14f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Search));
                GUI.color = Ui.C(1f, 1f, 1f, 0.82f * a);
                GUI.Label(Ui.R(r.m_XMin + 30f, r.m_YMin, r.m_Width - 38f, r.m_Height), Recent[i], PlayerGfx.Row);
            }
            if (GUI.Button(r, PlayerGfx.Blank, PlayerGfx.None))
                Pick = i;
        }

        GUI.DragWindow(Ui.R(0f, 0f, W - 48f, Top));
    }

    internal bool Type(Event ev)
    {
        if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
        {
            ev.Use();
            return Ready && Filled(Text);
        }
        if (ev.keyCode == KeyCode.Escape)
        {
            Focus = false;
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Backspace)
        {
            if (Text.Length > 0)
                Text = Text.Substring(0, Text.Length - 1);
            ev.Use();
        }
        else if ((ev.control || ev.command) && ev.keyCode == KeyCode.V)
        {
            try
            {
                string clip = GUIUtility.systemCopyBuffer ?? string.Empty;
                for (int i = 0; i < clip.Length && Text.Length < Limit; i++)
                {
                    if (!char.IsControl(clip[i]))
                        Text += clip[i];
                }
            }
            catch
            {
            }
            ev.Use();
        }
        else if (ev.character != '\0' && !char.IsControl(ev.character) && Text.Length < Limit)
        {
            Text += ev.character;
            ev.Use();
        }
        return false;
    }

    private void Field(Rect r, float a)
    {
        GUI.color = Ui.C(1f, 1f, 1f, (Focus ? 0.16f : 0.10f) * a);
        GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
        GUI.color = Ui.C(1f, 1f, 1f, 0.50f * a);
        GUI.Box(Ui.R(r.m_XMin + 10f, r.m_YMin + 9f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Search));

        if (!ReferenceEquals(Text, _probeFor))
        {
            _probeFor = Text;
            _probe.text = Text;
            _probeW = Text.Length == 0 ? 0f : PlayerGfx.Row.CalcSize(_probe).x;
        }
        Rect area = Ui.R(r.m_XMin + 34f, r.m_YMin, r.m_Width - 44f, r.m_Height);
        float dx = Ui.Max(0f, _probeW + 3f - area.m_Width);
        if (Text.Length == 0)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.35f * a);
            GUI.Label(Ui.R(area.m_XMin + 3f, area.m_YMin, area.m_Width - 3f, area.m_Height), Placeholder, PlayerGfx.Row);
            if (Focus && Now % 1f < 0.5f)
            {
                GUI.color = Ui.C(1f, 1f, 1f, 0.90f * a);
                GUI.Box(Ui.R(area.m_XMin + 1f, area.m_YMin + (area.m_Height - 16f) * 0.5f, 1.5f, 16f), PlayerGfx.Blank, PlayerGfx.Flat);
            }
            return;
        }
        GUI.color = Ui.C(1f, 1f, 1f, a);
        GUI.BeginGroup(area);
        GUI.Label(Ui.R(-dx, 0f, _probeW + 12f, area.m_Height), Text, PlayerGfx.Row);
        if (Focus && Now % 1f < 0.5f)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.90f * a);
            GUI.Box(Ui.R(_probeW + 1f - dx, (area.m_Height - 16f) * 0.5f, 1.5f, 16f), PlayerGfx.Blank, PlayerGfx.Flat);
        }
        GUI.EndGroup();
    }

    private void CloseButton(Rect r, Vector2 mouse)
    {
        float hv = Ui.MoveTowards(_hov[0], Ui.In(r, mouse) ? 1f : 0f, Dt * 8f);
        _hov[0] = hv;
        Vector2 c = Ui.Mid(r);
        GUI.color = Ui.C(1f, 1f, 1f, (0.08f + 0.10f * hv) * Alpha);
        GUI.Box(Ui.R(c.x - 14f, c.y - 14f, 28f, 28f), PlayerGfx.Blank, PlayerGfx.Dot);
        GUI.color = Ui.C(1f, 1f, 1f, (0.75f + 0.25f * hv) * Alpha);
        GUI.Box(Ui.R(c.x - 8f, c.y - 8f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Close));
    }
}

internal sealed class PlayerEq
{
    internal const float W = 300f;
    internal const float Tall = 600f;

    private const float BoxX = 12f;
    private const float BoxY = 52f;
    private const float BoxW = 276f;
    private const float BoxH = 96f;
    private const float Mid = 42f;
    private const float PerDb = 3f;
    private const int Cw = 276;
    private const int Ch = 96;

    internal float Dt = 1f / 60f;
    internal float Alpha = 1f;
    internal float[] Gains;
    internal int Preset;
    internal string[] Presets;
    internal string CustomText = string.Empty;
    internal Color Accent;
    internal (string Ru, string En, float Hue)[] Themes;
    internal string ThemeEn = string.Empty;
    internal Vector2 Scroll;
    internal string Title = string.Empty;
    internal string PresetsText = string.Empty;
    internal string ThemeText = string.Empty;
    internal string LoudText = string.Empty;
    internal string LoudInfo = string.Empty;
    internal string LoudA = string.Empty;
    internal string LoudB = string.Empty;
    internal string LoudC = string.Empty;
    internal int Loud;
    internal string FxText = string.Empty;
    internal string FxA = string.Empty;
    internal string FxB = string.Empty;
    internal int Fx;

    internal int PickPreset = -1;
    internal int PickTheme = -1;
    internal int PickLoud = -1;
    internal int PickFx = -1;
    internal int Band = -1;
    internal float BandGain;
    internal bool Commit;
    internal bool Close;

    private static readonly string[] Freq = { "32", "64", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };
    private readonly float[] _have = new float[PlayerChain.Bands];
    private readonly double[] _hz = new double[Cw];
    private readonly float[] _db = new float[Cw];
    private readonly float[] _yc = new float[Cw];
    private Texture2D _tex;
    private GUIStyle _style;
    private Color32[] _px;
    private Color _acc = Ui.C(-1f, 0f, 0f, 0f);
    private float _hov;
    private int _drag = -1;
    private float _tipGain = float.NaN;
    private string _tip = string.Empty;

    internal void Draw()
    {
        PlayerGfx.Ensure();
        PickPreset = PickTheme = PickLoud = PickFx = Band = -1;
        Close = Commit = false;

        Event ev = Event.current;
        bool paint = ev.type == EventType.Repaint;
        Vector2 mouse = ev.mousePosition;
        float a = Alpha;

        Rect cl = Ui.R(W - 42f, 10f, 28f, 28f);
        if (paint)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.70f * a);
            GUI.Label(Ui.R(18f, 12f, W - 70f, 24f), Title, PlayerGfx.Head);
            _hov = Ui.MoveTowards(_hov, Ui.In(cl, mouse) ? 1f : 0f, Dt * 8f);
            GUI.color = Ui.C(1f, 1f, 1f, (0.08f + 0.10f * _hov) * a);
            GUI.Box(Ui.R(cl.m_XMin, cl.m_YMin, 28f, 28f), PlayerGfx.Blank, PlayerGfx.Dot);
            GUI.color = Ui.C(1f, 1f, 1f, (0.75f + 0.25f * _hov) * a);
            GUI.Box(Ui.R(cl.m_XMin + 6f, cl.m_YMin + 6f, 16f, 16f), PlayerGfx.Blank, PlayerGfx.Icon(NocturneIcon.Close));
            Label(LoudText, 156f, a);
            Label(FxText, 232f, a);
            Label(PresetsText, 288f, a);
            Label(ThemeText, 508f, a);
        }
        if (GUI.Button(cl, PlayerGfx.Blank, PlayerGfx.None))
            Close = true;

        Plot(ev, mouse, paint, a);
        Loudness(paint, a);
        Effects(paint, a);
        Chips(paint, a);
        Swatches(paint, a);

        GUI.DragWindow(Ui.R(0f, 0f, W - 48f, 48f));
    }

    private static void Label(string text, float y, float a)
    {
        GUI.color = Ui.C(1f, 1f, 1f, 0.45f * a);
        GUI.Label(Ui.R(18f, y, W - 36f, 18f), text, PlayerGfx.Head);
    }

    private static float Bx(int b) => BoxX + BoxW * MathF.Log10(PlayerChain.Freqs[b] / 20f) / 3f;

    private static int Nearest(float x)
    {
        int best = 0;
        for (int b = 1; b < PlayerChain.Bands; b++)
            if (MathF.Abs(Bx(b) - x) < MathF.Abs(Bx(best) - x))
                best = b;
        return best;
    }

    private static float Snap(float y) => Ui.Clamp(MathF.Round((BoxY + Mid - y) / PerDb * 2f) * 0.5f, -12f, 12f);

    private void Plot(Event ev, Vector2 mouse, bool paint, float a)
    {
        Rect box = Ui.R(BoxX, BoxY, BoxW, BoxH);
        if (_drag >= 0 && !Input.GetMouseButton(0))
        {
            _drag = -1;
            Commit = true;
        }
        if (ev.type == EventType.MouseDown && ev.button == 0 && Ui.In(box, mouse))
        {
            int b = Nearest(mouse.x);
            Band = b;
            if (ev.clickCount == 2)
            {
                BandGain = 0f;
                Commit = true;
            }
            else
            {
                _drag = b;
                BandGain = Snap(mouse.y);
            }
            ev.Use();
        }
        else if (_drag >= 0 && ev.type == EventType.MouseDrag)
        {
            Band = _drag;
            BandGain = Snap(mouse.y);
            ev.Use();
        }
        else if (_drag >= 0 && ev.type == EventType.MouseUp)
        {
            _drag = -1;
            Commit = true;
            ev.Use();
        }

        if (!paint)
            return;
        Sync();
        GUI.color = Ui.C(1f, 1f, 1f, 0.05f * a);
        GUI.Box(box, PlayerGfx.Blank, PlayerGfx.Round8);
        GUI.color = Ui.C(1f, 1f, 1f, a);
        GUI.Box(box, PlayerGfx.Blank, _style);

        int near = _drag >= 0 ? _drag : Ui.In(box, mouse) ? Nearest(mouse.x) : -1;
        for (int b = 0; b < PlayerChain.Bands; b++)
        {
            float x = Bx(b);
            float y = BoxY + Mid - Ui.Clamp(Gains[b], -12f, 12f) * PerDb;
            if (b == near)
            {
                Dot(x, y, 26f, Accent, 0.30f * a);
                Dot(x, y, 14f, Ui.White, a);
                Dot(x, y, 8f, Accent, a);
            }
            else
            {
                Dot(x, y, 9f, Ui.White, 0.92f * a);
                Dot(x, y, 4f, Accent, 0.8f * a);
            }
            GUI.color = Ui.C(1f, 1f, 1f, (b == near ? 0.8f : 0.40f) * a);
            GUI.Label(Ui.R(x - 13f, BoxY + BoxH - 14f, 26f, 12f), Freq[b], PlayerGfx.Micro);
        }

        if (_drag >= 0)
        {
            float g = Gains[_drag];
            if (g != _tipGain)
            {
                _tipGain = g;
                _tip = g.ToString("+0.#;-0.#;0") + " dB";
            }
            float x = Ui.Clamp(Bx(_drag) - 28f, BoxX, BoxX + BoxW - 56f);
            float y = Ui.Max(BoxY + 2f, BoxY + Mid - Ui.Clamp(g, -12f, 12f) * PerDb - 34f);
            GUI.color = Ui.C(0f, 0f, 0f, 0.62f * a);
            GUI.Box(Ui.R(x, y, 56f, 20f), PlayerGfx.Blank, PlayerGfx.Round8);
            GUI.color = Ui.C(1f, 1f, 1f, a);
            GUI.Label(Ui.R(x, y, 56f, 20f), _tip, PlayerGfx.Chip);
        }
    }

    private static void Dot(float cx, float cy, float size, Color c, float alpha)
    {
        GUI.color = Ui.C(c.r, c.g, c.b, c.a * alpha);
        GUI.Box(Ui.R(cx - size * 0.5f, cy - size * 0.5f, size, size), PlayerGfx.Blank, PlayerGfx.Dot);
    }

    private void Sync()
    {
        bool moved = _tex == null || !Ui.Same(_acc, Accent);
        for (int b = 0; b < PlayerChain.Bands && !moved; b++)
            moved = _have[b] != Gains[b];
        if (!moved)
            return;
        Array.Copy(Gains, _have, PlayerChain.Bands);
        _acc = Accent;
        if (_tex == null)
        {
            _tex = new Texture2D(Cw, Ch, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _px = new Color32[Cw * Ch];
            _style = new GUIStyle(PlayerGfx.None);
            _style.normal.background = _tex;
            for (int x = 0; x < Cw; x++)
                _hz[x] = 20.0 * Math.Pow(10.0, 3.0 * x / (Cw - 1));
        }

        PlayerChain.Curve(Gains, 48000.0, _hz, _db);
        for (int x = 0; x < Cw; x++)
            _yc[x] = Mid - Ui.Clamp(_db[x], -14f, 14f) * PerDb;

        Color light = Ui.Mix(Accent, Ui.White, 0.35f);
        for (int row = 0; row < Ch; row++)
        {
            float py = row + 0.5f;
            float off = py - Mid;
            float step = MathF.Round(off / (6f * PerDb));
            float gh = MathF.Abs(step) <= 2f ? Ui.Clamp01(0.9f - MathF.Abs(off - step * 6f * PerDb)) * (step == 0f ? 0.16f : 0.07f) : 0f;
            for (int x = 0; x < Cw; x++)
            {
                float gv = 0f;
                for (int k = 0; k < 3; k++)
                    gv = Ui.Max(gv, Ui.Clamp01(0.9f - MathF.Abs(x + 0.5f - Cw * (k == 0 ? 0.2330f : k == 1 ? 0.5663f : 0.8997f))) * 0.06f);

                float yc = _yc[x];
                float slope = x > 0 && x < Cw - 1 ? (_yc[x + 1] - _yc[x - 1]) * 0.5f : 0f;
                float line = Ui.Clamp01(1.3f - MathF.Abs(py - yc) / MathF.Sqrt(1f + slope * slope));
                float fill = (py > yc && py < Mid) || (py < yc && py > Mid) ? 0.16f : 0f;

                float r = 1f;
                float g = 1f;
                float bl = 1f;
                float al = Ui.Max(gh, gv);
                Over(ref r, ref g, ref bl, ref al, Accent.r, Accent.g, Accent.b, fill);
                Over(ref r, ref g, ref bl, ref al, light.r, light.g, light.b, line);
                _px[(Ch - 1 - row) * Cw + x] = PlayerGfx.Px((byte)(r * 255f + 0.5f), (byte)(g * 255f + 0.5f), (byte)(bl * 255f + 0.5f), (byte)(al * 255f + 0.5f));
            }
        }
        PlayerGfx.Fill(_tex, _px);
    }

    private static void Over(ref float r, ref float g, ref float b, ref float a, float cr, float cg, float cb, float ca)
    {
        float oa = ca + a * (1f - ca);
        if (oa <= 0f)
            return;
        float keep = a * (1f - ca);
        r = (cr * ca + r * keep) / oa;
        g = (cg * ca + g * keep) / oa;
        b = (cb * ca + b * keep) / oa;
        a = oa;
    }

    private void Loudness(bool paint, float a)
    {
        Vector2 mouse = Event.current.mousePosition;
        for (int i = 0; i < 3; i++)
        {
            Rect r = Ui.R(12f + i * 94f, 176f, 88f, 28f);
            if (paint)
            {
                bool on = i == Loud;
                GUI.color = on ? Ui.C(Accent.r, Accent.g, Accent.b, 0.88f * a) : Ui.C(1f, 1f, 1f, (Ui.In(r, mouse) ? 0.13f : 0.07f) * a);
                GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
                GUI.color = Ui.C(1f, 1f, 1f, (on ? 1f : 0.80f) * a);
                GUI.Label(r, i == 0 ? LoudA : i == 1 ? LoudB : LoudC, PlayerGfx.Chip);
            }
            if (GUI.Button(r, PlayerGfx.Blank, PlayerGfx.None))
                PickLoud = i;
        }
        if (paint && LoudInfo.Length > 0)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.42f * a);
            GUI.Label(Ui.R(18f, 208f, W - 36f, 16f), LoudInfo, PlayerGfx.Tiny);
        }
    }

    private void Effects(bool paint, float a)
    {
        Vector2 mouse = Event.current.mousePosition;
        for (int i = 0; i < 2; i++)
        {
            Rect r = Ui.R(12f + i * 140f, 252f, 136f, 28f);
            if (paint)
            {
                bool on = (Fx & (1 << i)) != 0;
                GUI.color = on ? Ui.C(Accent.r, Accent.g, Accent.b, 0.88f * a) : Ui.C(1f, 1f, 1f, (Ui.In(r, mouse) ? 0.13f : 0.07f) * a);
                GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
                GUI.color = Ui.C(1f, 1f, 1f, (on ? 1f : 0.80f) * a);
                GUI.Label(r, i == 0 ? FxA : FxB, PlayerGfx.Chip);
            }
            if (GUI.Button(r, PlayerGfx.Blank, PlayerGfx.None))
                PickFx = i;
        }
    }

    private void Chips(bool paint, float a)
    {
        Event ev = Event.current;
        int n = Presets.Length;
        float full = (n + 1) / 2 * 34f + 4f;
        const float view = 190f;
        Rect area = Ui.R(0f, 310f, W, view);
        if (ev.type == EventType.ScrollWheel && Ui.In(area, ev.mousePosition))
        {
            Scroll.y += ev.delta.y * 24f;
            ev.Use();
        }
        Scroll.y = Ui.Clamp(Scroll.y, 0f, Ui.Max(0f, full - view));

        GUI.BeginGroup(area);
        Vector2 mouse = ev.mousePosition;
        for (int i = 0; i < n; i++)
        {
            float y = 2f + i / 2 * 34f - Scroll.y;
            if (y + 28f < 0f || y > view)
                continue;
            Rect r = Ui.R(12f + (i % 2) * 140f, y, 136f, 28f);
            float top = Ui.Max(y, 0f);
            Rect hit = Ui.R(r.m_XMin, top, 136f, Ui.Min(y + 28f, view) - top);
            if (paint)
            {
                bool on = i == Preset;
                GUI.color = on ? Ui.C(Accent.r, Accent.g, Accent.b, 0.88f * a) : Ui.C(1f, 1f, 1f, (Ui.In(hit, mouse) ? 0.13f : 0.07f) * a);
                GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Round8);
                GUI.color = Ui.C(1f, 1f, 1f, (on ? 1f : 0.80f) * a);
                GUI.Label(Ui.R(r.m_XMin + 10f, y, r.m_Width - 14f, 28f), i == n - 1 ? CustomText : Presets[i], PlayerGfx.Tiny);
            }
            if (GUI.Button(hit, PlayerGfx.Blank, PlayerGfx.None))
                PickPreset = i;
        }
        GUI.EndGroup();

        if (paint && full > view)
        {
            float len = Ui.Max(24f, view * view / full);
            GUI.color = Ui.C(1f, 1f, 1f, 0.22f * a);
            GUI.Box(Ui.R(W - 6f, 314f + (view - 8f - len) * Scroll.y / (full - view), 3f, len), PlayerGfx.Blank, PlayerGfx.Pill);
        }
    }

    private void Swatches(bool paint, float a)
    {
        int n = Themes.Length;
        for (int i = 0; i < n; i++)
        {
            float x = 19f + (i % 9) * 30f;
            float y = 532f + (i / 9) * 32f;
            Rect r = Ui.R(x, y, 24f, 24f);
            if (paint)
            {
                bool on = string.Equals(Themes[i].En, ThemeEn, StringComparison.OrdinalIgnoreCase);
                if (on)
                {
                    GUI.color = Ui.C(1f, 1f, 1f, 0.95f * a);
                    GUI.Box(Ui.R(x - 3f, y - 3f, 30f, 30f), PlayerGfx.Blank, PlayerGfx.Dot);
                    GUI.color = Ui.C(0.07f, 0.05f, 0.08f, a);
                    GUI.Box(Ui.R(x - 1f, y - 1f, 26f, 26f), PlayerGfx.Blank, PlayerGfx.Dot);
                }
                Color c = PlayerGfx.FromHsv(Themes[i].Hue, 0.80f, 0.95f);
                GUI.color = Ui.C(c.r, c.g, c.b, a);
                GUI.Box(r, PlayerGfx.Blank, PlayerGfx.Dot);
            }
            if (GUI.Button(r, PlayerGfx.Blank, PlayerGfx.None))
                PickTheme = i;
        }
    }
}
