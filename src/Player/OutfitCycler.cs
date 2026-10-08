using UnityEngine;

namespace Nocturne;

internal static class OutfitCycler
{
    private static float _next;
    private static int _turn;

    internal static void Tick()
    {
        if (!NocturneConfig.Cycler.Value) return;
        if (!NocturneOutfitApplier.Idle) return;

        float now = Time.unscaledTime;
        if (now < _next) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (!NocturneOutfits.Usable(me) || IntroCutscene.Instance != null)
            return;

        bool meeting = MeetingHud.Instance != null || ExileController.Instance != null;
        if (meeting && !NocturneConfig.CyclerMeeting.Value)
            return;

        if (NocturneConfig.CyclerPlayers.Value)
            Wear(me);
        else
            Shuffle(me);
        _next = now + NocturneConfig.CyclerInterval.Value;
    }

    private static void Shuffle(PlayerControl me)
    {
        int mask = NocturneConfig.CyclerMask.Value;
        for (int kind = 0; kind < 6; kind++)
        {
            if ((mask & (1 << kind)) == 0) continue;
            if (kind == 0)
            {
                NocturneOutfitApplier.Push(me, 0, null, Random.Range(0, NocturneOutfits.MaxColor() + 1));
                continue;
            }

            string id = kind switch
            {
                1 => NocturneOutfits.RandomHat(),
                2 => NocturneOutfits.RandomSkin(),
                3 => NocturneOutfits.RandomVisor(),
                4 => NocturneOutfits.RandomPet(),
                _ => NocturneOutfits.RandomPlate(),
            };
            if (id.Length > 0)
                NocturneOutfitApplier.Push(me, kind, id);
        }
    }

    private static void Wear(PlayerControl me)
    {
        int count = 0;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            if (e.Current != me && NocturneOutfits.Usable(e.Current)) count++;
        }
        if (count == 0) return;

        int skip = _turn++ % count;
        e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl pc = e.Current;
            if (pc == me || !NocturneOutfits.Usable(pc))
                continue;
            if (skip-- > 0) continue;

            NocturneOutfits.Apply(me, NocturneOutfits.Capture(pc));
            return;
        }
    }
}
