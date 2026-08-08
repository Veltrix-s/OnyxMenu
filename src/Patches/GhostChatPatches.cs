using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class GhostChatPatch
{
    public static bool Prefix(ChatController __instance, PlayerControl sourcePlayer, string chatText, bool censor)
    {
        if (!NocturneConfig.GhostChat.Value || __instance == null || sourcePlayer == null || sourcePlayer.Data == null)
            return HarmonyControl.Continue;

        var me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead || !sourcePlayer.Data.IsDead)
            return HarmonyControl.Continue;

        return Show(__instance, sourcePlayer, chatText, censor) ? HarmonyControl.SkipOriginal : HarmonyControl.Continue;
    }

    private static bool Show(ChatController chat, PlayerControl src, string text, bool censor)
    {
        ChatBubble bubble = null;
        try
        {
            NetworkedPlayerInfo data = src.Data;
            bubble = chat.GetPooledBubble();
            bubble.transform.SetParent(chat.scroller.Inner);
            bubble.transform.localScale = Vector3.one;

            bool mine = src == PlayerControl.LocalPlayer;
            if (mine) bubble.SetRight();
            else bubble.SetLeft();

            bool voted = MeetingHud.Instance != null && MeetingHud.Instance.DidVote(src.PlayerId);
            bubble.SetCosmetics(data);
            chat.SetChatBubbleName(bubble, data, data.IsDead, voted, PlayerNameColor.Get(data), null);

            if (censor && AmongUs.Data.DataManager.Settings.Multiplayer.CensorChat)
                text = BlockedWords.CensorWords(text, false);

            bubble.SetText(text);
            bubble.AlignChildren();
            chat.AlignAllBubbles();

            if (!chat.IsOpenOrOpening && chat.notificationRoutine == null)
                chat.notificationRoutine = chat.StartCoroutine(chat.BounceDot());

            if (!mine && !chat.IsOpenOrOpening)
            {
                var s = SoundManager.Instance.PlaySound(chat.messageSound, false);
                if (s != null) s.pitch = 0.5f + src.PlayerId / 15f;
                chat.chatNotification.SetUp(src, text);
            }

            return true;
        }
        catch
        {
            if (bubble != null)
            {
                try { chat.chatBubblePool.Reclaim(bubble); } catch { }
            }
            return false;
        }
    }
}
