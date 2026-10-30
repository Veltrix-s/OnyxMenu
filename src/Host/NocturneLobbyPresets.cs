using System.Collections.Generic;
using System.Text;

namespace Nocturne;

internal static class NocturneLobbyPresets
{
    private static readonly List<string> _names = new List<string>();
    private static readonly Dictionary<string, string> _data = new Dictionary<string, string>();
    private static readonly List<string> _visibleNames = new List<string>();
    private static string _raw;
    private static string _visibleRaw;
    private static bool _visibleHideAndSeek;

    private static void Sync()
    {
        string cur = NocturneConfig.LobbyPresets.Value;
        if (cur == _raw)
            return;
        _raw = cur;
        _names.Clear();
        _data.Clear();
        if (string.IsNullOrEmpty(cur))
            return;
        foreach (string line in cur.Split('\n'))
        {
            if (string.IsNullOrEmpty(line))
                continue;
            int bar = line.IndexOf('|');
            if (bar <= 0)
                continue;
            string name = line.Substring(0, bar);
            if (_data.ContainsKey(name))
                continue;
            _names.Add(name);
            _data[name] = line.Substring(bar + 1);
        }
    }

    private static void Flush()
    {
        var sb = new StringBuilder();
        foreach (string n in _names)
        {
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(n).Append('|').Append(_data[n]);
        }
        _raw = sb.ToString();
        NocturneConfig.LobbyPresets.Value = _raw;
    }

    internal static List<string> Names()
    {
        Sync();
        bool hideAndSeek = NocturneLobbySettings.IsHideAndSeek;
        if (_visibleRaw == _raw && _visibleHideAndSeek == hideAndSeek)
            return _visibleNames;
        _visibleRaw = _raw;
        _visibleHideAndSeek = hideAndSeek;
        _visibleNames.Clear();
        foreach (string name in _names)
            if (_data[name].StartsWith("hns1;", System.StringComparison.Ordinal) == hideAndSeek)
                _visibleNames.Add(name);
        return _visibleNames;
    }

    internal static string Save(string name)
    {
        Sync();
        name = Clean(name);
        if (name.Length == 0)
            return NocturneText.T("Пустое имя", "Empty name");
        if (!NocturneLobbySettings.Ready())
            return NocturneText.T("Только хост в лобби", "Host in lobby only");
        bool hideAndSeek = NocturneLobbySettings.IsHideAndSeek;
        if (_data.TryGetValue(name, out string previous) && previous.StartsWith("hns1;", System.StringComparison.Ordinal) != hideAndSeek)
            return NocturneText.T("Это имя занято пресетом другого режима", "This name belongs to a preset for another mode");
        string payload = NocturneLobbySettings.Capture();
        if (!_data.ContainsKey(name))
            _names.Add(name);
        _data[name] = payload;
        int count = 0;
        foreach (string n in _names)
            if (_data[n].StartsWith("hns1;", System.StringComparison.Ordinal) == hideAndSeek)
                count++;
        for (int i = 0; count > 12 && i < _names.Count;)
        {
            string drop = _names[i];
            if (_data[drop].StartsWith("hns1;", System.StringComparison.Ordinal) != hideAndSeek)
            {
                i++;
                continue;
            }
            _names.RemoveAt(i);
            _data.Remove(drop);
            count--;
        }
        Flush();
        return NocturneText.T("Пресет «" + name + "» сохранён", "Preset \"" + name + "\" saved");
    }

    internal static string Apply(string name)
    {
        Sync();
        if (!_data.TryGetValue(name, out string payload))
            return NocturneText.T("Нет пресета", "No such preset");
        if (!NocturneLobbySettings.Ready())
            return NocturneText.T("Только хост в лобби", "Host in lobby only");
        return NocturneLobbySettings.ApplyState(payload)
            ? NocturneText.T("Применён «" + name + "»", "Applied \"" + name + "\"")
            : NocturneText.T("Пресет повреждён или для другого режима", "Preset is corrupt or belongs to another mode");
    }

    internal static void Delete(string name)
    {
        Sync();
        if (!_data.ContainsKey(name))
            return;
        _names.Remove(name);
        _data.Remove(name);
        Flush();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        s = s.Replace("|", "").Replace("\n", "").Replace("\r", "").Trim();
        if (s.Length > 20)
            s = s.Substring(0, 20);
        return s;
    }
}
