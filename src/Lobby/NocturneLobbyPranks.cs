using System.Collections;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneLobbyPranks : MonoBehaviour
{
    internal static NocturneLobbyPranks Instance { get; private set; }

    private const float TickInterval = 1.2f;
    private const float TinyScale = 0.45f;
    private const float GiantScale = 2.0f;
    private const float SpinSpeed = 200f;
    private const float WobbleAmp = 18f;
    private const float WobbleFreq = 9f;
    private const float FloatAmp = 0.45f;
    private const float FloatFreq = 5f;
    private const float JellyAmp = 0.22f;
    private const float JellyFreq = 9f;

    private bool _rainbow;
    private bool _skinCycle;
    private bool _sync = true;
    private int _scaleMode;
    private int _spinMode;
    private int _animMode;
    private bool _rolesGranted;
    private float _effectTimer;
    private int _colorId;

    private readonly Dictionary<byte, int> _originalColors = new Dictionary<byte, int>();
    private readonly Dictionary<byte, string> _originalSkins = new Dictionary<byte, string>();
    private readonly Dictionary<byte, string> _originalHats = new Dictionary<byte, string>();
    private readonly Dictionary<byte, string> _originalVisors = new Dictionary<byte, string>();
    private readonly Dictionary<byte, RoleTypes> _savedRoles = new Dictionary<byte, RoleTypes>();
    private readonly Dictionary<byte, float> _nextChange = new Dictionary<byte, float>();
    private readonly Dictionary<byte, int> _playerColor = new Dictionary<byte, int>();
    private readonly HashSet<byte> _egged = new HashSet<byte>();

    public void Awake() => Instance = this;

    private static bool IsHost() => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    private static bool HasAnticheat() => AmongUsClient.Instance != null && (int)AmongUsClient.Instance.NetworkMode == 1;

    private static bool IsInGame() => ShipStatus.Instance != null && LobbyBehaviour.Instance == null;

    private static bool IsLocal(PlayerControl pc) =>
        PlayerControl.LocalPlayer != null && pc != null && pc.PlayerId == PlayerControl.LocalPlayer.PlayerId;

    private static bool Valid(PlayerControl pc) => pc != null && pc.Data != null && !pc.Data.Disconnected;

    public void Update()
    {
        if (_loopLeft > 0) LoopTick();

        bool live = _rainbow || _skinCycle;
        if (!live && _scaleMode == 0 && _spinMode == 0 && _animMode == 0) return;

        if (!IsInGame())
        {
            ResetScaleLocal();
            ResetRotationLocal();
            _rainbow = false;
            _skinCycle = false;
            _scaleMode = 0;
            _spinMode = 0;
            _animMode = 0;
            _rolesGranted = false;
            ClearMaps();
            return;
        }

        if (!IsHost() || PlayerControl.AllPlayerControls == null) return;

        if (_scaleMode != 0 || _spinMode != 0 || _animMode != 0)
        {
            float s = _scaleMode == 1 ? TinyScale : _scaleMode == 2 ? GiantScale : 1f;
            float t = Time.time;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc)) continue;
                bool egg = _egged.Contains(pc.PlayerId);
                try
                {
                    float bs = egg ? s : 1f;
                    float sx = bs, sy = bs;
                    if (egg && _animMode == 2)
                    {
                        float j = Mathf.Sin(t * JellyFreq) * JellyAmp;
                        sx = bs * (1f + j);
                        sy = bs * (1f - j);
                    }
                    pc.transform.localScale = new Vector3(sx, sy, 1f);

                    if (egg && _spinMode == 1)
                        pc.transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
                    else if (egg && _spinMode == 2)
                        pc.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.time * WobbleFreq) * WobbleAmp);
                    else
                        pc.transform.localEulerAngles = Vector3.zero;
                }
                catch { }
            }
        }

        if (!live) return;

        if (_sync)
        {
            _effectTimer += Time.deltaTime;
            if (_effectTimer < TickInterval) return;
            _effectTimer = 0f;

            if (_rainbow) { _colorId++; if (_colorId > 17) _colorId = 0; }

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc)) continue;
                ApplyOnce(pc, _colorId);
            }
        }
        else
        {
            float now = Time.time;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc)) continue;
                byte id = pc.PlayerId;
                if (!_nextChange.TryGetValue(id, out float due))
                {
                    _nextChange[id] = now + Random.Range(0f, TickInterval);
                    continue;
                }
                if (now < due) continue;
                _nextChange[id] = now + TickInterval;

                int col = 0;
                if (_rainbow)
                {
                    _playerColor.TryGetValue(id, out col);
                    col = (col + 1) % 18;
                    _playerColor[id] = col;
                }
                ApplyOnce(pc, col);
            }
        }
    }

    public void LateUpdate()
    {
        if (_animMode != 1) return;
        if (!IsInGame() || !IsHost() || PlayerControl.AllPlayerControls == null) return;

        float bob = Mathf.Abs(Mathf.Sin(Time.time * FloatFreq)) * FloatAmp;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc) || !_egged.Contains(pc.PlayerId)) continue;
                try
                {
                    Vector3 p = pc.transform.position;
                    p.y += bob;
                    pc.transform.position = p;
                }
                catch { }
            }
        }
        catch { }
    }

    private void ApplyOnce(PlayerControl pc, int colorId)
    {
        if (!IsLocal(pc)) { try { pc.RpcRejectShapeshift(); } catch { } }

        if (_rainbow) { try { pc.RpcSetColor((byte)colorId); } catch { } }
        if (_skinCycle)
        {
            try { pc.RpcSetHat(NocturneOutfits.RandomHat()); } catch { }
            try { pc.RpcSetSkin(NocturneOutfits.RandomSkin()); } catch { }
            try { pc.RpcSetVisor(NocturneOutfits.RandomVisor()); } catch { }
        }

        try { pc.RpcShapeshift(pc, true); } catch { }
        _egged.Add(pc.PlayerId);
    }

    private void EnsureEggSetup()
    {
        if (_rolesGranted) return;

        bool ac = HasAnticheat();
        _originalColors.Clear();
        _originalSkins.Clear();
        _originalHats.Clear();
        _originalVisors.Clear();
        _savedRoles.Clear();
        _nextChange.Clear();
        _playerColor.Clear();

        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null) continue;

                try { _originalColors[pc.PlayerId] = pc.Data.DefaultOutfit.ColorId; } catch { }
                try { _originalSkins[pc.PlayerId] = pc.Data.DefaultOutfit.SkinId; } catch { }
                try { _originalHats[pc.PlayerId] = pc.Data.DefaultOutfit.HatId; } catch { }
                try { _originalVisors[pc.PlayerId] = pc.Data.DefaultOutfit.VisorId; } catch { }
                _savedRoles[pc.PlayerId] = pc.Data.RoleType;

                if (ac && (int)pc.Data.RoleType != (int)RoleTypes.Shapeshifter)
                    NocturneForceRoles.Assign(pc, RoleTypes.Shapeshifter);
            }
        }
        catch { }

        _effectTimer = 0f;
        _rolesGranted = true;
    }

    private void TeardownEggIfIdle()
    {
        if (_rainbow || _skinCycle || !_rolesGranted) return;

        _rolesGranted = false;
        bool ac = HasAnticheat();

        if (IsHost() && PlayerControl.AllPlayerControls != null)
        {
            try
            {
                foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                {
                    if (!Valid(pc)) continue;

                    if (_originalColors.TryGetValue(pc.PlayerId, out int col)) { try { pc.RpcSetColor((byte)col); } catch { } }
                    if (_originalSkins.TryGetValue(pc.PlayerId, out string sk)) { try { pc.RpcSetSkin(sk); } catch { } }
                    if (_originalHats.TryGetValue(pc.PlayerId, out string ht)) { try { pc.RpcSetHat(ht); } catch { } }
                    if (_originalVisors.TryGetValue(pc.PlayerId, out string vs)) { try { pc.RpcSetVisor(vs); } catch { } }
                    try { pc.RpcRejectShapeshift(); } catch { }

                    if (ac && _savedRoles.TryGetValue(pc.PlayerId, out RoleTypes r))
                        NocturneForceRoles.Assign(pc, r);
                }
            }
            catch { }
        }

        ClearMaps();
    }

    private void ClearMaps()
    {
        _originalColors.Clear();
        _originalSkins.Clear();
        _originalHats.Clear();
        _originalVisors.Clear();
        _savedRoles.Clear();
        _nextChange.Clear();
        _playerColor.Clear();
        _egged.Clear();
    }

    private void ResetScaleLocal()
    {
        if (PlayerControl.AllPlayerControls == null) return;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc)) continue;
                try { pc.transform.localScale = Vector3.one; } catch { }
            }
        }
        catch { }
    }

    private void ResetRotationLocal()
    {
        if (PlayerControl.AllPlayerControls == null) return;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (!Valid(pc)) continue;
                try { pc.transform.localEulerAngles = Vector3.zero; } catch { }
            }
        }
        catch { }
    }

    internal static bool RainbowActive => Instance != null && Instance._rainbow;

    [HideFromIl2Cpp]
    internal static string ToggleRainbow()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");

        if (Instance._rainbow)
        {
            Instance._rainbow = false;
            Instance.TeardownEggIfIdle();
            return NocturneText.T("Радуга выключена.", "Rainbow off.");
        }

        Instance._rainbow = true;
        Instance.EnsureEggSetup();
        return NocturneText.T("Радуга включена.", "Rainbow on.");
    }

    internal static bool SkinCycleActive => Instance != null && Instance._skinCycle;

    [HideFromIl2Cpp]
    internal static string ToggleSkinCycle()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");

        if (Instance._skinCycle)
        {
            Instance._skinCycle = false;
            Instance.TeardownEggIfIdle();
            return NocturneText.T("Цикл косметики выключен.", "Cosmetic cycle off.");
        }

        Instance._skinCycle = true;
        Instance.EnsureEggSetup();
        return NocturneText.T("Цикл косметики включён.", "Cosmetic cycle on.");
    }

    internal static bool SyncMode => Instance == null || Instance._sync;

    [HideFromIl2Cpp]
    internal static string SyncName() => SyncMode ? NocturneText.T("Синхрон", "Sync") : NocturneText.T("Вразнобой", "Staggered");

    [HideFromIl2Cpp]
    internal static string ToggleSync()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        Instance._sync = !Instance._sync;
        Instance._nextChange.Clear();
        Instance._effectTimer = 0f;
        return Instance._sync
            ? NocturneText.T("Такт: синхрон (все разом).", "Beat: sync (all together).")
            : NocturneText.T("Такт: вразнобой (каждый в свой ритм).", "Beat: staggered (each on its own).");
    }

    internal static int ScaleMode => Instance == null ? 0 : Instance._scaleMode;

    [HideFromIl2Cpp]
    internal static string ScaleName()
    {
        int m = ScaleMode;
        return m == 1 ? NocturneText.T("Мелкие", "Tiny") : m == 2 ? NocturneText.T("Гиганты", "Giant") : NocturneText.T("Норма", "Normal");
    }

    [HideFromIl2Cpp]
    internal static string CycleScale()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");

        Instance._scaleMode = (Instance._scaleMode + 1) % 3;
        if (Instance._scaleMode == 0) Instance.ResetScaleLocal();
        return NocturneText.T("Размер: ", "Size: ") + ScaleName() + NocturneText.T(" (видно только тебе).", " (host view only).");
    }

    internal static int SpinMode => Instance == null ? 0 : Instance._spinMode;

    [HideFromIl2Cpp]
    internal static string SpinName()
    {
        int m = SpinMode;
        return m == 1 ? NocturneText.T("Вращение", "Spin") : m == 2 ? NocturneText.T("Качание", "Wobble") : NocturneText.T("Норма", "None");
    }

    [HideFromIl2Cpp]
    internal static string CycleSpin()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");

        Instance._spinMode = (Instance._spinMode + 1) % 3;
        if (Instance._spinMode == 0) Instance.ResetRotationLocal();
        return NocturneText.T("Движение: ", "Motion: ") + SpinName() + NocturneText.T(" (видно только тебе).", " (host view only).");
    }

    internal static int AnimMode => Instance == null ? 0 : Instance._animMode;

    [HideFromIl2Cpp]
    internal static string AnimName()
    {
        int m = AnimMode;
        return m == 1 ? NocturneText.T("Левитация", "Float") : m == 2 ? NocturneText.T("Желе", "Jelly") : NocturneText.T("Норма", "None");
    }

    [HideFromIl2Cpp]
    internal static string CycleAnim()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");

        Instance._animMode = (Instance._animMode + 1) % 3;
        if (Instance._animMode != 2) Instance.ResetScaleLocal();
        return NocturneText.T("Анимация: ", "Anim: ") + AnimName() + NocturneText.T(" (видно только тебе).", " (host view only).");
    }

    [HideFromIl2Cpp]
    internal static string MassMorphToEgg()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");
        Instance.StartCoroutine(Instance.MassMorph(null));
        return NocturneText.T("Все превращаются в яйца...", "Everyone is turning into an egg...");
    }

    [HideFromIl2Cpp]
    internal static string MorphAllIntoSelected()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");
        if (!IsInGame()) return NocturneText.T("Только в игре (не в лобби).", "In-game only (not in lobby).");
        PlayerControl sel = NocturneMouseTools.Selected;
        if (sel == null || sel.Data == null) return NocturneText.T("Цель не выбрана (ЛКМ по игроку).", "No target (LMB a player).");
        Instance.StartCoroutine(Instance.MassMorph(sel));
        return NocturneText.T("Все морфятся в выбранного...", "Morphing everyone into target...");
    }

    [HideFromIl2Cpp]
    private IEnumerator MassMorph(PlayerControl into)
    {
        if (!IsHost() || PlayerControl.AllPlayerControls == null) yield break;

        bool ac = HasAnticheat();
        Dictionary<byte, RoleTypes> roles = new Dictionary<byte, RoleTypes>();

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (!Valid(pc)) continue;
            roles[pc.PlayerId] = pc.Data.RoleType;
            if (ac && (int)pc.Data.RoleType != (int)RoleTypes.Shapeshifter)
                NocturneForceRoles.Assign(pc, RoleTypes.Shapeshifter);
        }

        if (ac) yield return new WaitForSeconds(0.5f);

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (!Valid(pc)) continue;
            PlayerControl target = into != null ? into : pc;
            try { pc.RpcShapeshift(target, true); } catch { }
            _egged.Add(pc.PlayerId);
        }

        if (ac) yield return new WaitForSeconds(0.5f);

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (!Valid(pc)) continue;
            if (ac && roles.TryGetValue(pc.PlayerId, out RoleTypes r))
                NocturneForceRoles.Assign(pc, r);
        }
    }

    private byte _loopId;
    private int _loopLeft;
    private float _loopAt;

    internal static int LoopLeft => Instance == null ? 0 : Instance._loopLeft;
    internal static int LoopTarget => Instance == null ? -1 : Instance._loopId;

    internal static string MurderLoop(PlayerControl target, int times)
    {
        if (Instance == null) return NocturneText.T("Недоступно.", "Unavailable.");
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return NocturneText.T("Только хост.", "Host only.");
        if (ShipStatus.Instance == null || MeetingHud.Instance != null) return NocturneText.T("Только в матче.", "In match only.");
        if (target == null || target.Data == null || target == PlayerControl.LocalPlayer) return NocturneText.T("Нет цели.", "No target.");

        if (Instance._loopLeft > 0 && Instance._loopId == target.PlayerId)
        {
            Instance._loopLeft = 0;
            return NocturneText.T("Стоп.", "Stopped.");
        }

        Instance._loopId = target.PlayerId;
        Instance._loopLeft = Mathf.Clamp(times, 1, 50);
        Instance._loopAt = 0f;
        return NocturneText.T("Гоняем: ", "Looping: ") + target.Data.PlayerName;
    }

    private void LoopTick()
    {
        if (_loopLeft <= 0) return;
        if (ShipStatus.Instance == null || MeetingHud.Instance != null) { _loopLeft = 0; return; }
        if (Time.time < _loopAt) return;
        _loopAt = Time.time + 0.4f;

        PlayerControl me = PlayerControl.LocalPlayer;
        PlayerControl t = ById(_loopId);
        if (me == null || t == null || t.Data == null || t.Data.Disconnected) { _loopLeft = 0; return; }

        _loopLeft--;
        try { me.RpcMurderPlayer(t, true); } catch { _loopLeft = 0; }
    }

    private static PlayerControl ById(byte id)
    {
        try
        {
            var e = PlayerControl.AllPlayerControls.GetEnumerator();
            while (e.MoveNext())
                if (e.Current != null && e.Current.PlayerId == id) return e.Current;
        }
        catch { }
        return null;
    }

    [HideFromIl2Cpp]
    internal static string ResetAppearance()
    {
        if (Instance == null) return NocturneText.T("Недоступно", "Unavailable");
        if (!IsHost()) return NocturneText.T("Только хост.", "Host only.");

        Instance._rainbow = false;
        Instance._skinCycle = false;
        Instance.TeardownEggIfIdle();

        Instance._scaleMode = 0;
        Instance.ResetScaleLocal();
        Instance._spinMode = 0;
        Instance.ResetRotationLocal();
        Instance._animMode = 0;
        Instance._egged.Clear();

        if (PlayerControl.AllPlayerControls != null)
        {
            try
            {
                foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                {
                    if (!Valid(pc)) continue;
                    try { pc.RpcRejectShapeshift(); } catch { }
                }
            }
            catch { }
        }

        return NocturneText.T("Облик сброшен.", "Appearance reset.");
    }
}
