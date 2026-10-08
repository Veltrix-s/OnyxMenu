using AmongUs.AnimationTestScene;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nocturne;

internal static class AnimLab
{
    internal static AnimationTestScene Live;

    internal static bool Inside => Live != null;

    internal static void Toggle()
    {
        if (Inside)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (Object.FindObjectOfType<MainMenuManager>() == null)
        {
            NocturneToast.Push(NocturneText.T("Лаборатория", "Lab"),
                NocturneText.T("Открывается только из главного меню", "Opens from the main menu only"), 2.6f, NocturneNotifyKind.Warning);
            return;
        }

        if (NocturneMenu.Opened)
            NocturneMenu.ToggleRequest = true;

        SceneManager.LoadScene("AnimationTestScene");
    }
}

[HarmonyPatch(typeof(AnimationTestScene), "Awake")]
internal static class AnimLabAwakePatch
{
    public static void Prefix(AnimationTestScene __instance) => AnimLab.Live = __instance;
}
