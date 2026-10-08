using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class StartControl
{
    private static float _forceZeroUntil = -1f;

    internal static bool CanUnlock(GameStartManager gsm)
    {
        if (!NocturneConfig.AlwaysUnlockStartButton.Value || gsm == null || AmongUsClient.Instance == null)
            return false;
        return AmongUsClient.Instance.AmHost;
    }

    internal static void PreUnlock(GameStartManager gsm)
    {
        if (CanUnlock(gsm))
            gsm.MinPlayers = 1;
    }

    internal static void UnlockStartButton(GameStartManager gsm)
    {
        if (!CanUnlock(gsm)) return;

        gsm.MinPlayers = 1;
        if (gsm.StartButton != null) gsm.StartButton.SetButtonEnableState(true);

        if (gsm.GameStartText == null) return;

        TMP_Text t = gsm.GameStartText;
        t.color = Ui.White;
        if (gsm.startState != GameStartManager.StartingStates.Countdown && t.text.Length > 0) t.text = string.Empty;
    }

    internal static void HandleQuickStartOnEnter(GameStartManager gsm)
    {
        if (gsm == null || AmongUsClient.Instance == null || LobbyBehaviour.Instance == null) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
            return;
        if (IsEnterStartBlockedByChat()) return;

        if (gsm.startState == GameStartManager.StartingStates.Countdown)
        {
            CancelCountdown(gsm);
            return;
        }

        if (!NocturneConfig.QuickStartOnEnter.Value) return;
        gsm.MinPlayers = 1;
        if (NocturneConfig.InstantStartOnEnter.Value)
            TryInstantStart(gsm);
        else
            gsm.BeginGame();
    }

    private static void CancelCountdown(GameStartManager gsm)
    {
        gsm.ResetStartState();
        NocturneToast.Push(NocturneText.T("Старт отменён", "Start canceled"), NocturneText.T("Отсчёт прерван.", "Countdown interrupted."), 2.5f, NocturneNotifyKind.Warning);
    }

    internal static void HandleCountdownCancel(GameStartManager gsm)
    {
        if (gsm == null || !Input.GetMouseButtonDown(0)) return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || LobbyBehaviour.Instance == null)
            return;
        if (!CountdownRunning(gsm)) return;
        if (!MouseOverStartButton(gsm) && !MouseOverText(gsm.GameStartText) && !MouseOverText(gsm.GameStartTextClient))
            return;
        CancelCountdown(gsm);
    }

    private static bool CountdownRunning(GameStartManager gsm)
    {
        if (gsm.startState == GameStartManager.StartingStates.Countdown) return true;
        return TextHasDigit(gsm.GameStartText) || TextHasDigit(gsm.GameStartTextClient);
    }

    private static bool TextHasDigit(object raw)
    {
        TMP_Text text = null;
        try
        {
            text = (TMP_Text)raw;
        }
        catch { }
        if (text == null || !text.gameObject.activeInHierarchy) return false;
        string s = text.text ?? string.Empty;
        foreach (char ch in s)
            if (char.IsDigit(ch))
                return true;
        return false;
    }

    private static bool MouseOverStartButton(GameStartManager gsm)
    {
        try
        {
            Camera cam = Camera.main;
            if (gsm.StartButton == null || cam == null)
                return false;
            Component bc = gsm.StartButton;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            Collider2D col = bc.GetComponent<Collider2D>();
            if (col != null)
                return col.OverlapPoint(mw);
            Vector3 sp = cam.WorldToScreenPoint(bc.transform.position);
            Vector2 m = Input.mousePosition;
            return Ui.Abs(m.x - sp.x) <= 260f && Ui.Abs(m.y - sp.y) <= 90f;
        }
        catch
        {
            return false;
        }
    }

    private static bool MouseOverText(object raw)
    {
        TMP_Text text = null;
        try
        {
            text = (TMP_Text)raw;
        }
        catch { }
        if (text == null || !text.gameObject.activeInHierarchy) return false;
        try
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Component comp = text;
            Vector2 m = Input.mousePosition;

            Renderer rend = comp.GetComponent<Renderer>();
            if (rend != null)
            {
                Vector3 a = cam.WorldToScreenPoint(rend.bounds.min);
                Vector3 c = cam.WorldToScreenPoint(rend.bounds.max);
                float minX = Ui.Min(a.x, c.x), maxX = Ui.Max(a.x, c.x);
                float minY = Ui.Min(a.y, c.y), maxY = Ui.Max(a.y, c.y);
                const float pad = 90f;
                return m.x >= minX - pad && m.x <= maxX + pad && m.y >= minY - pad && m.y <= maxY + pad;
            }

            Vector3 sp = cam.WorldToScreenPoint(comp.transform.position);
            return Ui.Abs(m.x - sp.x) <= 240f && Ui.Abs(m.y - sp.y) <= 100f;
        }
        catch
        {
            return false;
        }
    }

    internal static void HoldInstantStart(GameStartManager gsm)
    {
        if (gsm == null || _forceZeroUntil < 0f)
            return;
        if (AmongUsClient.Instance == null || LobbyBehaviour.Instance == null || !AmongUsClient.Instance.AmHost || Time.unscaledTime > _forceZeroUntil)
        {
            _forceZeroUntil = -1f;
            return;
        }
        gsm.countDownTimer = 0f;
    }

    internal static bool TryInstantStart(GameStartManager gsm)
    {
        if (gsm == null) return false;
        try
        {
            gsm.BeginGame();
        }
        catch
        {
            return false;
        }

        gsm.countDownTimer = 0f;
        _forceZeroUntil = Time.unscaledTime + 3.5f;
        return true;
    }

    private static bool IsEnterStartBlockedByChat()
    {
        if (NocturneChatWindow.Open) return true;
        var chat = DestroyableSingleton<HudManager>.InstanceExists ? DestroyableSingleton<HudManager>.Instance.Chat : null;
        return chat != null && chat.IsOpenOrOpening;
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
internal static class StartButtonUnlockPatch
{
    public static void Prefix(GameStartManager __instance) => StartControl.PreUnlock(__instance);

    public static void Postfix(GameStartManager __instance)
    {
        StartControl.UnlockStartButton(__instance);
        StartControl.HandleQuickStartOnEnter(__instance);
        StartControl.HandleCountdownCancel(__instance);
        StartControl.HoldInstantStart(__instance);
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
internal static class StartButtonBeginPatch
{
    public static void Prefix(GameStartManager __instance) => StartControl.UnlockStartButton(__instance);
}
