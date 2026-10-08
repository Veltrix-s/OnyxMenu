using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneNeonOutline
{
    private static readonly int Outline = Shader.PropertyToID("_Outline");
    private static readonly int OutlineCol = Shader.PropertyToID("_OutlineColor");
    private static readonly HashSet<byte> _lit = new HashSet<byte>();

    private static bool On => NocturneConfig.NeonOutline.Value;

    internal static bool Active => On || _lit.Count > 0;

    internal static void Apply(PlayerControl player)
    {
        if (player == null)
            return;
        byte pid = player.PlayerId;

        if (!On || (ShipStatus.Instance == null && LobbyBehaviour.Instance == null) || player.Data == null || player.Data.Disconnected)
        {
            if (_lit.Remove(pid))
                Off(player);
            return;
        }

        Renderer body = Body(player);
        if (body == null) return;
        Material m = body.material;
        m.SetFloat(Outline, 1f);
        m.SetColor(OutlineCol, ColorFor(player));
        _lit.Add(pid);
    }

    private static void Off(PlayerControl player)
    {
        try
        {
            Renderer b = Body(player);
            if (b != null)
                b.material.SetFloat(Outline, 0f);
        }
        catch { }
    }

    private static Renderer Body(PlayerControl p)
    {
        CosmeticsLayer c = p.cosmetics;
        PlayerBodySprite s = c != null ? c.currentBodySprite : null;
        return s != null ? (Renderer)s.BodySprite : null;
    }

    private static Color ColorFor(PlayerControl p)
    {
        int mode = NocturneConfig.NeonOutlineMode.Value;
        if (mode == 0)
            return Color.HSVToRGB(Ui.Repeat(Time.unscaledTime * 0.15f, 1f), 1f, 1f);
        if (mode == 2 && p.Data != null && p.Data.DefaultOutfit != null)
        {
            int cid = p.Data.DefaultOutfit.ColorId;
            if (cid >= 0 && cid < Palette.PlayerColors.Length)
                return Palette.PlayerColors[cid];
        }
        bool imp = p.Data != null && p.Data.Role != null && p.Data.Role.IsImpostor;
        return imp ? Ui.C(1f, 0.22f, 0.22f, 1f) : Ui.C(0.30f, 1f, 0.55f, 1f);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
internal static class NocturneNeonOutlinePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (!NocturneNeonOutline.Active)
            return;
        try
        {
            if (__instance != null) NocturneNeonOutline.Apply(__instance.myPlayer);
        }
        catch { }
    }
}
