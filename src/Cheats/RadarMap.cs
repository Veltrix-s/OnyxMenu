using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal sealed class RadarGeo
{
    internal readonly List<Vector2[]> Polys = new List<Vector2[]>();
    internal readonly List<Vector2> Roads = new List<Vector2>();
    internal Vector2 Min;
    internal Vector2 Max;

    internal float Width => Max.x - Min.x;

    internal float Height => Max.y - Min.y;

    internal float Aspect => Width / MathF.Max(0.01f, Height);

    internal bool Close()
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (int i = 0; i < Polys.Count; i++)
        {
            Vector2[] p = Polys[i];
            for (int k = 0; k < p.Length; k++)
            {
                x0 = MathF.Min(x0, p[k].x);
                x1 = MathF.Max(x1, p[k].x);
                y0 = MathF.Min(y0, p[k].y);
                y1 = MathF.Max(y1, p[k].y);
            }
        }
        for (int i = 0; i < Roads.Count; i++)
        {
            x0 = MathF.Min(x0, Roads[i].x);
            x1 = MathF.Max(x1, Roads[i].x);
            y0 = MathF.Min(y0, Roads[i].y);
            y1 = MathF.Max(y1, Roads[i].y);
        }
        if (x0 > x1 || y0 > y1)
            return false;

        float mx = (x1 - x0) * 0.02f + 0.4f;
        float my = (y1 - y0) * 0.02f + 0.4f;
        Min = Ui.V(x0 - mx, y0 - my);
        Max = Ui.V(x1 + mx, y1 + my);
        return true;
    }
}

internal readonly struct RadarFit
{
    internal readonly float S;
    private readonly float _ox;
    private readonly float _oy;
    private readonly float _minX;
    private readonly float _maxY;

    internal RadarFit(RadarGeo g, float w, float h)
    {
        float ww = MathF.Max(0.01f, g.Width);
        float wh = MathF.Max(0.01f, g.Height);
        S = MathF.Min(w / ww, h / wh);
        _ox = (w - ww * S) * 0.5f;
        _oy = (h - wh * S) * 0.5f;
        _minX = g.Min.x;
        _maxY = g.Max.y;
    }

    internal Vector2 Px(Vector2 world) => Ui.V(_ox + (world.x - _minX) * S, _oy + (_maxY - world.y) * S);

    internal Vector2 World(Vector2 px) => Ui.V(_minX + (px.x - _ox) / S, _maxY - (px.y - _oy) / S);
}

internal static class RadarMap
{
    private const float Far = 1e9f;

    private static float[] _cov = new float[0];
    private static float[] _edge = new float[0];
    private static float[] _road = new float[0];
    private static Color32[] _px = new Color32[0];
    private static readonly List<float> _xs = new List<float>(16);
    private static readonly List<Vector2> _pp = new List<Vector2>(64);

    internal static Texture2D Bake(Texture2D tex, RadarGeo g, int w, int h, Color accent, int rad)
    {
        w = Ui.Clamp(w, 16, 2048);
        h = Ui.Clamp(h, 16, 2048);
        int n = w * h;
        if (_cov.Length < n)
        {
            _cov = new float[n];
            _edge = new float[n];
            _road = new float[n];
        }
        if (_px.Length != n)
            _px = new Color32[n];
        Array.Clear(_cov, 0, n);
        Array.Fill(_edge, Far, 0, n);
        Array.Fill(_road, Far, 0, n);

        var fit = new RadarFit(g, w, h);
        float u = Ui.Clamp(MathF.Sqrt(w / 220f), 0.8f, 3f);
        float edgeHalf = 0.7f * u;
        float glowW = 5.5f * u;
        float innerW = 4f * u;
        float bandHalf = 2.2f * u;
        float coreHalf = 0.65f * u;

        for (int i = 0; i < g.Polys.Count; i++)
        {
            Vector2[] poly = g.Polys[i];
            if (poly.Length < 3)
                continue;
            _pp.Clear();
            for (int k = 0; k < poly.Length; k++)
                _pp.Add(fit.Px(poly[k]));
            Cover(w, h);
            for (int k = 0; k < _pp.Count; k++)
                Dist(_edge, w, h, _pp[k], _pp[(k + 1) % _pp.Count], glowW + 1f);
        }
        for (int i = 0; i + 1 < g.Roads.Count; i += 2)
            Dist(_road, w, h, fit.Px(g.Roads[i]), fit.Px(g.Roads[i + 1]), bandHalf + 1f);

        float ar = accent.r, ag = accent.g, ab = accent.b;
        float lr = ar + (1f - ar) * 0.32f, lg = ag + (1f - ag) * 0.32f, lb = ab + (1f - ab) * 0.32f;
        int cell = Ui.Max(8, Ui.RoundToInt(w / 11f));
        float corner = rad;

        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)(h - 1);
            float bgR = 0.060f + ar * 0.018f - fy * 0.020f;
            float bgG = 0.068f + ag * 0.018f - fy * 0.022f;
            float bgB = 0.094f + ab * 0.018f - fy * 0.030f;
            float flR = 0.112f + ar * 0.040f - fy * 0.014f;
            float flG = 0.130f + ag * 0.040f - fy * 0.016f;
            float flB = 0.176f + ab * 0.040f - fy * 0.020f;
            float dy = (y + 0.5f) / h * 2f - 1f;
            bool gridRow = y % cell == 0;
            int row = y * w;
            int outRow = (h - 1 - y) * w;

            for (int x = 0; x < w; x++)
            {
                int i = row + x;
                float dx = (x + 0.5f) / w * 2f - 1f;
                float vig = 1f - 0.32f * Ui.Clamp01((dx * dx + dy * dy - 0.30f) / 1.1f);
                float r = bgR * vig, gg = bgG * vig, b = bgB * vig;

                if (gridRow || x % cell == 0)
                {
                    bool major = y % (cell * 4) == 0 || x % (cell * 4) == 0;
                    float gl = major ? 0.062f : 0.030f;
                    r += gl;
                    gg += gl;
                    b += gl * 1.15f;
                }

                float e2 = _edge[i];
                float de = e2 < Far ? MathF.Sqrt(e2) : 99f;

                if (de < glowW)
                {
                    float gl = 1f - de / glowW;
                    gl = gl * gl * 0.17f;
                    r += (ar - r) * gl;
                    gg += (ag - gg) * gl;
                    b += (ab - b) * gl;
                }

                float d2 = _road[i];
                if (d2 < Far)
                {
                    float dd = MathF.Sqrt(d2);
                    float band = Ui.Clamp01(bandHalf - dd + 0.5f) * 0.11f;
                    r += (0.58f - r) * band;
                    gg += (0.68f - gg) * band;
                    b += (0.86f - b) * band;
                    float core = Ui.Clamp01(coreHalf - dd + 0.5f) * 0.30f;
                    r += (0.78f - r) * core;
                    gg += (0.86f - gg) * core;
                    b += (1f - b) * core;
                }

                float f = Ui.Clamp01(_cov[i]);
                if (f > 0f)
                {
                    float ig = de < innerW ? 1f - de / innerW : 0f;
                    ig = ig * ig * 0.10f;
                    float fr = flR + (ar - flR) * ig;
                    float fg = flG + (ag - flG) * ig;
                    float fb = flB + (ab - flB) * ig;
                    float fa = f * 0.93f;
                    r += (fr - r) * fa;
                    gg += (fg - gg) * fa;
                    b += (fb - b) * fa;
                }

                float ea = Ui.Clamp01(edgeHalf - de + 0.5f);
                if (ea > 0f)
                {
                    ea *= 0.96f;
                    r += (lr - r) * ea;
                    gg += (lg - gg) * ea;
                    b += (lb - b) * ea;
                }

                float alpha = 1f;
                float cx = x + 0.5f < corner ? corner - (x + 0.5f) : (x + 0.5f > w - corner ? x + 0.5f - (w - corner) : 0f);
                float cy = y + 0.5f < corner ? corner - (y + 0.5f) : (y + 0.5f > h - corner ? y + 0.5f - (h - corner) : 0f);
                if (cx > 0f && cy > 0f)
                    alpha = Ui.Clamp01(corner - MathF.Sqrt(cx * cx + cy * cy) + 0.5f);

                ref Color32 o = ref _px[outRow + x];
                o.r = (byte)(Ui.Clamp01(r) * 255f + 0.5f);
                o.g = (byte)(Ui.Clamp01(gg) * 255f + 0.5f);
                o.b = (byte)(Ui.Clamp01(b) * 255f + 0.5f);
                o.a = (byte)(alpha * 255f + 0.5f);
            }
        }

        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }
        tex.SetPixels32(_px);
        tex.Apply();
        return tex;
    }

    private static void Cover(int w, int h)
    {
        int n = _pp.Count;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            minY = MathF.Min(minY, _pp[i].y);
            maxY = MathF.Max(maxY, _pp[i].y);
        }
        int y0 = Ui.Max(0, Ui.FloorToInt(minY));
        int y1 = Ui.Min(h - 1, Ui.CeilToInt(maxY));

        for (int y = y0; y <= y1; y++)
        {
            for (int s = 0; s < 4; s++)
            {
                float yc = y + (s + 0.5f) * 0.25f;
                _xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = _pp[i], b = _pp[(i + 1) % n];
                    if ((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc))
                        _xs.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                if (_xs.Count < 2)
                    continue;
                _xs.Sort();
                for (int k = 0; k + 1 < _xs.Count; k += 2)
                    Span(y * w, w, _xs[k], _xs[k + 1]);
            }
        }
    }

    private static void Span(int row, int w, float xa, float xb)
    {
        if (xb <= 0f || xa >= w)
            return;
        xa = MathF.Max(xa, 0f);
        xb = MathF.Min(xb, w);
        int i0 = (int)xa;
        int i1 = (int)xb;
        if (i1 >= w)
            i1 = w - 1;
        if (i0 >= i1)
        {
            _cov[row + i0] += 0.25f * (xb - xa);
            return;
        }
        _cov[row + i0] += 0.25f * (i0 + 1 - xa);
        for (int i = i0 + 1; i < i1; i++)
            _cov[row + i] += 0.25f;
        _cov[row + i1] += 0.25f * (xb - i1);
    }

    private static void Dist(float[] d, int w, int h, Vector2 a, Vector2 b, float reach)
    {
        float vx = b.x - a.x, vy = b.y - a.y;
        float len = MathF.Sqrt(vx * vx + vy * vy);
        if (len > reach * 6f)
        {
            Vector2 m = Ui.V((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f);
            Dist(d, w, h, a, m, reach);
            Dist(d, w, h, m, b, reach);
            return;
        }

        int ix0 = Ui.Max(0, Ui.FloorToInt(MathF.Min(a.x, b.x) - reach));
        int ix1 = Ui.Min(w - 1, Ui.CeilToInt(MathF.Max(a.x, b.x) + reach));
        int iy0 = Ui.Max(0, Ui.FloorToInt(MathF.Min(a.y, b.y) - reach));
        int iy1 = Ui.Min(h - 1, Ui.CeilToInt(MathF.Max(a.y, b.y) + reach));
        float len2 = MathF.Max(vx * vx + vy * vy, 1e-4f);
        float r2 = reach * reach;

        for (int y = iy0; y <= iy1; y++)
        {
            float py = y + 0.5f;
            int row = y * w;
            for (int x = ix0; x <= ix1; x++)
            {
                float px = x + 0.5f;
                float t = Ui.Clamp01(((px - a.x) * vx + (py - a.y) * vy) / len2);
                float dx = px - (a.x + t * vx);
                float dy = py - (a.y + t * vy);
                float dd = dx * dx + dy * dy;
                if (dd < r2 && dd < d[row + x])
                    d[row + x] = dd;
            }
        }
    }
}
