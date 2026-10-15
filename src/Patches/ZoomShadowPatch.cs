using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class ZoomShadowPatch
{
    private static bool _wasHidden;

    public static void Postfix(HudManager __instance)
    {
        bool hide = NocturneConfig.Wallhack.Value || VisualAssist.IsZoomActive() || NocturnePet.Detached;
        if (!hide && !_wasHidden)
            return;

        try
        {
            MeshRenderer quad = __instance.ShadowQuad;
            if (quad == null)
                return;

            GameObject go = quad.gameObject;
            if (hide)
            {
                if (go.activeSelf)
                    go.SetActive(false);
            }
            else
            {
                go.SetActive(true);
            }
            _wasHidden = hide;
        }
        catch { }
    }
}
