using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal enum NocturneIcon
{
    Home, Star, People, Eye, Person, Wifi, Info, Gear, Bell, Chevron, Minimize, Close, Shield, Bolt, Door, Crew, Tune, Copy, Trash, Bookmark
}

internal static class NocturneIcons
{
    private const int Size = 48;

    private static readonly Dictionary<NocturneIcon, Texture2D> Cache = new Dictionary<NocturneIcon, Texture2D>();
    private static GUIStyle _style;

    internal static void Draw(NocturneIcon icon, Rect r, Color color)
    {
        if (NocturneStyle.Lite) return;
        if (_style == null) _style = new GUIStyle();
        _style.normal.background = Get(icon);
        Color prev = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, color.a * prev.a);
        GUI.Box(r, GUIContent.none, _style);
        GUI.color = prev;
    }

    private static Texture2D Get(NocturneIcon icon)
    {
        if (Cache.TryGetValue(icon, out Texture2D t) && t != null) return t;
        t = Build(icon);
        Cache[icon] = t;
        return t;
    }

    private static Texture2D Build(NocturneIcon icon)
    {
        var c = new Canvas(Size);
        float th = 0.085f;
        switch (icon)
        {
            case NocturneIcon.Home:
                c.Line(0.5f, 0.10f, 0.13f, 0.47f, th);
                c.Line(0.5f, 0.10f, 0.87f, 0.47f, th);
                c.Line(0.21f, 0.44f, 0.21f, 0.90f, th);
                c.Line(0.79f, 0.44f, 0.79f, 0.90f, th);
                c.Line(0.21f, 0.90f, 0.79f, 0.90f, th);
                break;
            case NocturneIcon.Star:
                c.Star(0.5f, 0.5f, 0.44f, 0.18f, th);
                break;
            case NocturneIcon.Bookmark:
                c.Line(0.31f, 0.16f, 0.69f, 0.16f, th);
                c.Line(0.31f, 0.16f, 0.31f, 0.84f, th);
                c.Line(0.69f, 0.16f, 0.69f, 0.84f, th);
                c.Line(0.31f, 0.84f, 0.5f, 0.66f, th);
                c.Line(0.69f, 0.84f, 0.5f, 0.66f, th);
                break;
            case NocturneIcon.People:
                c.Disc(0.34f, 0.36f, 0.13f);
                c.Arc(0.34f, 0.98f, 0.26f, th, 205f, 335f);
                c.Disc(0.66f, 0.36f, 0.13f);
                c.Arc(0.66f, 0.98f, 0.26f, th, 205f, 335f);
                break;
            case NocturneIcon.Eye:
                c.Ring(0.5f, 0.5f, 0.30f, th);
                c.Disc(0.5f, 0.5f, 0.11f);
                break;
            case NocturneIcon.Person:
                c.Disc(0.5f, 0.31f, 0.15f);
                c.Arc(0.5f, 1.02f, 0.33f, th, 205f, 335f);
                break;
            case NocturneIcon.Wifi:
                c.Disc(0.5f, 0.78f, 0.06f);
                c.Arc(0.5f, 0.80f, 0.22f, th, 225f, 315f);
                c.Arc(0.5f, 0.80f, 0.38f, th, 228f, 312f);
                c.Arc(0.5f, 0.80f, 0.54f, th, 231f, 309f);
                break;
            case NocturneIcon.Info:
                c.Ring(0.5f, 0.5f, 0.38f, th);
                c.Disc(0.5f, 0.30f, 0.055f);
                c.Line(0.5f, 0.44f, 0.5f, 0.72f, th);
                break;
            case NocturneIcon.Gear:
                c.Ring(0.5f, 0.5f, 0.20f, th);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
                    c.Line(0.5f + dx * 0.22f, 0.5f + dy * 0.22f, 0.5f + dx * 0.36f, 0.5f + dy * 0.36f, th * 1.1f);
                }
                break;
            case NocturneIcon.Bell:
                c.Arc(0.5f, 0.52f, 0.26f, th, 180f, 360f);
                c.Line(0.24f, 0.52f, 0.24f, 0.68f, th);
                c.Line(0.76f, 0.52f, 0.76f, 0.68f, th);
                c.Line(0.18f, 0.68f, 0.82f, 0.68f, th);
                c.Disc(0.5f, 0.80f, 0.06f);
                break;
            case NocturneIcon.Chevron:
                c.Line(0.28f, 0.60f, 0.5f, 0.40f, th);
                c.Line(0.72f, 0.60f, 0.5f, 0.40f, th);
                break;
            case NocturneIcon.Minimize:
                c.Line(0.28f, 0.5f, 0.72f, 0.5f, th);
                break;
            case NocturneIcon.Close:
                c.Line(0.30f, 0.30f, 0.70f, 0.70f, th);
                c.Line(0.70f, 0.30f, 0.30f, 0.70f, th);
                break;
            case NocturneIcon.Shield:
                c.Line(0.24f, 0.22f, 0.76f, 0.22f, th);
                c.Line(0.76f, 0.22f, 0.76f, 0.52f, th);
                c.Line(0.76f, 0.52f, 0.5f, 0.86f, th);
                c.Line(0.5f, 0.86f, 0.24f, 0.52f, th);
                c.Line(0.24f, 0.52f, 0.24f, 0.22f, th);
                c.Line(0.37f, 0.47f, 0.47f, 0.58f, th);
                c.Line(0.47f, 0.58f, 0.64f, 0.35f, th);
                break;
            case NocturneIcon.Bolt:
                c.Line(0.62f, 0.12f, 0.38f, 0.52f, th);
                c.Line(0.38f, 0.52f, 0.56f, 0.52f, th);
                c.Line(0.56f, 0.52f, 0.36f, 0.90f, th);
                break;
            case NocturneIcon.Door:
                c.Line(0.34f, 0.14f, 0.34f, 0.88f, th);
                c.Line(0.66f, 0.14f, 0.66f, 0.88f, th);
                c.Line(0.34f, 0.14f, 0.66f, 0.14f, th);
                c.Line(0.28f, 0.88f, 0.72f, 0.88f, th);
                c.Disc(0.59f, 0.52f, 0.045f);
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
                c.Arc(0.48f, 0.44f, 0.18f, th, 180f, 360f);
                c.Line(0.30f, 0.44f, 0.30f, 0.70f, th);
                c.Line(0.66f, 0.44f, 0.66f, 0.70f, th);
                c.Arc(0.48f, 0.70f, 0.18f, th, 0f, 180f);
                c.Disc(0.55f, 0.46f, 0.095f);
                c.Line(0.30f, 0.52f, 0.22f, 0.52f, th);
                c.Line(0.22f, 0.52f, 0.22f, 0.66f, th);
                c.Line(0.22f, 0.66f, 0.30f, 0.66f, th);
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

        internal void Line(float ax, float ay, float bx, float by, float th)
        {
            float axp = ax * _n, ayp = ay * _n, bxp = bx * _n, byp = by * _n;
            float half = th * _n * 0.5f;
            float vx = bxp - axp, vy = byp - ayp;
            float len2 = Mathf.Max(vx * vx + vy * vy, 1e-4f);
            for (int y = 0; y < _n; y++)
            {
                for (int x = 0; x < _n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float t = Mathf.Clamp01(((px - axp) * vx + (py - ayp) * vy) / len2);
                    float cx = axp + t * vx, cy = ayp + t * vy;
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    Put(x, y, Mathf.Clamp01(half - d + 0.5f));
                }
            }
        }

        internal void Ring(float cx, float cy, float r, float th)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n, half = th * _n * 0.5f;
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = Mathf.Abs(Mathf.Sqrt((px - cxp) * (px - cxp) + (py - cyp) * (py - cyp)) - rp);
                    Put(x, y, Mathf.Clamp01(half - d + 0.5f));
                }
        }

        internal void Disc(float cx, float cy, float r)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n;
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d = Mathf.Sqrt((px - cxp) * (px - cxp) + (py - cyp) * (py - cyp)) - rp;
                    Put(x, y, Mathf.Clamp01(0.5f - d));
                }
        }

        internal void Arc(float cx, float cy, float r, float th, float deg0, float deg1)
        {
            float cxp = cx * _n, cyp = cy * _n, rp = r * _n, half = th * _n * 0.5f;
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float ang = Mathf.Atan2(py - cyp, px - cxp) * Mathf.Rad2Deg;
                    if (ang < 0f) ang += 360f;
                    if (ang < deg0 || ang > deg1) continue;
                    float d = Mathf.Abs(Mathf.Sqrt((px - cxp) * (px - cxp) + (py - cyp) * (py - cyp)) - rp);
                    Put(x, y, Mathf.Clamp01(half - d + 0.5f));
                }
        }

        internal void Star(float cx, float cy, float rOut, float rIn, float th)
        {
            var pts = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? rOut : rIn;
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                pts[i * 2] = cx + Mathf.Cos(a) * r;
                pts[i * 2 + 1] = cy + Mathf.Sin(a) * r;
            }
            for (int i = 0; i < 10; i++)
            {
                int j = (i + 1) % 10;
                Line(pts[i * 2], pts[i * 2 + 1], pts[j * 2], pts[j * 2 + 1], th);
            }
        }

        private void Put(int x, int y, float a)
        {
            if (a <= 0f) return;
            int i = y * _n + x;
            if (a > _a[i]) _a[i] = a;
        }

        internal Texture2D ToTexture()
        {
            var tex = new Texture2D(_n, _n, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[_n * _n];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(_a[i]) * 255f));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
