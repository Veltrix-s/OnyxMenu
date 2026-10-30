using System.Collections.Generic;
using PowerTools;
using UnityEngine;

namespace Nocturne;

internal static class SyncGuard
{
    private static readonly HashSet<string> Told = new HashSet<string>();
    private static readonly HashSet<int> Off = new HashSet<int>();
    private static float _next;

    internal static void Tick()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 2f;

        var all = Object.FindObjectsOfType<SpriteAnimNodeSync>();
        for (int i = 0; i < all.Length; i++)
        {
            SpriteAnimNodeSync s = all[i];
            if (s == null) continue;

            int id = s.GetInstanceID();
            if (s.Parent != null)
            {
                if (Off.Remove(id)) s.enabled = true;
                continue;
            }
            if (!s.enabled) continue;

            s.enabled = false;
            Off.Add(id);
            string path = Where(s.transform);
            if (Told.Count < 8 && Told.Add(path))
                NocturnePlugin.Logger?.LogInfo("node sync without parent disabled: " + path);
        }
    }

    private static string Where(Transform t)
    {
        string path = t.name;
        for (int depth = 0; t.parent != null && depth < 6; depth++)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
