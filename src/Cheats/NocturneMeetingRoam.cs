using UnityEngine;

namespace Nocturne;

internal static class NocturneMeetingRoam
{
    internal static string Roam()
    {
        try
        {
            if (MeetingHud.Instance != null)
            {
                MeetingHud.Instance.DespawnOnDestroy = false;
                UnityEngine.Object.Destroy(MeetingHud.Instance.gameObject);
                Restore();
                return NocturneText.T("Ходим — у других собрание идёт.", "Roaming. The meeting still runs for others.");
            }
            if (ExileController.Instance != null)
            {
                ExileController.Instance.ReEnableGameplay();
                ExileController.Instance.WrapUp();
                Restore();
                return NocturneText.T("Вышли с экрана выгона.", "Left the ejection screen.");
            }
        }
        catch { }
        return NocturneText.T("Сейчас нет собрания.", "No meeting right now.");
    }

    private static void Restore()
    {
        HudManager h = HudManager.Instance;
        try
        {
            if (h != null)
            {
                h.SetHudActive(true);
                h.SetMapAndInfoButtonsEnabled(true);
                h.StartCoroutine(h.CoFadeFullScreen(Ui.Black, Ui.Clear, 0.2f, false));
            }
        }
        catch { }
        try
        {
            if (ControllerManager.Instance != null)
                ControllerManager.Instance.CloseAndResetAll();
        }
        catch { }
        try
        {
            if (Camera.main != null)
            {
                FollowerCamera fc = Camera.main.GetComponent<FollowerCamera>();
                if (fc != null)
                    fc.Locked = false;
            }
        }
        catch { }
    }
}
