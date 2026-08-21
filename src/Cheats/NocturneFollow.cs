using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturneFollow
{
    private static byte _target = 255;

    internal static bool IsTarget(byte id) => _target == id;

    internal static string Toggle(PlayerControl pc)
    {
        if (pc == null || pc.Data == null) return NocturneText.T("Нет цели.", "No target.");
        if (_target == pc.PlayerId) { _target = 255; return NocturneText.T("Стоп.", "Stopped."); }
        _target = pc.PlayerId;
        return NocturneText.T("Иду за: ", "Following: ") + pc.Data.PlayerName;
    }

    internal static void Stop() => _target = 255;

    internal static void Tick()
    {
        if (_target == 255) return;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.Data == null || me.MyPhysics == null || me.MyPhysics.body == null) { _target = 255; return; }
        if ((ShipStatus.Instance == null && LobbyBehaviour.Instance == null) || MeetingHud.Instance != null || me.inVent || !me.CanMove) return;

        PlayerControl t = ById(_target);
        if (t == null || t.Data == null || t.Data.Disconnected) { _target = 255; return; }

        Rigidbody2D body = me.MyPhysics.body;
        Vector2 mp = body.position;
        Vector2 tp = t.transform.position;
        Vector2 delta = tp - mp;
        float dist = delta.magnitude;
        if (dist <= 0.3f) { body.velocity = Vector2.zero; return; }

        Vector2 dir = delta / dist;
        float sp = me.MyPhysics.TrueSpeed;
        float speed = Mathf.Min(sp, dist * 4.5f + 0.4f);
        body.velocity = dir * speed;
        me.MyPhysics.FlipX = dir.x < 0f;
    }

    private static PlayerControl ById(byte id)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
                if (e.Current != null && e.Current.PlayerId == id) return e.Current;
        }
        catch { }
        return null;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
internal static class NocturneFollowPatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        try
        {
            if (__instance == null || __instance.myPlayer == null || !__instance.myPlayer.AmOwner) return;
            NocturneFollow.Tick();
        }
        catch { }
    }
}
