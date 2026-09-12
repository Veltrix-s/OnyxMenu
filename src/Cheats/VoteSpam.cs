using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class VoteSpam
{
    private const float Gap = 1f;

    private static float _next;

    internal static void Tick()
    {
        if (!NocturneConfig.VoteSpam.Value)
            return;

        InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
        if (net == null || !net.AmHost)
            return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || MeetingHud.Instance == null)
            return;

        float now = Time.unscaledTime;
        if (now < _next)
            return;

        _next = now + Gap;

        RpcBatch b = RpcBatch.All();
        int n = 0;
        foreach (PlayerControl p in PlayerControl.AllPlayerControls)
        {
            if (p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
            b.ChatNote(me, p.PlayerId, ChatNoteTypes.DidVote);
            n++;
        }

        if (n > 0)
            b.Send();
    }
}
