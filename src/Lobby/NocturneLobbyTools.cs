using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class NocturneLobbyTools
{
    internal static string DespawnMap()
    {
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null) return NocturneText.T("Карты сейчас нет.", "No map right now.");
        try
        {
            ((Il2CppObjectBase)ship).Cast<InnerNetObject>().Despawn();
            return NocturneText.T("Карта убрана.", "Map despawned.");
        }
        catch { return NocturneText.T("Не удалось.", "Failed."); }
    }

    internal static string SpawnMap(int mapId)
    {
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (AmongUsClient.Instance == null) return NocturneText.T("Недоступно.", "Unavailable.");
        try { ((MonoBehaviour)AmongUsClient.Instance).StartCoroutine(CoSpawnMap(mapId).WrapToIl2Cpp()); }
        catch { return NocturneText.T("Не удалось.", "Failed."); }
        return NocturneText.T("Спавн карты...", "Spawning map...");
    }

    private static IEnumerator CoSpawnMap(int mapId)
    {
        AmongUsClient client = AmongUsClient.Instance;
        var prefabs = client.ShipPrefabs;
        if (prefabs == null || mapId < 0 || mapId >= prefabs.Count) yield break;

        client.ShipLoadingAsyncHandle = prefabs[mapId].InstantiateAsync((Transform)null, false);
        while (!client.ShipLoadingAsyncHandle.IsDone) yield return null;

        GameObject go = client.ShipLoadingAsyncHandle.Result;
        if (go == null) yield break;
        ShipStatus ship = go.GetComponent<ShipStatus>();
        if (ship == null) yield break;

        ShipStatus.Instance = ship;
        InnerNetObject net = ((Component)ship).GetComponent<InnerNetObject>();
        ((InnerNetClient)client).Spawn(net, -2, (SpawnFlags)0);

        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me != null)
            {
                Vector2 p = me.GetTruePosition();
                go.transform.position = new Vector3(p.x, p.y, go.transform.position.z);
            }
        }
        catch { }

        try { client.ShipLoadingAsyncHandle = default; } catch { }
    }

    internal static string CreateLobby()
    {
        if (!IsHost()) return NocturneText.T("Только хост может создать лобби.", "Only the host can create a lobby.");
        if (LobbyBehaviour.Instance != null) return NocturneText.T("Лобби уже есть.", "Lobby already exists.");

        try
        {
            GameStartManager manager = TryGetGameStartManager();
            if (manager == null || manager.LobbyPrefab == null) return NocturneText.T("Префаб лобби не найден.", "Lobby prefab not found.");

            LobbyBehaviour lobby = UnityEngine.Object.Instantiate(manager.LobbyPrefab);
            if (lobby == null) return NocturneText.T("Не удалось создать лобби.", "Failed to create the lobby.");

            InnerNetObject netObject = ((Il2CppObjectBase)lobby).Cast<InnerNetObject>();
            ((InnerNetClient)AmongUsClient.Instance).Spawn(netObject, -2, (SpawnFlags)0);
            return NocturneText.T("Лобби создано заново.", "Lobby re-created.");
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning((object)$"Create lobby failed: {error}");
            return NocturneText.T("Создание лобби не удалось.", "Lobby creation failed.");
        }
    }

    internal static string DestroyLobby()
    {
        if (!IsHost()) return NocturneText.T("Только хост может разрушить лобби.", "Only the host can destroy the lobby.");

        LobbyBehaviour lobby = LobbyBehaviour.Instance;
        if (lobby == null) return NocturneText.T("Объекта лобби сейчас нет.", "No lobby object right now.");

        try
        {
            InnerNetObject netObject = ((Il2CppObjectBase)lobby).Cast<InnerNetObject>();
            netObject.Despawn();
            return NocturneText.T("Лобби разрушено.", "Lobby destroyed.");
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning((object)$"Destroy lobby failed: {error}");
            return NocturneText.T("Разрушение лобби не удалось.", "Lobby destruction failed.");
        }
    }

    private const float LeaveConfirmSeconds = 3f;
    private static float _leaveAt = -1f;

    internal static void RequestLeave()
    {
        if (AmongUsClient.Instance == null) return;

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
            try { AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame); } catch { }
            return;
        }

        _leaveAt = now + LeaveConfirmSeconds;
        NocturneToast.Push(NocturneText.T("Выход из лобби", "Leave lobby"), NocturneText.T("Нажми ещё раз для подтверждения.", "Press again to confirm."), LeaveConfirmSeconds, NocturneNotifyKind.Warning);
    }

    private static bool IsHost()
    {
        try { return AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost; }
        catch { return false; }
    }

    private static GameStartManager TryGetGameStartManager()
    {
        try
        {
            if (DestroyableSingleton<GameStartManager>.InstanceExists) return DestroyableSingleton<GameStartManager>.Instance;
        }
        catch { }
        try { return UnityEngine.Object.FindObjectOfType<GameStartManager>(); }
        catch { return null; }
    }
}
