using UnityEngine;

namespace Nocturne;

public sealed class NocturneChatSender : MonoBehaviour
{
    internal static string Message = "";
    internal static bool Spamming;
    private static float _next;

    internal static void SendNow() => Send(Message);

    internal static void Send(string text)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || string.IsNullOrWhiteSpace(text)) return;
        try { me.RpcSendChat(text); } catch { }
    }

    public void Update()
    {
        if (!Spamming) return;
        if (PlayerControl.LocalPlayer == null || string.IsNullOrWhiteSpace(Message)) { Spamming = false; return; }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + Mathf.Max(1.5f, NocturneConfig.ChatSpamDelay != null ? NocturneConfig.ChatSpamDelay.Value : 2.5f);
        SendNow();
    }
}
