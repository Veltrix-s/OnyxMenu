using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class LobbyCodeCachePatch
{
    internal static string LastCode = "";

    public static void Postfix(string gameIdString) => LastCode = gameIdString ?? "";
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow))]
internal static class DisconnectCopyCodePatch
{
    public static void Postfix(DisconnectPopup __instance)
    {
        var txt = (TMP_Text)__instance._textArea;

        var reason = AmongUsClient.Instance == null
            ? DisconnectReasons.NewConnection
            : AmongUsClient.Instance.LastDisconnectReason;

        string why = reason switch
        {
            DisconnectReasons.Banned => NocturneText.T("Причина: бан (хост)", "Reason: banned (host)"),
            DisconnectReasons.Kicked => NocturneText.T("Причина: кик (хост)", "Reason: kicked (host)"),
            DisconnectReasons.Hacking => NocturneText.T("Причина: неверный RPC / сервер", "Reason: bad RPC / server"),
            DisconnectReasons.Sanctions => NocturneText.T("Причина: санкция системы", "Reason: system sanction"),
            _ => null
        };
        if (why != null)
            txt.text += "\n<size=60%>" + why + "</size>";

        if (!NocturneConfig.CopyCodeOnDisconnect.Value || LobbyCodeCachePatch.LastCode.Length == 0)
            return;

        GUIUtility.systemCopyBuffer = LobbyCodeCachePatch.LastCode;
        txt.text += "\n\n<size=60%>" + NocturneText.T("Код лобби скопирован", "Lobby code copied") + "</size>";
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class NoWinConditionsPatch
{
    public static bool Prefix() => !NocturneConfig.NoWinConditions.Value;
}

[HarmonyPatch(typeof(LogicGameFlowHnS), nameof(LogicGameFlowHnS.CheckEndCriteria))]
internal static class NoWinConditionsHnSPatch
{
    public static bool Prefix() => !NocturneConfig.NoWinConditions.Value;
}
