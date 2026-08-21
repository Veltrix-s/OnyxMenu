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
    public const string PluginVersion = "1.1.8.1";

    internal static ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony = new Harmony(PluginId);

    public override void Load()
    {
        NocturneDependencies.Setup();
        Logger = Log;
        NocturneDependencies.FlushLog();
        NocturneConfig.Bind(Config);

        InstallHarmonyXNoiseFilter();
        _harmony.PatchAll(typeof(NocturnePlugin).Assembly);
        ApplyTelemetryPreference();
        Patches.NocturneBanWords.Init();

        AddComponent<NocturneMenu>();
        AddComponent<NocturneHud>();
        AddComponent<NocturneToast>();
        AddComponent<NocturneLobby>();
        AddComponent<NocturneMenuButton>();
        AddComponent<NocturneAutoLobbyReturn>();
        AddComponent<NocturneJoinDetector>();
        AddComponent<NocturneJoinLogger>();
        AddComponent<NocturneHistoryTracker>();
        AddComponent<NocturneTracers>();
        AddComponent<NocturneOverheadChat>();
        AddComponent<NocturneRadar>();
        AddComponent<NocturneMouseTools>();
        AddComponent<NocturneAutoVent>();
        AddComponent<NocturneColoredName>();
        AddComponent<NocturneColorSnipe>();
        AddComponent<NocturneOutfitApplier>();
        AddComponent<NocturneLobbyClones>();
        AddComponent<Patches.NocturneLobbyAnimDriver>();
        AddComponent<Patches.NocturneMainArtDriver>();
        AddComponent<Patches.NocturneStampDriver>();
        AddComponent<Patches.NocturneSpoofDriver>();
        AddComponent<Patches.NocturneAutoHost>();
        AddComponent<NocturneLobbySettings>();
        AddComponent<NocturneDummies>();
        AddComponent<Patches.NocturneAccessGuard>();
        AddComponent<Patches.NocturneModStampDriver>();
        AddComponent<NocturneLobbyPranks>();
        AddComponent<NocturneLobbyBar>();
        AddComponent<NocturneSnow>();
        AddComponent<NocturneBugRooms>();
        AddComponent<NocturneMusicPlayer>();
        AddComponent<NocturneDiscordPresence>();
        AddComponent<NocturneVotekick>();
        AddComponent<NocturneAntiVotekick>();
        AddComponent<NocturneSabotage>();
        AddComponent<NocturneGodMode>();
        AddComponent<NocturneRoleBuffs>();
        AddComponent<NocturneSpeed>();
        AddComponent<NocturneChatSender>();
        AddComponent<NocturneRadial>();
        AddComponent<NocturneEventNotify>();
        AddComponent<NocturneEventLog>();
        AddComponent<NocturneReplay>();
        AddComponent<NocturneChatWindow>();
        AddComponent<NocturneFakeTasks>();
        AddComponent<NocturneAutoTasks>();
        AddComponent<NocturneTwins>();
        AddComponent<NocturneUpdateCheck>();
        AddComponent<NocturneGuiHost>();

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
        catch
        {
        }
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
