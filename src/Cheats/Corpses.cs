using AmongUs.GameOptions;
using UnityEngine;

namespace Nocturne;

internal static class Corpses
{
    private const float ReviveGap = 0.4f;

    private static float _wakeAt;
    private static RoleTypes _prev;
    private static bool _hasPrev;

    internal static bool Busy => _wakeAt > 0f;

    internal static void Drop()
    {
        if (Busy || !Utils.Host || !Utils.InGame) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.Data.Disconnected || me.Data.IsDead) return;

        _prev = me.Data.Role != null ? me.Data.Role.Role : RoleTypes.Crewmate;
        _hasPrev = true;

        try
        {
            me.RpcMurderPlayer(me, true);
        }
        catch { }

        _wakeAt = Time.unscaledTime + ReviveGap;
    }

    internal static void Tick()
    {
        if (_wakeAt <= 0f || Time.unscaledTime < _wakeAt) return;
        _wakeAt = 0f;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null) return;

        try
        {
            me.Revive();
        }
        catch { }

        try
        {
            if (me.Data.IsDead)
            {
                me.Data.IsDead = false;
                me.Data.MarkDirty();
            }
        }
        catch { }

        if (_hasPrev && (me.Data.Role == null || me.Data.Role.Role != _prev))
            NocturneForceRoles.Assign(me, _prev, true);
        _hasPrev = false;

        try
        {
            me.moveable = true;
            if (me.MyPhysics != null)
            {
                me.MyPhysics.ResetMoveState(true);
                me.MyPhysics.ResetAnimState();
            }
        }
        catch { }
    }
}
