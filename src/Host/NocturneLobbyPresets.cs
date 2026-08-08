using System.Collections.Generic;
using System.Text;

namespace Nocturne;

internal static class NocturneLobbyPresets
{
    private static readonly List<string> _names = new List<string>();
    private static readonly Dictionary<string, string> _data = new Dictionary<string, string>();
    private static string _raw;

    private static void Sync()
    {
        string cur = NocturneConfig.LobbyPresets != null ? NocturneConfig.LobbyPresets.Value : "";
        if (cur == _raw) return;
        _raw = cur;
        _names.Clear();
        _data.Clear();
        if (string.IsNullOrEmpty(cur)) return;
        foreach (string line in cur.Split('\n'))
        {
            if (string.IsNullOrEmpty(line)) continue;
            int bar = line.IndexOf('|');
            if (bar <= 0) continue;
            string name = line.Substring(0, bar);
            if (_data.ContainsKey(name)) continue;
            _names.Add(name);
            _data[name] = line.Substring(bar + 1);
        }
    }

    private static void Flush()
    {
        var sb = new StringBuilder();
        foreach (string n in _names)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(n).Append('|').Append(_data[n]);
        }
        _raw = sb.ToString();
        if (NocturneConfig.LobbyPresets != null) NocturneConfig.LobbyPresets.Value = _raw;
    }

    internal static List<string> Names()
    {
        Sync();
        return _names;
    }

    internal static string Save(string name)
    {
        Sync();
        name = Clean(name);
        if (name.Length == 0) return NocturneText.T("Пустое имя", "Empty name");
        if (!NocturneLobbySettings.Ready()) return NocturneText.T("Только хост в лобби", "Host in lobby only");
        string payload = NocturneLobbySettings.Capture();
        if (!_data.ContainsKey(name)) _names.Add(name);
        _data[name] = payload;
        while (_names.Count > 12) { string drop = _names[0]; _names.RemoveAt(0); _data.Remove(drop); }
        Flush();
        return NocturneText.T("Пресет «" + name + "» сохранён", "Preset \"" + name + "\" saved");
    }

    internal static string Apply(string name)
    {
        Sync();
        if (!_data.TryGetValue(name, out string payload)) return NocturneText.T("Нет пресета", "No such preset");
        if (!NocturneLobbySettings.Ready()) return NocturneText.T("Только хост в лобби", "Host in lobby only");
        return NocturneLobbySettings.ApplyState(payload)
            ? NocturneText.T("Применён «" + name + "»", "Applied \"" + name + "\"")
            : NocturneText.T("Битый пресет", "Corrupt preset");
    }

    internal static void Delete(string name)
    {
        Sync();
        if (!_data.ContainsKey(name)) return;
        _names.Remove(name);
        _data.Remove(name);
        Flush();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("|", "").Replace("\n", "").Replace("\r", "").Trim();
        if (s.Length > 20) s = s.Substring(0, 20);
        return s;
    }
}
