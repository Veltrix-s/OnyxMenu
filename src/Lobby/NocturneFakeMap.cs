using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class NocturneFakeMap
{
    internal static bool Active { get; private set; }
    internal static bool Loading { get; private set; }
    private static ShipStatus _ship;

    private static bool AmHost => AmongUsClient.Instance != null && ((InnerNetClient)AmongUsClient.Instance).AmHost;

    internal static void Enable(int mapId)
    {
        if (Active || Loading || !AmHost) return;
        AmongUsClient client = AmongUsClient.Instance;
        if (client.ShipPrefabs == null || mapId < 0 || mapId >= client.ShipPrefabs.Count) return;
        HudManager hud = DestroyableSingleton<HudManager>.Instance;
        if (hud == null) return;
        MonoBehaviourExtensions.StartCoroutine((MonoBehaviour)(object)hud, CoEnable(mapId));
    }

    internal static void DisableAndRestoreLobby()
    {
        Disable();
        RestoreLobby();
    }

    private static void Disable()
    {
        if (Loading || !Active || !AmHost) return;

        try
        {
            if (_ship != null)
            {
                ((Il2CppObjectBase)_ship).Cast<InnerNetObject>().Despawn();
                Object.Destroy(((Component)_ship).gameObject);
                _ship = null;
                ShipStatus.Instance = null;
            }
        }
        catch (Exception e) { NocturnePlugin.Logger?.LogWarning((object)$"FakeMap disable failed: {e.Message}"); }

        try
        {
            if (PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.NetTransform != null)
                PlayerControl.LocalPlayer.NetTransform.SnapTo(Vector2.zero);
        }
        catch { }

        Active = false;
        Loading = false;
    }

    private static void RestoreLobby()
    {
        try
        {
            if (LobbyBehaviour.Instance != null || !DestroyableSingleton<GameStartManager>.InstanceExists) return;
            GameStartManager gsm = DestroyableSingleton<GameStartManager>.Instance;
            if (gsm == null || gsm.LobbyPrefab == null) return;
            LobbyBehaviour lobby = Object.Instantiate(gsm.LobbyPrefab);
            if (lobby == null) return;
            ((InnerNetClient)AmongUsClient.Instance).Spawn(((Il2CppObjectBase)lobby).Cast<InnerNetObject>(), -2, (SpawnFlags)0);
        }
        catch (Exception e) { NocturnePlugin.Logger?.LogWarning((object)$"FakeMap restore failed: {e.Message}"); }
    }

    private static IEnumerator CoEnable(int mapId)
    {
        Loading = true;
        AmongUsClient client = AmongUsClient.Instance;
        if (client == null || client.ShipPrefabs == null || mapId < 0 || mapId >= client.ShipPrefabs.Count)
        {
            Loading = false;
            yield break;
        }

        AssetReference assetRef = client.ShipPrefabs[mapId];
        if (assetRef == null) { Loading = false; yield break; }

        LobbyBehaviour lobby = LobbyBehaviour.Instance;
        if (lobby != null)
        {
            try
            {
                ((Il2CppObjectBase)lobby).Cast<InnerNetObject>().Despawn();
                Object.Destroy(((Component)lobby).gameObject);
                LobbyBehaviour.Instance = null;
            }
            catch { }
            yield return null;
        }

        GameObject prefab;
        if (assetRef.Asset != null)
        {
            prefab = ((Il2CppObjectBase)assetRef.Asset).TryCast<GameObject>();
        }
        else
        {
            AsyncOperationHandle<GameObject> handle = assetRef.LoadAssetAsync<GameObject>();
            while (!handle.IsDone) yield return null;
            if ((int)handle.Status != 1) { Loading = false; yield break; }
            prefab = handle.Result;
        }
        if (prefab == null) { Loading = false; yield break; }

        ShipStatus shipPrefab = prefab.GetComponent<ShipStatus>();
        if (shipPrefab == null) { Loading = false; yield break; }

        _ship = Object.Instantiate(shipPrefab);
        if (_ship == null) { Loading = false; yield break; }

        DisableInteractions(_ship);
        ShipStatus.Instance = _ship;
        ((InnerNetClient)AmongUsClient.Instance).Spawn(((Il2CppObjectBase)_ship).Cast<InnerNetObject>(), -2, (SpawnFlags)0);

        var cursor = PlayerControl.AllPlayerControls.GetEnumerator();
        while (cursor.MoveNext())
        {
            PlayerControl pc = cursor.Current;
            if (pc != null) ShipStatus.Instance.SpawnPlayer(pc, 5, false);
        }

        Active = true;
        Loading = false;
    }

    private static void DisableInteractions(ShipStatus ship)
    {
        if (ship == null) return;

        if (ship.EmergencyButton != null)
        {
            try { ship.BreakEmergencyButton(); } catch { }
            ((Behaviour)ship.EmergencyButton).enabled = false;
            ((Component)ship.EmergencyButton).gameObject.SetActive(false);
        }

        AirshipStatus airship = ((Il2CppObjectBase)ship).TryCast<AirshipStatus>();
        if (airship != null)
        {
            if (airship.GapPlatform != null)
            {
                ((Behaviour)airship.GapPlatform).enabled = false;
                ((Component)airship.GapPlatform).gameObject.SetActive(false);
            }
            foreach (MovingPlatformBehaviour mpb in ((Component)ship).GetComponentsInChildren<MovingPlatformBehaviour>(true))
            {
                if (mpb == null) continue;
                ((Behaviour)mpb).enabled = false;
                ((Component)mpb).gameObject.SetActive(false);
            }
        }

        FungleShipStatus fungle = ((Il2CppObjectBase)ship).TryCast<FungleShipStatus>();
        if (fungle == null) return;

        foreach (ZiplineConsole zc in ((Component)ship).GetComponentsInChildren<ZiplineConsole>(true))
        {
            if (zc == null) continue;
            ((Behaviour)zc).enabled = false;
            ((Component)zc).gameObject.SetActive(false);
        }
        foreach (Mushroom m in ((Component)ship).GetComponentsInChildren<Mushroom>(true))
        {
            if (m == null) continue;
            ((Behaviour)m).enabled = false;
            ((Component)m).gameObject.SetActive(false);
        }
    }
}
