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

        if (NocturneKeys.Down(NocturneConfig.CopyCodeKey))
            CopyLobbyCode();

        if (NocturneKeys.Down(NocturneConfig.EndMatchKey))
            TryEndMatch();

        NocturneInvisible.Tick();
        LobbyPhantom.Tick();
        Corpses.Tick();
        NocturneVentTp.AutoTick();
        NocturneVentTp.EnforceVentMode();
        NocturneShield.Tick();
        NocturneVentKick.Tick();
        NocturneJail.Tick();
        NocturneColorAll.Tick();
        Platform.Tick();
        TaskDrain.Tick();
        NocturnePet.Tick();
        NocturneMeetingTools.Tick();
        VoteSpam.Tick();
        SmokeSpam.Tick();
        LobbyHistory.Tick();
        Dleks.Sync();

        if (!NocturneMenu.Rebinding)
        {
            if (NocturneKeys.Down(NocturneConfig.GodModeKey))
            {
                NocturneConfig.GodMode.Value = !NocturneConfig.GodMode.Value;
                NocturneToast.Push("God Mode", NocturneConfig.GodMode.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.PhantomKey))
                LobbyPhantom.Vanish();
            if (NocturneKeys.Down(NocturneConfig.CorpseKey))
                Corpses.Drop();
            if (NocturneKeys.Down(NocturneConfig.MirageKey))
            {
                NocturneConfig.LagComp.Value = !NocturneConfig.LagComp.Value;
                NocturneToast.Push(NocturneText.T("Мираж", "Mirage"), NocturneConfig.LagComp.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.MeetingRoamKey))
                NocturneToast.Push(NocturneText.T("Собрание", "Meeting"), NocturneMeetingRoam.Roam(), 2.5f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.VotekickKey))
                NocturneVotekick.ToggleAuto();
            if (NocturneKeys.Down(NocturneConfig.VotekickAllKey))
                NocturneVotekick.VoteEveryone();
            if (NocturneKeys.Down(NocturneConfig.ZiplineSelectKey))
            {
                RideTargets.All();
                NocturneToast.Push(NocturneText.T("Цели", "Targets"), NocturneText.T("Отмечено: ", "Selected: ") + RideTargets.Count, 1.6f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.ZiplineDownKey))
                NocturneToast.Push(NocturneText.T("Зиплайн", "Zipline"), Zipline.RideSelected(true), 2.5f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.ZiplineUpKey))
                NocturneToast.Push(NocturneText.T("Зиплайн", "Zipline"), Zipline.RideSelected(false), 2.5f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.VotekickHostKey))
                NocturneVotekick.VoteHost();
            if (NocturneKeys.Down(NocturneConfig.RejoinLastKey))
                NocturneVotekick.RejoinLast();
            if (NocturneKeys.Down(NocturneConfig.VentKickSelectKey))
                NocturneVentKick.SelectAll();
            if (NocturneKeys.Down(NocturneConfig.VentKickKey))
                NocturneToast.Push(NocturneText.T("Вент кик", "Vent kick"), NocturneVentKick.KickSelected(), 2.4f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.SabotageKey))
                NocturneSabotage.All();
            if (NocturneKeys.Down(NocturneConfig.DoorsKey))
                NocturneDoors.CloseAll();
            if (NocturneKeys.Down(NocturneConfig.InvisibleKey))
            {
                NocturneConfig.Invisible.Value = !NocturneConfig.Invisible.Value;
                NocturneToast.Push(NocturneText.T("Невидимость", "Invisibility"), NocturneConfig.Invisible.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.NoClipKey))
            {
                NocturneConfig.VisualNoClip.Value = !NocturneConfig.VisualNoClip.Value;
                NocturneToast.Push(NocturneText.T("Ноклип", "No-clip"), NocturneConfig.VisualNoClip.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.ZoomKey))
            {
                NocturneConfig.VisualCameraZoom.Value = !NocturneConfig.VisualCameraZoom.Value;
                NocturneToast.Push(NocturneText.T("Зум", "Zoom"), NocturneConfig.VisualCameraZoom.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.EventConsoleKey))
            {
                NocturneConfig.EventConsole.Value = !NocturneConfig.EventConsole.Value;
                NocturneToast.Push(NocturneText.T("Консоль событий", "Event console"), NocturneConfig.EventConsole.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.ChatWindowKey))
                NocturneConfig.ChatWindow.Value = !NocturneConfig.ChatWindow.Value;
            if (NocturneKeys.Down(NocturneConfig.OpenChatKey))
                ToggleGameChat();
            if (NocturneKeys.Down(NocturneConfig.ChatSpamKey))
            {
                NocturneChatSender.Spamming = !NocturneChatSender.Spamming;
                NocturneToast.Push(NocturneText.T("Спам чата", "Chat spam"), NocturneChatSender.Spamming ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.GhostNowKey))
                NocturneToast.Push(NocturneText.T("Суицид", "Suicide"), Patches.GhostStart.Now(), 2.5f, NocturneNotifyKind.Warning);
            if (NocturneKeys.Down(NocturneConfig.VentTpKey))
            {
                string vr = NocturneVentTp.SendMarked();
                if (vr.Length > 0)
                    NocturneToast.Push(NocturneText.T("Вент-ТП", "Vent TP"), vr, 2.2f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.VentCycleKey))
                NocturneToast.Push(NocturneText.T("Люк", "Vent"), NocturneVentTp.CycleVent(1), 1.5f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.GhostKey))
            {
                NocturneConfig.GhostAfterStart.Value = !NocturneConfig.GhostAfterStart.Value;
                if (NocturneConfig.GhostAfterStart.Value)
                    NocturneConfig.GameMaster.Value = false;
                NocturneToast.Push(NocturneText.T("Призрак после старта", "Ghost after start"), NocturneConfig.GhostAfterStart.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.SeeGhostsKey))
            {
                NocturneConfig.SeeGhosts.Value = !NocturneConfig.SeeGhosts.Value;
                NocturneToast.Push(NocturneText.T("Видеть призраков", "See ghosts"), NocturneConfig.SeeGhosts.Value ? NocturneText.T("Вкл", "On") : NocturneText.T("Выкл", "Off"), 1.5f, NocturneNotifyKind.Info);
            }
            if (NocturneKeys.Down(NocturneConfig.SpawnLobbyKey))
                NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.CreateLobby(), 2.5f, NocturneNotifyKind.Success);
            if (NocturneKeys.Down(NocturneConfig.DespawnLobbyKey))
                NocturneToast.Push(NocturneText.T("Лобби", "Lobby"), NocturneLobbyTools.DestroyLobby(), 2.5f, NocturneNotifyKind.Warning);
            if (NocturneKeys.Down(NocturneConfig.LeaveLobbyKey))
                NocturneLobbyTools.RequestLeave();

            if (NocturneKeys.Down(NocturneConfig.FunEggKey))
                FunKey(NocturneLobbyPranks.MassMorphToEgg());
            if (NocturneKeys.Down(NocturneConfig.FunMorphKey))
                FunKey(NocturneLobbyPranks.MorphAllIntoSelected());
            if (NocturneKeys.Down(NocturneConfig.FunRainbowKey))
                FunKey(NocturneLobbyPranks.ToggleRainbow());
            if (NocturneKeys.Down(NocturneConfig.FunSkinCycleKey))
                FunKey(NocturneLobbyPranks.ToggleSkinCycle());
            if (NocturneKeys.Down(NocturneConfig.FunBeatKey))
                FunKey(NocturneLobbyPranks.ToggleSync());
            if (NocturneKeys.Down(NocturneConfig.FunSizeKey))
                FunKey(NocturneLobbyPranks.CycleScale());
            if (NocturneKeys.Down(NocturneConfig.FunMotionKey))
                FunKey(NocturneLobbyPranks.CycleSpin());
            if (NocturneKeys.Down(NocturneConfig.FunAnimKey))
                FunKey(NocturneLobbyPranks.CycleAnim());
            if (NocturneKeys.Down(NocturneConfig.FunResetKey))
                FunKey(NocturneLobbyPranks.ResetAppearance());
        }

        NocturneXmas.Tick();

        if (!NocturneMenu.Rebinding && MeetingHud.Instance != null)
        {
            if (NocturneKeys.Down(NocturneConfig.CloseVotingKey))
                NocturneToast.Push(NocturneText.T("Голосование", "Voting"), NocturneMeetingTools.CloseVoting(), 2f, NocturneNotifyKind.Info);
            if (NocturneKeys.Down(NocturneConfig.CloseMeetingKey))
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
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null || GameManager.Instance == null) return;
        try
        {
            GameManager.Instance.RpcEndGame((GameOverReason)1, false);
        }
        catch { }
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
            if (id != _apId)
            {
                _apId = id;
                _apEdge = ap.DistanceFromEdge;
            }
            Vector3 want = _apEdge;
            want.y = _apEdge.y * 0.75f;
            want.x = _apEdge.x + 0.13f;
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
        if (tracker == null || tracker.text == null)
            return;

        LowerTracker(tracker);

        int ping = 0;
        if (AmongUsClient.Instance != null) ping = ((InnerNetClient)AmongUsClient.Instance).Ping;

        string text = BuildLine(ping);

        if (text == _lastTracker)
            return;
        _lastTracker = text;

        TMP_Text t = tracker.text;
        t.richText = true;
        t.enableWordWrapping = false;
        t.alignment = TextAlignmentOptions.Center;
        t.lineSpacing = -6f;
        t.overflowMode = TextOverflowModes.Overflow;
        t.text = text;
    }

    private const int GradSteps = 32;
    private const float GradSpeed = 12f;
    private const int GradShift = 4;

    private const string Brand = "NocturneMenu";

    private static readonly string[] _hot = new string[GradSteps];
    private static readonly string[] _dim = new string[GradSteps];
    private static readonly string[] _brand = new string[GradSteps];
    private static readonly string[] _ver = new string[GradSteps];
    private static readonly string[] _by = new string[GradSteps];
    private static readonly string[] _ping = new string[GradSteps];
    private static readonly string[] _ms = new string[GradSteps];
    private static readonly string[] _fps = new string[GradSteps];
    private static readonly string[] _lob = new string[GradSteps];
    private static readonly string[] _hostLbl = new string[GradSteps];
    private static string _gradLob;
    private static readonly System.Collections.Generic.List<string> _segs = new System.Collections.Generic.List<string>(5);
    private static Color _gradKey;
    private static bool _gradReady;

    private static int Wrap(int i) => (i % GradSteps + GradSteps) % GradSteps;

    internal static string StampLine()
    {
        Gradient();
        int i = Wrap((int)(Time.unscaledTime * GradSpeed));
        return "<b>" + _brand[i] + "</b> " + _ver[i] + " " + _by[i];
    }

    private static string Tint(System.Text.StringBuilder sb, string text, int p, int at, bool hot)
    {
        string[] src = hot ? _hot : _dim;
        sb.Clear();
        for (int c = 0; c < text.Length; c++)
            sb.Append("<color=#").Append(src[Wrap(p - (at + c))]).Append('>').Append(text[c]).Append("</color>");
        return sb.ToString();
    }

    private static void Gradient()
    {
        Color a = NocturneStyle.Current.Accent;
        string lob = NocturneText.T("Лобби:", "Lobby:");
        if (_gradReady && a == _gradKey && lob == _gradLob) return;

        _gradKey = a;
        _gradLob = lob;
        _gradReady = true;
        Color.RGBToHSV(a, out float h, out float s, out float v);

        s = Mathf.Max(s, 0.72f);
        v = Mathf.Max(v, 0.88f);

        for (int i = 0; i < GradSteps; i++)
        {
            float t = i / (float)GradSteps;
            float w = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
            float spark = w * w * w * w * w * w;
            float deep = Mathf.Pow(1f - w, 3f);

            float hh = Mathf.Repeat(h + Mathf.Lerp(-0.06f, 0.06f, w), 1f);
            float ss = Mathf.Clamp01(Mathf.Lerp(Mathf.Lerp(s, 1f, deep * 0.35f), s * 0.5f, spark));
            float vv = Mathf.Clamp01(Mathf.Lerp(Mathf.Lerp(v, v * 0.78f, deep), 1f, spark));

            _hot[i] = Hex(Color.HSVToRGB(hh, ss, vv));
            _dim[i] = Hex(Color.HSVToRGB(hh, Mathf.Clamp01(ss * 0.94f), Mathf.Clamp01(vv * 0.58f)));
        }

        var sb = new System.Text.StringBuilder(64);
        for (int p = 0; p < GradSteps; p++)
        {
            _brand[p] = Tint(sb, Brand, p, 0, true);
            _ver[p] = Tint(sb, "v" + NocturnePlugin.PluginVersion, p, 13, false);
            _by[p] = Tint(sb, "by Kawasaki", p, 20, false);
            _ping[p] = Tint(sb, "PING", p, 35, false);
            _ms[p] = Tint(sb, "ms", p, 43, false);
            _fps[p] = Tint(sb, "FPS", p, 49, false);
            _lob[p] = Tint(sb, lob, p, 58, false);
            _hostLbl[p] = Tint(sb, NocturneText.T("Хост:", "Host:"), p, 68, false);
        }
    }

    private static string BuildLine(int ping)
    {
        const string Div = "  <color=#FFFFFF>•</color>  ";
        Gradient();

        int p = (int)(Time.unscaledTime * GradSpeed);
        _segs.Clear();

        int i = Wrap(p);

        _segs.Add($"<b>{_brand[i]}</b> {_ver[i]} {_by[i]}");

        _segs.Add($"{_ping[i]} <mspace=0.56em><b><color=#FFFFFF>{ping,3}</color></b></mspace> {_ms[i]}");

        if (NocturneConfig.ShowFps.Value)
            _segs.Add($"{_fps[i]} <mspace=0.56em><b><color=#FFFFFF>{CurrentFps,3}</color></b></mspace>");

        if (NocturneConfig.ShowLobbyTimer.Value && TryLobbyTimer(out int remaining))
        {
            string value = $"{remaining / 60}:{remaining % 60:00}";
            _segs.Add($"{_lob[i]} <mspace=0.56em><b><color=#FFFFFF>{value}</color></b></mspace>");
        }

        if (NocturneConfig.ShowHostLine.Value && ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
        {
            string host = HostName();
            if (host.Length > 0)
                _segs.Add($"{_hostLbl[i]} <color=#{_hot[Wrap(p - 63)]}>{host}</color>");
        }

        return "<size=82%>" + string.Join(Div, _segs) + "</size>";
    }

    private static void ToggleGameChat()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            ChatController chat = hud != null ? hud.Chat : null;
            if (chat == null) return;
            if (chat.IsOpenOrOpening)
                chat.Close();
            else
            {
                ((Component)chat).gameObject.SetActive(true);
                chat.SetVisible(true);
                chat.Toggle();
            }
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
            if (net == null)
                return _hostC = string.Empty;
            ClientData host = net.GetHost();
            string raw = host != null ? host.PlayerName : null;
            if (string.IsNullOrWhiteSpace(raw) && host != null && host.Character != null && host.Character.Data != null)
                raw = host.Character.Data.PlayerName;
            if (string.IsNullOrWhiteSpace(raw)) return _hostC = string.Empty;

            string clean = NocturneNameColor.Strip(raw).Trim();
            if (clean.Length > 16)
                clean = clean.Substring(0, 15) + "…";
            return _hostC = net.AmHost ? clean + NocturneText.T(" (Вы)", " (You)") : clean;
        }
        catch
        {
            return _hostC = string.Empty;
        }
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
        if (AmongUsClient.Instance == null)
            return;
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

[HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
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
