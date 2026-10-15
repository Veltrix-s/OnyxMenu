using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Nocturne;

internal static class CoverLookup
{
    internal struct Cand
    {
        internal string Artist;
        internal string Title;
        internal string Url;
        internal long Rank;
    }

    private static readonly HttpClient Http = Make();
    private static readonly object Gate = new object();
    private static readonly string[] Variants =
    {
        "live", "remix", "remaster", "remastered", "version", "ver", "demo", "acoustic", "instrumental",
        "cappella", "cover", "slowed", "sped", "speed", "reverb", "edit", "mix", "karaoke", "tribute", "mashup",
    };
    private static readonly string[] Joins = { ",", "&", "/", ";", " feat", " ft.", " ft ", " x ", " и ", " and " };

    private static readonly DateTime[] Skip = new DateTime[2];

    private static DateTime _next = DateTime.MinValue;

    private static HttpClient Make()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        h.DefaultRequestHeaders.Add("User-Agent", "NocturneMenu");
        return h;
    }

    internal static string Key(string artist, string title)
    {
        string t = Norm(Plain(title));
        if (t.Length == 0)
            return null;
        string a = Norm(Primary(artist));
        string key = (a.Length > 0 ? a + " - " + t : t).Replace(' ', '_');
        return key.Length > 100 ? key.Substring(0, 100) : key;
    }

    internal static byte[] Cached(string dir, string artist, string title)
    {
        try
        {
            string key = Key(artist, title);
            string file = key == null ? null : Path.Combine(dir, key + ".jpg");
            return file != null && File.Exists(file) ? File.ReadAllBytes(file) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static bool Wanted(string dir, string artist, string title)
    {
        try
        {
            string key = Key(artist, title);
            DateTime now = DateTime.UtcNow;
            if (key == null || (now < Skip[0] && now < Skip[1]) || File.Exists(Path.Combine(dir, key + ".jpg")))
                return false;
            string none = Path.Combine(dir, key + ".none");
            return !File.Exists(none) || DateTime.UtcNow - File.GetLastWriteTimeUtc(none) > TimeSpan.FromDays(14);
        }
        catch
        {
            return false;
        }
    }

    internal static byte[] Fetch(string dir, string artist, string title, out string why)
    {
        why = string.Empty;
        string key = Key(artist, title);
        if (key == null)
            return null;
        try
        {
            lock (Gate)
            {
                string file = Path.Combine(dir, key + ".jpg");
                if (File.Exists(file))
                    return File.ReadAllBytes(file);

                int wait = (int)(_next - DateTime.UtcNow).TotalMilliseconds;
                if (wait > 0)
                    Thread.Sleep(wait);
                byte[] bytes;
                bool settled;
                try
                {
                    bytes = Search(artist, title, out why, out settled);
                }
                finally
                {
                    _next = DateTime.UtcNow.AddMilliseconds(700);
                }
                if (bytes == null && !settled)
                    return null;

                Directory.CreateDirectory(dir);
                if (bytes != null)
                    File.WriteAllBytes(file, bytes);
                else
                    File.WriteAllBytes(Path.Combine(dir, key + ".none"), Array.Empty<byte>());
                return bytes;
            }
        }
        catch (Exception e)
        {
            why += e.GetType().Name + ": " + e.Message;
            return null;
        }
    }

    private static byte[] Search(string artist, string title, out string why, out bool settled)
    {
        string q = string.IsNullOrEmpty(artist) ? title : artist + " " + title;
        var sb = new StringBuilder();
        settled = true;
        for (int s = 0; s < 2; s++)
        {
            string name = s == 0 ? "itunes" : "deezer";
            if (DateTime.UtcNow < Skip[s])
            {
                sb.Append(name).Append(": paused; ");
                settled = false;
                continue;
            }
            try
            {
                string url = s == 0 ? Itunes(q, artist, title) : Deezer(q, artist, title);
                if (url == null)
                {
                    sb.Append(name).Append(": no match; ");
                    continue;
                }
                byte[] img = Download(url);
                if (img != null)
                {
                    why = sb.Append(name).Append(": ok").ToString();
                    return img;
                }
                sb.Append(name).Append(": unusable image; ");
                settled = false;
            }
            catch (Exception e)
            {
                Skip[s] = DateTime.UtcNow.AddMinutes(5);
                settled = false;
                sb.Append(name).Append(": ").Append(e.GetType().Name).Append(' ').Append(e.Message).Append("; ");
            }
        }
        why = sb.ToString();
        return null;
    }

    private static string Deezer(string q, string artist, string title)
    {
        string text = Http.GetStringAsync("https://api.deezer.com/search?limit=10&q=" + Uri.EscapeDataString(q)).GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(text);
        var list = new List<Cand>();
        if (doc.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement e in data.EnumerateArray())
            {
                if (!e.TryGetProperty("title", out JsonElement t) || !e.TryGetProperty("artist", out JsonElement a)
                    || !a.TryGetProperty("name", out JsonElement who) || !e.TryGetProperty("album", out JsonElement album))
                    continue;
                list.Add(new Cand
                {
                    Artist = who.GetString(),
                    Title = t.GetString(),
                    Url = album.TryGetProperty("cover_big", out JsonElement cover) ? cover.GetString() : null,
                    Rank = e.TryGetProperty("rank", out JsonElement r) && r.ValueKind == JsonValueKind.Number ? r.GetInt64() : 0L,
                });
            }
        }
        return Pick(list, artist, title);
    }

    private static string Itunes(string q, string artist, string title)
    {
        string text = Http.GetStringAsync("https://itunes.apple.com/search?media=music&entity=song&limit=10&term=" + Uri.EscapeDataString(q)).GetAwaiter().GetResult();
        using JsonDocument doc = JsonDocument.Parse(text);
        var list = new List<Cand>();
        if (doc.RootElement.TryGetProperty("results", out JsonElement res) && res.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement e in res.EnumerateArray())
            {
                if (!e.TryGetProperty("trackName", out JsonElement t) || !e.TryGetProperty("artistName", out JsonElement who)
                    || !e.TryGetProperty("artworkUrl100", out JsonElement art))
                    continue;
                list.Add(new Cand { Artist = who.GetString(), Title = t.GetString(), Url = art.GetString()?.Replace("100x100bb", "600x600bb") });
            }
        }
        return Pick(list, artist, title);
    }

    private static byte[] Download(string url)
    {
        byte[] b = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        if (b.Length < 3000 || b.Length > 8 << 20)
            return null;
        bool jpg = b[0] == 0xFF && b[1] == 0xD8;
        bool png = b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
        return jpg || png ? b : null;
    }

    internal static string Pick(List<Cand> list, string artist, string title)
    {
        string[] want = Tokens(Strip(title));
        if (want.Length == 0)
            return null;
        string[] who = Tokens(Primary(artist));
        bool plain = !Variant(title);

        Cand best = default;
        bool found = false;
        bool bestBad = false;
        foreach (Cand c in list)
        {
            if (c.Url == null || c.Title == null || c.Artist == null)
                continue;
            if (!SameSet(Tokens(Strip(c.Title)), want))
                continue;
            if (who.Length > 0 && !Has(Tokens(c.Artist), who))
                continue;
            bool bad = plain && Variant(c.Title);
            if (!found || (bestBad && !bad) || (bestBad == bad && c.Rank > best.Rank))
            {
                best = c;
                bestBad = bad;
                found = true;
            }
        }
        if (!found)
            return null;

        if (who.Length == 0)
        {
            if (best.Rank <= 0)
                return null;
            string[] owner = Tokens(best.Artist);
            long other = 0;
            foreach (Cand c in list)
            {
                if (c.Title == null || c.Artist == null || !SameSet(Tokens(Strip(c.Title)), want) || SameSet(Tokens(c.Artist), owner))
                    continue;
                other = Math.Max(other, c.Rank);
            }
            if (other * 3 > best.Rank)
                return null;
        }
        return best.Url;
    }

    private static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        var sb = new StringBuilder(s.Length);
        bool gap = true;
        foreach (char raw in s)
        {
            char ch = char.ToLowerInvariant(raw);
            if (ch == 'ё')
                ch = 'е';
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                gap = false;
            }
            else if (!gap)
            {
                sb.Append(' ');
                gap = true;
            }
        }
        return sb.ToString().Trim();
    }

    private static string[] Tokens(string s) => Norm(s).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string Strip(string s) => Brackets(s, false);

    private static string Plain(string s) => Brackets(s, true);

    private static string Brackets(string s, bool onlySites)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        var sb = new StringBuilder(s.Length);
        int from = -1;
        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '(' || ch == '[')
            {
                if (depth++ == 0)
                    from = i;
            }
            else if ((ch == ')' || ch == ']') && depth > 0)
            {
                if (--depth == 0)
                {
                    string inner = s.Substring(from, i - from + 1);
                    if (onlySites && !inner.Contains('.'))
                        sb.Append(inner);
                }
            }
            else if (depth == 0)
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string Primary(string artist)
    {
        if (string.IsNullOrEmpty(artist))
            return string.Empty;
        int cut = artist.Length;
        foreach (string j in Joins)
        {
            int at = artist.IndexOf(j, StringComparison.OrdinalIgnoreCase);
            if (at > 0 && at < cut && !(j == "/" && at < 3))
                cut = at;
        }
        return artist.Substring(0, cut);
    }

    private static bool Variant(string title)
    {
        foreach (string t in Tokens(title))
            if (Array.IndexOf(Variants, t) >= 0)
                return true;
        return false;
    }

    private static bool Has(string[] big, string[] small)
    {
        foreach (string s in small)
            if (Array.IndexOf(big, s) < 0)
                return false;
        return true;
    }

    private static bool SameSet(string[] a, string[] b) => Has(a, b) && Has(b, a);
}
