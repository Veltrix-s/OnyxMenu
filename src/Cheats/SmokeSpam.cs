using AmongUs.GameOptions;
using UnityEngine;

namespace Nocturne;

internal static class SmokeSpam
{
    private const float Gap = 0.12f;

    private static float _next;
    private static bool _armed;
    private static int _origColor = -1;

    internal static void Tick()
    {
        if (!NocturneConfig.SmokeSpam.Value)
        {
            if (_armed) Restore();
            return;
        }

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.IsDead)
            return;
        if (ShipStatus.Instance == null || MeetingHud.Instance != null) return;
        if (me.Data.RoleType != RoleTypes.Phantom) return;

        float now = Time.unscaledTime;
        if (now < _next) return;

        _next = now + Gap;

        if (!_armed)
        {
            _armed = true;
            if (me.Data.DefaultOutfit != null) _origColor = me.Data.DefaultOutfit.ColorId;
        }

        if (Palette.PlayerColors != null && Palette.PlayerColors.Length > 0)
        {
            try
            {
                me.CmdCheckColor((byte)Random.Range(0, Palette.PlayerColors.Length));
            }
            catch { }
        }

        RpcBatch.All()
            .Appear(me)
            .Vanish(me)
            .Send();
    }

    private static void Restore()
    {
        _armed = false;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null)
        {
            _origColor = -1;
            return;
        }

        RpcBatch.All().Appear(me).Send();

        if (_origColor >= 0)
        {
            try
            {
                me.CmdCheckColor((byte)_origColor);
            }
            catch { }
        }
        _origColor = -1;
    }
}
