using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneHud : MonoBehaviour
{
    private const float LobbyLifetime = 10f * 60f;
    private const float Refresh = 0.25f;

    internal static int CurrentFps = 60;

    private int _frames;
    private float _accum;

    private static int _lobbyGameId = -1;
    private static float _lobbyStart = -1f;

    public void Update()
    {
        _frames++;
        _accum += Mathf.Max(Time.unscaledDeltaTime, 0f);
        if (_accum >= Refresh)
        {
            CurrentFps = Mathf.Clamp(Mathf.RoundToInt(_frames / Mathf.Max(_accum, 0.0001f)), 1, 999);
            _frames = 0;
            _accum = 0f;
        }

        if (NocturneConfig.CopyCodeKey != null && NocturneConfig.CopyCodeKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.CopyCodeKey.Value))
            CopyLobbyCode();

        if (NocturneConfig.EndMatchKey != null && NocturneConfig.EndMatchKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.EndMatchKey.Value))
            TryEndMatch();

        NocturneVentTp.AutoTick();
        NocturneColorAll.Tick();

        if (!NocturneMenu.Rebinding)
        {
            if (NocturneConfig.GodModeKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.GodModeKey.Value))
            {
                NocturneConfig.GodMode.Value = !NocturneConfig.GodMode.Value;
                NocturneToast.Push("God Mode", NocturneConfig.GodMode.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.MirageKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.MirageKey.Value))
            {
                NocturneConfig.LagComp.Value = !NocturneConfig.LagComp.Value;
                NocturneToast.Push(NocturneText.T("Мираж", "Mirage"), NocturneConfig.LagComp.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.VotekickKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VotekickKey.Value))
                NocturneVotekick.ToggleAuto();
            if (NocturneConfig.VotekickAllKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VotekickAllKey.Value))
                NocturneVotekick.VoteEveryone();
            if (NocturneConfig.VotekickHostKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VotekickHostKey.Value))
                NocturneVotekick.VoteHost();
            if (NocturneConfig.RejoinLastKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.RejoinLastKey.Value))
                NocturneVotekick.RejoinLast();
            if (NocturneConfig.VentKickSelectKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VentKickSelectKey.Value))
                NocturneVentKick.SelectAll();
            if (NocturneConfig.VentKickKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VentKickKey.Value))
                NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), NocturneVentKick.KickSelected(), 2.4f, NocturneNotifyKind.Info);
            if (NocturneConfig.SabotageKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.SabotageKey.Value))
                NocturneSabotage.All();
            if (NocturneConfig.DoorsKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.DoorsKey.Value))
                NocturneDoors.CloseAll();
            if (NocturneConfig.InvisibleKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.InvisibleKey.Value))
            {
                NocturneConfig.Invisible.Value = !NocturneConfig.Invisible.Value;
                NocturneToast.Push(NocturneText.T("Невидимость", "Invisibility"), NocturneConfig.Invisible.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.NoClipKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.NoClipKey.Value))
            {
                NocturneConfig.VisualNoClip.Value = !NocturneConfig.VisualNoClip.Value;
                NocturneToast.Push(NocturneText.T("Ноклип", "No-clip"), NocturneConfig.VisualNoClip.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.ZoomKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.ZoomKey.Value))
            {
                NocturneConfig.VisualCameraZoom.Value = !NocturneConfig.VisualCameraZoom.Value;
                NocturneToast.Push(NocturneText.T("Зум", "Zoom"), NocturneConfig.VisualCameraZoom.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.EventConsoleKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.EventConsoleKey.Value))
            {
                NocturneConfig.EventConsole.Value = !NocturneConfig.EventConsole.Value;
                NocturneToast.Push(NocturneText.T("Консоль событий", "Event console"), NocturneConfig.EventConsole.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.ChatWindowKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.ChatWindowKey.Value))
                NocturneConfig.ChatWindow.Value = !NocturneConfig.ChatWindow.Value;
            if (NocturneConfig.OpenChatKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.OpenChatKey.Value))
                ToggleGameChat();
            if (NocturneConfig.ChatSpamKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.ChatSpamKey.Value))
            {
                NocturneChatSender.Spamming = !NocturneChatSender.Spamming;
                NocturneToast.Push(NocturneText.T("Спам чата", "Chat spam"), NocturneChatSender.Spamming ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.GhostNowKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.GhostNowKey.Value))
                NocturneToast.Push(NocturneText.T("Суицид", "Suicide"), Patches.GhostStart.Now(), 2.5f, NocturneNotifyKind.Warning);
            if (NocturneConfig.VentTpKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VentTpKey.Value))
            {
                string vr = NocturneVentTp.SendMarked();
                if (vr.Length > 0) NocturneToast.Push(NocturneText.T("Вент-ТП", "Vent TP"), vr, 2.2f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.VentCycleKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.VentCycleKey.Value))
                NocturneToast.Push(NocturneText.T("Люк", "Vent"), NocturneVentTp.CycleVent(1), 1.5f, NocturneNotifyKind.Info);
            if (NocturneConfig.GhostKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.GhostKey.Value))
            {
                NocturneConfig.GhostAfterStart.Value = !NocturneConfig.GhostAfterStart.Value;
                if (NocturneConfig.GhostAfterStart.Value) NocturneConfig.GameMaster.Value = false;
                NocturneToast.Push(NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostAfterStart.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.SeeGhostsKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.SeeGhostsKey.Value))
            {
                NocturneConfig.SeeGhosts.Value = !NocturneConfig.SeeGhosts.Value;
                NocturneToast.Push(NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhosts.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneConfig.SpawnLobbyKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.SpawnLobbyKey.Value))
                NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.CreateLobby(), 2.5f, NocturneNotifyKind.Success);
            if (NocturneConfig.DespawnLobbyKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.DespawnLobbyKey.Value))
                NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.DestroyLobby(), 2.5f, NocturneNotifyKind.Warning);
            if (NocturneConfig.LeaveLobbyKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.LeaveLobbyKey.Value))
                NocturneLobbyTools.RequestLeave();

            if (NocturneConfig.FunEggKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunEggKey.Value))
                FunKey(NocturneLobbyPranks.MassMorphToEgg());
            if (NocturneConfig.FunMorphKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunMorphKey.Value))
                FunKey(NocturneLobbyPranks.MorphAllIntoSelected());
            if (NocturneConfig.FunRainbowKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunRainbowKey.Value))
                FunKey(NocturneLobbyPranks.ToggleRainbow());
            if (NocturneConfig.FunSkinCycleKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunSkinCycleKey.Value))
                FunKey(NocturneLobbyPranks.ToggleSkinCycle());
            if (NocturneConfig.FunBeatKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunBeatKey.Value))
                FunKey(NocturneLobbyPranks.ToggleSync());
            if (NocturneConfig.FunSizeKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunSizeKey.Value))
                FunKey(NocturneLobbyPranks.CycleScale());
            if (NocturneConfig.FunMotionKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunMotionKey.Value))
                FunKey(NocturneLobbyPranks.CycleSpin());
            if (NocturneConfig.FunAnimKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunAnimKey.Value))
                FunKey(NocturneLobbyPranks.CycleAnim());
            if (NocturneConfig.FunResetKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.FunResetKey.Value))
                FunKey(NocturneLobbyPranks.ResetAppearance());
        }

        NocturneXmas.Tick();

        if (!NocturneMenu.Rebinding && MeetingHud.Instance != null)
        {
            if (NocturneConfig.CloseVotingKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.CloseVotingKey.Value))
                NocturneToast.Push(NocturneText.T("Голосование", "Voting"), NocturneMeetingTools.CloseVoting(), 2f, NocturneNotifyKind.Info);
            if (NocturneConfig.CloseMeetingKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.CloseMeetingKey.Value))
                NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingTools.CloseMeeting(), 2f, NocturneNotifyKind.Info);
        }
    }

    public void LateUpdate()
    {
        int fps = NocturneConfig.FpsLock30.Value ? 30 : (NocturneConfig.FpsCap.Value >= 300 ? -1 : NocturneConfig.FpsCap.Value);
        if (Application.targetFrameRate != fps || QualitySettings.vSyncCount != 0 || QualitySettings.maxQueuedFrames != 2)
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.maxQueuedFrames = 2;
            Application.targetFrameRate = fps;
        }
    }

    private static void TryEndMatch()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null || GameManager.Instance == null) return;
        try { GameManager.Instance.RpcEndGame((GameOverReason)1, false); } catch { }
    }

    private static void FunKey(string msg) => NocturneToast.Push(NocturneText.T("Фан", "Fun"), msg, 2.5f, NocturneNotifyKind.Info);

    private static string _lastTracker;
    private static int _apId;
    private static Vector3 _apEdge;

    private static void LowerTracker(PingTracker tracker)
    {
        try
        {
            AspectPosition ap = tracker.GetComponent<AspectPosition>();
            if (ap == null) return;
            int id = ap.GetInstanceID();
            if (id != _apId) { _apId = id; _apEdge = ap.DistanceFromEdge; }
            Vector3 want = _apEdge; want.y = _apEdge.y * 0.75f; want.x = _apEdge.x + 0.13f;
            if ((ap.DistanceFromEdge - want).sqrMagnitude > 1e-6f)
            {
                ap.DistanceFromEdge = want;
                ap.AdjustPosition();
            }
        }
        catch { }
    }

    internal static void RenderTracker(PingTracker tracker)
    {
        if (tracker == null || tracker.text == null) return;

        if (NocturneConfig.LobbyBar != null && NocturneConfig.LobbyBar.Value && LobbyBehaviour.Instance != null)
        {
            if (_lastTracker != string.Empty) { tracker.text.text = string.Empty; _lastTracker = string.Empty; }
            return;
        }

        LowerTracker(tracker);

        int ping = 0;
        try { if (AmongUsClient.Instance != null) ping = ((InnerNetClient)AmongUsClient.Instance).Ping; }
        catch { ping = 0; }

        string text = BuildLine(ping);

        if (text == _lastTracker) return;
        _lastTracker = text;

        TMP_Text t = tracker.text;
        t.richText = true;
        t.enableWordWrapping = false;
        t.alignment = TextAlignmentOptions.Center;
        t.lineSpacing = -6f;
        t.overflowMode = TextOverflowModes.Overflow;
        t.text = text;
    }

    private static string BuildLine(int ping)
    {
        const string Div = "  <color=#3B4E5C>│</color>  ";
        var segs = new System.Collections.Generic.List<string>(4);

        string pc = ping <= 0 ? "9AA7B4" : ping < 80 ? "7FEC9A" : ping < 160 ? "EDE87A" : "F07070";
        segs.Add($"<color=#607888>PING</color> <mspace=0.56em><b><color=#{pc}>{ping,3}</color></b></mspace> <color=#607888>ms</color>");

        if (NocturneConfig.ShowFps != null && NocturneConfig.ShowFps.Value)
        {
            int fps = CurrentFps;
            string c = fps >= 55 ? "7FEC9A" : fps >= 30 ? "EDE87A" : fps >= 20 ? "F5A84A" : "F07070";
            segs.Add($"<color=#{c}>●</color> <mspace=0.56em><b><color=#{c}>{fps,3}</color></b></mspace> <color=#607888>fps</color>");
        }

        if (NocturneConfig.ShowLobbyTimer != null && NocturneConfig.ShowLobbyTimer.Value && TryLobbyTimer(out int remaining))
        {
            string value = $"{remaining / 60}:{remaining % 60:00}";
            if (remaining < 60)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.6f);
                pulse = pulse * pulse * (3f - 2f * pulse);
                string tp = Hex(Color.Lerp(new Color(0.74f, 0.16f, 0.19f), new Color(1f, 0.48f, 0.48f), pulse));
                segs.Add($"<color=#{tp}>●</color> <color=#607888>{NocturneText.T("Лобби:", "Lobby:")}</color> <mspace=0.56em><b><color=#{tp}>{value}</color></b></mspace>");
            }
            else
            {
                segs.Add($"<color=#D4E4F0>●</color> <color=#607888>{NocturneText.T("Лобби:", "Lobby:")}</color> <mspace=0.56em><color=#D4E4F0>{value}</color></mspace>");
            }
        }

        if (NocturneConfig.ShowHostLine != null && NocturneConfig.ShowHostLine.Value && ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
        {
            string host = HostName();
            if (host.Length > 0)
                segs.Add($"<color=#607888>{NocturneText.T("Хост:", "Host:")}</color> <color=#FFD9A3>{host}</color>");
        }

        return "<size=82%>" + string.Join(Div, segs) + "</size>";
    }

    private static void ToggleGameChat()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            ChatController chat = hud != null ? hud.Chat : null;
            if (chat == null) return;
            if (chat.IsOpenOrOpening) chat.Close();
            else { ((Component)chat).gameObject.SetActive(true); chat.SetVisible(true); chat.Toggle(); }
        }
        catch { }
    }

    private static string _hostC = string.Empty;
    private static float _hostAt = -99f;

    private static string HostName()
    {
        float now = Time.unscaledTime;
        if (now - _hostAt < 1f) return _hostC;
        _hostAt = now;

        try
        {
            InnerNetClient net = (InnerNetClient)AmongUsClient.Instance;
            if (net == null) return _hostC = string.Empty;
            ClientData host = net.GetHost();
            string raw = host != null ? host.PlayerName : null;
            if (string.IsNullOrWhiteSpace(raw) && host != null && host.Character != null && host.Character.Data != null)
                raw = host.Character.Data.PlayerName;
            if (string.IsNullOrWhiteSpace(raw)) return _hostC = string.Empty;

            string clean = NocturneNameColor.Strip(raw).Trim();
            if (clean.Length > 16) clean = clean.Substring(0, 15) + "…";
            return _hostC = net.AmHost ? clean + NocturneText.T(" (Вы)", " (You)") : clean;
        }
        catch { return _hostC = string.Empty; }
    }

    private static bool TryLobbyTimer(out int remaining)
    {
        remaining = 0;
        if (LobbyBehaviour.Instance == null || AmongUsClient.Instance == null)
        {
            _lobbyStart = -1f;
            _lobbyGameId = -1;
            return false;
        }

        int gid = ((InnerNetClient)AmongUsClient.Instance).GameId;
        if (_lobbyStart < 0f || gid != _lobbyGameId)
        {
            _lobbyGameId = gid;
            float elapsed = LobbyBehaviour.Instance.optionsTimer;
            float seed = elapsed > 0f && elapsed < LobbyLifetime ? elapsed : 0f;
            _lobbyStart = Time.realtimeSinceStartup - seed;
        }

        remaining = Mathf.Max(0, Mathf.CeilToInt(LobbyLifetime - (Time.realtimeSinceStartup - _lobbyStart)));
        return true;
    }

    private static void CopyLobbyCode()
    {
        if (AmongUsClient.Instance == null) return;
        int id = ((InnerNetClient)AmongUsClient.Instance).GameId;
        string code = GameCode.IntToGameName(id);
        if (string.IsNullOrEmpty(code))
        {
            NocturneToast.Push(NocturneText.T("Код лобби недоступен", "Lobby code unavailable"));
            return;
        }

        GUIUtility.systemCopyBuffer = code;
        NocturneToast.Push(NocturneText.T($"Код скопирован: <b>{code}</b>", $"Code copied: <b>{code}</b>"));
    }

    private static string Hex(Color c)
    {
        Color32 c32 = c;
        return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
    }
}

[HarmonyPatch(typeof(PingTracker), "Update")]
internal static class NocturnePingTrackerPatch
{
    public static bool Prefix(PingTracker __instance)
    {
        try
        {
            NocturneHud.RenderTracker(__instance);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
