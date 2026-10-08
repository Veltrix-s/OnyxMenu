using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

internal static class VisualAssist
{
    private const float DefaultCameraSize = 3f;
    private const float ZoomStep = 1f;
    private const float NameInfoYOffset = 0.105f;
    private const float NameRebuildIntervalSeconds = 0.40f;
    private const float NameStableIntervalSeconds = 3.0f;

    private static readonly HashSet<byte> DecoratedPlayers = new HashSet<byte>();
    private static readonly Dictionary<byte, string> CachedNameDisplays = new Dictionary<byte, string>();
    private static readonly Dictionary<byte, float> NextNameRebuildAt = new Dictionary<byte, float>();
    private static readonly Dictionary<byte, float> NameYOffsets = new Dictionary<byte, float>();
    private static readonly Dictionary<byte, string> AppliedNames = new Dictionary<byte, string>();
    private static readonly Dictionary<byte, float> NextAssertAt = new Dictionary<byte, float>();
    private const float AssertIntervalSeconds = 0.25f;
    private static readonly StringBuilder NameInfoBuilder = new StringBuilder(192);

    internal static float currentZoomSize = DefaultCameraSize;
    private static bool zoomTouched;
    private static float lobbyDefaultSize = DefaultCameraSize;
    private static bool wasInLobby;
    private static bool freeCameraActive;
    private static bool localMovementSuppressed;
    private static bool localMovementWasMoveable;

    internal static void UpdateHud()
    {
        UpdateFreeCamera();
        UpdateCameraZoom();
        MapTilt.Tick();
    }

    internal static bool IsZoomActive() => zoomTouched && !Overlay();

    private static bool Overlay()
    {
        if (MeetingHud.Instance != null || ExileController.Instance != null)
            return true;
        if (Minigame.Instance != null && !NocturneConfig.ZoomDuringTasks.Value)
            return true;
        MapBehaviour map = MapBehaviour.Instance;
        return map != null && map.isActiveAndEnabled;
    }

    internal static void ApplyNoClip()
    {
        if (PlayerControl.LocalPlayer == null)
        {
            return;
        }

        bool active = NocturneConfig.VisualNoClip.Value;
        PlayerControl.LocalPlayer.Collider.enabled = !active;
    }

    internal static void ForceChatVisible(ref bool visible)
    {
        if (IsAlwaysChatEnabled())
        {
            visible = true;
        }
    }

    private static PassiveButton _hiddenGuide;

    internal static void TickHud()
    {
        bool shown = IsAlwaysChatEnabled() && DestroyableSingleton<HudManager>.InstanceExists;
        if (shown)
        {
            ChatController chat = DestroyableSingleton<HudManager>.Instance.Chat;
            if (chat != null)
            {
                chat.gameObject.SetActive(true);
            }
        }

        if (shown && InMatch() && MeetingHud.Instance == null)
        {
            PassiveButton guide = DestroyableSingleton<HudManager>.Instance.MatchInfoButton;
            if (guide != null && guide.gameObject.activeSelf)
            {
                guide.gameObject.SetActive(false);
                _hiddenGuide = guide;
            }
        }
        else if (_hiddenGuide != null)
        {
            _hiddenGuide.gameObject.SetActive(true);
            _hiddenGuide = null;
        }
    }

    private static void UpdateCameraZoom()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            zoomTouched = false;
            return;
        }

        bool inLobby = LobbyBehaviour.Instance != null;
        if (inLobby != wasInLobby)
        {
            wasInLobby = inLobby;
            ResetCameraZoom(camera);
        }

        if (!zoomTouched && LobbyBehaviour.Instance != null)
        {
            float cur = camera.orthographicSize;
            if (cur > 0.5f && cur < 30f)
                lobbyDefaultSize = cur;
        }

        if (!CameraZoomEnabled() || PlayerControl.LocalPlayer == null)
        {
            ResetCameraZoom(camera);
            return;
        }

        if (Overlay())
        {
            if (zoomTouched)
            {
                float def = DefaultSize();
                camera.orthographicSize = def;
                Camera ui = UICam();
                if (ui != null)
                    ui.orthographicSize = def;
            }
            return;
        }

        if (IsChatFocused() || NocturneMenu.Opened || VanillaPanelOpen())
        {
            if (zoomTouched) PinZoom(camera);
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Ui.Abs(wheel) > 0.01f)
        {
            if (!zoomTouched)
            {
                currentZoomSize = DefaultSize();
            }

            float next = Ui.Max(DefaultSize(), currentZoomSize + (wheel < 0f ? ZoomStep : -ZoomStep));

            if (next <= DefaultSize() + 0.001f)
            {
                ResetCameraZoom(camera);
                return;
            }

            if (!Ui.Approximately(next, currentZoomSize))
            {
                currentZoomSize = next;
                zoomTouched = true;
                PinZoom(camera);
                RefreshHudResolution();
            }
        }

        if (zoomTouched)
        {
            PinZoom(camera);
        }
    }

    private static OptionsMenuBehaviour _optMenu;
    private static GameSettingMenu _gameMenu;
    private static PlayerCustomizationMenu _custMenu;
    private static FriendsListUI _friendsPanel;
    private static float _panelAt = -99f;

    private static bool VanillaPanelOpen()
    {
        try
        {
            bool scan = Time.unscaledTime - _panelAt > 0.1f;
            if (scan) _panelAt = Time.unscaledTime;

            if (_optMenu == null && scan)
                _optMenu = UnityEngine.Object.FindObjectOfType<OptionsMenuBehaviour>();
            if (_optMenu != null && _optMenu.gameObject.activeInHierarchy)
                return true;

            if (_gameMenu == null && scan)
                _gameMenu = UnityEngine.Object.FindObjectOfType<GameSettingMenu>();
            if (_gameMenu != null && _gameMenu.gameObject.activeInHierarchy) return true;

            if (_custMenu == null && scan) _custMenu = UnityEngine.Object.FindObjectOfType<PlayerCustomizationMenu>();
            if (_custMenu != null && _custMenu.gameObject.activeInHierarchy) return true;

            if (_friendsPanel == null && scan)
                _friendsPanel = UnityEngine.Object.FindObjectOfType<FriendsListUI>();
            if (_friendsPanel != null && _friendsPanel.gameObject.activeInHierarchy) return true;
        }
        catch { }
        return false;
    }

    private static void PinZoom(Camera camera)
    {
        if (camera != null)
            camera.orthographicSize = currentZoomSize;
        Camera ui = UICam();
        if (ui != null)
            ui.orthographicSize = currentZoomSize;
    }

    private static Camera UICam()
    {
        HudManager hud = DestroyableSingleton<HudManager>.Instance;
        return hud != null ? hud.UICamera : null;
    }

    private static void RefreshHudResolution()
    {
        try
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w >= 320 && h >= 240)
                ResolutionManager.ResolutionChanged.Invoke((float)w / h, w, h, Screen.fullScreen);
        }
        catch { }
    }

    private static void ResetCameraZoom(Camera camera)
    {
        if (!zoomTouched)
            return;

        Camera cam = camera ?? Camera.main;
        Camera ui = UICam();
        float def = DefaultSize();
        currentZoomSize = def;
        zoomTouched = false;
        if (cam != null)
            cam.orthographicSize = def;
        if (ui != null) ui.orthographicSize = def;
        RefreshHudResolution();
    }

    private static float DefaultSize()
    {
        return LobbyBehaviour.Instance != null ? lobbyDefaultSize : DefaultCameraSize;
    }

    private static bool CameraZoomEnabled()
    {
        return NocturneConfig.VisualCameraZoom.Value;
    }

    private static void UpdateFreeCamera()
    {
        if (!FreeCameraEnabled() || !IsLobby() || PlayerControl.LocalPlayer == null || Camera.main == null)
        {
            DisableFreeCamera();
            return;
        }

        if (!freeCameraActive)
        {
            EnableFreeCamera();
        }

        UpdateLocalMovementSuppression();

        if (IsChatFocused())
        {
            return;
        }

        Vector3 movement = Ui.Zero3;
        if (Input.GetKey(KeyCode.W)) movement.y += 1f;
        if (Input.GetKey(KeyCode.S))
            movement.y -= 1f;
        if (Input.GetKey(KeyCode.A))
            movement.x -= 1f;
        if (Input.GetKey(KeyCode.D))
            movement.x += 1f;

        if (movement.sqrMagnitude <= 0.001f)
        {
            return;
        }

        movement.Normalize();
        float speed = Ui.Clamp(NocturneConfig.VisualFreeCameraSpeed.Value, 4, 30);
        Camera.main.transform.position += movement * speed * Time.deltaTime;
    }

    private static bool FreeCameraEnabled()
    {
        return NocturneConfig.VisualFreeCamera.Value;
    }

    private static void EnableFreeCamera()
    {
        try
        {
            DetachFollowerCamera();
            freeCameraActive = true;
        }
        catch { }
    }

    private static void DisableFreeCamera()
    {
        if (!freeCameraActive)
        {
            UpdateLocalMovementSuppression();
            return;
        }

        try
        {
            RestoreFollowerCamera();
        }
        catch { }
        finally
        {
            freeCameraActive = false;
            UpdateLocalMovementSuppression();
        }
    }

    private static void DetachFollowerCamera()
    {
        FollowerCamera follower = Camera.main != null ? Camera.main.gameObject.GetComponent<FollowerCamera>() : null;
        if (follower == null)
        {
            return;
        }

        follower.enabled = false;
        follower.Target = null;
    }

    private static void RestoreFollowerCamera()
    {
        Camera camera = Camera.main;
        FollowerCamera follower = camera != null ? camera.gameObject.GetComponent<FollowerCamera>() : null;
        if (follower != null)
        {
            follower.enabled = true;
            if (PlayerControl.LocalPlayer != null)
            {
                follower.SetTarget(PlayerControl.LocalPlayer);
            }
        }
    }

    private static void UpdateLocalMovementSuppression()
    {
        if (IsLobby() && PlayerControl.LocalPlayer != null && freeCameraActive)
        {
            SuppressLocalMovement();
            return;
        }

        RestoreLocalMovement();
    }

    private static void SuppressLocalMovement()
    {
        try
        {
            PlayerControl player = PlayerControl.LocalPlayer;
            if (player == null)
            {
                return;
            }

            if (!localMovementSuppressed)
            {
                localMovementWasMoveable = player.moveable;
                localMovementSuppressed = true;
            }

            player.moveable = false;
        }
        catch { }
    }

    private static void RestoreLocalMovement()
    {
        if (!localMovementSuppressed)
        {
            return;
        }

        try
        {
            PlayerControl player = PlayerControl.LocalPlayer;
            if (player != null)
            {
                player.moveable = localMovementWasMoveable;
            }
        }
        catch { }
        finally
        {
            localMovementSuppressed = false;
            localMovementWasMoveable = false;
        }
    }

    private static bool _animCfg;

    private static bool NameSettled(byte id, float now)
    {
        return AppliedNames.TryGetValue(id, out string shown)
            && CachedNameDisplays.TryGetValue(id, out string cached)
            && string.Equals(shown, cached, StringComparison.Ordinal)
            && NextAssertAt.TryGetValue(id, out float assertAt) && now < assertAt
            && NextNameRebuildAt.TryGetValue(id, out float rebuildAt) && now < rebuildAt;
    }

    internal static void UpdatePlayerName(PlayerPhysics physics)
    {
        PlayerControl player = physics.myPlayer;
        if (player == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        ForceHnsName(player, now);

        if (!OwnsName(player, now))
        {
            RestorePlayerName(player);
            return;
        }

        bool animCfg = NocturneConfig.NameColorAnimated.Value;
        if (animCfg != _animCfg)
        {
            _animCfg = animCfg;
            NextNameRebuildAt.Clear();
        }

        byte playerId = player.PlayerId;
        if (NameSettled(playerId, now))
        {
            return;
        }

        bool infoNames = InfoLine;
        bool revealRoles = NocturneConfig.RevealRoles.Value;
        bool unmask = NocturneConfig.UnmaskShapeshifter.Value;

        try
        {
            if (player.Data == null || player.Data.Disconnected || player.CurrentOutfit == null || player.cosmetics == null)
            {
                RestorePlayerName(player);
                return;
            }

            bool firstDecorate = !DecoratedPlayers.Contains(playerId);
            bool animated = LocalColored(player) && NocturneConfig.NameColorAnimated.Value;
            bool needRebuild = firstDecorate || !NextNameRebuildAt.TryGetValue(playerId, out float nextAt) || now >= nextAt
                || (animated && nextAt - now > NocturneNameColor.AnimStep);

            if (!needRebuild && CachedNameDisplays.TryGetValue(playerId, out string cached))
            {
                Reassert(player, cached, now);
                return;
            }

            string baseName = MixupNames.Reveal(player) ? player.Data.PlayerName : player.CurrentOutfit.PlayerName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = player.Data.PlayerName;
            }

            string display = BuildNameDisplay(player, baseName, infoNames, revealRoles, unmask);
            bool unchanged = CachedNameDisplays.TryGetValue(playerId, out string prev) && string.Equals(prev, display, StringComparison.Ordinal);
            NextNameRebuildAt[playerId] = now + (animated ? NocturneNameColor.AnimStep : unchanged ? NameStableIntervalSeconds : NameRebuildIntervalSeconds);
            CachedNameDisplays[playerId] = display;
            Reassert(player, display, now);
            DecoratedPlayers.Add(playerId);
        }
        catch { }
    }

    private static void Reassert(PlayerControl player, string display, float now)
    {
        byte pid = player.PlayerId;

        bool applied = AppliedNames.TryGetValue(pid, out string prev) && string.Equals(prev, display, StringComparison.Ordinal);
        if (applied && NextAssertAt.TryGetValue(pid, out float at) && now < at)
            return;

        NextAssertAt[pid] = now + AssertIntervalSeconds;

        var nt = player.cosmetics != null ? player.cosmetics.nameText : null;
        if (nt == null || !applied || !string.Equals(nt.text, display, StringComparison.Ordinal))
        {
            player.cosmetics.SetName(display);
            float yoff = NameYOffsets.TryGetValue(pid, out float o) ? o : (display.Contains("\n") ? NameInfoYOffset : 0f);
            MoveName(player, yoff);
        }
        AppliedNames[pid] = display;
    }

    internal static void ResetNameCaches()
    {
        DecoratedPlayers.Clear();
        CachedNameDisplays.Clear();
        NextNameRebuildAt.Clear();
        NameYOffsets.Clear();
        AppliedNames.Clear();
        NextAssertAt.Clear();
    }

    private static bool LocalColored(PlayerControl p) =>
        p == PlayerControl.LocalPlayer && NocturneConfig.NameColor.Value;

    private static bool InfoLine =>
        NocturneConfig.VisualPlayerInfoNames.Value
        || NocturneConfig.ShowPlayerIds.Value
        || NocturneConfig.ShowPlayerFc.Value
        || NocturneConfig.ShowVotekickCount.Value;

    internal static bool OwnsName(PlayerControl p, float now) =>
        InfoLine || NocturneConfig.RevealRoles.Value || NocturneConfig.UnmaskShapeshifter.Value || NocturneAuthor.Is(p, now);

    private static string BuildNameDisplay(PlayerControl player, string baseName, bool infoNames, bool revealRoles, bool unmask)
    {
        int above = 0, below = 0;
        string detail = infoNames ? BuildInfoLine(player) : string.Empty;
        string namePart = LocalColored(player)
            ? NocturneNameColor.Apply(baseName, NocturneConfig.NameColorStyle.Value, NocturneConfig.NameColorAnimated.Value)
            : EscapeRichText(baseName);
        string result;
        if (string.IsNullOrEmpty(detail))
            result = namePart;
        else
        {
            result = "<size=62%><b>" + detail + "</b></size>\n" + namePart;
            above++;
        }

        if (revealRoles)
        {
            string rolePrefix = BuildRolePrefix(player);
            if (!string.IsNullOrEmpty(rolePrefix))
            {
                result = rolePrefix + "\n" + result;
                above++;
            }
        }

        if (unmask)
        {
            string realLine = BuildUnmaskLine(player);
            if (!string.IsNullOrEmpty(realLine))
            {
                result = result + "\n" + realLine;
                below++;
            }
        }

        if (NocturneAuthor.Is(player))
        {
            result = "<size=58%>" + NocturneAuthor.Tag + "</size>\n" + result;
            above++;
        }

        NameYOffsets[player.PlayerId] = (above - below) * NameInfoYOffset;
        return result;
    }

    private static string BuildUnmaskLine(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer || player.Data == null)
            return string.Empty;
        if (player.CurrentOutfitType == PlayerOutfitType.Default || MixupNames.Reveal(player))
            return string.Empty;
        var def = player.Data.DefaultOutfit;
        string real = def != null ? def.PlayerName : string.Empty;
        if (string.IsNullOrWhiteSpace(real)) return string.Empty;
        string shown = player.CurrentOutfit != null ? player.CurrentOutfit.PlayerName : string.Empty;
        if (string.Equals(shown, real, StringComparison.Ordinal))
            return string.Empty;
        return "<size=56%><b><color=#FF6B6B>▾ " + EscapeRichText(real) + "</color></b></size>";
    }

    private static string BuildRolePrefix(PlayerControl player)
    {
        if (player.Data == null)
            return string.Empty;
        int roleId;
        string fallbackName;
        Color fallbackCol;
        if (player.Data.Role != null)
        {
            roleId = (int)player.Data.Role.Role;
            fallbackName = player.Data.Role.Role.ToString();
            fallbackCol = player.Data.Role.TeamColor;
        }
        else
        {
            roleId = (int)player.Data.RoleType;
            fallbackName = player.Data.RoleType.ToString();
            fallbackCol = Ui.Gray;
        }
        string name = RoleDisplayName(roleId, fallbackName);
        Color col = RoleColor(roleId, fallbackCol);
        string hex = ColorUtility.ToHtmlStringRGB(col);
        return "<size=58%><b><color=#" + hex + ">" + name + "</color></b></size>";
    }

    internal static string RoleLabelForInfo(NetworkedPlayerInfo info)
    {
        if (info == null)
            return string.Empty;
        int roleId;
        string fallbackName;
        Color fallbackCol;
        if (info.Role != null)
        {
            roleId = (int)info.Role.Role;
            fallbackName = info.Role.Role.ToString();
            fallbackCol = info.Role.TeamColor;
        }
        else
        {
            roleId = (int)info.RoleType;
            fallbackName = info.RoleType.ToString();
            fallbackCol = Ui.Gray;
        }
        string name = RoleDisplayName(roleId, fallbackName);
        Color col = RoleColor(roleId, fallbackCol);
        string hex = ColorUtility.ToHtmlStringRGB(col);
        return "<size=58%><b><color=#" + hex + ">" + name + "</color></b></size>";
    }

    internal static string RoleTag(NetworkedPlayerInfo info)
    {
        if (info == null)
            return string.Empty;
        int roleId;
        string fallbackName;
        Color fallbackCol;
        if (info.Role != null)
        {
            roleId = (int)info.Role.Role;
            fallbackName = info.Role.Role.ToString();
            fallbackCol = info.Role.TeamColor;
        }
        else
        {
            roleId = (int)info.RoleType;
            fallbackName = info.RoleType.ToString();
            fallbackCol = Ui.Gray;
        }
        string name = RoleDisplayName(roleId, fallbackName);
        Color col = RoleColor(roleId, fallbackCol);
        return "<color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + name + "</color>";
    }

    private static string RoleDisplayName(int roleId, string fallback)
    {
        switch (roleId)
        {
            case 0:
                return NocturneText.T("Мирный", "Crewmate");
            case 1:
                return NocturneText.T("Предатель", "Impostor");
            case 2:
                return NocturneText.T("Учёный", "Scientist");
            case 3:
                return NocturneText.T("Инженер", "Engineer");
            case 4:
                return NocturneText.T("Ангел", "Guardian Angel");
            case 5:
                return NocturneText.T("Оборотень", "Shapeshifter");
            case 6:
                return NocturneText.T("Мирный-призрак", "Crew Ghost");
            case 7:
                return NocturneText.T("Предатель-призрак", "Impostor Ghost");
            case 8:
                return NocturneText.T("Паникёр", "Noisemaker");
            case 9:
                return NocturneText.T("Фантом", "Phantom");
            case 10:
                return NocturneText.T("Трекер", "Tracker");
            case 12:
                return NocturneText.T("Детектив", "Detective");
            case 18:
                return NocturneText.T("Гадюка", "Viper");
            case 19:
                return NocturneText.T("Судья", "Judge");
            case 21:
                return NocturneText.T("Инфлюэнсер", "Spirit Guide");
            default:
                return fallback;
        }
    }

    private static Color RoleColor(int roleId, Color fallback)
    {
        switch (roleId)
        {
            case 0:
                return Ui.C(0.70f, 0.95f, 1f, 1f);
            case 1:
                return Ui.C(1f, 0.25f, 0.25f, 1f);
            case 2:
                return Ui.C(0.45f, 0.70f, 1f, 1f);
            case 3:
                return Ui.C(0.35f, 1f, 0.80f, 1f);
            case 4:
                return Ui.C(0.85f, 0.90f, 1f, 1f);
            case 5:
                return Ui.C(1f, 0.60f, 0.15f, 1f);
            case 8:
                return Ui.C(1f, 0.50f, 0.80f, 1f);
            case 9:
                return Ui.C(0.80f, 0.20f, 0.25f, 1f);
            case 10:
                return Ui.C(0.60f, 0.50f, 1f, 1f);
            case 12:
                return Ui.C(0.90f, 0.85f, 0.45f, 1f);
            case 18:
                return Ui.C(0.75f, 1f, 0.30f, 1f);
            case 19:
                return Ui.C(0.35f, 0.85f, 0.95f, 1f);
            case 21:
                return Ui.C(0.75f, 0.70f, 1f, 1f);
            default:
                return fallback;
        }
    }

    private static string BuildInfoLine(PlayerControl player)
    {
        const string LevelColor = "#FFD166";
        const string PlatformColor = "#B5DBFF";
        const string HostColor = "#FFB347";
        const string Separator = "<color=#5A6378> · </color>";

        ClientData client = null;
        ClientData hostClient = null;
        InnerNetClient inner = AmongUsClient.Instance;
        if (inner != null)
        {
            client = inner.GetClientFromCharacter(player);
            hostClient = inner.GetHost();
        }

        const string IdColor = "#9BD1FF";
        const string FcColor = "#C8B6FF";
        const string VkColor = "#FF8A8A";

        NameInfoBuilder.Clear();
        bool first = true;
        bool baseInfo = NocturneConfig.VisualPlayerInfoNames.Value;

        if (NocturneConfig.ShowPlayerIds.Value)
            AppendSegment(NameInfoBuilder, ref first, Separator, IdColor, "#" + player.PlayerId);

        if (baseInfo)
            AppendSegment(NameInfoBuilder, ref first, Separator, LevelColor, "★ " + NocturneJoinLevels.Display(player));

        if (baseInfo && client != null && client.PlatformData != null)
        {
            string platform = PlatformLabel(client.PlatformData.Platform);
            if (!string.IsNullOrWhiteSpace(platform))
            {
                AppendSegment(NameInfoBuilder, ref first, Separator, PlatformColor, platform);
            }
        }

        if (NocturneConfig.ShowPlayerFc.Value)
        {
            string fc = FriendCodeOf(client);
            if (fc.Length > 0)
                AppendSegment(NameInfoBuilder, ref first, Separator, FcColor, fc);
        }

        if (NocturneConfig.ShowVotekickCount.Value)
        {
            int vk = VotekicksOf(client);
            if (vk > 0)
                AppendSegment(NameInfoBuilder, ref first, Separator, VkColor, NocturneText.T("ВК ", "VK ") + vk);
        }

        if (baseInfo && client != null && hostClient != null && client == hostClient)
        {
            AppendSegment(NameInfoBuilder, ref first, Separator, HostColor, "★ " + NocturneText.T("Хост", "Host"));
        }

        return NameInfoBuilder.ToString();
    }

    private static float _hnsAt = -1f;
    private static bool _hns;

    private static void ForceHnsName(PlayerControl player, float now)
    {
        if (!NocturneConfig.ForceNamesInHns.Value)
            return;

        try
        {
            if (now >= _hnsAt)
            {
                _hnsAt = now + 0.25f;
                _hns = GameManager.Instance != null && GameManager.Instance.IsHideAndSeek();
            }
            if (!_hns)
                return;
            CosmeticsLayer c = player.cosmetics;
            if (c == null || c.nameText == null) return;

            GameObject go = c.nameText.gameObject;
            PlayerControl me = PlayerControl.LocalPlayer;
            bool ghost = player != me && player.Data != null && player.Data.IsDead
                && me != null && me.Data != null && !me.Data.IsDead
                && !NocturneConfig.SeeGhosts.Value;
            if (ghost)
            {
                if (go.activeSelf)
                    go.SetActive(false);
                return;
            }

            if (!go.activeSelf)
                go.SetActive(true);

            if (string.IsNullOrEmpty(c.nameText.text) && player.Data != null && !string.IsNullOrWhiteSpace(player.Data.PlayerName))
                c.SetName(player.Data.PlayerName);
        }
        catch { }
    }

    private static string FriendCodeOf(ClientData client)
    {
        string fc = client != null ? client.FriendCode : null;
        if (string.IsNullOrWhiteSpace(fc)) return string.Empty;
        fc = fc.Trim();
        return fc.Length > 24 ? fc.Substring(0, 23) + "…" : fc;
    }

    private static int VotekicksOf(ClientData client)
    {
        return client != null ? NocturneVoteTally.Count(client.Id) : 0;
    }

    private static void AppendSegment(StringBuilder sb, ref bool first, string separator, string colorHex, string text)
    {
        if (!first)
            sb.Append(separator);
        sb.Append("<color=").Append(colorHex).Append('>').Append(text).Append("</color>");
        first = false;
    }

    private static void RestorePlayerName(PlayerControl player)
    {
        if (player == null || !DecoratedPlayers.Remove(player.PlayerId))
        {
            return;
        }

        CachedNameDisplays.Remove(player.PlayerId);
        NextNameRebuildAt.Remove(player.PlayerId);
        NameYOffsets.Remove(player.PlayerId);
        AppliedNames.Remove(player.PlayerId);
        NextAssertAt.Remove(player.PlayerId);

        try
        {
            string baseName = !MixupNames.Reveal(player) && player.CurrentOutfit != null && !string.IsNullOrWhiteSpace(player.CurrentOutfit.PlayerName)
                ? player.CurrentOutfit.PlayerName
                : player.Data?.PlayerName;
            if (player.cosmetics != null && !string.IsNullOrWhiteSpace(baseName))
            {
                player.cosmetics.SetName(LocalColored(player)
                    ? NocturneNameColor.Apply(baseName, NocturneConfig.NameColorStyle.Value, NocturneConfig.NameColorAnimated.Value)
                    : baseName);
            }

            MoveName(player, 0f);
        }
        catch { }
    }

    private static void MoveName(PlayerControl player, float y)
    {
        if (player.cosmetics != null && player.cosmetics.nameText != null)
        {
            player.cosmetics.nameText.transform.localPosition = Ui.V3(0f, y, 0f);
        }
    }

    private static string PlatformLabel(Platforms platform)
    {
        return platform switch
        {
            Platforms.StandaloneEpicPC => "Epic",
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneMac => "Mac",
            Platforms.StandaloneWin10 => "MS Store",
            Platforms.StandaloneItch => "Itch",
            Platforms.IPhone => "iOS",
            Platforms.Android => "Android",
            Platforms.Switch => "Switch",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "PS",
            _ => string.Empty,
        };
    }

    private static string EscapeRichText(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static bool IsAlwaysChatEnabled()
    {
        return NocturneConfig.VisualAlwaysShowChat.Value;
    }

    private static bool IsLobby()
    {
        return LobbyBehaviour.Instance != null;
    }

    internal static bool InMatch()
    {
        return ShipStatus.Instance != null && LobbyBehaviour.Instance == null;
    }

    private static bool IsChatFocused()
    {
        HudManager hud = DestroyableSingleton<HudManager>.Instance;
        ChatController chat = hud != null ? hud.Chat : null;
        return chat != null && chat.IsOpenOrOpening;
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class VisualAssistHudPatch
{
    public static void Postfix()
    {
        try
        {
            VisualAssist.UpdateHud();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SetVisible))]
internal static class VisualAssistChatVisiblePatch
{
    public static void Prefix(ref bool visible) => VisualAssist.ForceChatVisible(ref visible);
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
internal static class VisualAssistPlayerNamePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        VisualAssist.UpdatePlayerName(__instance);
        if (__instance.myPlayer == PlayerControl.LocalPlayer)
        {
            VisualAssist.ApplyNoClip();
        }
        else if (MapTilt.Applied)
        {
            MapTilt.PlaceOther(__instance.myPlayer);
        }
    }
}

[HarmonyPatch(typeof(FollowerCamera), nameof(FollowerCamera.Update))]
internal static class FollowerCameraZoomPatch
{
    public static void Postfix(FollowerCamera __instance)
    {
        if (!VisualAssist.IsZoomActive())
            return;
        Camera camera = __instance.GetComponent<Camera>();
        if (camera != null && camera == Camera.main) camera.orthographicSize = VisualAssist.currentZoomSize;
    }
}
