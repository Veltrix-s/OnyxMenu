using HarmonyLib;
using UnityEngine.EventSystems;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnActiveSceneChange))]
internal static class NocturneSceneNavFix
{
    public static void Postfix()
    {
        try { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        catch { }
        try { NocturneOverheadChat.Clear(); }
        catch { }
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class NocturneMenuNavFix
{
    public static void Prefix()
    {
        try { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        catch { }
    }
}
