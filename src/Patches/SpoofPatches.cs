using AmongUs.Data;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(PlatformSpecificData), "Serialize")]
internal static class PlatformSpoofOutboundPatch
{

    private static readonly Platforms[] PlatformValues =
    {
        (Platforms)1,
        (Platforms)2,
        (Platforms)3,
        (Platforms)4,
        (Platforms)5,
        (Platforms)6,
        (Platforms)7,
        (Platforms)8,
        (Platforms)9,
        (Platforms)10,
        (Platforms)112,
        (Platforms)0,
    };

    public static void Prefix(PlatformSpecificData __instance)
    {
        if (__instance == null) return;
        if (!(NocturneConfig.SpoofPlatformEnabled?.Value ?? false)) return;
        try
        {
            int idx = Mathf.Clamp(NocturneConfig.SpoofPlatformIndex?.Value ?? 1, 0, PlatformValues.Length - 1);
            Platforms platform = PlatformValues[idx];
            __instance.Platform = platform;

            switch ((int)platform)
            {
                case 4:
                    __instance.XboxPlatformId = 2584878536129841uL;
                    break;
                case 8:
                    __instance.PlatformName = "StargazerS";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                case 9:
                    __instance.PlatformName = "CosmicVoid7";
                    __instance.XboxPlatformId = 2584878536129841uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                case 10:
                    __instance.PlatformName = "";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
                default:
                    __instance.PlatformName = "TESTNAME";
                    __instance.XboxPlatformId = 0uL;
                    __instance.PsnPlatformId = 0uL;
                    break;
            }
        }
        catch { }
    }
}

public sealed class NocturneSpoofDriver : MonoBehaviour
{
    private float _nextLevel;
    private float _nextFc;
    private string _fcApplied;
    private bool _fcPrevOn;

    public void Update()
    {
        float now = Time.realtimeSinceStartup;

        if (NocturneConfig.SpoofLevelEnabled?.Value ?? false)
        {
            if (now >= _nextLevel) { _nextLevel = now + 3f; ApplyLevel(); }
        }

        bool on = NocturneFcSpoof.On;
        string val = NocturneFcSpoof.Value;
        bool edge = on && !string.IsNullOrEmpty(val) && (!_fcPrevOn || val != _fcApplied);
        _fcPrevOn = on;
        if (edge && now >= _nextFc)
        {
            _nextFc = now + 2f;
            _fcApplied = val;
            NocturneFcSpoof.Apply();
        }
    }

    private static void ApplyLevel()
    {
        int display = Mathf.Clamp(NocturneConfig.SpoofLevelValue?.Value ?? 100, 1, 9999);
        uint raw = (uint)(display - 1);
        try
        {
            if (DataManager.Player.stats.level != raw)
            {
                DataManager.Player.stats.level = raw;
                ((AbstractSaveData)DataManager.Player).Save();
            }
        }
        catch
        {
            try
            {
                if (DataManager.Player.Stats.Level != raw)
                {
                    DataManager.Player.Stats.Level = raw;
                    ((AbstractSaveData)DataManager.Player).Save();
                }
            }
            catch { }
        }
    }

    internal static void ApplyNow() => ApplyLevel();
}

[HarmonyPatch(typeof(SystemInfo), nameof(SystemInfo.deviceUniqueIdentifier), MethodType.Getter)]
internal static class DeviceIdSpoofPatch
{
    private static string fake;

    public static void Postfix(ref string __result)
    {
        if (!(NocturneConfig.SpoofDeviceId?.Value ?? false)) return;
        fake ??= System.Guid.NewGuid().ToString("N");
        __result = fake;
    }
}

internal static class NocturneFcSpoof
{
    private static readonly string[] A = { "cosmic", "stellar", "nebula", "astral", "lunar", "solar", "vortex", "plasma", "photon", "quasar", "nocturne", "ember", "frost", "raven" };
    private static readonly string[] B = { "flux", "wave", "beam", "core", "node", "link", "zone", "pulse", "spark", "glow", "byte", "rift", "vibe", "haze" };

    internal static bool On => NocturneConfig.SpoofFriendCodeEnabled?.Value ?? false;
    internal static string Value => (NocturneConfig.SpoofFriendCodeValue?.Value ?? "").Trim();

    internal static string Random() => A[UnityEngine.Random.Range(0, A.Length)] + B[UnityEngine.Random.Range(0, B.Length)] + "#" + UnityEngine.Random.Range(1000, 9999);

    internal static void Apply()
    {
        string value = Value;
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            FriendsListManager mgr = FriendsListManager.Instance;
            if (mgr == null)
            {
                NocturneToast.Push(NocturneText.T("ФК", "FC"), NocturneText.T("Недоступно сейчас.", "Not available now."), 2.5f, NocturneNotifyKind.Warning);
                return;
            }
            EditAccountUsername edit = null;
            try { if (EOSManager.Instance != null) edit = EOSManager.Instance.editAccountUsername; } catch { }
            var managed = new System.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>((state, resp) =>
            {
                bool usedGame = false;
                try { if (edit != null) { edit._SaveUsername_b__8_0(state, resp); usedGame = true; } } catch { }
                try
                {
                    bool ok = state == Assets.InnerNet.ResponseState.Success;
                    if (ok && !usedGame) try { if (EOSManager.Instance != null) EOSManager.Instance.FriendCode = value; } catch { }
                    NocturneToast.Push(NocturneText.T("ФК", "FC"),
                        ok ? NocturneText.T("Имя изменено.", "Name changed.") : NocturneText.T("Сервер отклонил имя.", "Server rejected the name."),
                        3.5f, ok ? NocturneNotifyKind.Success : NocturneNotifyKind.Warning);
                }
                catch { }
            });
            var cb = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(managed);
            mgr.SetFriendCode(value, cb);
            NocturneToast.Push(NocturneText.T("ФК", "FC"), NocturneText.T("Отправлено на сервер…", "Sent to server…"), 2f, NocturneNotifyKind.Info);
        }
        catch (System.Exception e)
        {
            NocturneToast.Push(NocturneText.T("ФК", "FC"), e.Message, 3.5f, NocturneNotifyKind.Danger);
        }
    }
}

[HarmonyPatch(typeof(FriendsListManager), nameof(FriendsListManager.SetFriendCode))]
internal static class NocturneFcRegisterPatch
{
    private static bool _fallback;

    public static bool Prefix([HarmonyArgument(0)] ref string username,
        [HarmonyArgument(1)] ref Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>> resultCallback)
    {
        if (_fallback || !NocturneFcSpoof.On) return true;
        string value = NocturneFcSpoof.Value;
        if (string.IsNullOrEmpty(value)) return true;

        int hash = value.LastIndexOf('#');
        if (hash <= 0) { username = value; return true; }

        string fallback = value.Substring(0, hash);
        string suffix = value.Substring(hash + 1);
        if (string.IsNullOrEmpty(suffix)) { username = fallback; return true; }
        for (int i = 0; i < suffix.Length; i++)
        {
            if (suffix[i] >= '0' && suffix[i] <= '9') continue;
            username = fallback;
            return true;
        }

        username = value;
        var original = resultCallback;
        resultCallback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(
            (System.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>)((state, response) =>
            {
                if (state == Assets.InnerNet.ResponseState.Failed)
                {
                    try
                    {
                        FriendsListManager mgr = FriendsListManager.Instance;
                        if (mgr != null)
                        {
                            _fallback = true;
                            mgr.SetFriendCode(fallback, original);
                            return;
                        }
                    }
                    catch { }
                    finally { _fallback = false; }
                }
                if (original != null) original.Invoke(state, response);
            }));
        return true;
    }
}

[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.OnEnable))]
internal static class NocturneFcScreenPatch
{
    public static void Postfix(EditAccountUsername __instance)
    {
        if (!NocturneFcSpoof.On || __instance == null) return;
        string value = NocturneFcSpoof.Value;
        if (string.IsNullOrEmpty(value)) return;
        try { if (__instance.UsernameText != null) __instance.UsernameText.text = value; } catch { }
        try
        {
            foreach (PassiveButton b in __instance.GetComponentsInChildren<PassiveButton>(true))
                try { if (b != null) b.SetButtonEnableState(true); } catch { }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.RandomizeName))]
internal static class NocturneFcRandomPatch
{
    public static void Postfix(EditAccountUsername __instance)
    {
        if (!NocturneFcSpoof.On || __instance == null || __instance.UsernameText == null) return;
        string value = NocturneFcSpoof.Value;
        if (!string.IsNullOrEmpty(value)) try { __instance.UsernameText.text = value; } catch { }
    }
}

[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.SaveUsername))]
internal static class NocturneFcConfirmPatch
{
    public static bool Prefix(EditAccountUsername __instance)
    {
        if (!NocturneFcSpoof.On || __instance == null) return true;
        string value = NocturneFcSpoof.Value;
        if (string.IsNullOrEmpty(value)) return true;
        try
        {
            FriendsListManager mgr = FriendsListManager.Instance;
            if (mgr == null) return true;

            if (__instance.UsernameText != null) __instance.UsernameText.text = value;

            var callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(
                new System.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>(__instance._SaveUsername_b__8_0));
            mgr.SetFriendCode(value, callback);
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.SetButtonEnableState))]
internal static class NocturneFcButtonPatch
{
    public static void Prefix(PassiveButton __instance, [HarmonyArgument(0)] ref bool enable)
    {
        if (!NocturneFcSpoof.On || __instance == null || enable) return;
        try { if (__instance.GetComponentInParent<EditAccountUsername>() != null) enable = true; } catch { }
    }
}
