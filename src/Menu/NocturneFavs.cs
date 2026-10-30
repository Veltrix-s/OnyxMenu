using System.Collections.Generic;
using BepInEx.Configuration;

namespace Nocturne;

internal static class NocturneFavs
{
    internal static readonly List<string> Order = new List<string>();
    internal static readonly HashSet<string> Set = new HashSet<string>();

    private static bool _loaded;

    internal static int Version;

    internal static string KeyOf(ConfigEntryBase e) => e.Definition.Section + "/" + e.Definition.Key;

    internal static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;

        string csv = NocturneConfig.FavFeatures != null ? NocturneConfig.FavFeatures.Value : "";
        if (!string.IsNullOrEmpty(csv))
        {
            foreach (string raw in csv.Split(';'))
            {
                string k = raw.Trim();
                if (k.Length > 0 && Set.Add(k))
                    Order.Add(k);
            }
        }

        string legacy = NocturneConfig.RadialFavorites != null ? NocturneConfig.RadialFavorites.Value : "";
        if (string.IsNullOrEmpty(legacy))
            return;

        foreach (string raw in legacy.Split(','))
        {
            QuickItem it = NocturneQuick.ById(raw.Trim());
            if (it != null && Set.Add(it.Key))
                Order.Add(it.Key);
        }
        NocturneConfig.RadialFavorites.Value = "";
        Save();
    }

    internal static bool Contains(string key)
    {
        Load();
        return Set.Contains(key);
    }

    internal static bool Toggle(string key)
    {
        Load();
        if (Set.Remove(key))
        {
            Order.Remove(key);
            Save();
            return false;
        }

        Set.Add(key);
        Order.Add(key);
        Save();
        return true;
    }

    internal static void Save()
    {
        Version++;
        if (NocturneConfig.FavFeatures != null)
            NocturneConfig.FavFeatures.Value = string.Join(";", Order.ToArray());
    }
}
