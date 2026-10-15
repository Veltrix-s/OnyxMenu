using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Nocturne;

internal sealed class PlayerCover
{
    internal Texture2D Tile;
    internal Texture2D Back;
    internal GUIStyle TileStyle;
    internal GUIStyle BackStyle;
    internal Color Color;
    internal string Letter = "♪";
    internal bool Real;
    internal bool Dark;
}

internal sealed class PlayerArt
{
    internal Color32[] Tile;
    internal Color32[] Back;
    internal Color Color;
    internal string Letter = string.Empty;
    internal bool Real;
    internal bool Dark;
}

internal sealed class PlayerCovers
{
    internal const int Side = 184;
    private const int Soft = 16;
    private const float Dim = 0.30f;
    private const int BackW = (int)PlayerPoster.W;
    private const int BackH = (int)PlayerPoster.Edge;

    private static readonly string Cache = Path.Combine(BepInEx.Paths.PluginPath, "Nocturne", "Covers");
    private static readonly string[] Exts = { ".jpg", ".jpeg", ".png" };
    private static readonly string[] Names = { "cover", "folder", "front", "album", "albumart" };
    private static readonly float[] Rail = { 0.676f, 0.7125f, 0.7646f, 0.8306f, 0.9094f };
    private static readonly float[] Wire = { 0.004f, 0.0055f, 0.0075f, 0.010f, 0.013f };

    private static readonly Color[] Palette =
    {
        Ui.C(0.72f, 0.10f, 0.16f, 1f),
        Ui.C(0.12f, 0.35f, 0.72f, 1f),
        Ui.C(0.12f, 0.58f, 0.35f, 1f),
        Ui.C(0.62f, 0.32f, 0.05f, 1f),
        Ui.C(0.52f, 0.10f, 0.62f, 1f),
        Ui.C(0.05f, 0.52f, 0.60f, 1f),
        Ui.C(0.65f, 0.50f, 0.08f, 1f),
        Ui.C(0.10f, 0.50f, 0.50f, 1f),
    };

    private readonly Dictionary<string, PlayerCover> _have = new Dictionary<string, PlayerCover>();
    private readonly Queue<string> _order = new Queue<string>();

    internal PlayerCovers()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Color32).TypeHandle);
        RuntimeHelpers.RunClassConstructor(typeof(Color).TypeHandle);
    }

    private struct Rgb
    {
        internal float R, G, B;
    }

    private struct Pal
    {
        internal Color Deep, Mid, Lite, Pop, Paper;
    }

    internal PlayerCover Cached(string path) => _have.TryGetValue(path, out PlayerCover c) ? c : null;

    internal PlayerCover Store(string path, PlayerCover c)
    {
        if (_have.TryGetValue(path, out PlayerCover old))
        {
            Free(c);
            return old;
        }
        _have[path] = c;
        _order.Enqueue(path);
        while (_have.Count > 10)
        {
            string gone = _order.Dequeue();
            if (_have.TryGetValue(gone, out PlayerCover g))
            {
                _have.Remove(gone);
                Free(g);
            }
        }
        return c;
    }

    internal PlayerCover Swap(string path, PlayerCover c)
    {
        if (!_have.TryGetValue(path, out PlayerCover old))
        {
            Store(path, c);
            return null;
        }
        _have[path] = c;
        return old;
    }

    internal static void Release(PlayerCover c) => Free(c);

    private static void Free(PlayerCover c)
    {
        UnityEngine.Object.Destroy(c.Tile);
        UnityEngine.Object.Destroy(c.Back);
    }

    internal sealed class Hit
    {
        internal byte[] Bytes;
        internal string Artist;
        internal string Title;
        internal string Why = string.Empty;
        internal bool Ask;
    }

    internal static Hit Find(string path, string clean, bool online)
    {
        var hit = new Hit();
        try
        {
            string tagArtist = null;
            string tagTitle = null;
            if (path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                hit.Bytes = Tag(path, out tagArtist, out tagTitle);
            hit.Bytes ??= Beside(path);
            hit.Artist = tagArtist;
            hit.Title = tagTitle;
            if (string.IsNullOrWhiteSpace(tagArtist) || string.IsNullOrWhiteSpace(tagTitle))
                Split(clean, out hit.Artist, out hit.Title);
            if (hit.Bytes == null && online)
            {
                hit.Bytes = CoverLookup.Cached(Cache, hit.Artist, hit.Title);
                hit.Ask = hit.Bytes == null && CoverLookup.Wanted(Cache, hit.Artist, hit.Title);
            }
        }
        catch (Exception e)
        {
            NocturnePlugin.Logger?.LogWarning($"[Cover] {Path.GetFileName(path)}: {e.GetType().Name} {e.Message}");
        }
        return hit;
    }

    internal static byte[] Fetch(Hit hit) => CoverLookup.Fetch(Cache, hit.Artist, hit.Title, out hit.Why);

    internal static byte[] Lookup(string artist, string title)
    {
        byte[] have = CoverLookup.Cached(Cache, artist, title);
        if (have != null)
            return have;
        return CoverLookup.Wanted(Cache, artist, title) ? CoverLookup.Fetch(Cache, artist, title, out _) : null;
    }

    private static void Split(string clean, out string artist, out string title)
    {
        int cut = clean.IndexOf(" - ", StringComparison.Ordinal);
        if (cut < 0)
            cut = clean.IndexOf(" – ", StringComparison.Ordinal);
        artist = cut > 0 ? clean.Substring(0, cut).Trim() : null;
        title = (cut > 0 ? clean.Substring(cut + 3) : clean).Trim();
    }

    private static byte[] Beside(string path)
    {
        string dir = Path.GetDirectoryName(path);
        if (dir == null)
            return null;
        string own = Path.GetFileNameWithoutExtension(path);
        for (int n = -1; n < Names.Length; n++)
        {
            string name = n < 0 ? own : Names[n];
            for (int e = 0; e < Exts.Length; e++)
            {
                string f = Path.Combine(dir, name + Exts[e]);
                if (!File.Exists(f))
                    continue;
                long len = new FileInfo(f).Length;
                if (len > 0 && len <= 12 << 20)
                    return File.ReadAllBytes(f);
            }
        }
        return null;
    }

    private static byte[] Tag(string path, out string artist, out string title)
    {
        artist = null;
        title = null;
        using FileStream fs = File.OpenRead(path);
        var head = new byte[10];
        if (fs.Read(head, 0, 10) < 10 || head[0] != 0x49 || head[1] != 0x44 || head[2] != 0x33)
            return null;
        int ver = head[3];
        int size = Synch(head, 6);
        if (ver < 2 || ver > 4 || size < 10 || size > 16 << 20)
            return null;
        var tag = new byte[size];
        int got = 0;
        while (got < size)
        {
            int n = fs.Read(tag, got, size - got);
            if (n <= 0)
                return null;
            got += n;
        }

        bool unsync = (head[5] & 0x80) != 0;
        if (unsync && ver < 4)
            tag = Unsync(tag);
        int p = 0;
        if (ver >= 3 && (head[5] & 0x40) != 0)
            p = ver == 4 ? Synch(tag, 0) : 4 + Be(tag, 0, 4);

        int hdr = ver == 2 ? 6 : 10;
        byte[] best = null;
        int rank = 0;
        while (p + hdr <= tag.Length && tag[p] != 0)
        {
            int len = ver == 2 ? Be(tag, p + 3, 3) : ver == 4 ? Synch(tag, p + 4) : Be(tag, p + 4, 4);
            int fl = ver == 2 ? 0 : (tag[p + 8] << 8) | tag[p + 9];
            string id = Encoding.ASCII.GetString(tag, p, ver == 2 ? 3 : 4);
            int at = p + hdr;
            if (len <= 0 || at + len > tag.Length)
                break;
            p = at + len;
            if (id == "TIT2" || id == "TT2")
                title ??= Text(Payload(tag, at, len, ver, fl, unsync));
            else if (id == "TPE1" || id == "TP1")
                artist ??= Text(Payload(tag, at, len, ver, fl, unsync));
            if (id != "APIC" && id != "PIC")
                continue;

            byte[] img = Picture(tag, at, len, ver, fl, unsync, out int type);
            int r = type == 3 ? 3 : type == 0 ? 2 : 1;
            if (img != null && r > rank)
            {
                best = img;
                rank = r;
            }
        }
        return best;
    }

    private static byte[] Payload(byte[] tag, int at, int len, int ver, int fl, bool unsync)
    {
        if ((ver == 3 && (fl & 0x00C0) != 0) || (ver == 4 && (fl & 0x000C) != 0))
            return null;
        int s = at;
        if ((ver == 3 && (fl & 0x0020) != 0) || (ver == 4 && (fl & 0x0040) != 0))
            s++;
        if (ver == 4 && (fl & 0x0001) != 0)
            s += 4;
        if (s >= at + len)
            return null;

        var d = new byte[at + len - s];
        Array.Copy(tag, s, d, 0, d.Length);
        if (ver == 4 && (unsync || (fl & 0x0002) != 0))
            d = Unsync(d);
        return d;
    }

    private static string Text(byte[] d)
    {
        if (d == null || d.Length < 2)
            return null;
        int enc = d[0];
        string s;
        if (enc == 1 || enc == 2)
        {
            bool be = enc == 2;
            int from = 1;
            if (d.Length >= 3 && d[1] == 0xFE && d[2] == 0xFF)
            {
                be = true;
                from = 3;
            }
            else if (d.Length >= 3 && d[1] == 0xFF && d[2] == 0xFE)
            {
                be = false;
                from = 3;
            }
            int n = d.Length - from;
            s = (be ? Encoding.BigEndianUnicode : Encoding.Unicode).GetString(d, from, n - n % 2);
        }
        else if (enc == 3)
            s = Encoding.UTF8.GetString(d, 1, d.Length - 1);
        else
        {
            bool high = false;
            for (int i = 1; i < d.Length; i++)
                if (d[i] >= 0x80)
                    high = true;
            if (high)
            {
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    s = Encoding.GetEncoding(1251).GetString(d, 1, d.Length - 1);
                }
                catch
                {
                    return null;
                }
            }
            else
                s = Encoding.ASCII.GetString(d, 1, d.Length - 1);
        }
        int z = s.IndexOf('\0');
        if (z >= 0)
            s = s.Substring(0, z);
        s = s.Trim();
        return s.Length == 0 ? null : s;
    }

    private static byte[] Picture(byte[] tag, int at, int len, int ver, int fl, bool unsync, out int type)
    {
        type = -1;
        byte[] d = Payload(tag, at, len, ver, fl, unsync);
        if (d == null || d.Length < 16)
            return null;

        int enc = d[0];
        int q = 1;
        if (ver == 2)
            q += 3;
        else
        {
            while (q < d.Length && d[q] != 0)
                q++;
            q++;
        }
        if (q >= d.Length)
            return null;
        type = d[q++];
        if (enc == 1 || enc == 2)
        {
            while (q + 1 < d.Length && (d[q] != 0 || d[q + 1] != 0))
                q += 2;
            q += 2;
        }
        else
        {
            while (q < d.Length && d[q] != 0)
                q++;
            q++;
        }
        if (q + 8 > d.Length)
            return null;
        bool jpg = d[q] == 0xFF && d[q + 1] == 0xD8;
        bool png = d[q] == 0x89 && d[q + 1] == 0x50 && d[q + 2] == 0x4E && d[q + 3] == 0x47;
        if (!jpg && !png)
            return null;

        var img = new byte[d.Length - q];
        Array.Copy(d, q, img, 0, img.Length);
        return img;
    }

    private static int Synch(byte[] b, int i) => ((b[i] & 0x7F) << 21) | ((b[i + 1] & 0x7F) << 14) | ((b[i + 2] & 0x7F) << 7) | (b[i + 3] & 0x7F);

    private static int Be(byte[] b, int i, int n)
    {
        int v = 0;
        for (int k = 0; k < n; k++)
            v = (v << 8) | b[i + k];
        return v;
    }

    private static byte[] Unsync(byte[] b)
    {
        var r = new byte[b.Length];
        int n = 0;
        for (int i = 0; i < b.Length; i++)
        {
            r[n++] = b[i];
            if (b[i] == 0xFF && i + 1 < b.Length && b[i + 1] == 0)
                i++;
        }
        Array.Resize(ref r, n);
        return r;
    }

    internal Texture2D Decode(byte[] bytes)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(tex, bytes))
                return tex;
            UnityEngine.Object.Destroy(tex);
        }
        catch
        {
        }
        return null;
    }

    internal static Color32[] Read(Texture2D tex, out int w, out int h)
    {
        w = 0;
        h = 0;
        Color32[] px = null;
        try
        {
            w = tex.width;
            h = tex.height;
            if (w >= 16 && h >= 16 && (long)w * h <= 16000000L)
                px = PlayerGfx.Copy(tex.GetPixels32());
        }
        catch
        {
        }
        UnityEngine.Object.Destroy(tex);
        return px;
    }

    internal static PlayerArt Shrink(Color32[] src, int w, int h)
    {
        int side = Math.Min(w, h);
        float cell = (float)side / Side;
        int n = Math.Min(6, Math.Max(1, (int)MathF.Ceiling(cell)));
        float x0 = (w - side) * 0.5f;
        float y0 = (h - side) * 0.5f;
        var px = new Color32[Side * Side];
        float inv = 1f / (n * n);
        for (int y = 0; y < Side; y++)
        {
            for (int x = 0; x < Side; x++)
            {
                float r = 0f;
                float g = 0f;
                float b = 0f;
                if (cell < 1f)
                {
                    Linear(src, w, h, x0 + (x + 0.5f) * cell, y0 + (y + 0.5f) * cell, ref r, ref g, ref b);
                    px[y * Side + x] = PlayerGfx.Px((byte)r, (byte)g, (byte)b, 255);
                    continue;
                }
                for (int j = 0; j < n; j++)
                {
                    int sy = Math.Min(h - 1, (int)(y0 + (y + (j + 0.5f) / n) * cell));
                    for (int i = 0; i < n; i++)
                    {
                        int sx = Math.Min(w - 1, (int)(x0 + (x + (i + 0.5f) / n) * cell));
                        Color32 s = src[sy * w + sx];
                        r += s.r;
                        g += s.g;
                        b += s.b;
                    }
                }
                px[y * Side + x] = PlayerGfx.Px((byte)(r * inv + 0.5f), (byte)(g * inv + 0.5f), (byte)(b * inv + 0.5f), 255);
            }
        }
        return Compose(px, Dominant(px), string.Empty, true);
    }

    private static void Linear(Color32[] src, int w, int h, float fx, float fy, ref float r, ref float g, ref float b)
    {
        fx = Ui.Clamp(fx - 0.5f, 0f, w - 1f);
        fy = Ui.Clamp(fy - 0.5f, 0f, h - 1f);
        int ix = Math.Min(w - 2, (int)fx);
        int iy = Math.Min(h - 2, (int)fy);
        float tx = fx - ix;
        float ty = fy - iy;
        Color32 a = src[iy * w + ix];
        Color32 c = src[iy * w + ix + 1];
        Color32 d = src[(iy + 1) * w + ix];
        Color32 e = src[(iy + 1) * w + ix + 1];
        r = (a.r * (1f - tx) + c.r * tx) * (1f - ty) + (d.r * (1f - tx) + e.r * tx) * ty;
        g = (a.g * (1f - tx) + c.g * tx) * (1f - ty) + (d.g * (1f - tx) + e.g * tx) * ty;
        b = (a.b * (1f - tx) + c.b * tx) * (1f - ty) + (d.b * (1f - tx) + e.b * tx) * ty;
    }

    private static Color Dominant(Color32[] px)
    {
        float r = 0f;
        float g = 0f;
        float b = 0f;
        float sum = 0f;
        for (int i = 0; i < px.Length; i += 3)
        {
            float cr = px[i].r / 255f;
            float cg = px[i].g / 255f;
            float cb = px[i].b / 255f;
            float hi = Math.Max(cr, Math.Max(cg, cb));
            float lo = Math.Min(cr, Math.Min(cg, cb));
            float w = 0.05f + (hi > 0f ? (hi - lo) / hi : 0f) * (0.4f + hi);
            r += cr * w;
            g += cg * w;
            b += cb * w;
            sum += w;
        }
        PlayerGfx.Hsv(Ui.C(r / sum, g / sum, b / sum, 1f), out float h, out float s, out float v);
        return PlayerGfx.FromHsv(h, Ui.Clamp(s * 1.25f, 0.35f, 0.88f), Ui.Clamp(v * 1.15f, 0.55f, 0.92f));
    }

    internal static PlayerArt Draw(string name)
    {
        uint seed = 2166136261u;
        for (int i = 0; i < name.Length; i++)
            seed = (seed ^ name[i]) * 16777619u;
        PlayerGfx.Hsv(Palette[(int)(seed % (uint)Palette.Length)], out float h, out float s, out float v);
        Color tint = PlayerGfx.FromHsv(h + ((int)((seed >> 8) % 100u) - 50) * 0.0016f, s, v);

        var px = new Color32[Side * Side];
        Paint(px, tint, seed);
        string clean = PlayerGfx.Clean(name);
        string mark = "♪";
        for (int i = 0; i < clean.Length; i++)
        {
            if (char.IsLetterOrDigit(clean[i]))
            {
                mark = clean[i].ToString().ToUpper();
                break;
            }
        }
        float lum = 0f;
        int n = 0;
        for (int y = Side - 54; y < Side - 8; y += 2)
        {
            for (int x = 12; x < 70; x += 2)
            {
                Color32 c = px[y * Side + x];
                lum += (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
                n++;
            }
        }
        PlayerArt art = Compose(px, tint, mark, false);
        art.Dark = lum / n > 0.6f;
        return art;
    }

    private static PlayerArt Compose(Color32[] px, Color color, string letter, bool real)
    {
        Color32[] back = Backdrop(Soften(px));
        Mask(px);
        return new PlayerArt { Tile = px, Back = back, Color = color, Letter = letter, Real = real };
    }

    internal PlayerCover Bake(PlayerArt a)
    {
        var c = new PlayerCover { Color = a.Color, Letter = a.Letter, Real = a.Real, Dark = a.Dark };
        c.Tile = PlayerGfx.Upload(Side, Side, a.Tile);
        c.Back = PlayerGfx.Upload(BackW, BackH, a.Back);
        c.TileStyle = new GUIStyle();
        c.TileStyle.normal.background = c.Tile;
        c.BackStyle = new GUIStyle();
        c.BackStyle.normal.background = c.Back;
        return c;
    }

    private static void Mask(Color32[] px)
    {
        const float rad = 14f;
        float half = Side * 0.5f - rad;
        for (int y = 0; y < Side; y++)
        {
            float qy = MathF.Abs(y + 0.5f - Side * 0.5f) - half;
            for (int x = 0; x < Side; x++)
            {
                float qx = MathF.Abs(x + 0.5f - Side * 0.5f) - half;
                if (qx < -1f && qy < -1f)
                    continue;
                float ox = Math.Max(qx, 0f);
                float oy = Math.Max(qy, 0f);
                float d = MathF.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - rad;
                Color32 c = px[y * Side + x];
                c.a = (byte)(Ui.Clamp01(0.5f - d) * 255f);
                px[y * Side + x] = c;
            }
        }
    }

    private static Color32[] Soften(Color32[] px)
    {
        float k = (float)Side / Soft;
        var a = new float[Soft * Soft * 3];
        for (int y = 0; y < Soft; y++)
        {
            int y0 = (int)(y * k);
            int y1 = Math.Max(y0 + 1, (int)((y + 1) * k));
            for (int x = 0; x < Soft; x++)
            {
                int x0 = (int)(x * k);
                int x1 = Math.Max(x0 + 1, (int)((x + 1) * k));
                float r = 0f;
                float g = 0f;
                float b = 0f;
                for (int yy = y0; yy < y1; yy++)
                {
                    for (int xx = x0; xx < x1; xx++)
                    {
                        Color32 c = px[yy * Side + xx];
                        r += c.r;
                        g += c.g;
                        b += c.b;
                    }
                }
                float inv = 1f / ((y1 - y0) * (x1 - x0));
                int o = (y * Soft + x) * 3;
                a[o] = r * inv;
                a[o + 1] = g * inv;
                a[o + 2] = b * inv;
            }
        }

        var t = new float[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int y = 0; y < Soft; y++)
            {
                for (int x = 0; x < Soft; x++)
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        float sum = 0f;
                        for (int j = -1; j <= 1; j++)
                            for (int i = -1; i <= 1; i++)
                                sum += a[(Ui.Clamp(y + j, 0, Soft - 1) * Soft + Ui.Clamp(x + i, 0, Soft - 1)) * 3 + ch];
                        t[(y * Soft + x) * 3 + ch] = sum / 9f;
                    }
                }
            }
            Array.Copy(t, a, a.Length);
        }

        float mean = 0f;
        for (int i = 0; i < Soft * Soft; i++)
            mean += (a[i * 3] * 0.299f + a[i * 3 + 1] * 0.587f + a[i * 3 + 2] * 0.114f) / 255f;
        mean /= Soft * Soft;
        if (mean < Dim)
        {
            float g = Math.Max(0.5f, MathF.Log(Dim) / MathF.Log(Math.Max(mean, 0.02f)));
            for (int i = 0; i < a.Length; i++)
                a[i] = 255f * MathF.Pow(a[i] / 255f, g);
        }

        var res = new Color32[Soft * Soft];
        for (int i = 0; i < res.Length; i++)
        {
            float r = a[i * 3];
            float g = a[i * 3 + 1];
            float b = a[i * 3 + 2];
            float lum = r * 0.299f + g * 0.587f + b * 0.114f;
            res[i] = PlayerGfx.Px(Boost(r, lum), Boost(g, lum), Boost(b, lum), 255);
        }
        return res;
    }

    private static Color32[] Backdrop(Color32[] soft)
    {
        const float rad = 14f;
        const float drop = 80f;
        float hx = BackW * 0.5f - rad;
        float hy = (BackH + drop) * 0.5f - rad;
        float cy = (BackH - drop) * 0.5f;
        var px = new Color32[BackW * BackH];
        for (int y = 0; y < BackH; y++)
        {
            float sv = 0.133f + 0.734f * (y + 0.5f) / BackH;
            float qy = MathF.Abs(y + 0.5f - cy) - hy;
            for (int x = 0; x < BackW; x++)
            {
                float qx = MathF.Abs(x + 0.5f - BackW * 0.5f) - hx;
                float ox = Math.Max(qx, 0f);
                float oy = Math.Max(qy, 0f);
                float depth = rad - MathF.Sqrt(ox * ox + oy * oy) - Math.Min(Math.Max(qx, qy), 0f);

                float k = 1f - Ui.Clamp01(depth / 56f);
                float shade = 0.72f * (1f - 0.27f * k * k * (3f - 2f * k));
                float grain = 0.03f * Noise(x, y);
                Tap(soft, 0.143f + 0.714f * (x + 0.5f) / BackW, sv, out float r, out float g, out float b);
                px[y * BackW + x] = PlayerGfx.Px(Byte(Grain(r / 255f * shade, grain)), Byte(Grain(g / 255f * shade, grain)), Byte(Grain(b / 255f * shade, grain)), (byte)(Ui.Clamp01(0.5f + depth) * 255f));
            }
        }
        return px;
    }

    private static float Grain(float c, float g) => c + (1f - c) * g;

    private static void Tap(Color32[] soft, float u, float v, out float r, out float g, out float b)
    {
        float fx = Ui.Clamp(u * Soft - 0.5f, 0f, Soft - 1f);
        float fy = Ui.Clamp(v * Soft - 0.5f, 0f, Soft - 1f);
        int ix = Math.Min(Soft - 2, (int)fx);
        int iy = Math.Min(Soft - 2, (int)fy);
        float tx = Smooth(fx - ix);
        float ty = Smooth(fy - iy);
        Color32 a = soft[iy * Soft + ix];
        Color32 c = soft[iy * Soft + ix + 1];
        Color32 d = soft[(iy + 1) * Soft + ix];
        Color32 e = soft[(iy + 1) * Soft + ix + 1];
        r = Ui.Lerp(Ui.Lerp(a.r, c.r, tx), Ui.Lerp(d.r, e.r, tx), ty);
        g = Ui.Lerp(Ui.Lerp(a.g, c.g, tx), Ui.Lerp(d.g, e.g, tx), ty);
        b = Ui.Lerp(Ui.Lerp(a.b, c.b, tx), Ui.Lerp(d.b, e.b, tx), ty);
    }

    private static byte Boost(float c, float lum) => (byte)Ui.Clamp(lum + (c - lum) * 1.25f + 0.5f, 0f, 255f);

    private static void Paint(Color32[] px, Color art, uint seed)
    {
        PlayerGfx.Hsv(art, out float h, out _, out _);
        float shift = (seed & 1u) == 0u ? 0.09f : -0.09f;
        if (((seed >> 1) & 7u) == 0u)
            shift = 0.33f;
        var c = new Pal
        {
            Deep = PlayerGfx.FromHsv(h, 0.80f, 0.17f),
            Mid = PlayerGfx.FromHsv(h, 0.78f, 0.55f),
            Lite = PlayerGfx.FromHsv(h + 0.02f, 0.46f, 0.98f),
            Pop = PlayerGfx.FromHsv(h + shift, 0.80f, 1f),
            Paper = PlayerGfx.FromHsv(h, 0.10f, 0.97f),
        };
        Color tl = Ui.Mix(c.Mid, c.Lite, 0.45f);
        Color tr = Ui.Mix(c.Pop, c.Mid, 0.35f);
        Color br = Ui.Mix(c.Mid, c.Deep, 0.40f);
        int motif = (int)((seed >> 5) % 5u);

        var ridge = new float[4 * Side];
        if (motif == 3)
        {
            for (int k = 0; k < 4; k++)
            {
                uint s = seed >> (3 * k);
                float ph = (s & 15u) / 15f;
                float ph2 = ((s >> 4) & 15u) / 15f;
                for (int x = 0; x < Side; x++)
                {
                    float u = (x + 0.5f) / Side;
                    ridge[k * Side + x] = 0.48f + 0.13f * k
                        + 0.05f * MathF.Sin(MathF.PI * 2f * (u * (0.8f + 0.3f * k) + ph))
                        + 0.022f * MathF.Sin(MathF.PI * 2f * (u * (2.1f + 0.5f * k) + ph2));
                }
            }
        }

        for (int y = 0; y < Side; y++)
        {
            float v = 1f - (y + 0.5f) / Side;
            float fv = Smooth(v);
            for (int x = 0; x < Side; x++)
            {
                float u = (x + 0.5f) / Side;
                float fu = Smooth(u);
                var p = new Rgb
                {
                    R = Bi(tl.r, tr.r, c.Deep.r, br.r, fu, fv),
                    G = Bi(tl.g, tr.g, c.Deep.g, br.g, fu, fv),
                    B = Bi(tl.b, tr.b, c.Deep.b, br.b, fu, fv),
                };

                switch (motif)
                {
                    case 0:
                        Sun(u, v, ref p, c);
                        break;
                    case 1:
                        Arches(u, v, ref p, c);
                        break;
                    case 2:
                        Orbs(u, v, ref p, c);
                        break;
                    case 3:
                        Hills(u, v, x, ref p, c, ridge, seed);
                        break;
                    default:
                        Tiles(u, v, ref p, c, seed);
                        break;
                }

                float dx = u - 0.5f;
                float dy = v - 0.5f;
                float shade = 1f - 0.22f * Smooth((MathF.Sqrt(dx * dx + dy * dy) - 0.30f) / 0.45f);
                float gx = u - 0.18f;
                float gy = v - 0.12f;
                float glow = Ui.Clamp01(1f - MathF.Sqrt(gx * gx + gy * gy) / 0.75f);
                glow = 0.07f * glow * glow;
                float dither = (Noise(x, y) - 0.5f) * (1.6f / 255f);
                px[y * Side + x] = PlayerGfx.Px(Byte(p.R * shade + glow + dither), Byte(p.G * shade + glow + dither), Byte(p.B * shade + glow + dither), 255);
            }
        }
    }

    private static void Sun(float u, float v, ref Rgb p, in Pal c)
    {
        float a = Disc(u, v, 0.5f, 0.42f, 0.27f);
        if (a > 0f)
        {
            a *= 1f - Slab(v, 0.50f, 0.515f);
            a *= 1f - Slab(v, 0.545f, 0.57f);
            a *= 1f - Slab(v, 0.60f, 0.635f);
            Over(ref p, Ui.Mix(c.Paper, c.Pop, Smooth((v - 0.15f) / 0.54f)), a);
        }
        Over(ref p, c.Deep, 0.88f * Ui.Clamp01((v - 0.66f) * Side + 0.5f));
        if (v < 0.66f)
            return;
        for (int k = 0; k < Rail.Length; k++)
            Over(ref p, c.Lite, 0.4f * Ui.Clamp01(Wire[k] * Side * 0.5f + 0.5f - MathF.Abs(v - Rail[k]) * Side));
    }

    private static void Arches(float u, float v, ref Rgb p, in Pal c)
    {
        float dx = u - 0.5f;
        float dy = v - 1.02f;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        Over(ref p, c.Pop, Cov((d - 0.50f) * Side));
        Over(ref p, c.Paper, Cov((d - 0.38f) * Side));
        Over(ref p, c.Lite, Cov((d - 0.26f) * Side));
        Over(ref p, c.Mid, Cov((d - 0.14f) * Side));
        Over(ref p, c.Paper, 0.95f * Disc(u, v, 0.80f, 0.20f, 0.07f));
    }

    private static void Orbs(float u, float v, ref Rgb p, in Pal c)
    {
        Over(ref p, c.Lite, 0.92f * Disc(u, v, 0.36f, 0.58f, 0.30f));
        Over(ref p, c.Pop, 0.88f * Disc(u, v, 0.64f, 0.40f, 0.26f));
        Over(ref p, c.Paper, 0.90f * Disc(u, v, 0.52f, 0.72f, 0.15f));
        float dx = u - 0.5f;
        float dy = v - 0.5f;
        Over(ref p, c.Paper, 0.5f * Ui.Clamp01(1.4f - MathF.Abs(MathF.Sqrt(dx * dx + dy * dy) - 0.43f) * Side));
    }

    private static void Hills(float u, float v, int x, ref Rgb p, in Pal c, float[] ridge, uint seed)
    {
        Over(ref p, c.Paper, 0.95f * Disc(u, v, 0.30f + 0.40f * ((seed >> 9) & 7u) / 7f, 0.27f, 0.11f));
        for (int k = 0; k < 4; k++)
        {
            float r = ridge[k * Side + x];
            float a = Ui.Clamp01((v - r) * Side + 0.5f);
            if (a <= 0f)
                continue;
            Color top = Ui.Mix(c.Mid, c.Lite, 0.5f - 0.15f * k);
            Color bottom = Ui.Mix(c.Mid, c.Deep, 0.25f + 0.25f * k);
            Over(ref p, Ui.Mix(top, bottom, Ui.Clamp01((v - r) / 0.4f)), a);
        }
    }

    private static void Tiles(float u, float v, ref Rgb p, in Pal c, uint seed)
    {
        float fu = u * 3f;
        float fv = v * 3f;
        int i = Math.Min(2, (int)fu);
        int j = Math.Min(2, (int)fv);
        float lu = fu - i;
        float lv = fv - j;
        uint r = (seed + (uint)(i * 7 + j * 13 + 1)) * 2654435761u;
        r ^= r >> 15;
        r *= 2246822519u;
        r ^= r >> 13;

        float cell = Side / 3f;
        int rot = (int)((r >> 8) & 3u);
        float ex = (rot & 1) == 0 ? 0f : 1f;
        float ey = (rot & 2) == 0 ? 0f : 1f;
        float cov = 0f;
        switch ((int)(r % 8u))
        {
            case 1:
                cov = Cov((Dist(lu, lv, ex, ey) - 1f) * cell);
                break;
            case 2:
                cov = Cov((Dist(lu, lv, 0.5f, 0.5f) - 0.40f) * cell);
                break;
            case 3:
                cov = (rot & 1) == 0 ? Cov((Dist(lu, lv, 0.5f, ey) - 0.5f) * cell) : Cov((Dist(lu, lv, ex, 0.5f) - 0.5f) * cell);
                break;
            case 4:
                cov = Cov((Math.Max(MathF.Abs(lu - 0.5f), MathF.Abs(lv - 0.5f)) - 0.34f) * cell);
                break;
            case 5:
                cov = Cov((MathF.Abs(Dist(lu, lv, 0.5f, 0.5f) - 0.34f) - 0.07f) * cell);
                break;
        }
        if (cov <= 0f)
            return;
        uint pick = (r >> 12) & 3u;
        Over(ref p, pick == 0u ? c.Pop : pick == 1u ? c.Paper : pick == 2u ? c.Lite : c.Mid, cov);
    }

    private static float Dist(float u, float v, float cx, float cy)
    {
        float dx = u - cx;
        float dy = v - cy;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Disc(float u, float v, float cx, float cy, float r) => Cov((Dist(u, v, cx, cy) - r) * Side);

    private static float Slab(float v, float a, float b) => Ui.Clamp01(0.5f + Math.Min(v - a, b - v) * Side);

    private static float Cov(float d) => Ui.Clamp01(0.5f - d);

    private static float Smooth(float t)
    {
        t = Ui.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Bi(float a, float b, float c, float d, float fu, float fv) => Ui.Lerp(Ui.Lerp(a, b, fu), Ui.Lerp(c, d, fu), fv);

    private static void Over(ref Rgb p, Color c, float a)
    {
        p.R += (c.r - p.R) * a;
        p.G += (c.g - p.G) * a;
        p.B += (c.b - p.B) * a;
    }

    private static byte Byte(float v) => (byte)(Ui.Clamp01(v) * 255f + 0.5f);

    private static float Noise(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFFu) / 65535f;
    }
}
