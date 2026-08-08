using AmongUs.InnerNet.GameDataMessages;
using AmongUs.QuickChat;
using Hazel;
using InnerNet;

namespace Nocturne;

internal static class NocturneQuickChatChain
{
    private const byte CallSendQuickChat = 33;

    internal const int KnownRoot = 78;

    internal static readonly int[] KnownSubs =
    {
        1904, 1905, 1907, 1912, 1914, 1915, 1916, 1917, 1919, 1920,
        2067, 2084, 2087, 2090, 2093, 2104, 2113, 2115, 2123, 2126, 2130, 2132, 2152, 2192, 2264, 2276, 2279, 2285, 2286, 2309, 2313,
        118, 156, 159, 160, 161, 178, 196, 197, 301, 397, 399, 400, 401,
        700, 701, 702, 704, 705, 706, 707, 708, 709, 712, 716, 719, 722, 729, 731,
        1562, 1563, 1703, 1704, 1712, 1715, 1717, 1718, 1721, 1722
    };

    private static string RootGuard(PlayerControl me, int root)
    {
        if (me == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmConnected) return NocturneText.T("не в игре", "not in game");
        return root <= 0 || root > ushort.MaxValue ? NocturneText.T("нет корневой фразы", "no root phrase") : null;
    }

    internal static string Send(int root, int[] subs, bool alsoToSelf = false)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        string rootErr = RootGuard(me, root);
        if (rootErr != null) return rootErr;

        int count = subs != null ? subs.Length : 0;
        if (count > byte.MaxValue) return NocturneText.T("слишком много фраз", "too many phrases");
        for (int i = 0; i < count; i++)
        {
            if (subs[i] <= 0 || subs[i] > ushort.MaxValue) return NocturneText.T("плохой id фразы", "bad phrase id");
        }

        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            bool ok = PushChain(net, me, root, subs, -1);
            if (ok && alsoToSelf) PushChain(net, me, root, subs, me.OwnerId);
            return ok ? NocturneText.T("отправлено", "sent") : NocturneText.T("не удалось", "failed");
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    private static bool PushChain(InnerNetClient net, PlayerControl me, int root, int[] subs, int targetClientId)
    {
        MessageWriter w = null;
        try
        {
            w = net.StartRpcImmediately(me.NetId, CallSendQuickChat, SendOption.Reliable, targetClientId);
            if (w == null) return false;

            int count = subs != null ? subs.Length : 0;
            w.Write((byte)(count > 0 ? 3 : 2));
            w.Write((ushort)root);
            w.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                w.Write((byte)2);
                w.Write((ushort)subs[i]);
            }

            net.FinishRpcImmediately(w);
            return true;
        }
        catch { return false; }
    }

    private static bool Push(InnerNetClient net, PlayerControl me, QuickChatPhraseBuilderResult result, int targetClientId)
    {
        MessageWriter w = null;
        try
        {
            w = net.StartRpcImmediately(me.NetId, CallSendQuickChat, SendOption.Reliable, targetClientId);
            if (w == null) return false;

            var msg = new RpcSendQuickChatMessage(me.NetId, result);
            msg.SerializeRpcValues(w);

            net.FinishRpcImmediately(w);
            return true;
        }
        catch { return false; }
    }

    internal static string SendFromText(string ids, bool alsoToSelf = false)
    {
        if (string.IsNullOrWhiteSpace(ids)) return NocturneText.T("пусто", "empty");
        string[] parts = ids.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return NocturneText.T("пусто", "empty");
        if (!int.TryParse(parts[0], out int root)) return NocturneText.T("нет корневой фразы", "no root phrase");

        var subs = new int[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out subs[i - 1])) return NocturneText.T("плохой id фразы", "bad phrase id");
        }
        return Send(root, subs, alsoToSelf);
    }

    internal static string SendTemplate(int root, PlayerControl[] players, bool alsoToSelf = false)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        string rootErr = RootGuard(me, root);
        if (rootErr != null) return rootErr;
        if (players == null || players.Length == 0) return NocturneText.T("нет цели", "no target");
        foreach (PlayerControl p in players)
        {
            if (p == null || p.Data == null) return NocturneText.T("нет цели", "no target");
        }

        try
        {
            var net = (InnerNetClient)AmongUsClient.Instance;
            var array = new QuickChatPhrase[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                array[i] = QuickChatPhrase.NewPlayerId(players[i].PlayerId);
            }
            var result = new QuickChatPhraseBuilderResult((QuickChatPhraseType)3, (StringNames)root, (byte)0, array);

            bool ok = Push(net, me, result, -1);
            if (ok && alsoToSelf) Push(net, me, result, me.OwnerId);
            return ok ? NocturneText.T("отправлено", "sent") : NocturneText.T("не удалось", "failed");
        }
        catch { return NocturneText.T("не удалось", "failed"); }
    }

    internal static string SendTemplateFromText(string rootText, bool alsoToSelf, params PlayerControl[] players)
    {
        if (!int.TryParse((rootText ?? "").Trim(), out int root)) return NocturneText.T("нет корневой фразы", "no root phrase");
        return SendTemplate(root, players, alsoToSelf);
    }
}
