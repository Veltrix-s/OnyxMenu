using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne.Patches;

public sealed class NocturneAutoHost : MonoBehaviour
{
    public void Update()
    {
        NocturneAutoHostService.Tick();
    }

    public void OnDisable()
    {
        NocturneAutoHostService.ResetTransientState();
    }
}

internal static class NocturneAutoHostService
{
    private enum AutoHostState
    {
        Disabled, Idle, Warmup, WaitingPlayers, WaitingLoad, Countdown, Starting, InGame, Returning, Backoff,
    }

    private const float TickIntervalSeconds = 0.2f;
    private const float StartRequestGraceSeconds = 7f;
    private const float LobbyLifetimeSeconds = 10f * 60f;
    private const float LastMinuteStartSeconds = 60f;
    private const float NotificationCooldownSeconds = 0.75f;

    private static AutoHostState state = AutoHostState.Disabled;
    private static string lastReason = "disabled";
    private static float nextTickAt;
    private static float countdownStartedAt = -1f;
    private static float activeCountdownDelay = -1f;
    private static float backoffUntil = -1f;
    private static float lastStartIssuedAt = -1f;
    private static float lobbyOpenedAt = -1f;
    private static float loadWaitStartedAt = -1f;
    private static float lastNotificationAt = -1f;
    private static int lobbyGameId = -1;
    private static int lastCountdownNotice = -1;

    internal static void Tick()
    {
        float now = Time.unscaledTime;
        if (now < nextTickAt)
        {
            return;
        }

        nextTickAt = now + TickIntervalSeconds;
        if (!IsEnabled)
        {
            ResetLobbyFlow(clearBackoff: true);
            SetState(AutoHostState.Disabled, "выключен");
            return;
        }

        InnerNetClient client = TryGetClient();
        if (client == null)
        {
            ResetLobbyFlow(clearBackoff: false);
            SetState(AutoHostState.Idle, "клиент недоступен");
            return;
        }

        if (!client.AmHost)
        {
            ResetLobbyFlow(clearBackoff: false);
            SetState(AutoHostState.Idle, "ожидаю хост-контекст");
            return;
        }

        if (IsEndGameScreen())
        {
            ResetLobbyFlow(clearBackoff: false);
            SetState(ShouldReturnAfterMatch ? AutoHostState.Returning : AutoHostState.InGame, ShouldReturnAfterMatch ? "возврат в лобби" : "матч завершен");
            return;
        }

        if (IsInMatch())
        {
            ResetLobbyFlow(clearBackoff: true);
            SetState(AutoHostState.InGame, "матч идет");
            return;
        }

        if (LobbyBehaviour.Instance == null)
        {
            ResetLobbyFlow(clearBackoff: false);
            lobbyOpenedAt = -1f;
            lobbyGameId = -1;
            SetState(AutoHostState.Idle, "вне лобби");
            return;
        }

        TrackLobby(client, now);
        TickHostedLobby(client, now);
    }

    internal static void ResetTransientState()
    {
        nextTickAt = 0f;
        ResetLobbyFlow(clearBackoff: true);
        SetState(IsEnabled ? AutoHostState.Idle : AutoHostState.Disabled, IsEnabled ? "сброшен" : "выключен");
    }

    private static void TickHostedLobby(InnerNetClient client, float now)
    {
        int connectedPlayers = CountLobbyPlayers(client, out int readyPlayers, out string loadingName);
        bool forceStart = ShouldForceStart(connectedPlayers, out string forceReason);
        float warmupRemaining = WarmupRemaining;

        if (!forceStart && warmupRemaining > 0.05f)
        {
            countdownStartedAt = -1f;
            activeCountdownDelay = -1f;
            lastStartIssuedAt = -1f;
            lastCountdownNotice = -1;
            SetState(AutoHostState.Warmup, $"прогрев лобби {Mathf.CeilToInt(warmupRemaining)}с");
            return;
        }

        bool waitingForLoad = NocturneConfig.AutoHostWaitLoadedPlayers.Value && connectedPlayers > readyPlayers;

        if (waitingForLoad && !forceStart && !CanBypassLoadWait(now, readyPlayers, connectedPlayers, loadingName))
        {
            countdownStartedAt = -1f;
            activeCountdownDelay = -1f;
            lastStartIssuedAt = -1f;
            lastCountdownNotice = -1;
            SetState(AutoHostState.WaitingLoad, $"ожидаю прогрузку {readyPlayers}/{connectedPlayers}: {loadingName}");
            return;
        }
        if (!waitingForLoad)
        {
            loadWaitStartedAt = -1f;
        }

        if (lastStartIssuedAt > 0f)
        {
            if (now - lastStartIssuedAt < StartRequestGraceSeconds)
            {
                SetState(AutoHostState.Starting, "старт отправлен");
                return;
            }

            lastStartIssuedAt = -1f;
            EnterBackoff(NocturneText.T("старт не подтвердился", "start not confirmed"));
            return;
        }

        if (backoffUntil > now)
        {
            SetState(AutoHostState.Backoff, "пауза после попытки");
            return;
        }

        int requiredPlayers = RequiredPlayers;
        bool enoughPlayers = NocturneConfig.AutoHostWaitLoadedPlayers.Value ? readyPlayers >= requiredPlayers : connectedPlayers >= requiredPlayers;
        bool continueBelowMin = !NocturneConfig.AutoHostCancelBelowMin.Value && countdownStartedAt >= 0f && connectedPlayers >= 2;
        if (!forceStart && !enoughPlayers && !continueBelowMin)
        {
            if (countdownStartedAt >= 0f)
            {
                Notify(NocturneText.T("Автохост", "Auto-host"), NocturneText.T("Отсчёт отменён: игроков меньше минимума.", "Countdown canceled: below minimum players."));
            }

            countdownStartedAt = -1f;
            activeCountdownDelay = -1f;
            lastCountdownNotice = -1;
            SetState(AutoHostState.WaitingPlayers, $"игроки {connectedPlayers}/{requiredPlayers}");
            return;
        }

        float delay = EffectiveStartDelay(connectedPlayers);
        if (!forceStart && countdownStartedAt < 0f)
        {
            countdownStartedAt = now;
            activeCountdownDelay = delay;
            lastCountdownNotice = -1;
            SetState(AutoHostState.Countdown, IsFastStartActive(connectedPlayers) ? "быстрый старт" : "минимум игроков набран");
            Notify(NocturneText.T("Автохост", "Auto-host"), NocturneText.T($"Старт через {Mathf.CeilToInt(delay)} с.", $"Start in {Mathf.CeilToInt(delay)}s"));
        }

        if (!forceStart && now - countdownStartedAt < delay)
        {
            AnnounceCountdown(delay - (now - countdownStartedAt));
            SetState(AutoHostState.Countdown, "отсчет");
            return;
        }

        GameStartManager manager = TryGetGameStartManager();
        if (manager == null)
        {
            EnterBackoff(NocturneText.T("кнопка старта не найдена", "start button not found"));
            return;
        }

        if (!TryConfiguredStart(manager))
        {
            EnterBackoff(forceStart ? NocturneText.T("форс-старт отклонён", "force start rejected") : NocturneText.T("старт отклонён", "start rejected"));
            return;
        }

        countdownStartedAt = -1f;
        activeCountdownDelay = -1f;
        backoffUntil = -1f;
        lastStartIssuedAt = now;
        lastCountdownNotice = -1;
        SetState(AutoHostState.Starting, forceStart ? forceReason : "старт матча");
        Notify(NocturneText.T("Автохост", "Auto-host"), forceStart ? forceReason : NocturneText.T("Минимум набран, запускаю матч.", "Minimum reached, starting match."));
    }

    private static void TrackLobby(InnerNetClient client, float now)
    {
        int gameId;
        try { gameId = client.GameId; }
        catch { gameId = 0; }

        if (lobbyOpenedAt >= 0f && lobbyGameId == gameId)
        {
            return;
        }

        lobbyOpenedAt = now;
        lobbyGameId = gameId;
        ResetLobbyFlow(clearBackoff: true);
        SetState(AutoHostState.WaitingPlayers, "новое лобби");
    }

    private static void AnnounceCountdown(float remaining)
    {
        int whole = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (whole == lastCountdownNotice)
        {
            return;
        }

        if (whole == 60 || whole == 30 || whole == 15 || whole == 10 || whole == 5 || whole == 3 || whole == 2 || whole == 1)
        {
            lastCountdownNotice = whole;
            Notify(NocturneText.T("Автохост", "Auto-host"), NocturneText.T($"Старт через {whole} с.", $"Start in {whole}s"));
        }
    }

    private static bool TryConfiguredStart(GameStartManager manager)
    {
        if (manager == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || LobbyBehaviour.Instance == null)
        {
            return false;
        }

        try
        {
            manager.MinPlayers = 1;
            StartControl.UnlockStartButton(manager);
            if (NocturneConfig.AutoHostInstantStart.Value)
            {
                return StartControl.TryInstantStart(manager);
            }

            manager.BeginGame();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnterBackoff(string reason)
    {
        countdownStartedAt = -1f;
        activeCountdownDelay = -1f;
        lastStartIssuedAt = -1f;
        loadWaitStartedAt = -1f;
        lastCountdownNotice = -1;
        backoffUntil = Time.unscaledTime + BackoffSeconds;
        SetState(AutoHostState.Backoff, reason);
        Notify(NocturneText.T("Автохост: пауза", "Auto-host: paused"), reason);
    }

    private static void ResetLobbyFlow(bool clearBackoff)
    {
        countdownStartedAt = -1f;
        lastStartIssuedAt = -1f;
        lastCountdownNotice = -1;
        if (clearBackoff)
        {
            backoffUntil = -1f;
        }
    }

    private static void SetState(AutoHostState nextState, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            lastReason = reason.Trim();
        }

        state = nextState;
    }

    private static int CountLobbyPlayers(InnerNetClient client, out int readyPlayers, out string loadingName)
    {
        readyPlayers = 0;
        loadingName = "игрок";
        if (client == null || client.allClients == null)
        {
            return 0;
        }

        int connected = 0;
        try
        {
            var cursor = client.allClients.GetEnumerator();
            while (cursor.MoveNext())
            {
                ClientData data = cursor.Current;
                if (data == null || data.Id < 0)
                {
                    continue;
                }

                if (IsDisconnected(data))
                {
                    continue;
                }

                connected++;
                if (IsReady(data))
                {
                    readyPlayers++;
                }
                else
                {
                    loadingName = CleanName(data.PlayerName);
                }
            }
        }
        catch
        {
            return CountReadyPlayerControls(out readyPlayers);
        }

        return connected;
    }

    private static int CountReadyPlayerControls(out int readyPlayers)
    {
        readyPlayers = 0;
        try
        {
            if (PlayerControl.AllPlayerControls == null)
            {
                return 0;
            }

            int count = 0;
            var cursor = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cursor.MoveNext())
            {
                PlayerControl player = cursor.Current;
                if (player == null || player.Data == null || player.Data.Disconnected || player.PlayerId >= 100)
                {
                    continue;
                }

                count++;
                readyPlayers++;
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsReady(ClientData data)
    {
        try
        {
            PlayerControl character = data.Character;
            return character != null && character.Data != null && !character.Data.Disconnected && character.PlayerId < 100;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDisconnected(ClientData data)
    {
        try
        {
            return data.Character != null && data.Character.Data != null && data.Character.Data.Disconnected;
        }
        catch
        {
            return false;
        }
    }

    private static GameStartManager TryGetGameStartManager()
    {
        try
        {
            if (DestroyableSingleton<GameStartManager>.InstanceExists)
            {
                return DestroyableSingleton<GameStartManager>.Instance;
            }
        }
        catch { }

        try
        {
            return Object.FindObjectOfType<GameStartManager>();
        }
        catch
        {
            return null;
        }
    }

    private static InnerNetClient TryGetClient()
    {
        try
        {
            return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
        }
        catch
        {
            return null;
        }
    }

    private static bool CanBypassLoadWait(float now, int readyPlayers, int connectedPlayers, string loadingName)
    {
        if (readyPlayers < RequiredPlayers)
        {
            loadWaitStartedAt = -1f;
            return false;
        }

        int grace = Mathf.Clamp(NocturneConfig.AutoHostLoadGraceSeconds?.Value ?? 20, 0, 90);
        if (grace <= 0)
        {
            loadWaitStartedAt = -1f;
            return false;
        }

        if (loadWaitStartedAt < 0f)
        {
            loadWaitStartedAt = now;
        }

        if (now - loadWaitStartedAt < grace)
        {
            SetState(AutoHostState.WaitingLoad, $"жду прогрузку {readyPlayers}/{connectedPlayers}: {loadingName}");
            return false;
        }

        SetState(AutoHostState.Countdown, "прогрузка задержалась, старт по готовым");
        return true;
    }

    private static bool ShouldForceStart(int connectedPlayers, out string reason)
    {
        int minPlayers = ForceMinPlayers;
        if (ForceLastMinuteEnabled && connectedPlayers >= minPlayers && LobbyLifeRemaining >= 0f && LobbyLifeRemaining <= LastMinuteStartSeconds)
        {
            reason = NocturneText.T("форс-старт: лобби скоро закроется", "force start: lobby closing soon");
            return true;
        }

        int forceAfterMinutes = Mathf.Clamp(NocturneConfig.AutoHostForceAfterMinutes?.Value ?? 0, 0, 10);
        if (forceAfterMinutes > 0 && connectedPlayers >= minPlayers && lobbyOpenedAt > 0f && Time.unscaledTime - lobbyOpenedAt >= forceAfterMinutes * 60f)
        {
            reason = NocturneText.T($"форс-старт: ожидание {forceAfterMinutes} мин.", $"force start: waited {forceAfterMinutes} min");
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsFastStartActive(int connectedPlayers)
    {
        int threshold = Mathf.Clamp(NocturneConfig.AutoHostFastStartPlayers?.Value ?? 13, 0, 15);
        return threshold > 0 && connectedPlayers >= threshold;
    }

    private static float EffectiveStartDelay(int connectedPlayers)
    {
        float delay = StartDelaySeconds;
        if (IsFastStartActive(connectedPlayers))
        {
            delay = Mathf.Min(delay, Mathf.Clamp(NocturneConfig.AutoHostFastStartDelaySeconds?.Value ?? 5, 0, 60));
        }

        return delay;
    }

    private static bool IsInMatch()
    {
        return ShipStatus.Instance != null && LobbyBehaviour.Instance == null && !IsEndGameScreen();
    }

    private static bool IsEndGameScreen()
    {
        try
        {
            return Object.FindObjectOfType<EndGameManager>() != null;
        }
        catch
        {
            return false;
        }
    }

    private static void Notify(string title, string detail)
    {
        if (!NocturneConfig.AutoHostNotifications.Value)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (lastNotificationAt > 0f && now - lastNotificationAt < NotificationCooldownSeconds)
        {
            return;
        }

        lastNotificationAt = now;
        NocturneToast.Push(title, detail, 3.2f, NocturneNotifyKind.Info);
    }

    private static string CleanName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "игрок";
        }

        string clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 18 ? clean : clean.Substring(0, 17) + "...";
    }

    private static bool IsEnabled => NocturneConfig.AutoHostEnabled.Value;
    internal static bool ShouldReturnAfterMatch => IsEnabled && NocturneConfig.AutoHostReturnAfterMatch.Value;
    private static bool ForceLastMinuteEnabled => NocturneConfig.AutoHostForceLastMinute.Value;
    private static int RequiredPlayers => Mathf.Clamp(NocturneConfig.AutoHostMinPlayers?.Value ?? 4, 1, 15);
    private static int ForceMinPlayers => Mathf.Clamp(NocturneConfig.AutoHostForceMinPlayers?.Value ?? 2, 1, 15);
    private static float StartDelaySeconds => Mathf.Clamp(NocturneConfig.AutoHostStartDelaySeconds?.Value ?? 15, 0f, 180f);
    private static float BackoffSeconds => Mathf.Clamp(NocturneConfig.AutoHostBackoffSeconds?.Value ?? 8, 2f, 60f);
    private static float LobbyLifeRemaining => lobbyOpenedAt < 0f ? -1f : Mathf.Clamp(LobbyLifetimeSeconds - (Time.unscaledTime - lobbyOpenedAt), 0f, LobbyLifetimeSeconds);
    private static float WarmupRemaining => lobbyOpenedAt < 0f ? 0f : Mathf.Clamp((NocturneConfig.AutoHostWarmupSeconds?.Value ?? 5) - (Time.unscaledTime - lobbyOpenedAt), 0f, 120f);
}
