using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(AmongUsClient), "OnGameJoined")]
internal static class LobbyCodeCachePatch
{
    internal static string LastCode = "";

    public static void Postfix(string gameIdString) => LastCode = gameIdString ?? "";
}

[HarmonyPatch(typeof(DisconnectPopup), "DoShow")]
internal static class DisconnectCopyCodePatch
{
    public static void Postfix(DisconnectPopup __instance)
    {
        var txt = (TMP_Text)__instance._textArea;

        var reason = AmongUsClient.Instance == null
            ? DisconnectReasons.NewConnection
            : ((InnerNetClient)AmongUsClient.Instance).LastDisconnectReason;

        string why = (int)reason switch
        {
            6 => "Причина: бан (хост)",
            7 => "Причина: кик (хост)",
            10 => "Причина: неверный RPC / сервер",
            112 => "Причина: санкция системы",
            _ => null
        };
        if (why != null)
            txt.text += "\n<size=60%>" + why + "</size>";

        if (!NocturneConfig.CopyCodeOnDisconnect.Value || LobbyCodeCachePatch.LastCode.Length == 0)
            return;

        GUIUtility.systemCopyBuffer = LobbyCodeCachePatch.LastCode;
        txt.text += "\n\n<size=60%>Код лобби скопирован</size>";
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class NoWinConditionsPatch
{
    public static bool Prefix() => !NocturneConfig.NoWinConditions.Value;
}
