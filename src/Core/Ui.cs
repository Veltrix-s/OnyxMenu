using System;
using UnityEngine;

namespace Nocturne;

internal static class Ui
{
    internal static readonly Color White = C(1f, 1f, 1f, 1f);
    internal static readonly Color Black = C(0f, 0f, 0f, 1f);
    internal static readonly Color Clear = C(0f, 0f, 0f, 0f);
    internal static readonly Color Gray = C(0.5f, 0.5f, 0.5f, 1f);
    internal static readonly Color Red = C(1f, 0f, 0f, 1f);

    internal static readonly Vector2 Zero2 = V(0f, 0f);
    internal static readonly Vector2 One2 = V(1f, 1f);
    internal static readonly Vector2 Up2 = V(0f, 1f);
    internal static readonly Vector3 Zero3 = V3(0f, 0f, 0f);
    internal static readonly Vector3 One3 = V3(1f, 1f, 1f);

    internal static Rect R(float x, float y, float w, float h)
    {
        Rect r = default;
        r.m_XMin = x;
        r.m_YMin = y;
        r.m_Width = w;
        r.m_Height = h;
        return r;
    }

    internal static Color C(float r, float g, float b, float a)
    {
        Color c = default;
        c.r = r;
        c.g = g;
        c.b = b;
        c.a = a;
        return c;
    }

    internal static Vector2 V(float x, float y)
    {
        Vector2 v = default;
        v.x = x;
        v.y = y;
        return v;
    }

    internal static Vector3 V3(float x, float y, float z)
    {
        Vector3 v = default;
        v.x = x;
        v.y = y;
        v.z = z;
        return v;
    }

    internal static float Right(Rect r) => r.m_XMin + r.m_Width;

    internal static float Bottom(Rect r) => r.m_YMin + r.m_Height;

    internal static Vector2 Mid(Rect r) => V(r.m_XMin + r.m_Width * 0.5f, r.m_YMin + r.m_Height * 0.5f);

    internal static bool In(Rect r, Vector2 p) =>
        p.x >= r.m_XMin && p.x < r.m_XMin + r.m_Width && p.y >= r.m_YMin && p.y < r.m_YMin + r.m_Height;

    internal static bool Same(Rect a, Rect b) =>
        a.m_XMin == b.m_XMin && a.m_YMin == b.m_YMin && a.m_Width == b.m_Width && a.m_Height == b.m_Height;

    internal static bool Same(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    internal static Color Mix(Color a, Color b, float t)
    {
        t = Clamp01(t);
        return C(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }

    internal static float Clamp(float v, float lo, float hi)
    {
        if (v < lo)
            v = lo;
        else if (v > hi)
            v = hi;
        return v;
    }

    internal static int Clamp(int v, int lo, int hi)
    {
        if (v < lo)
            v = lo;
        else if (v > hi)
            v = hi;
        return v;
    }

    internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    internal static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    internal static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;

    internal static float Repeat(float t, float len) => Clamp(t - MathF.Floor(t / len) * len, 0f, len);

    internal static float PingPong(float t, float len)
    {
        t = Repeat(t, len * 2f);
        return len - MathF.Abs(t - len);
    }

    internal static float MoveTowards(float cur, float target, float step)
    {
        if (MathF.Abs(target - cur) <= step)
            return target;
        return cur + (target - cur >= 0f ? 1f : -1f) * step;
    }

    internal static bool Approximately(float a, float b) =>
        MathF.Abs(b - a) < MathF.Max(0.000001f * MathF.Max(MathF.Abs(a), MathF.Abs(b)), float.Epsilon * 8f);

    internal static float Abs(float f) => MathF.Abs(f);

    internal static int Abs(int i) => Math.Abs(i);

    internal static float Max(float a, float b) => a > b ? a : b;

    internal static int Max(int a, int b) => a > b ? a : b;

    internal static float Min(float a, float b) => a < b ? a : b;

    internal static int Min(int a, int b) => a < b ? a : b;

    internal static float Sqrt(float f) => MathF.Sqrt(f);

    internal static float Sin(float f) => MathF.Sin(f);

    internal static float Cos(float f) => MathF.Cos(f);

    internal static float Pow(float f, float p) => MathF.Pow(f, p);

    internal static float Exp(float f) => MathF.Exp(f);

    internal static float Atan2(float y, float x) => MathF.Atan2(y, x);

    internal static float Floor(float f) => MathF.Floor(f);

    internal static float Ceil(float f) => MathF.Ceiling(f);

    internal static float Round(float f) => MathF.Round(f);

    internal static int RoundToInt(float f) => (int)MathF.Round(f);

    internal static int FloorToInt(float f) => (int)MathF.Floor(f);

    internal static int CeilToInt(float f) => (int)MathF.Ceiling(f);
}
