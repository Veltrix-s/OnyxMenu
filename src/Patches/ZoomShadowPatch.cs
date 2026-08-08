using HarmonyLib;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class ZoomShadowPatch
{
    private static bool _wasHidden;

    public static void Postfix(HudManager __instance)
    {
        try
        {
            if (__instance == null || __instance.ShadowQuad == null || __instance.ShadowQuad.gameObject == null) return;

            bool wall = NocturneConfig.Wallhack != null && NocturneConfig.Wallhack.Value;
            bool hide = wall || VisualAssist.IsZoomActive();
            if (hide)
            {
                if (__instance.ShadowQuad.gameObject.activeSelf) __instance.ShadowQuad.gameObject.SetActive(false);
            }
            else if (_wasHidden)
            {
                __instance.ShadowQuad.gameObject.SetActive(true);
            }
            _wasHidden = hide;
        }
        catch { }
    }
}
