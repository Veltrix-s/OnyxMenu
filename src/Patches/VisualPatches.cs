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
    private static readonly StringBuilder NameInfoBuilder = new StringBuilder(192);

    internal static float currentZoomSize = DefaultCameraSize;
    private static bool zoomTouched;
    private static float lobbyDefaultSize = DefaultCameraSize;
    private static bool wasInLobby;
    private static bool freeCameraActive;
    private static bool localMovementSuppressed;
    private static bool localMovementWasMoveable;

    internal static void UpdateHud(HudManager hud)
    {
        UpdateFreeCamera();
        UpdateCameraZoom();
        UpdateChatVisibility(hud);
    }

    internal static bool IsZoomActive() => zoomTouched && !Overlay();

    private static bool Overlay()
    {
        if (MeetingHud.Instance != null || ExileController.Instance != null) return true;
        if (Minigame.Instance != null && !(NocturneConfig.ZoomDuringTasks != null && NocturneConfig.ZoomDuringTasks.Value)) return true;
        MapBehaviour map = MapBehaviour.Instance;
        return map != null && map.isActiveAndEnabled;
    }

    internal static void ApplyNoClip()
    {
        try
        {
            if (PlayerControl.LocalPlayer == null)
            {
                return;
            }

            bool active = NocturneConfig.VisualNoClip.Value;
            ((Behaviour)PlayerControl.LocalPlayer.Collider).enabled = !active;
        }
        catch { }
    }

    internal static void ForceChatVisible(ref bool visible)
    {
        if (IsAlwaysChatEnabled())
        {
            visible = true;
        }
    }

    private static void UpdateChatVisibility(HudManager hud)
    {
        if (!IsAlwaysChatEnabled() || hud == null || hud.Chat == null)
        {
            return;
        }

        ((Component)hud.Chat).gameObject.SetActive(true);
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
            if (cur > 0.5f && cur < 30f) lobbyDefaultSize = cur;
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
                if (ui != null) ui.orthographicSize = def;
            }
            return;
        }

        if (IsChatFocused() || NocturneMenu.Opened || VanillaPanelOpen())
        {
            if (zoomTouched) PinZoom(camera);
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            if (!zoomTouched)
            {
                currentZoomSize = DefaultSize();
            }

            float next = Mathf.Max(DefaultSize(), currentZoomSize + (wheel < 0f ? ZoomStep : -ZoomStep));

            if (next <= DefaultSize() + 0.001f)
            {
                ResetCameraZoom(camera);
                return;
            }

            if (!Mathf.Approximately(next, currentZoomSize))
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

            if (_optMenu == null && scan) _optMenu = UnityEngine.Object.FindObjectOfType<OptionsMenuBehaviour>();
            if (_optMenu != null && ((Component)_optMenu).gameObject.activeInHierarchy) return true;

            if (_gameMenu == null && scan) _gameMenu = UnityEngine.Object.FindObjectOfType<GameSettingMenu>();
            if (_gameMenu != null && ((Component)_gameMenu).gameObject.activeInHierarchy) return true;

            if (_custMenu == null && scan) _custMenu = UnityEngine.Object.FindObjectOfType<PlayerCustomizationMenu>();
            if (_custMenu != null && ((Component)_custMenu).gameObject.activeInHierarchy) return true;

            if (_friendsPanel == null && scan) _friendsPanel = UnityEngine.Object.FindObjectOfType<FriendsListUI>();
            if (_friendsPanel != null && ((Component)_friendsPanel).gameObject.activeInHierarchy) return true;
        }
        catch { }
        return false;
    }

    private static void PinZoom(Camera camera)
    {
        if (camera != null) camera.orthographicSize = currentZoomSize;
        Camera ui = UICam();
        if (ui != null) ui.orthographicSize = currentZoomSize;
    }

    private static Camera UICam()
    {
        try
        {
            HudManager hud = DestroyableSingleton<HudManager>.Instance;
            return hud != null ? hud.UICamera : null;
        }
        catch { return null; }
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
        if (!zoomTouched) return;

        Camera cam = camera ?? Camera.main;
        Camera ui = UICam();
        float def = DefaultSize();
        currentZoomSize = def;
        zoomTouched = false;
        if (cam != null) cam.orthographicSize = def;
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

        Vector3 movement = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) movement.y += 1f;
        if (Input.GetKey(KeyCode.S)) movement.y -= 1f;
        if (Input.GetKey(KeyCode.A)) movement.x -= 1f;
        if (Input.GetKey(KeyCode.D)) movement.x += 1f;

        if (movement.sqrMagnitude <= 0.001f)
        {
            return;
        }

        movement.Normalize();
        float speed = Mathf.Clamp(NocturneConfig.VisualFreeCameraSpeed.Value, 4, 30);
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
        FollowerCamera follower = Camera.main != null ? ((Component)Camera.main).gameObject.GetComponent<FollowerCamera>() : null;
        if (follower == null)
        {
            return;
        }

        ((Behaviour)follower).enabled = false;
        follower.Target = null;
    }

    private static void RestoreFollowerCamera()
    {
        Camera camera = Camera.main;
        FollowerCamera follower = camera != null ? ((Component)camera).gameObject.GetComponent<FollowerCamera>() : null;
        if (follower != null)
        {
            ((Behaviour)follower).enabled = true;
            if (PlayerControl.LocalPlayer != null)
            {
                follower.SetTarget((MonoBehaviour)(object)PlayerControl.LocalPlayer);
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

    internal static void UpdatePlayerName(PlayerPhysics physics)
    {
        PlayerControl player = physics != null ? physics.myPlayer : null;
        if (player == null)
        {
            return;
        }

        ForceHnsName(player);

        bool infoNames = NocturneConfig.VisualPlayerInfoNames.Value
            || (NocturneConfig.ShowPlayerIds != null && NocturneConfig.ShowPlayerIds.Value)
            || (NocturneConfig.ShowPlayerFc != null && NocturneConfig.ShowPlayerFc.Value)
            || (NocturneConfig.ShowVotekickCount != null && NocturneConfig.ShowVotekickCount.Value);
        bool revealRoles = NocturneConfig.RevealRoles.Value;
        bool unmask = NocturneConfig.UnmaskShapeshifter != null && NocturneConfig.UnmaskShapeshifter.Value;
        if (!infoNames && !revealRoles && !unmask && !NocturneAuthor.Is(player))
        {
            RestorePlayerName(player);
            return;
        }

        try
        {
            if (player.Data == null || player.Data.Disconnected || player.CurrentOutfit == null || player.cosmetics == null)
            {
                RestorePlayerName(player);
                return;
            }

            byte playerId = player.PlayerId;
            float now = Time.unscaledTime;
            bool firstDecorate = !DecoratedPlayers.Contains(playerId);
            bool needRebuild = firstDecorate || !NextNameRebuildAt.TryGetValue(playerId, out float nextAt) || now >= nextAt;
            if (LocalColored(player) && NocturneConfig.NameColorAnimated.Value) needRebuild = true;

            if (!needRebuild && CachedNameDisplays.TryGetValue(playerId, out string cached))
            {
                Reassert(player, cached);
                return;
            }

            string baseName = player.CurrentOutfit.PlayerName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = player.Data.PlayerName;
            }

            string display = BuildNameDisplay(player, baseName, infoNames, revealRoles, unmask);
            bool unchanged = CachedNameDisplays.TryGetValue(playerId, out string prev) && string.Equals(prev, display, StringComparison.Ordinal);
            NextNameRebuildAt[playerId] = now + (unchanged ? NameStableIntervalSeconds : NameRebuildIntervalSeconds);
            CachedNameDisplays[playerId] = display;
            Reassert(player, display);
            DecoratedPlayers.Add(playerId);
        }
        catch { }
    }

    private static void Reassert(PlayerControl player, string display)
    {
        try
        {
            var nt = player.cosmetics != null ? player.cosmetics.nameText : null;
            if (nt == null || !string.Equals(nt.text, display, StringComparison.Ordinal))
            {
                player.cosmetics.SetName(display);
                float yoff = NameYOffsets.TryGetValue(player.PlayerId, out float o) ? o : (display.Contains("\n") ? NameInfoYOffset : 0f);
                MoveName(player, yoff);
            }
        }
        catch { }
    }

    internal static void ResetNameCaches()
    {
        DecoratedPlayers.Clear();
        CachedNameDisplays.Clear();
        NextNameRebuildAt.Clear();
        NameYOffsets.Clear();
    }

    private static bool LocalColored(PlayerControl p) =>
        p == PlayerControl.LocalPlayer && NocturneConfig.NameColor != null && NocturneConfig.NameColor.Value;

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
        else { result = "<size=62%><b>" + detail + "</b></size>\n" + namePart; above++; }

        if (revealRoles)
        {
            string rolePrefix = BuildRolePrefix(player);
            if (!string.IsNullOrEmpty(rolePrefix)) { result = rolePrefix + "\n" + result; above++; }
        }

        if (unmask)
        {
            string realLine = BuildUnmaskLine(player);
            if (!string.IsNullOrEmpty(realLine)) { result = result + "\n" + realLine; below++; }
        }

        if (NocturneAuthor.Is(player))
        {
            result = "<size=58%>" + NocturneAuthor.Tag + "</size>\n" + result; above++;
        }

        try { NameYOffsets[player.PlayerId] = (above - below) * NameInfoYOffset; } catch { }
        return result;
    }

    private static string BuildUnmaskLine(PlayerControl player)
    {
        try
        {
            if (player == PlayerControl.LocalPlayer || player.Data == null) return string.Empty;
            if ((int)player.CurrentOutfitType == 0) return string.Empty;
            var def = player.Data.DefaultOutfit;
            string real = def != null ? def.PlayerName : string.Empty;
            if (string.IsNullOrWhiteSpace(real)) return string.Empty;
            string shown = player.CurrentOutfit != null ? player.CurrentOutfit.PlayerName : string.Empty;
            if (string.Equals(shown, real, StringComparison.Ordinal)) return string.Empty;
            return "<size=56%><b><color=#FF6B6B>▾ " + EscapeRichText(real) + "</color></b></size>";
        }
        catch { return string.Empty; }
    }

    private static string BuildRolePrefix(PlayerControl player)
    {
        try
        {
            if (player.Data == null) return string.Empty;
            int roleId;
            string fallbackName;
            Color fallbackCol;
            if (player.Data.Role != null) { roleId = (int)player.Data.Role.Role; fallbackName = player.Data.Role.Role.ToString(); fallbackCol = player.Data.Role.TeamColor; }
            else { roleId = (int)player.Data.RoleType; fallbackName = player.Data.RoleType.ToString(); fallbackCol = Color.gray; }
            string name = RoleDisplayName(roleId, fallbackName);
            Color col = RoleColor(roleId, fallbackCol);
            string hex = ColorUtility.ToHtmlStringRGB(col);
            return "<size=58%><b><color=#" + hex + ">" + name + "</color></b></size>";
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string RoleLabelForInfo(NetworkedPlayerInfo info)
    {
        try
        {
            if (info == null) return string.Empty;
            int roleId;
            string fallbackName;
            Color fallbackCol;
            if (info.Role != null) { roleId = (int)info.Role.Role; fallbackName = info.Role.Role.ToString(); fallbackCol = info.Role.TeamColor; }
            else { roleId = (int)info.RoleType; fallbackName = info.RoleType.ToString(); fallbackCol = Color.gray; }
            string name = RoleDisplayName(roleId, fallbackName);
            Color col = RoleColor(roleId, fallbackCol);
            string hex = ColorUtility.ToHtmlStringRGB(col);
            return "<size=58%><b><color=#" + hex + ">" + name + "</color></b></size>";
        }
        catch { return string.Empty; }
    }

    internal static string RoleTag(NetworkedPlayerInfo info)
    {
        try
        {
            if (info == null) return string.Empty;
            int roleId;
            string fallbackName;
            Color fallbackCol;
            if (info.Role != null) { roleId = (int)info.Role.Role; fallbackName = info.Role.Role.ToString(); fallbackCol = info.Role.TeamColor; }
            else { roleId = (int)info.RoleType; fallbackName = info.RoleType.ToString(); fallbackCol = Color.gray; }
            string name = RoleDisplayName(roleId, fallbackName);
            Color col = RoleColor(roleId, fallbackCol);
            return "<color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + name + "</color>";
        }
        catch { return string.Empty; }
    }

    private static string RoleDisplayName(int roleId, string fallback)
    {
        switch (roleId)
        {
            case 0: return NocturneText.T("Мирный", "Crewmate");
            case 1: return NocturneText.T("Предатель", "Impostor");
            case 2: return NocturneText.T("Учёный", "Scientist");
            case 3: return NocturneText.T("Инженер", "Engineer");
            case 4: return NocturneText.T("Ангел", "Guardian Angel");
            case 5: return NocturneText.T("Оборотень", "Shapeshifter");
            case 6: return NocturneText.T("Мирный-призрак", "Crew Ghost");
            case 7: return NocturneText.T("Предатель-призрак", "Impostor Ghost");
            case 8: return NocturneText.T("Паникёр", "Noisemaker");
            case 9: return NocturneText.T("Фантом", "Phantom");
            case 10: return NocturneText.T("Трекер", "Tracker");
            case 12: return NocturneText.T("Детектив", "Detective");
            case 18: return NocturneText.T("Гадюка", "Viper");
            default: return fallback;
        }
    }

    private static Color RoleColor(int roleId, Color fallback)
    {
        switch (roleId)
        {
            case 0: return new Color(0.70f, 0.95f, 1f);
            case 1: return new Color(1f, 0.25f, 0.25f);
            case 2: return new Color(0.45f, 0.70f, 1f);
            case 3: return new Color(0.35f, 1f, 0.80f);
            case 4: return new Color(0.85f, 0.90f, 1f);
            case 5: return new Color(1f, 0.60f, 0.15f);
            case 8: return new Color(1f, 0.50f, 0.80f);
            case 9: return new Color(0.80f, 0.20f, 0.25f);
            case 10: return new Color(0.60f, 0.50f, 1f);
            case 12: return new Color(0.90f, 0.85f, 0.45f);
            case 18: return new Color(0.75f, 1f, 0.30f);
            default: return fallback;
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
        try
        {
            InnerNetClient inner = (InnerNetClient)AmongUsClient.Instance;
            if (inner != null)
            {
                client = inner.GetClientFromCharacter(player);
                hostClient = inner.GetHost();
            }
        }
        catch { }

        const string IdColor = "#9BD1FF";
        const string FcColor = "#C8B6FF";
        const string VkColor = "#FF8A8A";

        NameInfoBuilder.Clear();
        bool first = true;
        bool baseInfo = NocturneConfig.VisualPlayerInfoNames.Value;

        if (NocturneConfig.ShowPlayerIds != null && NocturneConfig.ShowPlayerIds.Value)
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

        if (NocturneConfig.ShowPlayerFc != null && NocturneConfig.ShowPlayerFc.Value)
        {
            string fc = FriendCodeOf(client);
            if (fc.Length > 0) AppendSegment(NameInfoBuilder, ref first, Separator, FcColor, fc);
        }

        if (NocturneConfig.ShowVotekickCount != null && NocturneConfig.ShowVotekickCount.Value)
        {
            int vk = VotekicksOf(client);
            if (vk > 0) AppendSegment(NameInfoBuilder, ref first, Separator, VkColor, NocturneText.T("ВК ", "VK ") + vk);
        }

        if (baseInfo && client != null && hostClient != null && client == hostClient)
        {
            AppendSegment(NameInfoBuilder, ref first, Separator, HostColor, "★ " + NocturneText.T("Хост", "Host"));
        }

        return NameInfoBuilder.ToString();
    }

    private static void ForceHnsName(PlayerControl player)
    {
        if (NocturneConfig.ForceNamesInHns == null || !NocturneConfig.ForceNamesInHns.Value) return;

        try
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsHideAndSeek()) return;
            CosmeticsLayer c = player.cosmetics;
            if (c == null || c.nameText == null) return;

            GameObject go = ((Component)c.nameText).gameObject;
            if (!go.activeSelf) go.SetActive(true);

            if (string.IsNullOrEmpty(c.nameText.text) && player.Data != null && !string.IsNullOrWhiteSpace(player.Data.PlayerName))
                c.SetName(player.Data.PlayerName);
        }
        catch { }
    }

    private static string FriendCodeOf(ClientData client)
    {
        try
        {
            string fc = client != null ? client.FriendCode : null;
            if (string.IsNullOrWhiteSpace(fc)) return string.Empty;
            fc = fc.Trim();
            return fc.Length > 24 ? fc.Substring(0, 23) + "…" : fc;
        }
        catch { return string.Empty; }
    }

    private static int VotekicksOf(ClientData client)
    {
        try { return client != null ? NocturneVoteTally.Count(client.Id) : 0; }
        catch { return 0; }
    }

    private static void AppendSegment(StringBuilder sb, ref bool first, string separator, string colorHex, string text)
    {
        if (!first) sb.Append(separator);
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

        try
        {
            string baseName = player.CurrentOutfit != null && !string.IsNullOrWhiteSpace(player.CurrentOutfit.PlayerName)
                ? player.CurrentOutfit.PlayerName
                : player.Data?.PlayerName;
            if (player.cosmetics != null && !string.IsNullOrWhiteSpace(baseName))
            {
                player.cosmetics.SetName(baseName);
            }

            MoveName(player, 0f);
        }
        catch { }
    }

    private static void MoveName(PlayerControl player, float y)
    {
        try
        {
            if (player.cosmetics != null && player.cosmetics.nameText != null)
            {
                player.cosmetics.nameText.transform.localPosition = new Vector3(0f, y, 0f);
            }
        }
        catch { }
    }

    private static string PlatformLabel(Platforms platform)
    {
        return (int)platform switch
        {
            1 => "Epic",
            2 => "Steam",
            3 => "Mac",
            4 => "MS Store",
            5 => "Itch",
            6 => "iOS",
            7 => "Android",
            8 => "Switch",
            9 => "Xbox",
            10 => "PS",
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
        try
        {
            HudManager hud = DestroyableSingleton<HudManager>.Instance;
            ChatController chat = hud != null ? hud.Chat : null;
            return chat != null && chat.IsOpenOrOpening;
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(HudManager), "Update")]
internal static class VisualAssistHudPatch
{
    public static void Postfix(HudManager __instance)
    {
        try
        {
            if (__instance == null) return;
            VisualAssist.UpdateHud(__instance);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), "SetVisible")]
internal static class VisualAssistChatVisiblePatch
{
    public static void Prefix(ref bool visible) => VisualAssist.ForceChatVisible(ref visible);
}

[HarmonyPatch(typeof(PlayerPhysics), "LateUpdate")]
internal static class VisualAssistPlayerNamePatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        VisualAssist.UpdatePlayerName(__instance);
        if (__instance.myPlayer == PlayerControl.LocalPlayer)
            VisualAssist.ApplyNoClip();
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class VisualAssistNameResetPatch
{
    public static void Postfix() => VisualAssist.ResetNameCaches();
}

[HarmonyPatch(typeof(FollowerCamera), "Update")]
internal static class FollowerCameraZoomPatch
{
    public static void Postfix(FollowerCamera __instance)
    {
        if (!VisualAssist.IsZoomActive())
            return;
        Camera camera = ((Component)__instance).GetComponent<Camera>();
        if (camera != null && camera == Camera.main)
            camera.orthographicSize = VisualAssist.currentZoomSize;
    }
}
