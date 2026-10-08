using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Nocturne;

internal sealed class SearchCard
{
    internal int Tab;
    internal int Grp;
    internal string Title;
}

internal sealed class SearchFeat
{
    internal string Key;
    internal int Kind;
    internal string Label;
    internal float Min;
    internal float Max;
    internal string Fmt;
    internal string[] Vals;
    internal string[] Disp;
}

internal static class NocturneSearchIndex
{
    private static readonly List<SearchCard> _cards = new List<SearchCard>();
    private static readonly Dictionary<int, HashSet<string>> _seen = new Dictionary<int, HashSet<string>>();
    private static readonly Dictionary<string, SearchFeat> _feats = new Dictionary<string, SearchFeat>();
    private static bool _loaded;
    private static bool _dirty;
    private static bool _ru;

    private static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne");

    private static string FilePath => Path.Combine(Dir, _ru ? "searchindex_ru.dat" : "searchindex_en.dat");

    internal static Dictionary<string, SearchFeat>.ValueCollection Feats
    {
        get
        {
            Load();
            return _feats.Values;
        }
    }

    private static bool Mark(int tab, string title)
    {
        if (!_seen.TryGetValue(tab, out HashSet<string> set))
        {
            set = new HashSet<string>();
            _seen[tab] = set;
        }
        return set.Add(title);
    }

    internal static void Note(int tab, int grp, string title)
    {
        if (string.IsNullOrEmpty(title)) return;
        Load();
        if (!Mark(tab, title)) return;
        _cards.Add(new SearchCard { Tab = tab, Grp = grp, Title = title });
        _dirty = true;
    }

    internal static void NoteFeat(string key, int kind, string label, float min, float max, string fmt, string[] vals, string[] disp)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(label)) return;
        Load();
        if (_feats.TryGetValue(key, out SearchFeat f)
            && f.Kind == kind && f.Label == label && f.Fmt == fmt
            && f.Min == min && f.Max == max
            && Same(f.Vals, vals) && Same(f.Disp, disp))
            return;

        _feats[key] = new SearchFeat
        {
            Key = key,
            Kind = kind,
            Label = label,
            Min = min,
            Max = max,
            Fmt = fmt,
            Vals = vals != null ? (string[])vals.Clone() : null,
            Disp = disp != null ? (string[])disp.Clone() : null,
        };
        _dirty = true;
    }

    internal static void Collect(string q, List<SearchCard> into)
    {
        Load();
        if (string.IsNullOrEmpty(q)) return;
        for (int i = 0; i < _cards.Count; i++)
            if (_cards[i].Title.ToLowerInvariant().Contains(q)) into.Add(_cards[i]);
    }

    internal static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        try
        {
            Directory.CreateDirectory(Dir);
            var sb = new StringBuilder();
            for (int i = 0; i < _cards.Count; i++)
                sb.Append(_cards[i].Tab).Append('\t').Append(_cards[i].Grp).Append('\t').Append(_cards[i].Title).Append('\n');
            foreach (SearchFeat f in _feats.Values)
            {
                sb.Append("F\t").Append(f.Key).Append('\t').Append(f.Kind).Append('\t').Append(f.Label).Append('\t');
                sb.Append(f.Min.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(f.Max.ToString(CultureInfo.InvariantCulture)).Append('\t');
                sb.Append(f.Fmt).Append('\t');
                sb.Append(f.Vals != null ? string.Join("|", f.Vals) : "").Append('\t');
                sb.Append(f.Disp != null ? string.Join("|", f.Disp) : "").Append('\n');
            }
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { }
    }

    private static void Load()
    {
        bool ru = NocturneText.IsRussian;
        if (_loaded && ru == _ru) return;

        Flush();
        _loaded = true;
        _ru = ru;
        _cards.Clear();
        _seen.Clear();
        _feats.Clear();
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] p = line.Split('\t');
                if (p[0] == "F")
                {
                    SearchFeat f = ReadFeat(p);
                    if (f != null) _feats[f.Key] = f;
                    continue;
                }
                if (p.Length < 3 || !int.TryParse(p[0], out int tab) || !int.TryParse(p[1], out int grp)) continue;
                if (Mark(tab, p[2]))
                    _cards.Add(new SearchCard { Tab = tab, Grp = grp, Title = p[2] });
            }
        }
        catch { }
    }

    private static SearchFeat ReadFeat(string[] p)
    {
        if (p.Length < 9 || !int.TryParse(p[2], out int kind)) return null;
        if (!float.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float min)) return null;
        if (!float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float max)) return null;

        return new SearchFeat
        {
            Key = p[1],
            Kind = kind,
            Label = p[3],
            Min = min,
            Max = max,
            Fmt = p[6].Length > 0 ? p[6] : null,
            Vals = p[7].Length > 0 ? p[7].Split('|') : null,
            Disp = p[8].Length > 0 ? p[8].Split('|') : null,
        };
    }

    private static bool Same(string[] a, string[] b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
