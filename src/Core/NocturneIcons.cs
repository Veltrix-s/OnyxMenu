using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal enum NocturneIcon
{
    Home, Star, Eye, Info, Gear, Bell, Minimize, Close, Shield, Bolt, Door, Crew, Tune, Copy, Trash, Check, Warn, Error, Sparkle, Crown, Badge, Folder, Lock, Unlock, Radar,
    Play, Pause, Prev, Next, Shuffle, Repeat, Volume, Eq, List, Up, Down, Refresh, Screen, Search, Arcs, Bars, Note, Collapse, Expand, Chat, Smile, Send
}

internal static class NocturneIcons
{
    private const int Size = 64;

    private static readonly Dictionary<NocturneIcon, Texture2D> Cache = new Dictionary<NocturneIcon, Texture2D>();
    private static readonly Dictionary<NocturneIcon, Slot> Slots = new Dictionary<NocturneIcon, Slot>();

    private sealed class Slot
    {
        internal Texture2D Tex;
        internal GUIStyle Style;
    }

    private static GUIStyle StyleOf(NocturneIcon icon)
    {
        Texture2D t = Get(icon);
        if (!Slots.TryGetValue(icon, out Slot s))
        {
            s = new Slot();
            Slots[icon] = s;
        }
        if (!ReferenceEquals(s.Tex, t))
        {
            s.Tex = t;
            s.Style = new GUIStyle();
            s.Style.normal.background = t;
        }
        return s.Style;
    }

    internal static void Draw(NocturneIcon icon, Rect r, Color color)
    {
        if (NocturneStyle.Lite || !NocturneStyle.Painting)
            return;
        if (NocturneStyle.Off(r))
            return;
        GUIStyle st = StyleOf(icon);
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Use(Ui.C(color.r, color.g, color.b, color.a * prev.a));
        GUI.Box(r, NocturneStyle.Blank, st);
        NocturneStyle.Done(prev);
    }

    private static int _warmAt;

    internal static bool WarmStep()
    {
        var vals = System.Enum.GetValues(typeof(NocturneIcon));
        while (_warmAt < vals.Length)
        {
            var ic = (NocturneIcon)vals.GetValue(_warmAt);
            _warmAt++;
            if (Cache.TryGetValue(ic, out Texture2D have) && have != null)
                continue;
            Cache[ic] = Build(ic);
            return true;
        }
        return false;
    }

    internal static Texture2D Tex(NocturneIcon icon) => Get(icon);

    private static Texture2D Get(NocturneIcon icon)
    {
        if (Cache.TryGetValue(icon, out Texture2D t) && t != null)
            return t;
        t = Build(icon);
        Cache[icon] = t;
        return t;
    }

    private static Texture2D Build(NocturneIcon icon)
    {
        var c = new Canvas(Size);
        float th = 0.085f;
        float U(float v) => v / 24f;
        float[] P(params float[] v)
        {
            var o = new float[v.Length];
            for (int i = 0; i < o.Length; i++)
                o[i] = v[i] / 24f;
            return o;
        }

        switch (icon)
        {
            case NocturneIcon.Home:
                c.Path(P(3, 11.5f, 12, 3.5f, 21, 11.5f), th, false);
                c.Path(P(5.5f, 9.4f, 5.5f, 20.5f, 18.5f, 20.5f, 18.5f, 9.4f), th, false);
                c.Path(P(10, 20.5f, 10, 14.5f, 14, 14.5f, 14, 20.5f), th, false);
                break;
            case NocturneIcon.Star:
                c.Star(0.5f, 0.52f, 0.41f, 0.17f, th);
                break;
            case NocturneIcon.Sparkle:
                c.Star(U(10.5f), U(13.5f), U(8.8f), U(2.6f), th * 0.6f, 4, true);
                c.Star(U(19), U(5.8f), U(3.8f), U(1.2f), th * 0.6f, 4, true);
                break;
            case NocturneIcon.Eye:
                c.Arc(0.5f, U(16.2f), U(10.4f), th, 203.7f, 336.3f);
                c.Arc(0.5f, U(7.8f), U(10.4f), th, 23.7f, 156.3f);
                c.Disc(U(2.5f), 0.5f, th * 0.5f);
                c.Disc(U(21.5f), 0.5f, th * 0.5f);
                c.Disc(0.5f, 0.5f, U(3.4f));
                break;
            case NocturneIcon.Info:
                c.Ring(0.5f, 0.5f, 0.38f, th);
                c.Disc(0.5f, 0.30f, 0.055f);
                c.Line(0.5f, 0.44f, 0.5f, 0.72f, th);
                break;
            case NocturneIcon.Gear:
            {
                float[] da = { -13.5f, -8.5f, 8.5f, 13.5f };
                float[] dr = { 7.8f, 10f, 10f, 7.8f };
                var cog = new float[64];
                for (int i = 0; i < 32; i++)
                {
                    float a = (i / 4 * 45f + da[i % 4]) * MathF.PI / 180f;
                    cog[i * 2] = 0.5f + MathF.Cos(a) * U(dr[i % 4]);
                    cog[i * 2 + 1] = 0.5f + MathF.Sin(a) * U(dr[i % 4]);
                }
                c.Path(cog, th, true);
                c.Ring(0.5f, 0.5f, U(3.4f), th);
                break;
            }
            case NocturneIcon.Bell:
                c.Arc(0.5f, 0.52f, 0.26f, th, 180f, 360f);
                c.Line(0.24f, 0.52f, 0.24f, 0.68f, th);
                c.Line(0.76f, 0.52f, 0.76f, 0.68f, th);
                c.Line(0.18f, 0.68f, 0.82f, 0.68f, th);
                c.Disc(0.5f, 0.80f, 0.06f);
                break;
            case NocturneIcon.Minimize:
                c.Line(0.28f, 0.5f, 0.72f, 0.5f, th);
                break;
            case NocturneIcon.Close:
                c.Line(0.30f, 0.30f, 0.70f, 0.70f, th);
                c.Line(0.70f, 0.30f, 0.30f, 0.70f, th);
                break;
            case NocturneIcon.Shield:
                c.Path(P(4.5f, 5.5f, 12, 2.8f, 19.5f, 5.5f, 19.5f, 12, 18.2f, 15.6f, 15.4f, 18.9f, 12, 21.4f, 8.6f, 18.9f, 5.8f, 15.6f, 4.5f, 12), th, true);
                c.Path(P(8.3f, 12, 11, 14.6f, 15.7f, 9.3f), th, false);
                break;
            case NocturneIcon.Bolt:
            {
                float[] zap = P(13.4f, 2, 4.2f, 13.6f, 11, 13.6f, 10.2f, 22, 19.8f, 10.4f, 13, 10.4f);
                c.Fill(zap);
                c.Path(zap, th * 0.6f, true);
                break;
            }
            case NocturneIcon.Door:
                c.Frame(U(6.5f), U(3), U(17.5f), U(21), U(1.4f), th);
                c.Path(P(3.5f, 21, 20.5f, 21), th, false);
                c.Disc(U(14.2f), U(12.4f), U(1.25f));
                break;
            case NocturneIcon.Tune:
                c.Line(0.16f, 0.30f, 0.84f, 0.30f, th);
                c.Line(0.16f, 0.50f, 0.84f, 0.50f, th);
                c.Line(0.16f, 0.70f, 0.84f, 0.70f, th);
                c.Disc(0.34f, 0.30f, 0.08f);
                c.Disc(0.66f, 0.50f, 0.08f);
                c.Disc(0.44f, 0.70f, 0.08f);
                break;
            case NocturneIcon.Crew:
                c.Slab(U(4.3f), U(8.5f), U(8.8f), U(16), U(1.7f));
                c.Slab(U(7.6f), U(3.5f), U(19.6f), U(16.5f), U(5.6f));
                c.Slab(U(7.6f), U(8), U(12.6f), U(21), U(1.8f));
                c.Slab(U(14.6f), U(8), U(19.6f), U(21), U(1.8f));
                c.Cut(U(11.2f), U(6.4f), U(17.6f), U(11.4f), U(2.5f));
                break;
            case NocturneIcon.Crown:
                c.Path(P(3.5f, 19.5f, 3, 7.5f, 8, 12, 12, 4.8f, 16, 12, 21, 7.5f, 20.5f, 19.5f), th, true);
                c.Disc(U(3), U(7.5f), U(1.4f));
                c.Disc(U(12), U(4.8f), U(1.4f));
                c.Disc(U(21), U(7.5f), U(1.4f));
                break;
            case NocturneIcon.Badge:
                c.Frame(U(2.5f), U(5), U(21.5f), U(19), U(2.4f), th);
                c.Disc(U(8.3f), U(10.4f), U(2));
                c.Arc(U(8.3f), U(17.6f), U(3.6f), th, 212f, 328f);
                c.Path(P(13.6f, 10, 18.5f, 10), th, false);
                c.Path(P(13.6f, 14, 17.5f, 14), th, false);
                break;
            case NocturneIcon.Folder:
                c.Path(P(3, 19.5f, 3, 5.5f, 9.2f, 5.5f, 11.8f, 8.5f, 21, 8.5f, 21, 19.5f), th, true);
                break;
            case NocturneIcon.Lock:
                c.Slab(U(5), U(10.5f), U(19), U(21), U(2.2f));
                c.Arc(U(12), U(10.5f), U(4.3f), th, 180f, 360f);
                c.Cut(U(10.6f), U(13.4f), U(13.4f), U(16.2f), U(1.4f));
                c.Cut(U(11.35f), U(15.3f), U(12.65f), U(18.4f), U(0.5f));
                break;
            case NocturneIcon.Unlock:
                c.Slab(U(5), U(10.5f), U(19), U(21), U(2.2f));
                c.Arc(U(12), U(6.2f), U(4.3f), th, 180f, 360f);
                c.Line(U(7.7f), U(6.2f), U(7.7f), U(10.5f), th);
                c.Cut(U(10.6f), U(13.4f), U(13.4f), U(16.2f), U(1.4f));
                c.Cut(U(11.35f), U(15.3f), U(12.65f), U(18.4f), U(0.5f));
                break;
            case NocturneIcon.Radar:
                c.Ring(0.5f, 0.5f, U(9.6f), th);
                c.Ring(0.5f, 0.5f, U(5.3f), th);
                c.Line(0.5f, 0.5f, U(18.6f), U(5.4f), th);
                c.Disc(0.5f, 0.5f, U(1.7f));
                c.Disc(U(16.4f), U(15.8f), U(1.5f));
                break;
            case NocturneIcon.Copy:
                c.Line(0.24f, 0.20f, 0.60f, 0.20f, th);
                c.Line(0.60f, 0.20f, 0.60f, 0.56f, th);
                c.Line(0.60f, 0.56f, 0.24f, 0.56f, th);
                c.Line(0.24f, 0.56f, 0.24f, 0.20f, th);
                c.Line(0.40f, 0.40f, 0.80f, 0.40f, th);
                c.Line(0.80f, 0.40f, 0.80f, 0.80f, th);
                c.Line(0.80f, 0.80f, 0.40f, 0.80f, th);
                c.Line(0.40f, 0.80f, 0.40f, 0.40f, th);
                break;
            case NocturneIcon.Trash:
                c.Line(0.22f, 0.30f, 0.78f, 0.30f, th);
                c.Line(0.40f, 0.22f, 0.60f, 0.22f, th);
                c.Line(0.30f, 0.32f, 0.34f, 0.80f, th);
                c.Line(0.70f, 0.32f, 0.66f, 0.80f, th);
                c.Line(0.34f, 0.80f, 0.66f, 0.80f, th);
                c.Line(0.45f, 0.40f, 0.46f, 0.72f, th);
                c.Line(0.55f, 0.40f, 0.54f, 0.72f, th);
                break;
            case NocturneIcon.Check:
                c.Ring(0.5f, 0.5f, 0.38f, th);
                c.Line(0.31f, 0.52f, 0.45f, 0.66f, th);
                c.Line(0.45f, 0.66f, 0.70f, 0.36f, th);
                break;
            case NocturneIcon.Warn:
                c.Line(0.5f, 0.14f, 0.90f, 0.82f, th);
                c.Line(0.90f, 0.82f, 0.10f, 0.82f, th);
                c.Line(0.10f, 0.82f, 0.5f, 0.14f, th);
                c.Line(0.5f, 0.38f, 0.5f, 0.58f, th);
                c.Disc(0.5f, 0.70f, 0.05f);
                break;
            case NocturneIcon.Error:
                c.Ring(0.5f, 0.5f, 0.38f, th);
                c.Line(0.36f, 0.36f, 0.64f, 0.64f, th);
                c.Line(0.64f, 0.36f, 0.36f, 0.64f, th);
                break;
            case NocturneIcon.Play:
            {
                float[] tri = { 0.32f, 0.20f, 0.32f, 0.80f, 0.82f, 0.50f };
                c.Fill(tri);
                c.Path(tri, 0.09f, true);
                break;
            }
            case NocturneIcon.Pause:
                c.Slab(0.27f, 0.20f, 0.43f, 0.80f, 0.05f);
                c.Slab(0.57f, 0.20f, 0.73f, 0.80f, 0.05f);
                break;
            case NocturneIcon.Prev:
            {
                float[] tri = { 0.80f, 0.22f, 0.80f, 0.78f, 0.34f, 0.50f };
                c.Fill(tri);
                c.Path(tri, 0.08f, true);
                c.Slab(0.18f, 0.22f, 0.28f, 0.78f, 0.035f);
                break;
            }
            case NocturneIcon.Next:
            {
                float[] tri = { 0.20f, 0.22f, 0.20f, 0.78f, 0.66f, 0.50f };
                c.Fill(tri);
                c.Path(tri, 0.08f, true);
                c.Slab(0.72f, 0.22f, 0.82f, 0.78f, 0.035f);
                break;
            }
            case NocturneIcon.Shuffle:
                c.Path(new[] { 0.10f, 0.30f, 0.34f, 0.30f, 0.64f, 0.70f, 0.74f, 0.70f }, th, false);
                c.Path(new[] { 0.10f, 0.70f, 0.34f, 0.70f, 0.64f, 0.30f, 0.74f, 0.30f }, th, false);
                c.Fill(new[] { 0.72f, 0.56f, 0.90f, 0.70f, 0.72f, 0.84f });
                c.Fill(new[] { 0.72f, 0.16f, 0.90f, 0.30f, 0.72f, 0.44f });
                break;
            case NocturneIcon.Repeat:
                c.Frame(0.14f, 0.28f, 0.86f, 0.72f, 0.16f, th);
                c.Cut(0.56f, 0.20f, 0.80f, 0.36f, 0f);
                c.Cut(0.20f, 0.64f, 0.44f, 0.80f, 0f);
                c.Fill(new[] { 0.56f, 0.12f, 0.56f, 0.40f, 0.78f, 0.26f });
                c.Fill(new[] { 0.44f, 0.60f, 0.44f, 0.88f, 0.22f, 0.74f });
                break;
            case NocturneIcon.Volume:
            {
                float[] sp = { 0.12f, 0.40f, 0.28f, 0.40f, 0.48f, 0.22f, 0.48f, 0.78f, 0.28f, 0.60f, 0.12f, 0.60f };
                c.Fill(sp);
                c.Path(sp, 0.06f, true);
                c.Arc(0.48f, 0.5f, 0.20f, th, 315f, 360f);
                c.Arc(0.48f, 0.5f, 0.20f, th, 0f, 45f);
                c.Arc(0.48f, 0.5f, 0.34f, th, 315f, 360f);
                c.Arc(0.48f, 0.5f, 0.34f, th, 0f, 45f);
                break;
            }
            case NocturneIcon.Eq:
                c.Line(0.25f, 0.16f, 0.25f, 0.84f, th);
                c.Line(0.50f, 0.16f, 0.50f, 0.84f, th);
                c.Line(0.75f, 0.16f, 0.75f, 0.84f, th);
                c.Disc(0.25f, 0.62f, 0.10f);
                c.Disc(0.50f, 0.36f, 0.10f);
                c.Disc(0.75f, 0.54f, 0.10f);
                break;
            case NocturneIcon.List:
                c.Line(0.36f, 0.28f, 0.86f, 0.28f, th);
                c.Line(0.36f, 0.50f, 0.86f, 0.50f, th);
                c.Line(0.36f, 0.72f, 0.86f, 0.72f, th);
                c.Disc(0.16f, 0.28f, 0.05f);
                c.Disc(0.16f, 0.50f, 0.05f);
                c.Disc(0.16f, 0.72f, 0.05f);
                break;
            case NocturneIcon.Up:
                c.Path(new[] { 0.26f, 0.62f, 0.50f, 0.38f, 0.74f, 0.62f }, th, false);
                break;
            case NocturneIcon.Down:
                c.Path(new[] { 0.26f, 0.40f, 0.50f, 0.64f, 0.74f, 0.40f }, th, false);
                break;
            case NocturneIcon.Refresh:
                c.Arc(0.5f, 0.5f, 0.30f, th, 20f, 300f);
                c.Fill(new[] { 0.605f, 0.318f, 0.695f, 0.162f, 0.754f, 0.30f });
                break;
            case NocturneIcon.Screen:
                c.Frame(0.12f, 0.18f, 0.88f, 0.66f, 0.07f, th);
                c.Line(0.50f, 0.66f, 0.50f, 0.80f, th);
                c.Line(0.30f, 0.82f, 0.70f, 0.82f, th);
                c.Fill(new[] { 0.43f, 0.31f, 0.43f, 0.53f, 0.62f, 0.42f });
                break;
            case NocturneIcon.Search:
                c.Ring(0.44f, 0.44f, 0.27f, th);
                c.Line(0.64f, 0.64f, 0.86f, 0.86f, th * 1.2f);
                break;
            case NocturneIcon.Arcs:
                c.Arc(0.5f, 1.12f, 0.82f, 0.085f, 247f, 293f);
                c.Arc(0.5f, 1.12f, 0.61f, 0.085f, 244f, 296f);
                c.Arc(0.5f, 1.12f, 0.40f, 0.085f, 237f, 303f);
                break;
            case NocturneIcon.Bars:
                for (int i = 0; i < 5; i++)
                {
                    float half = i == 2 ? 0.34f : i % 2 == 1 ? 0.24f : 0.13f;
                    float x = 0.15f + i * 0.175f;
                    c.Slab(x, 0.5f - half, x + 0.10f, 0.5f + half, 0.05f);
                }
                break;
            case NocturneIcon.Note:
                c.Disc(0.38f, 0.72f, 0.14f);
                c.Line(0.50f, 0.70f, 0.50f, 0.22f, 0.10f);
                c.Fill(new[] { 0.50f, 0.20f, 0.78f, 0.34f, 0.78f, 0.52f, 0.50f, 0.38f });
                break;
            case NocturneIcon.Collapse:
                c.Path(new[] { 0.28f, 0.46f, 0.50f, 0.24f, 0.72f, 0.46f }, th, false);
                c.Path(new[] { 0.28f, 0.74f, 0.50f, 0.52f, 0.72f, 0.74f }, th, false);
                break;
            case NocturneIcon.Expand:
                c.Path(new[] { 0.28f, 0.26f, 0.50f, 0.48f, 0.72f, 0.26f }, th, false);
                c.Path(new[] { 0.28f, 0.54f, 0.50f, 0.76f, 0.72f, 0.54f }, th, false);
                break;
            case NocturneIcon.Chat:
                c.Frame(0.12f, 0.17f, 0.88f, 0.69f, 0.17f, th);
                c.Cut(0.27f, 0.62f, 0.49f, 0.76f, 0f);
                c.Path(new[] { 0.29f, 0.67f, 0.29f, 0.85f, 0.49f, 0.69f }, th, false);
                c.Line(0.30f, 0.38f, 0.70f, 0.38f, th);
                c.Line(0.30f, 0.52f, 0.56f, 0.52f, th);
                break;
            case NocturneIcon.Smile:
                c.Ring(0.5f, 0.5f, 0.38f, th);
                c.Disc(0.375f, 0.42f, 0.05f);
                c.Disc(0.625f, 0.42f, 0.05f);
                c.Arc(0.5f, 0.47f, 0.2f, th, 28f, 152f);
                break;
            case NocturneIcon.Send:
                c.Line(0.5f, 0.78f, 0.5f, 0.25f, th * 1.15f);
                c.Path(new[] { 0.27f, 0.47f, 0.5f, 0.24f, 0.73f, 0.47f }, th * 1.15f, false);
                break;
        }

        return c.ToTexture();
    }

    private sealed class Canvas
    {
        private readonly int _n;
        private readonly float[] _a;

        internal Canvas(int n)
        {
            _n = n;
            _a = new float[n * n];
        }

        private static float Sat(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private void Paint(float x0, float y0, float x1, float y1, Func<float, float, float> cover)
        {
            int xa = Math.Max(0, (int)MathF.Floor(x0)), xb = Math.Min(_n - 1, (int)MathF.Ceiling(x1));
            int ya = Math.Max(0, (int)MathF.Floor(y0)), yb = Math.Min(_n - 1, (int)MathF.Ceiling(y1));
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float a = cover(x + 0.5f, y + 0.5f);
                    if (a > _a[y * _n + x])
                        _a[y * _n + x] = a;
                }
        }

        private static float RectSd(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            float qx = MathF.Abs(px - (x0 + x1) * 0.5f) - hx, qy = MathF.Abs(py - (y0 + y1) * 0.5f) - hy;
            float ox = MathF.Max(qx, 0f), oy = MathF.Max(qy, 0f);
            return MathF.Sqrt(ox * ox + oy * oy) + MathF.Min(MathF.Max(qx, qy), 0f) - r;
        }

        internal void Line(float ax, float ay, float bx, float by, float th)
        {
            float axp = ax * _n, ayp = ay * _n, bxp = bx * _n, byp = by * _n;
            float half = th * _n * 0.5f, pad = half + 1f;
            float vx = bxp - axp, vy = byp - ayp;
            float len2 = MathF.Max(vx * vx + vy * vy, 1e-4f);
            Paint(MathF.Min(axp, bxp) - pad, MathF.Min(ayp, byp) - pad, MathF.Max(axp, bxp) + pad, MathF.Max(ayp, byp) + pad, (px, py) =>
            {
                float t = Sat(((px - axp) * vx + (py - ayp) * vy) / len2);
                float dx = px - (axp + t * vx), dy = py - (ayp + t * vy);
                return Sat(half - MathF.Sqrt(dx * dx + dy * dy) + 0.5f);
            });
        }

        internal void Ring(float cx, float cy, float r, float th)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n, half = th * _n * 0.5f, pad = rp + half + 1f;
            Paint(cxp - pad, cyp - pad, cxp + pad, cyp + pad, (px, py) =>
            {
                float dx = px - cxp, dy = py - cyp;
                return Sat(half - MathF.Abs(MathF.Sqrt(dx * dx + dy * dy) - rp) + 0.5f);
            });
        }

        internal void Disc(float cx, float cy, float r)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n, pad = rp + 1f;
            Paint(cxp - pad, cyp - pad, cxp + pad, cyp + pad, (px, py) =>
            {
                float dx = px - cxp, dy = py - cyp;
                return Sat(0.5f - (MathF.Sqrt(dx * dx + dy * dy) - rp));
            });
        }

        internal void Arc(float cx, float cy, float r, float th, float deg0, float deg1)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n, half = th * _n * 0.5f, pad = rp + half + 1f;
            Paint(cxp - pad, cyp - pad, cxp + pad, cyp + pad, (px, py) =>
            {
                float dx = px - cxp, dy = py - cyp;
                float ang = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                if (ang < 0f)
                    ang += 360f;
                if (ang < deg0 || ang > deg1)
                    return 0f;
                return Sat(half - MathF.Abs(MathF.Sqrt(dx * dx + dy * dy) - rp) + 0.5f);
            });
        }

        internal void Path(float[] p, float th, bool close)
        {
            int n = p.Length / 2;
            for (int i = 0; i < (close ? n : n - 1); i++)
            {
                int j = (i + 1) % n;
                Line(p[i * 2], p[i * 2 + 1], p[j * 2], p[j * 2 + 1], th);
            }
        }

        internal void Fill(float[] p)
        {
            int n = p.Length / 2;
            var q = new float[p.Length];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = 0f, maxY = 0f;
            for (int i = 0; i < n; i++)
            {
                q[i * 2] = p[i * 2] * _n;
                q[i * 2 + 1] = p[i * 2 + 1] * _n;
                minX = MathF.Min(minX, q[i * 2]);
                maxX = MathF.Max(maxX, q[i * 2]);
                minY = MathF.Min(minY, q[i * 2 + 1]);
                maxY = MathF.Max(maxY, q[i * 2 + 1]);
            }
            Paint(minX - 1f, minY - 1f, maxX + 1f, maxY + 1f, (px, py) =>
            {
                float best = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float ax = q[j * 2], ay = q[j * 2 + 1], bx = q[i * 2], by = q[i * 2 + 1];
                    float vx = bx - ax, vy = by - ay;
                    float t = Sat(((px - ax) * vx + (py - ay) * vy) / MathF.Max(vx * vx + vy * vy, 1e-4f));
                    float dx = px - (ax + t * vx), dy = py - (ay + t * vy);
                    best = MathF.Min(best, dx * dx + dy * dy);
                    if ((ay > py) != (by > py) && px < vx * (py - ay) / vy + ax)
                        inside = !inside;
                }
                float d = MathF.Sqrt(best);
                return Sat(0.5f - (inside ? -d : d));
            });
        }

        internal void Star(float cx, float cy, float rOut, float rIn, float th, int tips = 5, bool solid = false)
        {
            int n = tips * 2;
            var pts = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                float r = i % 2 == 0 ? rOut : rIn;
                float a = (-90f + i * 360f / n) * MathF.PI / 180f;
                pts[i * 2] = cx + MathF.Cos(a) * r;
                pts[i * 2 + 1] = cy + MathF.Sin(a) * r;
            }
            if (solid)
                Fill(pts);
            Path(pts, th, true);
        }

        internal void Slab(float x0, float y0, float x1, float y1, float r)
        {
            float a = x0 * _n, b = y0 * _n, c = x1 * _n, d = y1 * _n, rp = r * _n;
            Paint(a - 1f, b - 1f, c + 1f, d + 1f, (px, py) => Sat(0.5f - RectSd(px, py, a, b, c, d, rp)));
        }

        internal void Frame(float x0, float y0, float x1, float y1, float r, float th)
        {
            float a = x0 * _n, b = y0 * _n, c = x1 * _n, d = y1 * _n, rp = r * _n, half = th * _n * 0.5f, pad = half + 1f;
            Paint(a - pad, b - pad, c + pad, d + pad, (px, py) => Sat(half - MathF.Abs(RectSd(px, py, a, b, c, d, rp)) + 0.5f));
        }

        internal void Cut(float x0, float y0, float x1, float y1, float r)
        {
            float a = x0 * _n, b = y0 * _n, c = x1 * _n, d = y1 * _n, rp = r * _n;
            for (int y = Math.Max(0, (int)MathF.Floor(b) - 1); y <= Math.Min(_n - 1, (int)MathF.Ceiling(d) + 1); y++)
                for (int x = Math.Max(0, (int)MathF.Floor(a) - 1); x <= Math.Min(_n - 1, (int)MathF.Ceiling(c) + 1); x++)
                    _a[y * _n + x] *= 1f - Sat(0.5f - RectSd(x + 0.5f, y + 0.5f, a, b, c, d, rp));
        }

        internal Texture2D ToTexture()
        {
            var tex = new Texture2D(_n, _n, TextureFormat.RGBA32, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            var px = new Color32[_n * _n];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(255, 255, 255, (byte)(Sat(_a[(_n - 1 - i / _n) * _n + i % _n]) * 255f + 0.5f));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
