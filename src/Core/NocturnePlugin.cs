using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.CrashReportHandler;

namespace Nocturne;

[BepInProcess("Among Us.exe")]
[BepInPlugin(PluginId, PluginName, PluginVersion)]
public sealed class NocturnePlugin : BasePlugin
{
    public const string PluginId = "nocturne.mod";
    public const string PluginName = "Nocturne";
    public const string PluginVersion = "1.2.1";

    internal static ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony(PluginId);
    private static Harmony _harmonyRef;
    private static readonly List<Behaviour> _components = new List<Behaviour>();

    internal static void Disable()
    {
        try
        {
            _harmonyRef?.UnpatchSelf();
        }
        catch { }

        foreach (Behaviour b in _components)
        {
            try
            {
                if (b != null)
                    b.enabled = false;
            }
            catch { }
        }
    }

    private void AddTracked<T>() where T : MonoBehaviour
    {
        T comp = AddComponent<T>();
        if (comp != null)
            _components.Add(comp);
    }

    public override void Load()
    {
        NocturneDependencies.Setup();
        Logger = Log;
        NocturneDependencies.FlushLog();
        NocturneConfig.Bind(Config);

        InstallHarmonyXNoiseFilter();
        _harmony.PatchAll(typeof(NocturnePlugin).Assembly);
        _harmonyRef = _harmony;
        ApplyTelemetryPreference();
        Patches.NocturneBanWords.Init();

        AddTracked<NocturneMenu>();
        AddTracked<NocturneHud>();
        AddTracked<NocturneToast>();
        AddTracked<NocturneMenuButton>();
        AddTracked<NocturneAutoLobbyReturn>();
        AddTracked<NocturneJoinDetector>();
        AddTracked<NocturneJoinLogger>();
        AddTracked<NocturneHistoryTracker>();
        AddTracked<NocturneTracers>();
        AddTracked<NocturneOverheadChat>();
        AddTracked<NocturneRadar>();
        AddTracked<NocturneMouseTools>();
        AddTracked<NocturneAutoVent>();
        AddTracked<NocturneColoredName>();
        AddTracked<NocturneColorSnipe>();
        AddTracked<NocturneOutfitApplier>();
        AddTracked<NocturneLobbyClones>();
        AddTracked<Patches.NocturneLobbyAnimDriver>();
        AddTracked<Patches.NocturneMainArtDriver>();
        AddTracked<Patches.NocturneStampDriver>();
        AddTracked<Patches.NocturneSpoofDriver>();
        AddTracked<Patches.NocturneAutoHost>();
        AddTracked<NocturneLobbySettings>();
        AddTracked<NocturneDummies>();
        AddTracked<Patches.NocturneAccessGuard>();
        AddTracked<Patches.NocturneModStampDriver>();
        AddTracked<NocturneLobbyPranks>();
        AddTracked<NocturneSnow>();
        AddTracked<NocturneGlichRooms>();
        AddTracked<NocturneMusicPlayer>();
        AddTracked<NocturneDiscordPresence>();
        AddTracked<NocturneVotekick>();
        AddTracked<NocturneAntiVotekick>();
        AddTracked<NocturneSabotage>();
        AddTracked<CameraJammer>();
        AddTracked<NocturneGodMode>();
        AddTracked<NocturneRoleBuffs>();
        AddTracked<NocturneSpeed>();
        AddTracked<NocturneChatSender>();
        AddTracked<NocturneRadial>();
        AddTracked<NocturneEventNotify>();
        AddTracked<NocturneEventLog>();
        AddTracked<NocturneReplay>();
        AddTracked<NocturneChatWindow>();
        AddTracked<NocturneFakeTasks>();
        AddTracked<NocturneAutoTasks>();
        AddTracked<NocturneTwins>();
        AddTracked<NocturneUpdateCheck>();
        AddTracked<NocturneGuiHost>();

        NocturneGate.Init();

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static void InstallHarmonyXNoiseFilter()
    {
        try
        {
            var listeners = BepInEx.Logging.Logger.Listeners;
            var toWrap = new List<ILogListener>(listeners);
            var collection = (ICollection<ILogListener>)listeners;
            foreach (var listener in toWrap)
            {
                collection.Remove(listener);
                collection.Add(new HarmonyXNoiseFilter(listener));
            }
        }
        catch { }
    }

    private sealed class HarmonyXNoiseFilter : ILogListener
    {
        private readonly ILogListener _inner;

        internal HarmonyXNoiseFilter(ILogListener inner) => _inner = inner;

        public LogLevel LogLevelFilter => _inner.LogLevelFilter;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (eventArgs.Level == LogLevel.Warning && eventArgs.Source?.SourceName == "HarmonyX")
            {
                return;
            }

            _inner.LogEvent(sender, eventArgs);
        }

        public void Dispose() => _inner.Dispose();
    }

    private static void ApplyTelemetryPreference()
    {
        if (!NocturneConfig.BlockTelemetry.Value || Application.platform == RuntimePlatform.Android)
        {
            return;
        }

        Analytics.enabled = false;
        Analytics.deviceStatsEnabled = false;
        Analytics.initializeOnStartup = false;
        Analytics.limitUserTracking = true;
        PerformanceReporting.enabled = false;
        CrashReportHandler.enableCaptureExceptions = false;
    }
}
