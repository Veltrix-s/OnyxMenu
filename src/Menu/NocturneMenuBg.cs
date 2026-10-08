using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Nocturne;

internal static class NocturneMenuBg
{
    private static readonly string[] Exts = { ".png", ".jpg", ".jpeg" };

    private const int Radius = 16;

    private static Texture2D _tex;
    private static GUIStyle _style;
    private static bool _tried;
    private static float _dim;
    private static int _step;
    private static byte[] _bytes;
    private static Texture2D _raw;
    private static volatile bool _reading;
    private static volatile bool _readDone;

    internal static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "MenuBg");

    internal static bool On => NocturneConfig.MenuBg != null && NocturneConfig.MenuBg.Value;

    internal static void Reload()
    {
        if (_tex != null)
        {
            UnityEngine.Object.Destroy(_tex);
            _tex = null;
        }
        _style = null;
        _tried = false;
        _dim = 0f;
        _step = 0;
        _bytes = null;
        _reading = false;
        _readDone = false;
        if (_raw != null)
        {
            UnityEngine.Object.Destroy(_raw);
            _raw = null;
        }
        _cuts.Clear();
    }

    internal static void Draw(Rect r)
    {
        if (!On) return;
        if (!NocturneStyle.Painting) return;

        Ensure();
        if (_tex == null || _style == null) return;

        Color prev = NocturneStyle.Tint;
        float a = NocturneConfig.MenuBgAlpha.Value * prev.a;

        r = Ui.R(Ui.Round(r.m_XMin), Ui.Round(r.m_YMin), Ui.Round(r.m_Width), Ui.Round(r.m_Height));
        Layout(r);

        NocturneStyle.Tint = Ui.C(1f, 1f, 1f, a);
        for (int i = 0; i < _cuts.Count; i++)
            Slice(_cuts[i], r, _img);

        NocturneStyle.Tint = prev;
        NocturneStyle.FillRounded(r, Ui.C(0f, 0f, 0f, _dim * a), Radius);
    }

    private static readonly List<Rect> _cuts = new List<Rect>(16);
    private static Rect _img;
    private static Rect _laidOut;

    private static void Layout(Rect r)
    {
        if (_cuts.Count > 0 && Ui.Same(_laidOut, r)) return;

        _laidOut = r;
        _cuts.Clear();

        float ka = _tex.width / Ui.Max(1f, _tex.height);
        float kr = r.m_Width / Ui.Max(1f, r.m_Height);
        float bw, bh;
        if (ka > kr)
        {
            bh = r.m_Height;
            bw = bh * ka;
        }
        else
        {
            bw = r.m_Width;
            bh = bw / Ui.Max(0.001f, ka);
        }

        _img = Ui.R(Ui.Round((r.m_Width - bw) * 0.5f), Ui.Round((r.m_Height - bh) * 0.5f), Ui.Ceil(bw), Ui.Ceil(bh));

        int row = 0;
        while (row < Radius)
        {
            float dx = Inset(row);
            if (dx <= 0f)
                break;

            int end = row + 1;
            while (end < Radius && dx - Inset(end) <= 2f && Inset(end) > 0f)
                end++;

            AddRun(r, dx, row, end - row);
            row = end;
        }

        _cuts.Add(Ui.R(r.m_XMin, r.m_YMin + row, r.m_Width, r.m_Height - row * 2f));
    }

    private static float Inset(int row) =>
        Ui.Round(Radius - Ui.Sqrt(Ui.Max(0f, Radius * Radius - (Radius - row - 0.5f) * (Radius - row - 0.5f))));

    private static void AddRun(Rect r, float dx, float top, int len)
    {
        float w = r.m_Width - dx * 2f;
        if (w <= 0f)
            return;

        _cuts.Add(Ui.R(r.m_XMin + dx, r.m_YMin + top, w, len));
        _cuts.Add(Ui.R(r.m_XMin + dx, Ui.Bottom(r) - top - len, w, len));
    }

    private static void Slice(Rect clip, Rect area, Rect img)
    {
        GUI.BeginGroup(clip);
        GUI.Box(Ui.R(img.m_XMin - (clip.m_XMin - area.m_XMin), img.m_YMin - (clip.m_YMin - area.m_YMin), img.m_Width, img.m_Height), NocturneStyle.Blank, _style);
        GUI.EndGroup();
    }

    internal static bool WarmStep()
    {
        if (_tried || !On)
            return false;
        return Step();
    }

    private static void ReadWork(object _)
    {
        byte[] data = null;
        try
        {
            string file = Find();
            if (file != null)
                data = File.ReadAllBytes(file);
        }
        catch { }

        if (_readDone) return;
        _bytes = data;
        _readDone = true;
    }

    private static void Ensure()
    {
        if (!_readDone)
        {
            try
            {
                string file = Find();
                _bytes = file != null ? File.ReadAllBytes(file) : null;
            }
            catch { }
            _readDone = true;
        }

        while (Step())
        {
        }
    }

    private static bool Step()
    {
        if (_tried) return false;

        try
        {
            switch (_step)
            {
                case 0:
                    if (!_readDone)
                    {
                        if (!_reading)
                        {
                            _reading = true;
                            ThreadPool.QueueUserWorkItem(ReadWork);
                        }
                        return true;
                    }
                    _step = 1;
                    if (_bytes == null)
                        break;
                    return true;

                case 1:
                    _step = 2;
                    if (_bytes == null)
                        break;
                    _raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    bool ok = ImageConversion.LoadImage(_raw, _bytes);
                    _bytes = null;
                    if (!ok)
                    {
                        UnityEngine.Object.Destroy(_raw);
                        _raw = null;
                        break;
                    }
                    _raw.hideFlags = HideFlags.HideAndDontSave;
                    _raw.wrapMode = TextureWrapMode.Clamp;
                    return true;

                case 2:
                    _step = 3;
                    if (_raw == null)
                        break;
                    Texture2D use = Shrink(_raw);
                    if (use != _raw)
                        UnityEngine.Object.Destroy(_raw);
                    _raw = null;
                    _tex = use;
                    return true;

                default:
                    if (_tex == null)
                        break;
                    _dim = DimFor(_tex);
                    _style = new GUIStyle();
                    _style.normal.background = _tex;
                    break;
            }
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning("[MenuBg] " + e.Message);
        }

        _tried = true;
        _bytes = null;
        _raw = null;
        return false;
    }

    private static string Find()
    {
        try
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
                return null;
            }

            foreach (string f in Directory.GetFiles(Dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                for (int i = 0; i < Exts.Length; i++)
                    if (ext == Exts[i]) return f;
            }
        }
        catch { }
        return null;
    }

    private static Texture2D Shrink(Texture2D src)
    {
        const int Max = 1024;
        int big = Ui.Max(src.width, src.height);
        if (big <= Max)
            return src;

        float k = Max / (float)big;
        int w = Ui.Max(2, Ui.RoundToInt(src.width * k));
        int h = Ui.Max(2, Ui.RoundToInt(src.height * k));

        RenderTexture rt = null;
        RenderTexture prev = RenderTexture.active;
        try
        {
            rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            dst.ReadPixels(Ui.R(0f, 0f, w, h), 0, 0);
            dst.Apply();
            return dst;
        }
        catch
        {
            return src;
        }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null)
                RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static float DimFor(Texture2D t)
    {
        const int N = 16;

        RenderTexture rt = null;
        RenderTexture prev = RenderTexture.active;
        Texture2D small = null;
        try
        {
            rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;

            small = new Texture2D(N, N, TextureFormat.RGBA32, false);
            small.ReadPixels(Ui.R(0f, 0f, N, N), 0, 0);
            small.Apply();

            Color32[] px = small.GetPixels32();
            if (px.Length == 0)
                return 0.45f;

            float sum = 0f;
            for (int i = 0; i < px.Length; i++)
                sum += (px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f) / 255f;

            return Ui.Lerp(0.18f, 0.72f, Ui.Clamp01(sum / px.Length));
        }
        catch
        {
            return 0.45f;
        }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null)
                RenderTexture.ReleaseTemporary(rt);
            if (small != null)
                UnityEngine.Object.Destroy(small);
        }
    }
}
