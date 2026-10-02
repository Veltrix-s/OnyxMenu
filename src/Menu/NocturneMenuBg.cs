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
        if (Event.current != null && Event.current.type != EventType.Repaint) return;

        Ensure();
        if (_tex == null || _style == null) return;

        Color prev = GUI.color;
        float a = NocturneConfig.MenuBgAlpha.Value * prev.a;

        r = new Rect(Mathf.Round(r.x), Mathf.Round(r.y), Mathf.Round(r.width), Mathf.Round(r.height));
        Layout(r);

        GUI.color = new Color(1f, 1f, 1f, a);
        for (int i = 0; i < _cuts.Count; i++)
            Slice(_cuts[i], r, _img);

        GUI.color = prev;
        NocturneStyle.FillRounded(r, new Color(0f, 0f, 0f, _dim * a), Radius);
    }

    private static readonly List<Rect> _cuts = new List<Rect>(16);
    private static Rect _img;
    private static Rect _laidOut;

    private static void Layout(Rect r)
    {
        if (_cuts.Count > 0 && _laidOut == r) return;

        _laidOut = r;
        _cuts.Clear();

        float ka = _tex.width / Mathf.Max(1f, (float)_tex.height);
        float kr = r.width / Mathf.Max(1f, r.height);
        float bw, bh;
        if (ka > kr)
        {
            bh = r.height;
            bw = bh * ka;
        }
        else
        {
            bw = r.width;
            bh = bw / Mathf.Max(0.001f, ka);
        }

        _img = new Rect(Mathf.Round((r.width - bw) * 0.5f), Mathf.Round((r.height - bh) * 0.5f), Mathf.Ceil(bw), Mathf.Ceil(bh));

        _cuts.Add(new Rect(r.x, r.y + Radius, r.width, r.height - Radius * 2f));

        float prevDx = -1f;
        float runTop = 0f;
        int runLen = 0;

        for (int i = 0; i <= Radius; i++)
        {
            float dx = i == Radius
                ? 0f
                : Mathf.Round(Radius - Mathf.Sqrt(Mathf.Max(0f, Radius * Radius - (Radius - i - 0.5f) * (Radius - i - 0.5f))));

            if (i > 0 && dx == prevDx)
            {
                runLen++;
                continue;
            }

            if (runLen > 0) AddRun(r, prevDx, runTop, runLen);

            prevDx = dx;
            runTop = i;
            runLen = 1;
        }

        if (runLen > 0)
            AddRun(r, prevDx, runTop, runLen);
    }

    private static void AddRun(Rect r, float dx, float top, int len)
    {
        float w = r.width - dx * 2f;
        if (w <= 0f)
            return;

        _cuts.Add(new Rect(r.x + dx, r.y + top, w, len));
        _cuts.Add(new Rect(r.x + dx, r.yMax - top - len, w, len));
    }

    private static void Slice(Rect clip, Rect area, Rect img)
    {
        GUI.BeginGroup(clip);
        GUI.Box(new Rect(img.x - (clip.x - area.x), img.y - (clip.y - area.y), img.width, img.height), GUIContent.none, _style);
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
            NocturnePlugin.Logger?.LogWarning((object)("[MenuBg] " + e.Message));
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
        int big = Mathf.Max(src.width, src.height);
        if (big <= Max)
            return src;

        float k = Max / (float)big;
        int w = Mathf.Max(2, Mathf.RoundToInt(src.width * k));
        int h = Mathf.Max(2, Mathf.RoundToInt(src.height * k));

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
            dst.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
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
            small.ReadPixels(new Rect(0f, 0f, N, N), 0, 0);
            small.Apply();

            Color32[] px = small.GetPixels32();
            if (px.Length == 0)
                return 0.45f;

            float sum = 0f;
            for (int i = 0; i < px.Length; i++)
                sum += (px[i].r * 0.299f + px[i].g * 0.587f + px[i].b * 0.114f) / 255f;

            return Mathf.Lerp(0.18f, 0.72f, Mathf.Clamp01(sum / px.Length));
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
