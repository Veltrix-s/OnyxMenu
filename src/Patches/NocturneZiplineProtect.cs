using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneZiplineProtect
{
    private static float _okUntil;
    private static float _lastToast;

    internal static bool On => NocturneConfig.ZiplineProtect != null && NocturneConfig.ZiplineProtect.Value;

    internal static void MarkOk()
    {
        _okUntil = Time.unscaledTime + 1.5f;
    }

    internal static bool IsForced(PlayerControl pc)
    {
        if (pc == null || pc != PlayerControl.LocalPlayer) return false;
        if (Time.unscaledTime < _okUntil) return false;
        if (Time.unscaledTime - _lastToast >= 2f)
        {
            _lastToast = Time.unscaledTime;
            NocturneToast.Push(NocturneText.T("Защита", "Protection"),
                NocturneText.T("Форс-зиплайн заблокирован", "Forced zipline blocked"), 2.5f, NocturneNotifyKind.Warning);
        }
        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckUseZipline))]
internal static class NocturneZiplineCmdPatch
{
    private static void Prefix(PlayerControl __instance)
    {
        if (NocturneZiplineProtect.On && __instance == PlayerControl.LocalPlayer) NocturneZiplineProtect.MarkOk();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcUseZipline))]
internal static class NocturneZiplineUsePatch
{
    private static void Prefix(PlayerControl __instance)
    {
        if (NocturneZiplineProtect.On && __instance == PlayerControl.LocalPlayer) NocturneZiplineProtect.MarkOk();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class NocturneZiplineRpcPatch
{
    private static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
    {
        try
        {
            if (!NocturneZiplineProtect.On || callId != (byte)RpcCalls.UseZipline) return true;
            return !NocturneZiplineProtect.IsForced(__instance);
        }
        catch { return true; }
    }
}
