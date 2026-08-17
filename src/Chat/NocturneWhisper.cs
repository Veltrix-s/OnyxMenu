using System.Text.RegularExpressions;
using HarmonyLib;
using Hazel;

namespace Nocturne;

internal static class NocturneWhisper
{
    private static readonly Regex Tags = new Regex("<.*?>");
    private static byte _keep = 255;

    internal static void Reset() => _keep = 255;

    internal static bool TryHandle(ChatController chat)
    {
        if (chat == null || chat.freeChatField == null || chat.freeChatField.textArea == null) return false;
        string text = chat.freeChatField.textArea.text;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.TrimStart();
        string low = trimmed.ToLowerInvariant();

        if (low == "/unwkeep" || low.StartsWith("/unwkeep "))
        {
            _keep = 255;
            Local("<color=#8AB4FF>[Nocturne]</color> " + NocturneText.T("Постоянный шёпот выключен.", "Persistent whisper off."));
            Clear(chat);
            return true;
        }

        if (low.StartsWith("/wkeep"))
        {
            string[] kp = trimmed.Split(new[] { ' ' }, 2);
            if (kp.Length < 2 || string.IsNullOrWhiteSpace(kp[1]))
            {
                Local("<color=#FF6B6B>[Nocturne]</color> " + NocturneText.T("Формат: /wkeep [ник или ID]", "Usage: /wkeep [name or ID]"));
                Clear(chat);
                return true;
            }
            PlayerControl kt = Find(kp[1].Trim().ToLowerInvariant());
            if (kt == null || kt.Data == null || kt == PlayerControl.LocalPlayer)
            {
                Local("<color=#FF6B6B>[Nocturne]</color> " + NocturneText.T("Игрок не найден.", "Player not found."));
                Clear(chat);
                return true;
            }
            _keep = kt.PlayerId;
            Local("<color=#8AB4FF>[Nocturne]</color> " + NocturneText.T($"Постоянный шёпот → {Strip(kt.Data.PlayerName)}. Выкл: /unwkeep", $"Persistent whisper → {Strip(kt.Data.PlayerName)}. Off: /unwkeep"));
            Clear(chat);
            return true;
        }

        if (low.StartsWith("/w ") || low.StartsWith("/pm ") || low.StartsWith("/msg "))
        {
            string[] parts = trimmed.Split(new[] { ' ' }, 3);
            if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
            {
                Local("<color=#FF6B6B>[Nocturne]</color> " + NocturneText.T("Формат: /w [ник или ID] сообщение", "Usage: /w [name or ID] message"));
                Clear(chat);
                return true;
            }

            PlayerControl target = Find(parts[1].Trim().ToLowerInvariant());
            if (target == null || target.Data == null || target == PlayerControl.LocalPlayer)
            {
                Local("<color=#FF6B6B>[Nocturne]</color> " + NocturneText.T("Игрок не найден.", "Player not found."));
                Clear(chat);
                return true;
            }

            Send(target, Strip(parts[2]));
            Clear(chat);
            return true;
        }

        if (_keep != 255 && !trimmed.StartsWith("/"))
        {
            PlayerControl kt = ById(_keep);
            if (kt == null || kt.Data == null || kt.Data.Disconnected)
            {
                _keep = 255;
                Local("<color=#FF6B6B>[Nocturne]</color> " + NocturneText.T("Цель ушла, постоянный шёпот выключен.", "Target left, persistent whisper off."));
                Clear(chat);
                return true;
            }
            Send(kt, Strip(text));
            Clear(chat);
            return true;
        }

        return false;
    }

    private static void Send(PlayerControl target, string msg)
    {
        try
        {
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 13, SendOption.Reliable, target.OwnerId);
            w.Write(NocturneText.T("шепчет тебе:\n", "whispers to you:\n") + msg);
            AmongUsClient.Instance.FinishRpcImmediately(w);
            Local($"<color=#8AB4FF>{NocturneText.T("Шепнул", "Whisper to")} {Strip(target.Data.PlayerName)}:</color>\n{msg}");
        }
        catch { }
    }

    internal static void Prefill(string name)
    {
        try
        {
            if (HudManager.Instance == null || HudManager.Instance.Chat == null) return;
            ChatController chat = HudManager.Instance.Chat;
            chat.SetVisible(true);
            if (chat.freeChatField != null && chat.freeChatField.textArea != null)
                chat.freeChatField.textArea.SetText("/w " + name + " ", string.Empty);
        }
        catch { }
    }

    private static PlayerControl ById(byte id)
    {
        try
        {
            if (PlayerControl.AllPlayerControls == null) return null;
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p != null && p.PlayerId == id) return p;
        }
        catch { }
        return null;
    }

    private static PlayerControl Find(string q)
    {
        try
        {
            if (PlayerControl.AllPlayerControls == null) return null;
            if (byte.TryParse(q, out byte id))
                foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                    if (p != null && p.PlayerId == id) return p;

            PlayerControl partial = null;
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p.Data == null || p.Data.Disconnected || p == PlayerControl.LocalPlayer) continue;
                string name = Strip(p.Data.PlayerName).ToLowerInvariant().Trim();
                if (name == q) return p;
                if (partial == null && name.StartsWith(q)) partial = p;
            }
            return partial;
        }
        catch { return null; }
    }

    private static string Strip(string s) => Tags.Replace(s ?? string.Empty, string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);

    private static void Local(string msg)
    {
        try
        {
            if (HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
        }
        catch { }
    }

    private static void Clear(ChatController chat)
    {
        try { chat.freeChatField.textArea.SetText(string.Empty, string.Empty); } catch { }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneWhisperReset
{
    public static void Postfix() => NocturneWhisper.Reset();
}
