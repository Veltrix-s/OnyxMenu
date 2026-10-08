using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

internal static class NocturneSecurityNotify
{
    private static readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();

    private static bool On => NocturneConfig.SecurityNotify.Value;

    internal static void Once(string key, float gap, string ru, string en)
    {
        float now = Time.unscaledTime;
        if (lastAt.TryGetValue(key, out float at) && now - at < gap) return;
        lastAt[key] = now;
        Fire(ru, en);
    }

    internal static void Fire(string ru, string en, NocturneNotifyKind kind = NocturneNotifyKind.Danger)
    {
        string msg = NocturneText.T(ru, en);
        NocturneEventLog.Add(msg, kind);
        if (!On)
            return;
        try
        {
            NocturneToast.Push(NocturneText.T("Защита", "Guard"), msg, 3.5f, kind);
        }
        catch { }
    }
}
