using Hazel;
using HarmonyLib;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneStartCounter
{
    private static float _endsAt = -99f;
    private static bool _active;

    internal static void Set(int sec)
    {
        if (sec < 0) { _active = false; return; }
        _active = true;
        _endsAt = Time.unscaledTime + sec;
    }

    internal static bool TryGet(out int sec)
    {
        sec = 0;
        if (!_active) return false;
        float rem = _endsAt - Time.unscaledTime;
        if (rem < -1.5f) { _active = false; return false; }
        sec = rem <= 0f ? 0 : Mathf.CeilToInt(rem);
        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
internal static class NocturneStartCounterRpcPatch
{
    public static void Postfix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 18 || reader == null) return;
        MessageReader copy = null;
        try
        {
            copy = MessageReader.Get(reader);
            copy.ReadPackedInt32();
            NocturneStartCounter.Set(copy.ReadSByte());
        }
        catch { }
        finally { try { copy?.Recycle(); } catch { } }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.SetStartCounter))]
internal static class NocturneStartCounterMethodPatch
{
    public static void Postfix([HarmonyArgument(0)] sbyte startCounter)
    {
        try { NocturneStartCounter.Set(startCounter); } catch { }
    }
}
