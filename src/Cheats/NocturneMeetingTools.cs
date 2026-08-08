using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class NocturneMeetingTools
{
    private static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    internal static string CloseVoting()
    {
        try
        {
            if (!Host) return NocturneText.T("Только хост.", "Host only.");
            MeetingHud m = MeetingHud.Instance;
            if (m == null) return NocturneText.T("Нет собрания.", "No meeting.");
            int state = (int)m.CurrentState;
            if (state != 2 && state != 3) return NocturneText.T("Голосование не активно.", "Voting isn't active.");
            m.ForceSkipAll();
            return NocturneText.T("Голосование закрыто.", "Voting closed.");
        }
        catch { return NocturneText.T("Не удалось закрыть.", "Failed to close."); }
    }

    internal static string CloseMeeting()
    {
        try
        {
            if (!Host) return NocturneText.T("Только хост.", "Host only.");
            MeetingHud m = MeetingHud.Instance;
            if (m == null) return NocturneText.T("Нет собрания.", "No meeting.");
            m.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), (NetworkedPlayerInfo)null, true);
            m.Close();
            m.RpcClose();
            return NocturneText.T("Собрание закрыто (без выгона).", "Meeting closed (no ejection).");
        }
        catch { return NocturneText.T("Не удалось закрыть.", "Failed to close."); }
    }

    internal static string Eject(PlayerControl t)
    {
        try
        {
            if (!Host) return NocturneText.T("Только хост.", "Host only.");
            if (t == null || t.Data == null || t.Data.Disconnected) return NocturneText.T("Нет цели.", "No target.");
            if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");

            HudManager hud = HudManager.Instance;
            if (hud == null || hud.MeetingPrefab == null) return NocturneText.T("Недоступно.", "Unavailable.");

            MeetingHud m = MeetingHud.Instance;
            if (m == null)
            {
                m = Object.Instantiate(hud.MeetingPrefab);
                MeetingHud.Instance = m;
                ((InnerNetClient)AmongUsClient.Instance).Spawn(((Il2CppObjectBase)m).Cast<InnerNetObject>(), -2, (SpawnFlags)0);
            }

            m.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), t.Data, false);
            m.RpcClose();
            return NocturneText.T("Выгнан: ", "Ejected: ") + t.Data.PlayerName;
        }
        catch { return NocturneText.T("Не удалось.", "Failed."); }
    }

}
