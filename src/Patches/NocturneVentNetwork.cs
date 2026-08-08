using System.Collections.Generic;
using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(Vent), nameof(Vent.SetButtons))]
internal static class NocturneVentNetworkPatch
{
    private static readonly Dictionary<int, Vent[]> _orig = new Dictionary<int, Vent[]>();

    internal static void Reset() => _orig.Clear();

    public static void Prefix(Vent __instance)
    {
        try
        {
            if (__instance == null || ShipStatus.Instance == null) return;
            var vents = ShipStatus.Instance.AllVents;
            if (vents == null || vents.Count < 2) return;

            int id = __instance.Id;
            if (!_orig.ContainsKey(id))
                _orig[id] = new[] { __instance.Left, __instance.Right, __instance.Center };

            bool on = NocturneConfig.VentNetwork != null && NocturneConfig.VentNetwork.Value;
            if (!on)
            {
                Vent[] o = _orig[id];
                __instance.Left = o[0];
                __instance.Right = o[1];
                __instance.Center = o[2];
                return;
            }

            int idx = -1;
            for (int i = 0; i < vents.Count; i++)
                if (vents[i] != null && vents[i].Id == id) { idx = i; break; }
            if (idx < 0) return;

            int n = vents.Count;
            __instance.Right = vents[(idx + 1) % n];
            __instance.Left = vents[(idx - 1 + n) % n];
            __instance.Center = n > 2 ? vents[(idx + n / 2) % n] : null;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NocturneVentNetworkReset
{
    public static void Postfix() => NocturneVentNetworkPatch.Reset();
}
