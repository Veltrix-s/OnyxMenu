using System;
using AmongUs.Data;
using UnityEngine;

namespace Nocturne;

internal static class FarmHud
{
    private sealed class Cell
    {
        internal readonly GUIContent Key = new GUIContent();
        internal readonly GUIContent Val = new GUIContent();
        internal Color Col = Bright;
        internal Color Tone;
        internal bool Meter;
        internal float To;
        internal float Fill;
        internal int A = int.MinValue;
        internal int B = int.MinValue;
    }

    private const int RLevel = 0;
    private const int RXp = 1;
    private const int RLast = 2;
    private const int RMatch = 3;
    private const int RKills = 4;
    private const int RPlayed = 5;

    private const float Width = 236f;
    private const float PadX = 12f;
    private const float PadY = 9f;
    private const float Head = 24f;
    private const float Row = 19f;
    private const float Gauge = 11f;
    private const float Split = 6f;

    private static readonly Color Muted = Ui.C(0.70f, 0.72f, 0.76f, 1f);
    private static readonly Color Bright = Ui.C(0.95f, 0.95f, 0.94f, 1f);
    private static readonly Color Good = Ui.C(0.33f, 0.84f, 0.53f, 1f);
    private static readonly Color Amber = Ui.C(1f, 0.84f, 0.35f, 1f);
    private static readonly Color Idle = Ui.C(0.50f, 0.52f, 0.56f, 1f);

    private static readonly Cell[] Cells = new Cell[6];
    private static readonly GUIContent[] Titles = new GUIContent[ActiveHud.Steps];
    private static readonly GUIContent[] States = new GUIContent[3];
    private static readonly GUIContent Gain = new GUIContent();

    private static GUIStyle _txt;
    private static GUIStyle _val;
    private static GUIStyle _ttl;
    private static GUIStyle _pill;
    private static GUIStyle _line;
    private static string _title;
    private static float _tw;
    private static float _pillW;
    private static float _gainW;
    private static int _gainN = int.MinValue;
    private static int _speedN;
    private static int _faultN;
    private static int _state;
    private static Color _tAcc;
    private static bool _tRu;
    private static bool _ready;

    private static int _frame = -1;
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
        if (!NocturneConfig.HudFarm.Value)
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
            _ax = NocturneConfig.HudFarmX.Value;
            _ay = NocturneConfig.HudFarmY.Value;
        }

        float ax = _ax;
        float ay = _ay;
        bool right;
        if (ax < 0f || ay < 0f)
        {
            if (ActiveHud.Shown(out Rect above, out right))
            {
                ax = right ? Ui.Right(above) : above.m_XMin;
                ay = Ui.Bottom(above) + 8f * _sc;
            }
            else
            {
                right = true;
                ax = Screen.width - 14f * _sc;
                ay = 168f * _sc;
            }
        }
        else
            right = ax > Screen.width * 0.5f;

        var box = Ui.R(
            Ui.Round(Ui.Clamp(right ? ax - _pw : ax, 0f, Ui.Max(0f, Screen.width - _pw))),
            Ui.Round(Ui.Clamp(ay, 0f, Ui.Max(0f, Screen.height - _ph))),
            _pw,
            _ph);

        _box = box;
        _seen = true;
        if (NocturneMenu.Opened)
            Patches.MouseShield.Cover(Reach(box));
        if (!NocturneStyle.Painting) return;

        _hov = NocturneMenu.Opened && Ui.In(Reach(box), NocturneStyle.Mouse);
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
            Save();
            return;
        }

        Event e = Event.current;
        if (e == null) return;

        if (NocturneStyle.Is(EventType.MouseDown) && Ui.In(Reach(_box), NocturneStyle.Mouse))
        {
            if (e.button == 0)
            {
                _drag = true;
                _off = NocturneStyle.Mouse - Ui.V(_box.m_XMin, _box.m_YMin);
                e.Use();
            }
            else if (e.button == 1)
            {
                NocturneConfig.HudFarmX.Value = -1f;
                NocturneConfig.HudFarmY.Value = -1f;
                e.Use();
            }
        }
        else if (_drag && NocturneStyle.Is(EventType.MouseDrag))
        {
            float nx = Ui.Clamp(NocturneStyle.Mouse.x - _off.x, 0f, Ui.Max(0f, Screen.width - _box.m_Width));
            float ny = Ui.Clamp(NocturneStyle.Mouse.y - _off.y, 0f, Ui.Max(0f, Screen.height - _box.m_Height));
            _ax = nx + _box.m_Width * 0.5f > Screen.width * 0.5f ? nx + _box.m_Width : nx;
            _ay = ny;
            e.Use();
        }
        else if (_drag && NocturneStyle.Is(EventType.MouseUp))
        {
            _drag = false;
            Save();
            e.Use();
        }
    }

    private static void Save()
    {
        NocturneConfig.HudFarmX.Value = _ax;
        NocturneConfig.HudFarmY.Value = _ay;
    }

    private static Rect Reach(Rect box)
    {
        float slop = Ui.Max(8f, 6f * _sc);
        float w = Ui.Max(box.m_Width + slop * 2f, 64f);
        float h = Ui.Max(box.m_Height + slop * 2f, 48f);
        return Ui.R(box.m_XMin + (box.m_Width - w) * 0.5f, box.m_YMin + (box.m_Height - h) * 0.5f, w, h);
    }

    private static void Step(bool want)
    {
        float dt = Ui.Min(Time.unscaledDeltaTime, 0.05f);
        float sc = Ui.Clamp(Screen.height / 1080f, 0.85f, 2.2f) * Ui.Clamp(NocturneConfig.HudActiveSize.Value, 60, 180) / 100f;
        bool ru = NocturneText.IsRussian;
        if (_txt == null || ru != _ru || Ui.Abs(sc - _sc) > 0.001f)
        {
            _sc = sc;
            _ru = ru;
            Restyle();
        }

        if (Time.unscaledTime >= _poll)
        {
            _poll = Time.unscaledTime + 0.12f;
            Poll();
        }

        float k = 1f - Ui.Exp(-9f * dt);
        for (int i = 0; i < Cells.Length; i++)
        {
            Cell c = Cells[i];
            if (c.Meter)
                c.Fill = Ui.Abs(c.To - c.Fill) < 0.002f ? c.To : c.Fill + (c.To - c.Fill) * k;
        }

        _pa = Ui.MoveTowards(_pa, want ? 1f : 0f, dt * 5f);
    }

    private static void Poll()
    {
        var st = DataManager.Player.Stats;
        int level = LevelFarm.Level();
        int stop = LevelFarm.StopAt;
        int xp = (int)st.Xp;
        int need = (int)st.XpForNextLevel;
        int gain = LevelFarm.LevelsGained;
        int last = LevelFarm.LastXp;
        int kills = LevelFarm.Kills;
        int goal = LevelFarm.Goal;

        Cell c = Cells[RLevel];
        if (c.A != level)
        {
            c.A = level;
            c.Val.text = level > 0 ? level.ToString() : "-";
        }
        if (c.B != stop)
        {
            c.B = stop;
            c.Key.text = LevelKey(stop);
        }

        if (_gainN != gain)
        {
            _gainN = gain;
            Gain.text = "+" + gain;
            _gainW = ActiveHud.Measure(_pill, Gain.text);
        }

        c = Cells[RXp];
        if (c.A != xp || c.B != need)
        {
            c.A = xp;
            c.B = need;
            c.Val.text = need > 0 ? Fmt(xp) + " / " + Fmt(need) : "-";
            c.To = need > 0 ? Ui.Clamp01((float)xp / need) : 0f;
        }

        c = Cells[RLast];
        if (c.A != last)
        {
            c.A = last;
            c.Val.text = last < 0 ? "-" : "+" + Fmt(last) + " XP";
            c.Col = last > 0 ? Good : Muted;
        }

        c = Cells[RMatch];
        if (c.A != kills || c.B != goal)
        {
            c.A = kills;
            c.B = goal;
            c.Val.text = Fmt(kills) + " / " + Fmt(goal);
            c.To = Ui.Clamp01((float)kills / goal);
        }

        c = Cells[RKills];
        if (c.A != LevelFarm.Total)
        {
            c.A = LevelFarm.Total;
            c.Val.text = Fmt(c.A);
        }

        c = Cells[RPlayed];
        if (c.A != LevelFarm.Matches)
        {
            c.A = LevelFarm.Matches;
            c.Val.text = c.A.ToString();
        }

        _state = LevelFarm.State();
        int speed = _state == 2 ? LevelFarm.Speed : 0;
        int faults = LevelFarm.Faults;
        if (_speedN != speed || _faultN != faults)
        {
            _speedN = speed;
            _faultN = faults;
            Cells[RMatch].Key.text = MatchKey();
        }
    }

    private static void Paint(Rect box, bool right)
    {
        NocturnePalette p = NocturneStyle.Current;
        Color acc = p.Accent;
        float sc = _sc;
        float fg = _pa;
        float bg = _pa * Ui.Clamp(NocturneConfig.HudActiveOpacity.Value, 30, 100) / 100f;
        bool menu = NocturneMenu.Opened;
        bool lite = NocturneStyle.Lite;
        int rad = Ui.RoundToInt(11f * sc);
        float ins = (PadX + 2f) * sc;
        float now = NocturneStyle.Now;
        Color prev = NocturneStyle.Tint;

        if (!_ready || acc != _tAcc || _ru != _tRu) Palette(acc);

        Color tone = Ui.Mix(p.Window, acc, 0.05f);
        NocturneStyle.Glow(Ui.R(box.m_XMin - 18f * sc, box.m_YMin - 10f * sc, box.m_Width + 36f * sc, box.m_Height + 30f * sc), Ui.C(0f, 0f, 0f, 0.42f * bg));
        NocturneStyle.FillRounded(box, Ui.C(tone.r, tone.g, tone.b, 0.9f * bg), rad);
        if (!lite)
        {
            NocturneStyle.Tint = Ui.C(1f, 1f, 1f, 0.075f * bg);
            GUI.Box(Ui.R(box.m_XMin + 1f, box.m_YMin + 1f, box.m_Width - 2f, (box.m_Height - 2f) * 0.7f), NocturneStyle.Blank, ActiveHud.Gloss(rad));
            NocturneStyle.Tint = prev;
        }
        if (_hov || _drag)
            NocturneStyle.FillRounded(box, Ui.C(acc.r, acc.g, acc.b, (_drag ? 0.11f : 0.06f) * fg), rad);
        NocturneStyle.StrokeRounded(box, Ui.C(acc.r, acc.g, acc.b, (_drag ? 0.95f : _hov ? 0.85f : menu ? 0.6f : 0.34f) * fg), rad, 1);

        float hy = box.m_YMin + PadY * sc;
        float hh = Head * sc;
        float ic = 13f * sc;
        float block = ic + 6f * sc + _tw;
        float x0 = right ? Ui.Right(box) - ins - block : box.m_XMin + ins;
        float wx = x0 + ic + 6f * sc;
        float pulse = 0.55f + 0.45f * MathF.Sin(now * 2.4f);
        var star = Ui.R(x0, hy + (hh - ic) * 0.5f, ic, ic);
        NocturneStyle.Glow(Ui.R(star.m_XMin - 8f * sc, star.m_YMin - 8f * sc, ic + 16f * sc, ic + 16f * sc), Ui.C(acc.r, acc.g, acc.b, 0.3f * pulse * fg));
        NocturneIcons.Draw(NocturneIcon.Star, star, Ui.C(acc.r, acc.g, acc.b, fg));

        NocturneStyle.Tint = Ui.C(1f, 1f, 1f, fg);
        GUI.Label(Ui.R(wx, hy, _tw + 2f, hh), Titles[lite ? 0 : (int)(now * 12f) % ActiveHud.Steps], _ttl);

        float ly = hy + hh + 1f * sc;
        var line = Ui.R(box.m_XMin + ins, ly, box.m_Width - ins * 2f, 1f);
        NocturneStyle.Tint = Ui.C(acc.r, acc.g, acc.b, 0.5f * fg);
        GUI.Box(line, NocturneStyle.Blank, _line);
        if (!lite)
        {
            float u = Ui.Repeat(now * 12f - 7f, ActiveHud.Steps) / ActiveHud.Steps;
            float gx0 = Ui.Max(line.m_XMin, wx + u * _tw - _tw * 0.17f);
            float gx1 = Ui.Min(Ui.Right(line), wx + u * _tw + _tw * 0.17f);
            if (gx1 - gx0 > 2f)
            {
                NocturneStyle.Tint = Ui.C(1f, 1f, 1f, 0.55f * MathF.Sin(u * MathF.PI) * fg);
                GUI.Box(Ui.R(gx0, ly, gx1 - gx0, 1f), NocturneStyle.Blank, _line);
            }
        }

        Color led = _state == 2 ? Good : _state == 1 ? Amber : Idle;
        float pt = 16f * sc;
        float dot = Ui.Max(4f, Ui.Round(6f * sc));
        var pill = Ui.R(right ? box.m_XMin + ins : Ui.Right(box) - ins - _pillW, hy + (hh - pt) * 0.5f, _pillW, pt);
        NocturneStyle.Tint = prev;
        var lamp = Ui.R(pill.m_XMin + 8f * sc, pill.m_YMin + (pt - dot) * 0.5f, dot, dot);
        NocturneStyle.FillRounded(pill, Ui.C(led.r, led.g, led.b, 0.16f * fg), Ui.RoundToInt(pt * 0.5f));
        NocturneStyle.FillRounded(lamp, Ui.C(led.r, led.g, led.b, (_state == 2 ? 0.55f + 0.45f * pulse : 0.9f) * fg), Ui.RoundToInt(dot * 0.5f));
        Color soft = Ui.Mix(led, Ui.White, 0.4f);
        NocturneStyle.Tint = Ui.C(soft.r, soft.g, soft.b, fg);
        GUI.Label(Ui.R(pill.m_XMin + 14f * sc, pill.m_YMin, _pillW - 18f * sc, pt), States[_state], _pill);

        NocturneStyle.Tint = prev;
        if (menu)
        {
            float gx = right ? Ui.Right(pill) + 7f * sc : pill.m_XMin - 14f * sc;
            float gy = hy + (hh - 6f * sc) * 0.5f;
            for (int c = 0; c < 3; c++)
                for (int r = 0; r < 2; r++)
                    NocturneStyle.Fill(Ui.R(gx + c * 4f * sc, gy + r * 4f * sc, 2f * sc, 2f * sc), Ui.C(1f, 1f, 1f, 0.38f * fg));
        }

        Cells[RXp].Tone = acc;
        float x = box.m_XMin + ins;
        float w = box.m_Width - ins * 2f;
        float y = box.m_YMin + (PadY + Head + 6f) * sc;
        float rh = Row * sc;
        for (int i = 0; i < Cells.Length; i++)
        {
            Cell cell = Cells[i];
            float vw = w;
            NocturneStyle.Tint = prev;
            if (i == RLevel && _gainN > 0)
            {
                float cw = _gainW + 12f * sc;
                float ch = 15f * sc;
                var chip = Ui.R(x + w - cw, y + (rh - ch) * 0.5f, cw, ch);
                NocturneStyle.FillRounded(chip, Ui.C(Good.r, Good.g, Good.b, 0.18f * fg), Ui.RoundToInt(ch * 0.5f));
                Color gs = Ui.Mix(Good, Ui.White, 0.35f);
                NocturneStyle.Tint = Ui.C(gs.r, gs.g, gs.b, fg);
                GUI.Label(chip, Gain, _pill);
                vw = w - cw - 6f * sc;
            }

            NocturneStyle.Tint = Ui.C(Muted.r, Muted.g, Muted.b, fg);
            GUI.Label(Ui.R(x, y, vw, rh), cell.Key, _txt);
            NocturneStyle.Tint = Ui.C(cell.Col.r, cell.Col.g, cell.Col.b, fg);
            GUI.Label(Ui.R(x, y, vw, rh), cell.Val, _val);
            y += rh;

            NocturneStyle.Tint = prev;
            if (cell.Meter)
            {
                float bh = Ui.Max(4f, 5f * sc);
                int br = Ui.RoundToInt(bh * 0.5f);
                var track = Ui.R(x, y + 2f * sc, w, bh);
                NocturneStyle.FillRounded(track, Ui.C(1f, 1f, 1f, 0.09f * fg), br);
                float fw = w * Ui.Clamp01(cell.Fill);
                if (fw >= 1f)
                    NocturneStyle.FillRounded(Ui.R(x, track.m_YMin, Ui.Max(fw, br * 2f + 1f), bh), Ui.C(cell.Tone.r, cell.Tone.g, cell.Tone.b, 0.95f * fg), br);
                y += Gauge * sc;
            }
            if (i == RLast)
            {
                NocturneStyle.Fill(Ui.R(x, y + 2f * sc, w, 1f), Ui.C(1f, 1f, 1f, 0.08f * fg));
                y += Split * sc;
            }
        }
        NocturneStyle.Tint = prev;
    }

    private static void Palette(Color acc)
    {
        _tAcc = acc;
        _tRu = _ru;
        _ready = true;
        ActiveHud.Shimmer(Titles, _title, acc);
    }

    private static void Restyle()
    {
        if (_txt == null)
        {
            _txt = ActiveHud.Make(FontStyle.Normal, TextAnchor.MiddleLeft);
            _val = ActiveHud.Make(FontStyle.Bold, TextAnchor.MiddleRight);
            _ttl = ActiveHud.Make(FontStyle.Bold, TextAnchor.MiddleLeft);
            _ttl.richText = true;
            _pill = ActiveHud.Make(FontStyle.Bold, TextAnchor.MiddleCenter);
            _line = new GUIStyle();
            _line.normal.background = ActiveHud.Bump(64);
            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = new Cell();
            for (int i = 0; i < States.Length; i++)
                States[i] = new GUIContent();
            Cells[RXp].Meter = true;
            Cells[RMatch].Meter = true;
            Cells[RMatch].Tone = Good;
        }

        _txt.fontSize = Ui.RoundToInt(12f * _sc);
        _val.fontSize = Ui.RoundToInt(13f * _sc);
        _ttl.fontSize = Ui.RoundToInt(11f * _sc);
        _pill.fontSize = Ui.RoundToInt(11f * _sc);

        _title = NocturneText.T("ФАРМ ЛВЛ", "LEVEL FARM");
        Cells[RLevel].Key.text = LevelKey(Cells[RLevel].B == int.MinValue ? 0 : Cells[RLevel].B);
        Cells[RXp].Key.text = "XP";
        Cells[RLast].Key.text = NocturneText.T("Прошлый матч", "Last match");
        Cells[RMatch].Key.text = MatchKey();
        Cells[RKills].Key.text = NocturneText.T("Киллов за сессию", "Session kills");
        Cells[RPlayed].Key.text = NocturneText.T("Матчей", "Matches");
        States[0].text = NocturneText.T("ВЫКЛ", "OFF");
        States[1].text = NocturneText.T("ЖДЁТ", "IDLE");
        States[2].text = NocturneText.T("ФАРМ", "FARM");

        _tw = ActiveHud.Measure(_ttl, _title);
        float widest = 0f;
        for (int i = 0; i < States.Length; i++)
            widest = Ui.Max(widest, ActiveHud.Measure(_pill, States[i].text));
        _pillW = widest + 24f * _sc;
        _gainW = _gainN > 0 ? ActiveHud.Measure(_pill, Gain.text) : 0f;
        _pw = Width * _sc;
        _ph = (PadY * 2f + Head + 6f + Row * 6f + Gauge * 2f + Split) * _sc;
    }

    private static string MatchKey()
    {
        string key = NocturneText.T("Матч", "Match");
        if (_speedN > 0)
            key += " · " + _speedN + NocturneText.T("/с", "/s");
        if (_faultN > 0)
            key += NocturneText.T(" · сбоев ", " · faults ") + _faultN;
        return key;
    }

    private static string LevelKey(int stop)
    {
        return stop > 0 ? NocturneText.T("Уровень → ", "Level → ") + stop : NocturneText.T("Уровень", "Level");
    }

    private static string Fmt(int n)
    {
        if (n < 10000)
            return n.ToString();
        return (n / 1000).ToString() + " " + (n % 1000).ToString("000");
    }
}
