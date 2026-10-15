using System.Collections.Generic;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class QuickRejoin
{
    private static int _last;
    private static float _since = -100f;
    private static int _direct;
    private static float _directAt;

    internal static void Remember()
    {
        AmongUsClient au = AmongUsClient.Instance;
        _since = -100f;
        _direct = 0;
        if (au.NetworkMode == NetworkModes.OnlineGame && au.GameId != 0 && au.GameId != -1)
            _last = au.GameId;
    }

    internal static void Tick()
    {
        if (_direct == 0) return;

        AmongUsClient au = AmongUsClient.Instance;
        if (au == null)
        {
            _direct = 0;
            return;
        }

        if (au.isConnecting)
        {
            au.InOnlineScene = false;
            return;
        }

        float age = Time.unscaledTime - _directAt;
        if (age < 2f || (au.AmConnected && age < 10f)) return;

        int code = _direct;
        _direct = 0;
        NocturneToast.Push(NocturneText.T("Перезаход", "Rejoin"), NocturneText.T("Напрямую не вышло, ищу комнату по коду.", "Direct join failed, looking the room up."), 2.5f, NocturneNotifyKind.Warning);
        au.ExitGame(DisconnectReasons.ExitGame);
        Join(code);
    }

    internal static void Leave()
    {
        AmongUsClient au = AmongUsClient.Instance;
        if (au == null || !InGame(au))
        {
            NocturneToast.Push(NocturneText.T("Выход", "Exit"), NocturneText.T("Ты не в игре.", "You are not in a game."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }

        string room = au.NetworkMode == NetworkModes.OnlineGame ? " " + GameCode.IntToGameName(au.GameId) : string.Empty;
        _since = -100f;
        _direct = 0;
        NocturneToast.Push(NocturneText.T("Выход", "Exit"), NocturneText.T("Вышел из игры", "Left the game") + room, 2f, NocturneNotifyKind.Info, NocturneIcon.Door);
        au.ExitGame(DisconnectReasons.ExitGame);
    }

    internal static void Reconnect()
    {
        AmongUsClient au = AmongUsClient.Instance;
        if (au == null || au.NetworkMode != NetworkModes.OnlineGame || !InGame(au) || au.GameId == 0 || au.GameId == -1)
        {
            NocturneToast.Push(NocturneText.T("Перезаход", "Rejoin"), NocturneText.T("Ты не в онлайн-комнате.", "You are not in an online room."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }
        if (au.IsGameStarted)
        {
            NocturneToast.Push(NocturneText.T("Перезаход", "Rejoin"), NocturneText.T("Матч идёт, обратно не зайти.", "Match in progress, you can't rejoin."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }
        if (_direct != 0)
        {
            NocturneToast.Push(NocturneText.T("Перезаход", "Rejoin"), NocturneText.T("Уже захожу.", "Already joining."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }

        int code = au.GameId;
        string ip = au.GetNetworkAddress();
        int port = au.GetNetworkPort();
        _last = code;
        _since = -100f;

        if (string.IsNullOrEmpty(ip) || port <= 0 || port > ushort.MaxValue)
        {
            au.ExitGame(DisconnectReasons.ExitGame);
            Join(code);
            return;
        }

        _direct = code;
        _directAt = Time.unscaledTime;
        au.StartCoroutine(au.CoJoinOnlinePublicGame(code, ip, (ushort)port, au.MenuTarget));
        NocturneToast.Push(NocturneText.T("Перезаход", "Rejoin"), NocturneText.T("Захожу в ", "Joining ") + GameCode.IntToGameName(code), 2.5f, NocturneNotifyKind.Info);
    }

    internal static void Back()
    {
        AmongUsClient au = AmongUsClient.Instance;
        if (au == null) return;

        if (InGame(au))
        {
            NocturneToast.Push(NocturneText.T("Возврат", "Return"), NocturneText.T("Ты уже в игре.", "You are already in a game."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }

        IReadOnlyList<LobbyRow> rows = LobbyHistory.Entries;
        int code = _last != 0 ? _last : rows.Count > 0 ? rows[0].Id : 0;
        if (code == 0)
        {
            NocturneToast.Push(NocturneText.T("Возврат", "Return"), NocturneText.T("Некуда возвращаться.", "No room to return to."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }
        Join(code);
    }

    internal static void Join(int code)
    {
        AmongUsClient au = AmongUsClient.Instance;
        if (au == null || code == 0) return;

        if (Time.unscaledTime - _since < 15f && !au.AmConnected)
        {
            NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneText.T("Комнату уже ищу.", "Already looking for the room."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }

        _since = Time.unscaledTime;
        au.StartCoroutine(au.CoFindGameInfoFromCodeAndJoin(code));
        NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneText.T("Захожу в ", "Joining ") + GameCode.IntToGameName(code), 2.5f, NocturneNotifyKind.Info);
    }

    private static bool InGame(AmongUsClient au) => au.AmConnected || LobbyBehaviour.Instance != null || ShipStatus.Instance != null;
}
