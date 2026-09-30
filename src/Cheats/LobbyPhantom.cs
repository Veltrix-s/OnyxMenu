using AmongUs.GameOptions;
using UnityEngine;

namespace Nocturne;

internal static class LobbyPhantom
{
    private const float RoleWait = 0.6f;
    private const float BackWait = 0.5f;
    private const float GiveUp = 3f;

    private static RoleTypes _prev;
    private static float _at;
    private static float _dead;
    private static int _step;

    internal static bool IsPhantom(PlayerControl pc) =>
        pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.Role == RoleTypes.Phantom;

    private static RoleTypes RoleOf(PlayerControl pc) =>
        pc.Data.Role != null ? pc.Data.Role.Role : RoleTypes.Crewmate;

    internal static void Vanish()
    {
        if (!Utils.Host || LobbyBehaviour.Instance == null) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null) return;

        if (IsPhantom(me))
        {
            Hide(me);
            return;
        }

        _prev = RoleOf(me);
        NocturneForceRoles.EnsureRate(RoleTypes.Phantom);
        NocturneForceRoles.Assign(me, RoleTypes.Phantom, true);

        _step = 1;
        _at = Time.unscaledTime + RoleWait;
        _dead = Time.unscaledTime + GiveUp;
    }

    internal static void Appear()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        if (_step != 0)
        {
            _step = 0;
            Restore(me);
        }

        try
        {
            me.RpcAppear(true);
            me.SetRoleInvisibility(false, true, true);
        }
        catch { }
    }

    internal static void Tick()
    {
        if (_step == 0) return;

        float now = Time.unscaledTime;
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null)
        {
            _step = 0;
            return;
        }

        if (_step == 1)
        {
            if (!IsPhantom(me))
            {
                if (now >= _dead) _step = 0;
                return;
            }
            if (now < _at) return;

            Hide(me);
            _step = 2;
            _at = now + BackWait;
            return;
        }

        if (now < _at) return;
        _step = 0;
        Restore(me);
    }

    private static void Hide(PlayerControl me)
    {
        try
        {
            me.RpcVanish();
            me.SetRoleInvisibility(true, true, true);
        }
        catch { }
    }

    private static void Restore(PlayerControl me)
    {
        if (RoleOf(me) == _prev) return;
        NocturneForceRoles.Assign(me, _prev, true);
    }
}
