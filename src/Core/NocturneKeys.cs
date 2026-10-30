using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace Nocturne;

internal static class NocturneKeys
{
    internal const int NoMod = 0;
    internal const int Ctrl = 1;
    internal const int Alt = 2;
    internal const int Shift = 3;
    internal const int CtrlShift = 4;
    internal const int CtrlAlt = 5;
    internal const int AltShift = 6;
    internal const int CtrlAltShift = 7;

    internal static readonly KeyCode[] ModKeys =
    {
        KeyCode.LeftControl, KeyCode.RightControl,
        KeyCode.LeftAlt, KeyCode.RightAlt,
        KeyCode.LeftShift, KeyCode.RightShift
    };

    private static readonly Dictionary<string, int> _mods = new Dictionary<string, int>();
    private static readonly Dictionary<ConfigEntry<KeyCode>, int> _cache = new Dictionary<ConfigEntry<KeyCode>, int>();
    private static bool _loaded;

    private static int _stateFrame = -1;
    private static bool _ctrl, _alt, _shift;
    private static bool _typing;

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
            if (m < Ctrl || m > CtrlAltShift) continue;
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
        if (mod < Ctrl || mod > CtrlAltShift) _mods.Remove(id);
        else _mods[id] = mod;

        _cache.Clear();
        Save();
    }

    private static readonly string[] _prefixes =
    {
        string.Empty, "CTRL+", "ALT+", "SHIFT+", "CTRL+SHIFT+", "CTRL+ALT+", "ALT+SHIFT+", "CTRL+ALT+SHIFT+"
    };

    internal static string Prefix(int mod) => mod > NoMod && mod <= CtrlAltShift ? _prefixes[mod] : string.Empty;

    internal static bool IsModKey(KeyCode k) =>
        k == KeyCode.LeftControl || k == KeyCode.RightControl
        || k == KeyCode.LeftAlt || k == KeyCode.RightAlt
        || k == KeyCode.LeftShift || k == KeyCode.RightShift
        || k == KeyCode.AltGr;

    private static readonly Dictionary<KeyCode, string> _names = new Dictionary<KeyCode, string>
    {
        { KeyCode.LeftControl, "Left Ctrl" },
        { KeyCode.RightControl, "Right Ctrl" },
        { KeyCode.LeftAlt, "Left Alt" },
        { KeyCode.RightAlt, "Right Alt" },
        { KeyCode.LeftShift, "Left Shift" },
        { KeyCode.RightShift, "Right Shift" },
        { KeyCode.Mouse0, "Left Mouse" },
        { KeyCode.Mouse1, "Right Mouse" },
        { KeyCode.Mouse2, "Middle Mouse" },
        { KeyCode.Mouse3, "Mouse 4" },
        { KeyCode.Mouse4, "Mouse 5" },
        { KeyCode.Mouse5, "Mouse 6" },
        { KeyCode.Mouse6, "Mouse 7" }
    };

    internal static string Name(KeyCode k) => _names.TryGetValue(k, out string s) ? s : k.ToString();

    private static void Refresh()
    {
        if (_stateFrame == Time.frameCount) return;
        _stateFrame = Time.frameCount;
        _ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        _alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        _shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        _typing = UiInput.Typing;
    }

    internal static int Pressed()
    {
        Refresh();
        if (_ctrl)
            return _alt ? (_shift ? CtrlAltShift : CtrlAlt) : _shift ? CtrlShift : Ctrl;
        if (_alt)
            return _shift ? AltShift : Alt;
        return _shift ? Shift : NoMod;
    }

    private static bool Match(ConfigEntry<KeyCode> e) => IsModKey(e.Value) || Pressed() == Mod(e);

    private static bool Muted(ConfigEntry<KeyCode> e)
    {
        Refresh();
        return NocturneMenu.Rebinding || (_typing && e != NocturneConfig.MenuKey);
    }

    internal static bool Down(ConfigEntry<KeyCode> e)
    {
        if (e == null) return false;
        KeyCode key = e.Value;
        if (key == KeyCode.None || !Input.GetKeyDown(key)) return false;
        return !Muted(e) && Match(e);
    }

    internal static bool Held(ConfigEntry<KeyCode> e)
    {
        if (e == null) return false;
        KeyCode key = e.Value;
        if (key == KeyCode.None || !Input.GetKey(key)) return false;
        return !Muted(e) && Match(e);
    }
}
