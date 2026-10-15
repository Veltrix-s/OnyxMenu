using HarmonyLib;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneChatSender : MonoBehaviour
{
    internal static string Message = "";
    internal static bool Spamming;
    private static float _next;

    internal static void SendNow() => Send(Message);

    internal static bool Send(string text)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || string.IsNullOrWhiteSpace(text))
            return false;
        try
        {
            me.RpcSendChat(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly string HugeMessage = "\uFFA0" + new string('\u2029', 118) + "\uFFA0";

    private const float FloodCooldownBase = 18f;
    private const float FloodCooldownPenalty = 3f;
    private static bool _floodSending;
    private static float _floodUntil;
    internal static float FloodCooldownLeft => Ui.Max(0f, _floodUntil - Time.unscaledTime);
    internal static bool FloodReady => FloodCooldownLeft <= 0f;

    internal static string Flood()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null)
            return NocturneText.T("Не в игре.", "Not in a game.");
        _floodSending = true;
        for (int i = 0; i < 6; i++)
            Send(HugeMessage);
        _floodSending = false;
        _floodUntil = Time.unscaledTime + FloodCooldownBase;
        return NocturneText.T("Чат затоплен.", "Chat flooded.");
    }

    internal static void NoteChatSent()
    {
        if (_floodSending || FloodCooldownLeft <= 0f) return;
        _floodUntil += FloodCooldownPenalty;
    }

    public void FixedUpdate()
    {
        if (!Spamming) return;
        if (PlayerControl.LocalPlayer == null || string.IsNullOrWhiteSpace(Message))
        {
            Spamming = false;
            return;
        }
        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + Ui.Max(1.5f, NocturneConfig.ChatSpamDelay.Value);
        SendNow();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class NocturneFloodChatPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == PlayerControl.LocalPlayer)
            NocturneChatSender.NoteChatSent();
    }
}
