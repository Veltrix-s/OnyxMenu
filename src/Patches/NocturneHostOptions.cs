using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;

namespace Nocturne.Patches;

internal static class NocturneHostOptions
{
    private const int NormalMode = 1;
    private const int FoolsMode = 3;
    private const int ImpostorTitle = 133;
    private const int SpeedTitle = 137;
    private const float Floor = 0.00001f;

    internal static bool SyncPass;

    private static bool Loose => NocturneConfig.LooseHostOptions.Value;
    private static bool MinStep => NocturneConfig.ForceMinValues.Value;
    private static bool FourImp => NocturneConfig.FourImpostors.Value;

    internal static bool TryStep(NumberOption option, float dir)
    {
        if (!Loose || KeepVanilla(option)) return HarmonyControl.Continue;
        float inc = MinStep && !IsInt(option) ? 0.1f : option.Increment;
        option.Value += inc * dir;
        Refresh(option);
        return HarmonyControl.SkipOriginal;
    }

    internal static void RelaxRange(NumberOption option)
    {
        if (!Loose || KeepVanilla(option)) return;
        option.ValidRange = new FloatRange(Floor, 999999999f);
        if (!IsInt(option) && option.Value < Floor)
        {
            option.Value = Floor;
            option.UpdateValue();
        }
        RelaxData(option);
    }

    internal static bool TryImpostorCount(ref int count)
    {
        if (FourImp && !IsHnS())
        {
            if (CountPlayers() >= 9) { count = 4; return HarmonyControl.SkipOriginal; }
        }

        var mgr = GameOptionsManager.Instance;
        if (!Loose || mgr == null || mgr.CurrentGameOptions == null) return HarmonyControl.Continue;
        count = mgr.CurrentGameOptions.NumImpostors;
        return HarmonyControl.SkipOriginal;
    }

    internal static bool AllowSync()
    {
        if (!Loose || SyncPass) return HarmonyControl.Continue;
        return IsPublicLobby() ? HarmonyControl.Continue : HarmonyControl.SkipOriginal;
    }

    private static bool IsPublicLobby()
    {
        try
        {
            if (AmongUsClient.Instance == null) return false;
            InnerNetClient c = (InnerNetClient)AmongUsClient.Instance;
            return c.IsGamePublic && !c.IsGameStarted;
        }
        catch { return false; }
    }

    private static bool KeepVanilla(NumberOption option)
    {
        if (IsRoleOption(option)) return true;
        var mgr = GameOptionsManager.Instance;
        if (mgr == null || mgr.CurrentGameOptions == null) return true;
        int mode = (int)mgr.CurrentGameOptions.GameMode;
        bool normal = mode == NormalMode || mode == FoolsMode;
        int title = (int)((OptionBehaviour)option).Title;
        if (normal && title == SpeedTitle) return true;
        if (normal) return false;
        return title == ImpostorTitle;
    }

    private static bool IsRoleOption(NumberOption option)
    {
        try { return ((OptionBehaviour)option).GetComponentInParent<RolesSettingsMenu>() != null; }
        catch { return false; }
    }

    private static bool IsInt(NumberOption option)
    {
        try
        {
            var data = ((OptionBehaviour)option).Data;
            return data != null && ((Il2CppObjectBase)(object)data).TryCast<IntGameSetting>() != null;
        }
        catch { return false; }
    }

    private static void RelaxData(NumberOption option)
    {
        try
        {
            var data = ((OptionBehaviour)option).Data;
            if (data == null) return;
            FloatGameSetting f = ((Il2CppObjectBase)(object)data).TryCast<FloatGameSetting>();
            if (f != null) { f.ValidRange = new FloatRange(Floor, 999999999f); return; }
            IntGameSetting i = ((Il2CppObjectBase)(object)data).TryCast<IntGameSetting>();
            if (i != null) i.ValidRange = new IntRange(0, 999999999);
        }
        catch { }
    }

    private static int CountPlayers()
    {
        int n = 0;
        try
        {
            var cur = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cur.MoveNext())
            {
                PlayerControl p = cur.Current;
                if (p != null && p.Data != null && !p.Data.Disconnected) n++;
            }
        }
        catch { }
        return n;
    }

    private static bool IsHnS()
    {
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true;
            var mgr = GameOptionsManager.Instance;
            if (mgr == null || mgr.CurrentGameOptions == null) return false;
            int mode = (int)mgr.CurrentGameOptions.GameMode;
            return mode == 2 || mode == 4;
        }
        catch { return false; }
    }

    private static void Refresh(NumberOption option)
    {
        option.UpdateValue();
        ((OptionBehaviour)option).OnValueChanged.Invoke((OptionBehaviour)(object)option);
        option.AdjustButtonsActiveState();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
internal static class OptionSyncPatch
{
    public static bool Prefix()
    {
        try { return NocturneHostOptions.AllowSync(); }
        catch { return HarmonyControl.Continue; }
    }
}
