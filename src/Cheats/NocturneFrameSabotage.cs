using Hazel;
using InnerNet;

namespace Nocturne;

internal static class NocturneFrameSabotage
{
    private const byte CallUpdateSystem = 35;

    internal static string Send(PlayerControl frameAs, SystemTypes system, byte value)
    {
        if (frameAs == null || frameAs.Data == null || frameAs.Data.Disconnected)
            return NocturneText.NoTargetLow;
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null)
            return NocturneText.MatchOnlyLow;

        var net = AmongUsClient.Instance;
        MessageWriter body = null;
        try
        {
            body = MessageWriter.Get(SendOption.None);
            body.Write(value);

            MessageWriter w = net.StartRpcImmediately(ShipStatus.Instance.NetId, CallUpdateSystem, SendOption.Reliable, -1);
            if (w == null)
                return NocturneText.FailedLow;
            w.Write((byte)system);
            w.WriteNetObject(frameAs);
            w.Write(body, false);
            net.FinishRpcImmediately(w);
            return NocturneText.T("отправлено: ", "sent: ") + frameAs.Data.PlayerName;
        }
        catch
        {
            return NocturneText.FailedLow;
        }
        finally { try { body?.Recycle(); } catch { } }
    }
}
