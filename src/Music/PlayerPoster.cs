using System;
using UnityEngine;

namespace Nocturne;

internal sealed class PlayerPoster
{
    internal const float W = 300f;
    internal const float Tall = 416f;
    internal const float Short = 300f;
    private const float Pad = 18f;
    internal const float Edge = Short - 24f;
    private const float Tile = 184f;
    private const float TX = (W - Tile) * 0.5f;
    private const float TY = 22f;

    internal sealed class State
    {
        internal float Now;
        internal float Dt = 1f / 60f;
        internal float Alpha = 1f;
        internal float Fold;
        internal float Beat;
        internal float Glow;
        internal float Phase;
        internal float Time;
        internal float Total;
        internal float Volume = 0.7f;
        internal float Slide = -1f;
        internal int SlideDir = 1;
        internal int Index = -1;
        internal int Count;
        internal bool Playing;
        internal bool Loading;
        internal bool Shuffle;
        internal bool Loop;
        internal bool EqOn;
        internal bool ListOn;
        internal bool Remote;
        internal bool Idle;
        internal bool Pick;
        internal bool CanVolume;
        internal bool CanSearch;
        internal bool CanLibrary;
        internal bool SearchOn;
        internal bool CanPrev = true;
        internal bool CanNext = true;
        internal bool CanPlay = true;
        internal bool CanSeek = true;
        internal bool CanShuffle = true;
        internal bool CanLoop = true;
        internal string Name = string.Empty;
        internal string Artist;
        internal string Chip;
        internal MediaService Service;
        internal string Sub = string.Empty;
        internal string Hint = string.Empty;
        internal PlayerCover Cover;
        internal PlayerCover PrevCover;
        internal Color Art;
        internal Color Accent;
        internal Color Bg;
    }

    internal sealed class Result
    {
        internal bool Prev, Next, Play, Close, Fold, Shuffle, Loop, Eq, List, Mute, Source, Cycle, Search, Library;
        internal float Seek = -1f;
        internal float Volume = -1f;

        internal void Clear()
        {
            Prev = Next = Play = Close = Fold = Shuffle = Loop = Eq = List = Mute = Source = Cycle = Search = Library = false;
            Seek = Volume = -1f;
        }
    }

    private sealed class Ellipsis
    {
        private string _src;
        private float _w;
        private string _cut;

        internal string Of(string text, float width, GUIStyle style, GUIContent probe)
        {
            if (ReferenceEquals(text, _src) && width == _w)
                return _cut;
            _src = text;
            _w = width;
            probe.text = text;
            if (style.CalcSize(probe).x <= width)
                return _cut = text;
            int lo = 0;
            int hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                probe.text = text.Substring(0, mid).TrimEnd() + "…";
                if (style.CalcSize(probe).x <= width)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return _cut = text.Substring(0, lo).TrimEnd() + "…";
        }
    }

    internal readonly State S = new State();
    internal readonly Result R = new Result();

    private readonly float[] _hov = new float[14];
    private readonly GUIContent _measure = new GUIContent();
    private Color _artFor = Ui.C(-1f, 0f, 0f, 0f);
    private Color _deep, _ca, _hi;
    private string _from, _fromArtist, _title = string.Empty, _artist = string.Empty;
    private readonly Ellipsis _titleCut = new Ellipsis();
    private readonly Ellipsis _artistCut = new Ellipsis();
    private string _mText;
    private float _mWidth, _mStart;
    private PlayerCover _shown;
    private float _shownAt;
    private bool _seen;
    private int _chipIdx = -1, _chipCnt = -1;
    private string _chip = string.Empty;
    private string _chipFor;
    private bool _chipBadge;
    private float _chipW = 48f;
    private MediaService _badgeFor;
    private Color _badge, _ink;
    private int _curSec = -1, _remSec = -2;
    private string _cur = "0:00", _rem = "-0:00";

    internal static float HeightFor(float fold) => Tall + (Short - Tall) * fold;

    internal void Draw()
    {
        State s = S;
        Result r = R;
        r.Clear();
        PlayerGfx.Ensure();

        Event ev = Event.current;
        bool paint = ev.type == EventType.Repaint;
        Vector2 mouse = ev.mousePosition;
        float t = s.Fold;
        float ctl = 1f - t;

        if (paint)
            Poster(s);

        Head(s, r, t, mouse, paint);
        Seek(s, r, t, ctl, mouse, paint);
        if (ctl > 0.02f)
        {
            Times(s, ctl, paint);
            Transport(s, r, mouse, paint, ctl);
            Toggles(s, r, mouse, paint, ctl);
        }

        Rect fold = Ui.R(14f, 12f, 28f, 28f);
        Rect close = Ui.R(W - 42f, 12f, 28f, 28f);
        Rect source = Ui.R(14f, 46f, 28f, 28f);
        Corner(s, fold, t > 0.5f ? NocturneIcon.Down : NocturneIcon.Up, 0, mouse, paint, false);
        Corner(s, close, NocturneIcon.Close, 1, mouse, paint, false);
        Corner(s, source, NocturneIcon.Screen, 12, mouse, paint, s.Remote);
        if (Hit(fold))
            r.Fold = true;
        if (Hit(close))
            r.Close = true;
        if (Hit(source))
            r.Source = true;

        if (ev.type == EventType.ScrollWheel && (!s.Remote || s.CanVolume))
        {
            r.Volume = Ui.Clamp01(s.Volume - ev.delta.y * 0.05f);
            ev.Use();
        }
        if (ev.type == EventType.MouseDown && ev.clickCount == 2 && ev.button == 0 && mouse.y < Short && !Ui.In(fold, mouse) && !Ui.In(close, mouse) && !Ui.In(source, mouse))
        {
            r.Fold = true;
            ev.Use();
        }

        GUI.DragWindow(Ui.R(0f, 0f, W, Short));
    }

    private void Poster(State s)
    {
        float a = s.Alpha;
        Tones(s.Art);

        GUI.color = Tint(_deep, a);
        GUI.Box(Ui.R(0f, 0f, W, Edge), PlayerGfx.Blank, PlayerGfx.Round14);

        PlayerCover cur = s.Cover;
        PlayerCover old = s.PrevCover;
        if (!ReferenceEquals(cur, _shown))
        {
            _shown = cur;
            _shownAt = _seen ? s.Now : -10f;
        }
        _seen = true;
        float sl = s.Slide;
        bool slide = sl >= 0f && sl < 1f;
        if (slide)
            _shownAt = -10f;
        float arrive = slide ? sl : Ui.Clamp01((s.Now - _shownAt) / 0.3f);

        if (old != null && (cur == null || slide || arrive < 1f))
            Backdrop(old, a);
        if (cur != null)
            Backdrop(cur, a * arrive);
        Soft(W * 0.5f, TY + Tile * 0.5f, 250f + 40f * s.Beat, _ca, 0.10f + 0.22f * s.Beat + 0.08f * s.Glow, a);

        Color rim = Ui.Mix(Ui.White, _hi, 0.35f);
        GUI.color = Ui.C(rim.r, rim.g, rim.b, a);
        GUI.Box(Ui.R(0f, 0f, W, 64f), PlayerGfx.Blank, PlayerGfx.Sheen);

        Color fade = Ui.Mix(s.Bg, _deep, 0.28f);
        GUI.color = Tint(fade, a);
        GUI.Box(Ui.R(0f, 120f, W, Edge - 120f), PlayerGfx.Blank, PlayerGfx.Fade);
        GUI.Box(Ui.R(0f, Edge, W, 140f), PlayerGfx.Blank, PlayerGfx.Rise);

        float z = 1f + 0.025f * s.Beat;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Ui.V(z, z), Ui.V(W * 0.5f, TY + Tile * 0.5f));
        if (slide)
        {
            float leave = sl * sl * (3f - 2f * sl);
            Square(old, -leave * 60f * s.SlideDir, (1f - sl) * a);
            Square(cur, (1f - Back(sl)) * 60f * s.SlideDir, sl * a);
        }
        else
        {
            if (cur != null && old != null && arrive < 1f)
                Square(old, 0f, a * (1f - arrive));
            Square(cur ?? old, 0f, a * (cur != null ? arrive : 1f));
        }
        GUI.matrix = m;
    }

    private static void Backdrop(PlayerCover c, float alpha)
    {
        GUI.color = Ui.C(1f, 1f, 1f, alpha);
        GUI.Box(Ui.R(0f, 0f, W, Edge), PlayerGfx.Blank, c.BackStyle);
    }
    private static void Square(PlayerCover c, float dx, float alpha)
    {
        if (alpha <= 0.01f)
            return;
        float x = TX + dx;
        GUI.color = Ui.C(0f, 0f, 0f, 0.62f * alpha);
        GUI.Box(Ui.R(x - 24f, TY - 2f, Tile + 48f, Tile + 62f), PlayerGfx.Blank, PlayerGfx.Shadow);

        string mark = "♪";
        if (c == null)
        {
            GUI.color = Ui.C(1f, 1f, 1f, 0.10f * alpha);
            GUI.Box(Ui.R(x, TY, Tile, Tile), PlayerGfx.Blank, PlayerGfx.Round14);
        }
        else
        {
            GUI.color = Ui.C(1f, 1f, 1f, alpha);
            GUI.Box(Ui.R(x, TY, Tile, Tile), PlayerGfx.Blank, c.TileStyle);
            GUI.color = Ui.C(1f, 1f, 1f, 0.16f * alpha);
            GUI.Box(Ui.R(x, TY, Tile, Tile), PlayerGfx.Blank, PlayerGfx.Ring);
            if (c.Real)
                return;
            mark = c.Letter;
        }

        bool dark = c != null && c.Dark;
        Rect r = Ui.R(x + 14f, TY + 8f, 100f, 44f);
        GUI.color = dark ? Ui.C(1f, 1f, 1f, 0.22f * alpha) : Ui.C(0f, 0f, 0f, 0.28f * alpha);
        GUI.Label(Ui.R(r.m_XMin + 1f, r.m_YMin + 1f, r.m_Width, r.m_Height), mark, PlayerGfx.Mark);
        GUI.color = dark ? Ui.C(0.07f, 0.05f, 0.09f, 0.90f * alpha) : Ui.C(1f, 1f, 1f, 0.94f * alpha);
        GUI.Label(r, mark, PlayerGfx.Mark);
    }
    private static void Soft(float cx, float cy, float size, Color c, float alpha, float a)
    {
        GUI.color = Ui.C(c.r, c.g, c.b, alpha * a);
        GUI.Box(Ui.R(cx - size * 0.5f, cy - size * 0.5f, size, size), PlayerGfx.Blank, PlayerGfx.Glow);
    }

    private void Head(State s, Result r, float t, Vector2 mouse, bool paint)
    {
        bool empty = s.Remote ? s.Idle : s.Count == 0;
        bool chip = !empty && (!s.Remote || s.Chip != null);
        Split(s);
        float ty = Ui.Lerp(216f, 224f, t);
        float chipW = chip ? Chip(s) : 0f;
        Rect box = Ui.R(W - Pad - chipW, ty + 7f, chipW, 20f);
        bool pick = chip && s.Remote && s.Pick;
        if (pick && Hit(box))
            r.Cycle = true;
        if (!paint)
            return;

        float a = s.Alpha;
        float fade = s.Slide >= 0f && s.Slide < 1f ? Ui.Clamp01(s.Slide * 1.4f) : 1f;
        Marquee(_title, Pad, ty, W - Pad * 2f - (chip ? chipW + 6f : 0f), 26f, a * fade, s.Playing, s.Now);

        string sub = empty ? s.Hint : _artist.Length > 0 ? _artist : s.Sub;
        GUI.color = Ui.C(1f, 1f, 1f, 0.80f * a * fade);
        GUI.Label(Ui.R(Pad, ty + 24f, W - Pad * 2f, 24f), _artistCut.Of(sub, W - Pad * 2f, PlayerGfx.Artist, _measure), PlayerGfx.Artist);

        if (!chip)
            return;
        float hv = Hov(13, pick && Ui.In(box, mouse), s.Dt);
        GUI.color = Ui.C(1f, 1f, 1f, (0.16f + 0.12f * hv) * a);
        GUI.Box(box, PlayerGfx.Blank, PlayerGfx.Round8);
        float tx = box.m_XMin;
        if (_chipBadge)
        {
            Badge(s.Service, tx + 11f, box.m_YMin + 10f, a);
            tx += 19f;
        }
        GUI.color = Ui.C(1f, 1f, 1f, 0.90f * a);
        GUI.Label(Ui.R(tx, box.m_YMin - 3f, Ui.Right(box) - tx, box.m_Height), _chip, PlayerGfx.Count);
    }

    private static Color Rgb(uint v) => Ui.C(((v >> 16) & 255u) / 255f, ((v >> 8) & 255u) / 255f, (v & 255u) / 255f, 1f);

    private void Badge(MediaService m, float cx, float cy, float a)
    {
        if (!ReferenceEquals(m, _badgeFor))
        {
            _badgeFor = m;
            _badge = Rgb(m.Tint);
            _ink = Rgb(m.Ink);
        }
        if (m.Flat)
        {
            GUI.color = Ui.C(_badge.r, _badge.g, _badge.b, a);
            GUI.Box(Ui.R(cx - 8f, cy - 5.5f, 16f, 11f), PlayerGfx.Blank, PlayerGfx.Round8);
            Ico(NocturneIcon.Play, cx + 0.5f, cy, 7f, _ink, a);
            return;
        }

        Disc(cx, cy, 14f, _badge, a);
        switch (m.Glyph)
        {
            case ServiceGlyph.Mark:
                GUI.color = Ui.C(_ink.r, _ink.g, _ink.b, a);
                GUI.Label(Ui.R(cx - 8f, cy - 9f, 16f, 16f), m.Mark, PlayerGfx.Micro);
                break;
            case ServiceGlyph.Play:
                Ico(NocturneIcon.Play, cx + 0.5f, cy, 8f, _ink, a);
                break;
            case ServiceGlyph.Arcs:
                Ico(NocturneIcon.Arcs, cx, cy, 12f, _ink, a);
                break;
            case ServiceGlyph.Bars:
                Ico(NocturneIcon.Bars, cx, cy, 10f, _ink, a);
                break;
            default:
                Ico(NocturneIcon.Note, cx, cy, 10f, _ink, a);
                break;
        }
    }

    private float Chip(State s)
    {
        if (s.Remote)
        {
            bool badge = s.Service != null;
            if (!ReferenceEquals(s.Chip, _chipFor) || badge != _chipBadge)
            {
                _chipFor = s.Chip;
                _chipBadge = badge;
                _chip = s.Chip;
                _measure.text = s.Chip;
                _chipW = Ui.Clamp(PlayerGfx.Count.CalcSize(_measure).x + (badge ? 38f : 22f), 48f, 132f);
            }
            return _chipW;
        }
        _chipBadge = false;
        if (s.Index != _chipIdx || s.Count != _chipCnt)
        {
            _chipIdx = s.Index;
            _chipCnt = s.Count;
            _chip = (s.Index + 1) + "/" + s.Count;
        }
        return 48f;
    }

    private void Split(State s)
    {
        string name = s.Name;
        if (ReferenceEquals(name, _from) && ReferenceEquals(s.Artist, _fromArtist))
            return;
        _from = name;
        _fromArtist = s.Artist;
        if (s.Artist != null)
        {
            _artist = s.Artist;
            _title = name;
            return;
        }
        string rest = PlayerGfx.Clean(name);
        int cut = rest.IndexOf(" - ", StringComparison.Ordinal);
        if (cut < 0)
            cut = rest.IndexOf(" – ", StringComparison.Ordinal);
        if (cut > 0)
        {
            _artist = rest.Substring(0, cut).Trim();
            _title = rest.Substring(cut + 3).Trim();
        }
        else
        {
            _artist = string.Empty;
            _title = rest;
        }
    }

    private void Marquee(string text, float x, float y, float w, float h, float alpha, bool run, float now)
    {
        if (!ReferenceEquals(text, _mText))
        {
            _mText = text;
            _measure.text = text;
            _mWidth = PlayerGfx.Title.CalcSize(_measure).x;
            _mStart = now;
        }
        GUI.color = Ui.C(1f, 1f, 1f, alpha);
        if (_mWidth <= w)
        {
            GUI.Label(Ui.R(x, y - 5f, w, h + 10f), text, PlayerGfx.Title);
            return;
        }
        if (!run)
        {
            GUI.Label(Ui.R(x, y - 5f, w, h + 10f), _titleCut.Of(text, w, PlayerGfx.Title, _measure), PlayerGfx.Title);
            return;
        }

        float over = _mWidth - w;
        float cycle = over / 38f + 3.6f;
        float tt = (now - _mStart) % cycle;
        float dx = tt <= 1.8f ? 0f : tt >= 1.8f + over / 38f ? over : (tt - 1.8f) * 38f;
        GUI.BeginGroup(Ui.R(x, y - 5f, w, h + 10f));
        GUI.Label(Ui.R(-dx, 0f, _mWidth + 8f, h + 10f), text, PlayerGfx.Title);
        GUI.EndGroup();
    }

    private void Seek(State s, Result r, float t, float ctl, Vector2 mouse, bool paint)
    {
        float y = Ui.Lerp(270f, 280f, t);
        Rect bar = Ui.R(Pad, y, W - Pad * 2f, 18f);
        float p = s.Total > 0f ? Ui.Clamp01(s.Time / s.Total) : 0f;
        float w = bar.m_Width;
        bool can = !s.Remote || s.CanSeek;

        if (paint)
        {
            float a = s.Alpha;
            float hv = Hov(10, can && Ui.In(bar, mouse), s.Dt);
            float th = Ui.Lerp(4f, 3f, t) + 2f * hv;
            float cy = y + 9f;
            Pill(Pad, cy - th * 0.5f, w, th, Ui.White, 0.20f * a);
            if (p > 0.002f)
                Pill(Pad, cy - th * 0.5f, Ui.Max(th, w * p), th, _hi, 0.96f * a);
            if (s.Loading)
            {
                GUI.BeginGroup(Ui.R(Pad, cy - 4f, w, 8f));
                Pill(((s.Now % 1.3f) / 1.3f) * (w + 90f) - 90f, 4f - th * 0.5f, 90f, th, Ui.White, 0.38f * a);
                GUI.EndGroup();
            }
            float vis = can ? Ui.Max(ctl, hv) : 0f;
            if (vis > 0.02f)
            {
                Soft(Pad + w * p, cy, 34f, _ca, 0.55f * vis, a);
                Disc(Pad + w * p, cy, 9f + 5f * hv, Ui.White, a * vis);
            }
        }

        if (!can)
            return;
        Color keep = GUI.color;
        GUI.color = Ui.Clear;
        float v = GUI.HorizontalSlider(bar, p, 0f, 1f, PlayerGfx.Track, PlayerGfx.Thumb);
        GUI.color = keep;
        if (s.Total > 0f && Ui.Abs(v - p) > 0.002f)
            r.Seek = v;
    }

    private void Times(State s, float ctl, bool paint)
    {
        int cur = (int)s.Time;
        int rem = s.Total > 0f ? (int)(s.Total - s.Time) : -1;
        if (cur != _curSec)
        {
            _curSec = cur;
            _cur = Clock(cur);
        }
        if (rem != _remSec)
        {
            _remSec = rem;
            _rem = rem >= 0 ? "-" + Clock(rem) : "--:--";
        }
        if (!paint)
            return;

        GUI.color = Ui.C(1f, 1f, 1f, 0.78f * s.Alpha * ctl);
        GUI.Label(Ui.R(Pad, 289f, 70f, 18f), _cur, PlayerGfx.Clock);
        GUI.Label(Ui.R(W - Pad - 70f, 289f, 70f, 18f), _rem, PlayerGfx.ClockRight);
    }

    private void Transport(State s, Result r, Vector2 mouse, bool paint, float ctl)
    {
        float a = s.Alpha * ctl;
        float cy = 338f;
        bool live = s.Remote ? !s.Idle : s.Count > 0;
        bool canPrev = s.Remote ? s.CanPrev : live;
        bool canNext = s.Remote ? s.CanNext : live;
        bool canPlay = !s.Remote || s.CanPlay;
        Rect prev = Ui.R(84f - 22f, cy - 22f, 44f, 44f);
        Rect play = Ui.R(150f - 32f, cy - 32f, 64f, 64f);
        Rect next = Ui.R(216f - 22f, cy - 22f, 44f, 44f);

        if (paint)
        {
            float dim = (s.Remote ? s.CanPlay : live) ? 1f : 0.35f;
            float hp = Hov(2, canPrev && Ui.In(prev, mouse), s.Dt);
            Ico(NocturneIcon.Prev, 84f, cy, 26f + 3f * hp, Ui.White, a * (canPrev ? 1f : 0.35f) * (0.78f + 0.22f * hp));
            float hn = Hov(4, canNext && Ui.In(next, mouse), s.Dt);
            Ico(NocturneIcon.Next, 216f, cy, 26f + 3f * hn, Ui.White, a * (canNext ? 1f : 0.35f) * (0.78f + 0.22f * hn));

            float hy = Hov(3, canPlay && Ui.In(play, mouse), s.Dt);
            Soft(150f, cy, 118f + 18f * s.Beat, _ca, (s.Playing ? 0.22f + 0.34f * s.Beat : 0.10f) + 0.12f * hy, a * dim);
            Disc(150f, cy, 56f + 4f * hy, Ui.White, a * dim * 0.96f);
            Color ink = Ui.C(0.07f, 0.05f, 0.08f, 1f);
            if (s.Loading)
            {
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(s.Now * 360f, Ui.V(150f, cy));
                Ico(NocturneIcon.Refresh, 150f, cy, 26f, ink, a);
                GUI.matrix = m;
            }
            else
                Ico(s.Playing ? NocturneIcon.Pause : NocturneIcon.Play, 150f, cy, 28f + 2f * hy, ink, a * dim);
        }

        if (canPrev && ctl > 0.5f && Hit(prev))
            r.Prev = true;
        if (canNext && ctl > 0.5f && Hit(next))
            r.Next = true;
        if (canPlay && ctl > 0.5f && Hit(play))
            r.Play = true;
    }

    private void Toggles(State s, Result r, Vector2 mouse, bool paint, float ctl)
    {
        float a = s.Alpha * ctl;
        float cy = 388f;
        Rect shuf = Ui.R(34f - 16f, cy - 16f, 32f, 32f);
        Rect loop = Ui.R(76f - 16f, cy - 16f, 32f, 32f);
        Rect mute = Ui.R(120f - 14f, cy - 14f, 28f, 28f);
        Rect vol = Ui.R(138f, cy - 9f, 70f, 18f);
        Rect eq = Ui.R(240f - 16f, cy - 16f, 32f, 32f);
        Rect list = Ui.R(272f - 16f, cy - 16f, 32f, 32f);
        bool remote = s.Remote;
        bool canShuf = !remote || s.CanShuffle;
        bool canLoop = !remote || s.CanLoop;
        bool canEq = !remote || s.CanSearch;
        bool canList = !remote || s.CanLibrary;
        bool canVol = !remote || s.CanVolume;

        if (paint)
        {
            Toggle(NocturneIcon.Shuffle, 34f, cy, s.Shuffle, Hov(5, canShuf && Ui.In(shuf, mouse), s.Dt), a * (canShuf ? 1f : 0.35f));
            Toggle(NocturneIcon.Repeat, 76f, cy, s.Loop, Hov(6, canLoop && Ui.In(loop, mouse), s.Dt), a * (canLoop ? 1f : 0.35f));
            float he = Hov(8, canEq && Ui.In(eq, mouse), s.Dt);
            float hl = Hov(9, canList && Ui.In(list, mouse), s.Dt);
            if (remote)
            {
                Toggle(NocturneIcon.Search, 240f, cy, s.SearchOn, he, a * (canEq ? 1f : 0.35f));
                Toggle(NocturneIcon.List, 272f, cy, false, hl, a * (canList ? 1f : 0.35f));
            }
            else
            {
                Toggle(NocturneIcon.Eq, 240f, cy, s.EqOn, he, a);
                Toggle(NocturneIcon.List, 272f, cy, s.ListOn, hl, a);
            }

            float va = a * (canVol ? 1f : 0.35f);
            float hm = Hov(7, canVol && Ui.In(mute, mouse), s.Dt);
            Ico(NocturneIcon.Volume, 120f, cy, 20f, Ui.White, va * (s.Volume > 0.001f ? 0.55f + 0.35f * hm : 0.25f));
            float hv = Hov(11, canVol && Ui.In(vol, mouse), s.Dt);
            float th = 3f + 1.5f * hv;
            Pill(vol.m_XMin, cy - th * 0.5f, vol.m_Width, th, Ui.White, 0.20f * va);
            if (s.Volume > 0.01f)
                Pill(vol.m_XMin, cy - th * 0.5f, Ui.Max(th, vol.m_Width * s.Volume), th, _hi, 0.9f * va);
            Disc(vol.m_XMin + vol.m_Width * s.Volume, cy, 7f + 4f * hv, Ui.White, va * (0.8f + 0.2f * hv));
        }

        if (ctl <= 0.5f)
            return;
        if (canShuf && Hit(shuf))
            r.Shuffle = true;
        if (canLoop && Hit(loop))
            r.Loop = true;
        if (canVol && Hit(mute))
            r.Mute = true;
        if (canEq && Hit(eq))
        {
            if (remote)
                r.Search = true;
            else
                r.Eq = true;
        }
        if (canList && Hit(list))
        {
            if (remote)
                r.Library = true;
            else
                r.List = true;
        }
        if (!canVol)
            return;

        Color keep = GUI.color;
        GUI.color = Ui.Clear;
        float nv = GUI.HorizontalSlider(vol, s.Volume, 0f, 1f, PlayerGfx.Track, PlayerGfx.Thumb);
        GUI.color = keep;
        if (Ui.Abs(nv - s.Volume) > 0.002f)
            r.Volume = nv;
    }
    private void Toggle(NocturneIcon icon, float cx, float cy, bool on, float hv, float a)
    {
        if (on)
        {
            Ico(icon, cx, cy, 20f, _hi, a);
            Disc(cx, cy + 14f, 4f, _hi, a);
        }
        else
            Ico(icon, cx, cy, 20f, Ui.White, a * (0.42f + 0.43f * hv));
    }

    private void Corner(State s, Rect r, NocturneIcon icon, int slot, Vector2 mouse, bool paint, bool on)
    {
        if (!paint)
            return;
        float hv = Hov(slot, Ui.In(r, mouse), s.Dt);
        Vector2 c = Ui.Mid(r);
        Disc(c.x, c.y, 28f, on ? _ca : Ui.Black, s.Alpha * (on ? 0.62f + 0.18f * hv : 0.26f + 0.22f * hv));
        Ico(icon, c.x, c.y, 16f, Ui.White, s.Alpha * (0.82f + 0.18f * hv));
    }

    private float Hov(int i, bool over, float dt)
    {
        float v = Ui.MoveTowards(_hov[i], over ? 1f : 0f, dt * 8f);
        _hov[i] = v;
        return v;
    }

    private void Tones(Color art)
    {
        if (Ui.Same(art, _artFor))
            return;
        _artFor = art;
        PlayerGfx.Hsv(art, out float h, out float sat, out float val);
        _deep = PlayerGfx.FromHsv(h, sat * 0.85f, val * 0.40f);
        _ca = PlayerGfx.FromHsv(h, sat * 0.80f, Ui.Min(1f, val * 1.30f));
        _hi = PlayerGfx.FromHsv(h, Ui.Min(sat * 0.42f, 0.5f), 1f);
    }

    private static bool Hit(Rect r) => GUI.Button(r, PlayerGfx.Blank, PlayerGfx.None);

    private static Color Tint(Color c, float a) => Ui.C(c.r, c.g, c.b, c.a * a);

    private static float Back(float t)
    {
        float v = Ui.Clamp01(t) - 1f;
        return 1f + 2.1f * v * v * v + 1.1f * v * v;
    }

    private static string Clock(int sec) => (sec / 60) + ":" + (sec % 60).ToString("D2");

    private static void Ico(NocturneIcon icon, float cx, float cy, float size, Color c, float alpha)
    {
        GUI.color = Ui.C(c.r, c.g, c.b, c.a * alpha);
        GUI.Box(Ui.R(cx - size * 0.5f, cy - size * 0.5f, size, size), PlayerGfx.Blank, PlayerGfx.Icon(icon));
    }

    private static void Disc(float cx, float cy, float size, Color c, float alpha)
    {
        GUI.color = Ui.C(c.r, c.g, c.b, c.a * alpha);
        GUI.Box(Ui.R(cx - size * 0.5f, cy - size * 0.5f, size, size), PlayerGfx.Blank, PlayerGfx.Dot);
    }

    private static void Pill(float x, float y, float w, float h, Color c, float alpha)
    {
        GUI.color = Ui.C(c.r, c.g, c.b, c.a * alpha);
        GUI.Box(Ui.R(x, y, w, h), PlayerGfx.Blank, PlayerGfx.Pill);
    }
}
