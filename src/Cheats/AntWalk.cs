using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class AntWalk
{
    private const float MinSpeed = 2.5f;
    private const float MinVel = 0.01f;

    private static float _at;
    private static int _leg;
    private static float _keepY;

    private static void Rest()
    {
        _at = 0f;
        _leg = 0;
    }

    internal static void Tick(PlayerPhysics phys)
    {
        PlayerControl me = phys.myPlayer;
        if (me == null || me.Data == null || me.Data.IsDead || me.inVent || !me.CanMove || MeetingHud.Instance != null)
        {
            Rest();
            return;
        }

        Rigidbody2D body = phys.body;
        if (body == null) return;

        Vector2 vel = body.velocity;
        if (vel.sqrMagnitude < MinVel)
        {
            Rest();
            return;
        }

        _at += Time.fixedDeltaTime;

        if (_leg == 0)
        {
            _keepY = vel.y;
            if (_at < Mathf.Clamp(NocturneConfig.AntWalkStep.Value, 0.1f, 1.2f))
                return;
            _at = 0f;
            _leg = 1;
            return;
        }

        bool left = _leg == 2;
        float sp = Mathf.Max(phys.TrueSpeed, MinSpeed);

        body.velocity = new Vector2(left ? -sp : sp, _keepY);
        phys.FlipX = left;
        if (me.cosmetics != null)
            me.cosmetics.SetFlipX(left);
        if (me.NetTransform != null)
            ((InnerNetObject)me.NetTransform).SetDirtyBit(1u);

        if (_at < Mathf.Clamp(NocturneConfig.AntWalkTwitch.Value, 0.03f, 0.15f))
            return;

        _at = 0f;
        _leg = _leg == 1 ? 2 : 0;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
internal static class AntWalkPatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (!NocturneConfig.AntWalk.Value) return;
        if (__instance == null || __instance.myPlayer == null || !__instance.myPlayer.AmOwner)
            return;

        AntWalk.Tick(__instance);
    }
}
