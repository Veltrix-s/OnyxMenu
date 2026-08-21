using Hazel;
using InnerNet;

namespace Nocturne;

internal static class NocturneFrameSabotage
{
    private const byte CallUpdateSystem = 35;

    internal static string Send(PlayerControl frameAs, SystemTypes system, byte value)
    {
        if (frameAs == null || frameAs.Data == null || frameAs.Data.Disconnected) return NocturneText.T("нет цели", "no target");
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return NocturneText.T("только в матче", "in-match only");

        var net = (InnerNetClient)AmongUsClient.Instance;
        MessageWriter body = null, w = null;
        try
        {
            body = MessageWriter.Get(SendOption.None);
            body.Write(value);

            w = net.StartRpcImmediately(((InnerNetObject)ShipStatus.Instance).NetId, CallUpdateSystem, SendOption.Reliable, -1);
            if (w == null) return NocturneText.T("не удалось", "failed");
            w.Write((byte)system);
            w.WriteNetObject(frameAs);
            w.Write(body, false);
            net.FinishRpcImmediately(w);
            return NocturneText.T("отправлено: ", "sent: ") + frameAs.Data.PlayerName;
        }
        catch { return NocturneText.T("не удалось", "failed"); }
        finally { try { body?.Recycle(); } catch { } }
    }
}
