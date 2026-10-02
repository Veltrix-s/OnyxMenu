using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(Vent), nameof(Vent.SetButtons))]
internal static class NocturneVentNetworkPatch
{
    private static readonly Dictionary<int, Vent[]> _orig = new Dictionary<int, Vent[]>();
    private static readonly List<Vent> _chain = new List<Vent>();
    private static readonly List<Vent> _rest = new List<Vent>();

    internal static void Reset()
    {
        _orig.Clear();
        _chain.Clear();
        _rest.Clear();
    }

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

            bool on = NocturneConfig.VentNetwork.Value;
            if (!on)
            {
                Vent[] o = _orig[id];
                __instance.Left = o[0];
                __instance.Right = o[1];
                __instance.Center = o[2];
                return;
            }

            if (_chain.Count < 2)
                BuildChain(vents);

            int idx = IndexOf(id);
            if (idx < 0)
            {
                BuildChain(vents);
                idx = IndexOf(id);
                if (idx < 0) return;
            }

            int n = _chain.Count;
            Vent right = _chain[(idx + 1) % n];
            Vent left = n > 2 ? _chain[(idx - 1 + n) % n] : null;

            __instance.Right = right;
            __instance.Left = left;
            __instance.Center = Nearest(__instance, right, left);
        }
        catch { }
    }

    private static int IndexOf(int id)
    {
        for (int i = 0; i < _chain.Count; i++)
            if (_chain[i] != null && _chain[i].Id == id) return i;
        return -1;
    }

    private static void BuildChain(Il2CppReferenceArray<Vent> vents)
    {
        _chain.Clear();
        _rest.Clear();
        for (int i = 0; i < vents.Count; i++)
            if (vents[i] != null) _rest.Add(vents[i]);
        if (_rest.Count == 0) return;

        Vent cur = _rest[0];
        _rest.RemoveAt(0);
        _chain.Add(cur);

        while (_rest.Count > 0)
        {
            Vector2 p = cur.transform.position;
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < _rest.Count; i++)
            {
                float d = Vector2.Distance(p, _rest[i].transform.position);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            cur = _rest[best];
            _rest.RemoveAt(best);
            _chain.Add(cur);
        }
        _rest.Clear();
    }

    private static Vent Nearest(Vent from, Vent a, Vent b)
    {
        Vector2 p = from.transform.position;
        Vent best = null;
        float bd = float.MaxValue;
        for (int i = 0; i < _chain.Count; i++)
        {
            Vent v = _chain[i];
            if (v == null || v.Id == from.Id) continue;
            if (a != null && v.Id == a.Id) continue;
            if (b != null && v.Id == b.Id) continue;

            float d = Vector2.Distance(p, v.transform.position);
            if (d < bd)
            {
                bd = d;
                best = v;
            }
        }
        return best;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NocturneVentNetworkReset
{
    public static void Postfix() => NocturneVentNetworkPatch.Reset();
}
