using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nocturne;

internal static class NocturneLobbyTools
{
    internal static string DespawnMap()
    {
        if (!Utils.Host)
            return NocturneText.HostOnly;
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null) return NocturneText.T("Карты сейчас нет.", "No map right now.");
        try
        {
            ship.Cast<InnerNetObject>().Despawn();
            return NocturneText.T("Карта убрана.", "Map despawned.");
        }
        catch
        {
            return NocturneText.Failed;
        }
    }

    internal static string SpawnMap(int mapId)
    {
        if (!Utils.Host)
            return NocturneText.HostOnly;
        if (AmongUsClient.Instance == null) return NocturneText.T("Недоступно.", "Unavailable.");
        try
        {
            AmongUsClient.Instance.StartCoroutine(CoSpawnMap(mapId).WrapToIl2Cpp());
        }
        catch
        {
            return NocturneText.Failed;
        }
        return NocturneText.T("Спавн карты...", "Spawning map...");
    }

    private static IEnumerator CoSpawnMap(int mapId)
    {
        AmongUsClient client = AmongUsClient.Instance;
        var prefabs = client.ShipPrefabs;
        if (prefabs == null || mapId < 0 || mapId >= prefabs.Count)
            yield break;

        client.ShipLoadingAsyncHandle = prefabs[mapId].InstantiateAsync(null, false);
        while (!client.ShipLoadingAsyncHandle.IsDone)
            yield return null;

        GameObject go = client.ShipLoadingAsyncHandle.Result;
        if (go == null) yield break;
        ShipStatus ship = go.GetComponent<ShipStatus>();
        if (ship == null)
            yield break;

        ShipStatus.Instance = ship;
        InnerNetObject net = ship.GetComponent<InnerNetObject>();
        client.Spawn(net, -2, SpawnFlags.None);

        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me != null)
            {
                Vector2 p = me.GetTruePosition();
                go.transform.position = Ui.V3(p.x, p.y, go.transform.position.z);
            }
        }
        catch { }

        client.ShipLoadingAsyncHandle = new AsyncOperationHandle<GameObject>();
    }

    internal static string CreateLobby()
    {
        if (!Utils.Host)
            return NocturneText.T("Только хост может создать лобби.", "Only the host can create a lobby.");
        if (LobbyBehaviour.Instance != null)
            return NocturneText.T("Лобби уже есть.", "Lobby already exists.");

        try
        {
            GameStartManager manager = TryGetGameStartManager();
            if (manager == null || manager.LobbyPrefab == null)
                return NocturneText.T("Префаб лобби не найден.", "Lobby prefab not found.");

            LobbyBehaviour lobby = UnityEngine.Object.Instantiate(manager.LobbyPrefab);
            if (lobby == null) return NocturneText.T("Не удалось создать лобби.", "Failed to create the lobby.");

            InnerNetObject netObject = lobby.Cast<InnerNetObject>();
            AmongUsClient.Instance.Spawn(netObject, -2, SpawnFlags.None);
            return NocturneText.T("Лобби создано заново.", "Lobby re-created.");
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning($"Create lobby failed: {error}");
            return NocturneText.T("Создание лобби не удалось.", "Lobby creation failed.");
        }
    }

    internal static string DestroyLobby()
    {
        if (!Utils.Host)
            return NocturneText.T("Только хост может разрушить лобби.", "Only the host can destroy the lobby.");

        LobbyBehaviour lobby = LobbyBehaviour.Instance;
        if (lobby == null) return NocturneText.T("Объекта лобби сейчас нет.", "No lobby object right now.");

        try
        {
            InnerNetObject netObject = lobby.Cast<InnerNetObject>();
            netObject.Despawn();
            return NocturneText.T("Лобби разрушено.", "Lobby destroyed.");
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning($"Destroy lobby failed: {error}");
            return NocturneText.T("Разрушение лобби не удалось.", "Lobby destruction failed.");
        }
    }

    private const float LeaveConfirmSeconds = 3f;
    private static float _leaveAt = -1f;

    internal static void RequestLeave()
    {
        if (AmongUsClient.Instance == null)
            return;

        if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null)
        {
            _leaveAt = -1f;
            NocturneToast.Push(NocturneText.T("Выход", "Leave"), NocturneText.T("Ты не в лобби.", "You are not in a lobby."), 1.8f, NocturneNotifyKind.Warning);
            return;
        }

        float now = Time.unscaledTime;
        if (_leaveAt > 0f && now <= _leaveAt)
        {
            _leaveAt = -1f;
            NocturneToast.Push(NocturneText.T("Выход", "Leave"), NocturneText.T("Покидаю лобби.", "Leaving lobby."), 1.8f, NocturneNotifyKind.Info);
            try
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
            }
            catch { }
            return;
        }

        _leaveAt = now + NocturneToast.Span(LeaveConfirmSeconds);
        NocturneToast.Push(NocturneText.T("Выход из лобби", "Leave lobby"), NocturneText.T("Нажми ещё раз для подтверждения.", "Press again to confirm."), LeaveConfirmSeconds, NocturneNotifyKind.Warning);
    }

    private static GameStartManager TryGetGameStartManager()
    {
        if (DestroyableSingleton<GameStartManager>.InstanceExists)
            return DestroyableSingleton<GameStartManager>.Instance;
        try
        {
            return UnityEngine.Object.FindObjectOfType<GameStartManager>();
        }
        catch
        {
            return null;
        }
    }
}
