using System.Collections.Generic;

namespace Nocturne;

internal static class RideTargets
{
    private static readonly HashSet<byte> _set = new HashSet<byte>();

    internal static int Count => _set.Count;
    internal static bool Has(byte pid) => _set.Contains(pid);
    internal static void Toggle(byte pid) { if (!_set.Remove(pid)) _set.Add(pid); }
    internal static void Clear() => _set.Clear();

    internal static void All()
    {
        if (PlayerControl.AllPlayerControls == null) return;
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
            if (pc == PlayerControl.LocalPlayer) continue;
            _set.Add(pc.PlayerId);
        }
    }

    internal static List<PlayerControl> Players()
    {
        var list = new List<PlayerControl>();
        if (_set.Count == 0 || PlayerControl.AllPlayerControls == null) return list;

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
            if (pc == PlayerControl.LocalPlayer) continue;
            if (_set.Contains(pc.PlayerId)) list.Add(pc);
        }
        return list;
    }
}
