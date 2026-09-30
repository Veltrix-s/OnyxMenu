using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace Nocturne;

// Аккорды: к обычному бинду добавляется один модификатор.
// Совпадение точное — ctrl-бинд не сработает, если зажат ещё и alt.
internal static class NocturneKeys
{
    internal const int NoMod = 0;
    internal const int Ctrl = 1;
    internal const int Alt = 2;
    internal const int Shift = 3;

    private static readonly Dictionary<string, int> _mods = new Dictionary<string, int>();
    private static readonly Dictionary<ConfigEntry<KeyCode>, int> _cache = new Dictionary<ConfigEntry<KeyCode>, int>();
    private static bool _loaded;

    private static int _stateFrame = -1;
    private static bool _ctrl, _alt, _shift;

    private static string Id(ConfigEntry<KeyCode> e) => e.Definition.Section + "/" + e.Definition.Key;

    private static void Load()
    {
        _loaded = true;
        _mods.Clear();
        _cache.Clear();

        string raw = NocturneConfig.KeyMods != null ? NocturneConfig.KeyMods.Value : null;
        if (string.IsNullOrEmpty(raw)) return;

        string[] parts = raw.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            int eq = parts[i].LastIndexOf('=');
            if (eq <= 0 || eq >= parts[i].Length - 1) continue;

            int m;
            if (!int.TryParse(parts[i].Substring(eq + 1), out m)) continue;
            if (m < Ctrl || m > Shift) continue;
            _mods[parts[i].Substring(0, eq)] = m;
        }
    }

    private static void Save()
    {
        if (NocturneConfig.KeyMods == null) return;

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, int> kv in _mods)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.Append(kv.Key).Append('=').Append(kv.Value);
        }
        NocturneConfig.KeyMods.Value = sb.ToString();
    }

    internal static int Mod(ConfigEntry<KeyCode> e)
    {
        if (e == null) return NoMod;
        if (!_loaded) Load();

        int m;
        if (_cache.TryGetValue(e, out m)) return m;

        int found;
        m = _mods.TryGetValue(Id(e), out found) ? found : NoMod;
        _cache[e] = m;
        return m;
    }

    internal static void SetMod(ConfigEntry<KeyCode> e, int mod)
    {
        if (e == null) return;
        if (!_loaded) Load();

        string id = Id(e);
        if (mod < Ctrl || mod > Shift) _mods.Remove(id);
        else _mods[id] = mod;

        _cache.Clear();
        Save();
    }

    internal static void Cycle(ConfigEntry<KeyCode> e) => SetMod(e, (Mod(e) + 1) % 4);

    internal static string Prefix(int mod)
    {
        if (mod == Ctrl) return "CTRL+";
        if (mod == Alt) return "ALT+";
        if (mod == Shift) return "SHIFT+";
        return string.Empty;
    }

    internal static bool IsModKey(KeyCode k) =>
        k == KeyCode.LeftControl || k == KeyCode.RightControl
        || k == KeyCode.LeftAlt || k == KeyCode.RightAlt
        || k == KeyCode.LeftShift || k == KeyCode.RightShift
        || k == KeyCode.AltGr;

    private static void Refresh()
    {
        if (_stateFrame == Time.frameCount) return;
        _stateFrame = Time.frameCount;
        _ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        _alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        _shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private static bool Match(int mod)
    {
        Refresh();
        switch (mod)
        {
            case Ctrl: return _ctrl && !_alt && !_shift;
            case Alt: return _alt && !_ctrl && !_shift;
            case Shift: return _shift && !_ctrl && !_alt;
            default: return !_ctrl && !_alt && !_shift;
        }
    }

    internal static bool Down(ConfigEntry<KeyCode> e)
    {
        if (e == null || e.Value == KeyCode.None) return false;
        if (!Match(Mod(e))) return false;
        return Input.GetKeyDown(e.Value);
    }

    internal static bool Held(ConfigEntry<KeyCode> e)
    {
        if (e == null || e.Value == KeyCode.None) return false;
        if (!Match(Mod(e))) return false;
        return Input.GetKey(e.Value);
    }
}
