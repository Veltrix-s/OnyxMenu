using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne.Patches;

public sealed class NocturneModStampDriver : MonoBehaviour
{
    private ModManager _mgr;
    private float _nextLookup;

    public void LateUpdate()
    {
        if (LobbyBehaviour.Instance != null) return;

        if (_mgr == null && Time.unscaledTime >= _nextLookup)
        {
            _mgr = Object.FindObjectOfType<ModManager>();
            _nextLookup = Time.unscaledTime + 0.75f;
        }

        if (_mgr == null) return;
        if (NocturneConfig.HideModStamp.Value) NocturneModStamp.Hide(_mgr);
        else NocturneModStamp.Show(_mgr);
    }
}

internal static class NocturneModStamp
{
    private static int _lastShowFrame = -1;

    internal static void Show(ModManager mgr)
    {
        if (mgr == null) return;
        if (NocturneConfig.HideModStamp.Value) { Hide(mgr); return; }
        if (_lastShowFrame == Time.frameCount) return;
        _lastShowFrame = Time.frameCount;

        try
        {
            if (mgr.localCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = Object.FindObjectOfType<Camera>();
                if (cam != null) mgr.localCamera = cam;
            }
        }
        catch { }

        try
        {
            SpriteRenderer stamp = mgr.ModStamp;
            if (stamp != null && ((Renderer)stamp).enabled) return;
        }
        catch { }

        try { mgr.ShowModStamp(); } catch { }
    }

    internal static void Hide(ModManager mgr)
    {
        if (mgr == null) return;
        try
        {
            SpriteRenderer stamp = mgr.ModStamp;
            if (stamp != null && ((Renderer)stamp).enabled) ((Renderer)stamp).enabled = false;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ModManager), "LateUpdate")]
internal static class NocturneModStampPatch
{
    public static void Postfix(ModManager __instance)
    {
        try
        {
            if (__instance == null) return;
            if (NocturneConfig.HideModStamp.Value) NocturneModStamp.Hide(__instance);
            else NocturneModStamp.Show(__instance);
        }
        catch { }
    }

    public static System.Exception Finalizer(System.Exception __exception) => null;
}
