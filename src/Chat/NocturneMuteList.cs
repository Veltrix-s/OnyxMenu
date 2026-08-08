using System;
using System.Collections.Generic;

namespace Nocturne;

internal static class NocturneMuteList
{
    private static readonly HashSet<string> Muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal static bool IsMuted(string fc) => !string.IsNullOrWhiteSpace(fc) && Muted.Contains(fc.Trim());

    internal static bool Toggle(string fc)
    {
        if (string.IsNullOrWhiteSpace(fc)) return false;
        fc = fc.Trim();
        if (Muted.Contains(fc)) { Muted.Remove(fc); return false; }
        Muted.Add(fc);
        return true;
    }
}
