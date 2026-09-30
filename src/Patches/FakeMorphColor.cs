using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class FakeMorphColor
{
    private static bool On
    {
        get
        {
            if (!NocturneConfig.FakeMorphColor.Value) return false;

            InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
            return net != null && net.AmHost;
        }
    }

    private static byte FreeColor(int mine)
    {
        int total = Palette.PlayerColors != null ? Palette.PlayerColors.Length : 12;
        var taken = new bool[total];

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc == null || pc.Data == null) continue;

            int c = pc.Data.DefaultOutfit.ColorId;
            if (c >= 0 && c < total) taken[c] = true;
        }

        int start = Random.Range(0, total);
        for (int i = 0; i < total; i++)
        {
            int c = (start + i) % total;
            if (c != mine && !taken[c])
                return (byte)c;
        }

        return (byte)((mine + 1) % total);
    }

    private static void Spoof(PlayerControl me, PlayerControl target)
    {
        int real = me.Data.DefaultOutfit.ColorId;
        byte fake = FreeColor(real);

        RpcBatch.All()
            .Color(me, fake)
            .Morph(me, target, true)
            .Color(me, (byte)real)
            .Send();
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckShapeshift))]
    private static class OnCheck
    {
        private static bool Prefix(PlayerControl __instance, PlayerControl target, bool shouldAnimate)
        {
            if (!On || !shouldAnimate || __instance != PlayerControl.LocalPlayer || target == null)
                return HarmonyControl.Continue;

            Spoof(__instance, target);
            return HarmonyControl.SkipOriginal;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckRevertShapeshift))]
    private static class OnRevert
    {
        private static bool Prefix(PlayerControl __instance, bool shouldAnimate)
        {
            if (!On || !shouldAnimate || __instance != PlayerControl.LocalPlayer) return HarmonyControl.Continue;

            Spoof(__instance, __instance);
            return HarmonyControl.SkipOriginal;
        }
    }
}
