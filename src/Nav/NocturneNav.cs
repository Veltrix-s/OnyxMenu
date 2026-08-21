using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Nocturne;

internal static class NocturneNav
{
    internal sealed class Graph
    {
        public Vector2[] Pos;
        public int[][] Adj;
    }

    private static Graph _g;
    private static int _gMap = -999;

    private static int Mask
    {
        get { try { return LayerMask.GetMask(new[] { "Ship", "Objects" }); } catch { return 1; } }
    }

    internal static int CurrentMapId()
    {
        try
        {
            GameOptionsManager gom = GameOptionsManager.Instance;
            if (gom != null && gom.CurrentGameOptions != null) return gom.CurrentGameOptions.MapId;
        }
        catch { }
        try { if (ShipStatus.Instance != null) return (int)ShipStatus.Instance.Type; } catch { }
        return -1;
    }

    private static string Res(int map)
    {
        switch (map)
        {
            case 0: return "Nocturne.grid.skeld_onx.json";
            case 1: return "Nocturne.grid.mira_onx.json";
            case 2: return "Nocturne.grid.polus_onx.json";
            case 3: return "Nocturne.grid.skeld_onx.json";
            case 4: return "Nocturne.grid.airship_onx.json";
            case 5: return "Nocturne.grid.fungle_onx.json";
            default: return null;
        }
    }

    internal static Graph Current()
    {
        int map = CurrentMapId();
        if (map < 0) return null;
        if (_g != null && _gMap == map) return _g;

        string res = Res(map);
        if (res == null) return null;
        try { _g = Load(res); _gMap = map; }
        catch (Exception e) { NocturnePlugin.Logger?.LogWarning((object)("Nav load failed: " + e.Message)); _g = null; }
        return _g;
    }

    private static Graph Load(string res)
    {
        using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(res);
        if (s == null) return null;
        using StreamReader r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    private static Graph Parse(string json)
    {
        Section(json, "\"hubs\"", out List<Vector2> mp, out List<int[]> ma);
        if (mp == null || mp.Count == 0) return null;

        int mainN = mp.Count;
        var pos = new List<Vector2>(mp);
        var adj = new List<List<int>>(mainN);
        for (int i = 0; i < mainN; i++) adj.Add(new List<int>(ma[i] ?? Array.Empty<int>()));

        Section(json, "\"spurs\"", out List<Vector2> sp, out List<int[]> sa);
        if (sp != null)
        {
            for (int j = 0; j < sp.Count; j++)
            {
                int g = mainN + j;
                pos.Add(sp[j]);
                var mine = new List<int>();
                foreach (int m in sa[j] ?? Array.Empty<int>())
                {
                    if (m < 0 || m >= mainN) continue;
                    mine.Add(m);
                    adj[m].Add(g);
                }
                adj.Add(mine);
            }
        }

        var g2 = new Graph { Pos = pos.ToArray(), Adj = new int[pos.Count][] };
        for (int i = 0; i < adj.Count; i++) g2.Adj[i] = adj[i].ToArray();
        return g2;
    }

    private static void Section(string json, string key, out List<Vector2> pos, out List<int[]> adj)
    {
        pos = null; adj = null;
        int k = json.IndexOf(key, StringComparison.Ordinal);
        if (k < 0) return;
        int open = json.IndexOf('[', k);
        int close = Bracket(json, open);
        if (open < 0 || close < 0) return;

        string sec = json.Substring(open, close - open + 1);
        pos = new List<Vector2>(128);
        adj = new List<int[]>(128);
        int i = 0;
        while (true)
        {
            int os = sec.IndexOf('{', i);
            if (os < 0) break;
            int oe = Brace(sec, os);
            if (oe < 0) break;
            string obj = sec.Substring(os, oe - os + 1);
            i = oe + 1;
            pos.Add(new Vector2(Flt(obj, "\"px\""), Flt(obj, "\"qy\"")));
            adj.Add(Ints(obj, "\"edg\""));
        }
    }

    private static int Bracket(string s, int open)
    {
        if (open < 0 || open >= s.Length || s[open] != '[') return -1;
        int d = 0;
        for (int k = open; k < s.Length; k++)
        {
            if (s[k] == '[') d++;
            else if (s[k] == ']' && --d == 0) return k;
        }
        return -1;
    }

    private static int Brace(string s, int open)
    {
        if (open < 0 || open >= s.Length || s[open] != '{') return -1;
        int d = 0;
        for (int k = open; k < s.Length; k++)
        {
            if (s[k] == '{') d++;
            else if (s[k] == '}' && --d == 0) return k;
        }
        return -1;
    }

    private static float Flt(string obj, string key)
    {
        int k = obj.IndexOf(key, StringComparison.Ordinal);
        if (k < 0) return 0f;
        int c = obj.IndexOf(':', k);
        if (c < 0) return 0f;
        int a = c + 1;
        while (a < obj.Length && char.IsWhiteSpace(obj[a])) a++;
        int b = a;
        while (b < obj.Length && (char.IsDigit(obj[b]) || "+-.eE".IndexOf(obj[b]) >= 0)) b++;
        return float.TryParse(obj.Substring(a, b - a), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    private static int[] Ints(string obj, string key)
    {
        int k = obj.IndexOf(key, StringComparison.Ordinal);
        if (k < 0) return Array.Empty<int>();
        int open = obj.IndexOf('[', k);
        int close = Bracket(obj, open);
        if (open < 0 || close < 0) return Array.Empty<int>();
        string inner = obj.Substring(open + 1, close - open - 1);
        var list = new List<int>();
        int p = 0;
        while (p < inner.Length)
        {
            while (p < inner.Length && !(char.IsDigit(inner[p]) || inner[p] == '-')) p++;
            if (p >= inner.Length) break;
            int st = p;
            while (p < inner.Length && (char.IsDigit(inner[p]) || inner[p] == '-')) p++;
            if (int.TryParse(inner.Substring(st, p - st), out int v)) list.Add(v);
        }
        return list.ToArray();
    }

    private static bool Clear(Vector2 a, Vector2 b)
    {
        try
        {
            Vector2 d = b - a;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            return Physics2D.CircleCast(a, 0.22f, d.normalized, dist, Mask).collider == null;
        }
        catch { return true; }
    }

    private static int[] _order;

    private static int Nearest(Graph g, Vector2 p)
    {
        int n = g.Pos.Length;
        if (n == 0) return -1;
        if (_order == null || _order.Length != n) _order = new int[n];
        for (int i = 0; i < n; i++) _order[i] = i;

        int probe = Mathf.Min(8, n);
        for (int a = 0; a < probe; a++)
        {
            int min = a;
            float md = (g.Pos[_order[a]] - p).sqrMagnitude;
            for (int b = a + 1; b < n; b++)
            {
                float dd = (g.Pos[_order[b]] - p).sqrMagnitude;
                if (dd < md) { md = dd; min = b; }
            }
            (_order[a], _order[min]) = (_order[min], _order[a]);
        }

        for (int a = 0; a < probe; a++)
            if (Clear(p, g.Pos[_order[a]])) return _order[a];
        return _order[0];
    }

    internal static Vector2 NearestWalkable(Vector2 p)
    {
        Graph g = Current();
        if (g == null || g.Pos.Length == 0) return p;
        int i = Nearest(g, p);
        return i >= 0 ? g.Pos[i] : p;
    }

    internal static List<Vector2> FindPath(Vector2 from, Vector2 to)
    {
        Graph g = Current();
        if (g == null) return null;
        if (Clear(from, to)) return new List<Vector2> { from, to };

        int start = Nearest(g, from), goal = Nearest(g, to);
        if (start < 0 || goal < 0) return null;

        int n = g.Pos.Length;
        var gs = new float[n];
        var from2 = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; i++) { gs[i] = float.MaxValue; from2[i] = -1; }
        gs[start] = 0f;

        var open = new List<int> { start };
        while (open.Count > 0)
        {
            int ci = 0; float cf = float.MaxValue;
            for (int k = 0; k < open.Count; k++)
            {
                float f = gs[open[k]] + (g.Pos[open[k]] - g.Pos[goal]).magnitude;
                if (f < cf) { cf = f; ci = k; }
            }
            int cur = open[ci];
            if (cur == goal) break;
            open.RemoveAt(ci);
            closed[cur] = true;

            foreach (int nb in g.Adj[cur])
            {
                if (nb < 0 || nb >= n || closed[nb]) continue;
                float t = gs[cur] + (g.Pos[cur] - g.Pos[nb]).magnitude;
                if (t < gs[nb])
                {
                    gs[nb] = t;
                    from2[nb] = cur;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
        }

        if (from2[goal] < 0 && start != goal) return null;

        var path = new List<Vector2> { to };
        int c = goal, guard = 0;
        while (c >= 0 && guard++ < n + 2)
        {
            path.Add(g.Pos[c]);
            if (c == start) break;
            c = from2[c];
        }
        path.Add(from);
        path.Reverse();
        return path;
    }
}
