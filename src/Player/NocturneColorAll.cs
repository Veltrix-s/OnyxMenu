using UnityEngine;

namespace Nocturne;

internal static class NocturneColorAll
{
    private static float _next;

    internal static void Tick()
    {
        if (NocturneConfig.ColorAll == null || !NocturneConfig.ColorAll.Value) return;
        if (AmongUsClient.Instance == null) return;
        try { if (!AmongUsClient.Instance.AmHost) return; } catch { return; }
        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

        float now = Time.unscaledTime;
        if (now - _next < 0.5f) return;
        _next = now;

        int max = NocturneColorSnipe.Max();
        byte col = (byte)Mathf.Clamp(NocturneConfig.ColorAllId != null ? NocturneConfig.ColorAllId.Value : 0, 0, max);

        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
            {
                PlayerControl p = e.Current;
                if (p == null || p.Data == null || p.Data.Disconnected || p.Data.DefaultOutfit == null) continue;
                if (p.Data.DefaultOutfit.ColorId == col) continue;
                try { p.RpcSetColor(col); } catch { }
            }
        }
        catch { }
    }
}
