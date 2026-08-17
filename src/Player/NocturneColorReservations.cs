using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Nocturne;

internal static class NocturneColorReservations
{
    internal sealed class Entry
    {
        internal readonly string Fc;
        internal readonly int ColorId;
        internal readonly string Name;
        internal Entry(string fc, int colorId, string name) { Fc = fc ?? string.Empty; ColorId = colorId; Name = name ?? fc ?? string.Empty; }
    }

    private static readonly List<Entry> Cache = new List<Entry>();
    private static bool _loaded;

    private static string FilePath => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne", "ColorReservations.txt");

    internal static IReadOnlyList<Entry> All() { EnsureLoaded(); return Cache; }

    internal static bool TryGet(string fc, out Entry entry)
    {
        EnsureLoaded();
        fc = Norm(fc);
        entry = null;
        for (int i = 0; i < Cache.Count; i++)
            if (Norm(Cache[i].Fc) == fc) { entry = Cache[i]; return true; }
        return false;
    }

    internal static void AddOrUpdate(string fc, int colorId, string name)
    {
        EnsureLoaded();
        string n = Norm(fc);
        Cache.RemoveAll(e => Norm(e.Fc) == n);
        Cache.Add(new Entry(fc.Trim(), colorId, name));
        Save();
    }

    internal static void Remove(string fc)
    {
        EnsureLoaded();
        string n = Norm(fc);
        if (Cache.RemoveAll(e => Norm(e.Fc) == n) > 0) Save();
    }

    internal static bool TryApplyOnJoin(PlayerControl player)
    {
        if (!NocturneConfig.ColorReservationsEnabled.Value) return false;
        if (player == null || player.Data == null || player.Data.DefaultOutfit == null) return false;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;

        string fc = Fc(player);
        if (string.IsNullOrWhiteSpace(fc)) return false;
        if (!TryGet(fc, out Entry res)) return false;

        int target = Mathf.Clamp(res.ColorId, 0, MaxColor());
        if (player.Data.DefaultOutfit.ColorId == target) return false;

        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl other = e.Current;
                if (other == null || other.Data == null || other.Data.DefaultOutfit == null) continue;
                if (other.PlayerId == player.PlayerId) continue;
                if (other.Data.DefaultOutfit.ColorId != target) continue;

                string otherFc = Fc(other);
                if (!string.IsNullOrWhiteSpace(otherFc) && TryGet(otherFc, out Entry otherRes) && otherRes.ColorId == target)
                    return false;

                int free = FreeColor(target);
                if (free >= 0) other.RpcSetColor((byte)free);
                break;
            }

            player.RpcSetColor((byte)target);
            return true;
        }
        catch { return false; }
    }

    private static int FreeColor(int exclude)
    {
        int max = MaxColor();
        var used = new HashSet<int>();
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p != null && p.Data != null && p.Data.DefaultOutfit != null) used.Add(p.Data.DefaultOutfit.ColorId);
            }
        }
        catch { }
        for (int i = 0; i <= max; i++)
            if (i != exclude && !used.Contains(i)) return i;
        return -1;
    }

    internal static int MaxColor()
    {
        try { if (Palette.PlayerColors != null) return Mathf.Max(0, Palette.PlayerColors.Length - 1); }
        catch { }
        return 17;
    }

    internal static string Fc(PlayerControl p)
    {
        try { return p != null && p.Data != null ? (p.Data.FriendCode ?? string.Empty).Trim().ToLowerInvariant() : null; }
        catch { return null; }
    }

    private static string Norm(string fc) => fc == null ? string.Empty : fc.Trim().ToLowerInvariant();

    private static void EnsureLoaded() { if (_loaded) return; _loaded = true; Load(); }

    private static void Load()
    {
        Cache.Clear();
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split('|');
                if (p.Length < 2) continue;
                string fc = p[0].Trim();
                if (fc.Length == 0 || !int.TryParse(p[1].Trim(), out int col)) continue;
                string name = p.Length >= 3 ? p[2].Trim() : fc;
                Cache.Add(new Entry(fc, col, name.Length > 0 ? name : fc));
            }
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var sb = new StringBuilder();
            sb.Append(NocturneText.T("# Nocturne — FriendCode | ColorId | Имя\n", "# Nocturne — FriendCode | ColorId | Name\n"));
            foreach (Entry e in Cache) sb.Append(e.Fc).Append('|').Append(e.ColorId).Append('|').Append(e.Name).Append('\n');
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
}
