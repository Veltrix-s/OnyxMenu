using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nocturne;

internal sealed class SearchCard
{
    internal int Tab;
    internal int Grp;
    internal string Title;
}

internal static class NocturneSearchIndex
{
    private static readonly List<SearchCard> _cards = new List<SearchCard>();
    private static readonly Dictionary<int, HashSet<string>> _seen = new Dictionary<int, HashSet<string>>();
    private static bool _loaded;
    private static bool _dirty;

    private static string FilePath => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "searchindex.dat");

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
            Directory.CreateDirectory(Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne"));
            var sb = new StringBuilder();
            for (int i = 0; i < _cards.Count; i++)
                sb.Append(_cards[i].Tab).Append('\t').Append(_cards[i].Grp).Append('\t').Append(_cards[i].Title).Append('\n');
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { }
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 3 || !int.TryParse(p[0], out int tab) || !int.TryParse(p[1], out int grp)) continue;
                if (Mark(tab, p[2]))
                    _cards.Add(new SearchCard { Tab = tab, Grp = grp, Title = p[2] });
            }
        }
        catch { }
    }
}
