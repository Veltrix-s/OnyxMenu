using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class NocturnePhantomVanish
{
    private const float Smoke = 1.5f;
    private static readonly Vector2 Away = new Vector2(1000f, 1000f);

    private static bool _hidden;
    private static float _until;
    private static Vector2 _home;

    internal static bool Active => _hidden && Time.time <= _until + 0.5f && NocturneConfig.PhantomNoVanish.Value;

    internal static void Begin(PlayerControl me)
    {
        if (me == null || me.NetTransform == null) return;

        bool running = _hidden && Time.time <= _until + 0.5f;

        _home = me.transform.position;
        _until = Time.time + Smoke;
        _hidden = true;

        try { me.NetTransform.RpcSnapTo(Away); } catch { }
        try { me.transform.position = _home; } catch { }

        if (running) return;
        try { ((MonoBehaviour)me).StartCoroutine(Co(me).WrapToIl2Cpp()); }
        catch { _hidden = false; }
    }

    private static IEnumerator Co(PlayerControl me)
    {
        while (Time.time < _until && _hidden && me != null && me.AmOwner)
            yield return null;

        if (_hidden && me != null && me.AmOwner && MeetingHud.Instance == null)
            try { me.NetTransform.RpcSnapTo(me.transform.position); } catch { }

        _hidden = false;
    }

    internal static void Cancel() => _hidden = false;
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckVanish))]
internal static class NocturnePhantomVanishPatch
{
    public static void Prefix(PlayerControl __instance)
    {
        if (!NocturneConfig.PhantomNoVanish.Value) return;
        if (__instance == null || __instance != PlayerControl.LocalPlayer || __instance.inVent) return;
        NocturnePhantomVanish.Begin(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckAppear))]
internal static class NocturnePhantomAppearPatch
{
    public static void Prefix(PlayerControl __instance)
    {
        if (!NocturneConfig.PhantomNoVanish.Value) return;
        if (__instance == null || __instance != PlayerControl.LocalPlayer || __instance.inVent) return;
        NocturnePhantomVanish.Begin(__instance);
    }
}

[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Serialize))]
internal static class NocturnePhantomFreezeNet
{
    public static bool Prefix(CustomNetworkTransform __instance, bool __1, ref bool __result)
    {
        if (!NocturnePhantomVanish.Active || __1) return true;
        if (__instance == null || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
internal static class NocturnePhantomFreezeCam
{
    public static bool Prefix() => !NocturnePhantomVanish.Active;
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
internal static class NocturnePhantomVanishMeetingPatch
{
    public static void Prefix() => NocturnePhantomVanish.Cancel();
}
