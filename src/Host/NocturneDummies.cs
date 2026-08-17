using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneDummies : MonoBehaviour
{
    private const int Cap = 15;

    private sealed class Dummy
    {
        public PlayerControl Pc;
        public string Name;
        public byte Color;
        public string Hat, Skin, Visor;
    }

    private static readonly Dictionary<byte, Dummy> _bots = new Dictionary<byte, Dummy>();
    private static readonly List<(byte pid, float at)> _pending = new List<(byte, float)>();
    private byte _next = 100;
    private float _resync;

    internal static NocturneDummies Instance { get; private set; }

    public void Awake() => Instance = this;
    public void OnDestroy() { if (Instance == this) Instance = null; }

    internal static bool IsDummy(byte pid) => _bots.ContainsKey(pid);

    internal static IEnumerable<PlayerControl> Live()
    {
        foreach (var kv in _bots)
        {
            PlayerControl pc = kv.Value?.Pc;
            if (pc != null && pc.Data != null && !pc.Data.IsDead) yield return pc;
        }
    }

    internal static void Forget() { _bots.Clear(); _pending.Clear(); NocturneDummyAI.Reset(); NocturneDummyChat.Reset(); }

    private static readonly List<PlayerControl> _scratch = new List<PlayerControl>();

    public void FixedUpdate()
    {
        if (_bots.Count == 0) return;
        _scratch.Clear();
        foreach (var kv in _bots) if (kv.Value != null && kv.Value.Pc != null) _scratch.Add(kv.Value.Pc);
        try { NocturneDummyAI.Tick(_scratch); } catch { }
        try { NocturneDummyChat.TickVision(); } catch { }
        try { NocturneDummyChat.TickMeeting(); } catch { }
    }

    public void Update()
    {
        if (NocturneConfig.DummyEnabled.Value && !NocturneMenu.Rebinding
            && NocturneConfig.DummyKey.Value != KeyCode.None && Input.GetKeyDown(NocturneConfig.DummyKey.Value))
            SpawnNow();

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime < _pending[i].at) continue;
            try { NocturneForceRoles.ForceNow(_pending[i].pid); } catch { }
            _pending.RemoveAt(i);
        }

        if (_bots.Count > 0 && Time.unscaledTime >= _resync)
        {
            _resync = Time.unscaledTime + 2.5f;
            try { Restamp(); } catch { }
        }
    }

    internal static string SpawnNow()
    {
        if (Instance == null) return NocturneText.T("Недоступно.", "Unavailable.");
        return Instance.Spawn();
    }

    private static bool CanSpawn()
    {
        try
        {
            return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
                && LobbyBehaviour.Instance != null && PlayerControl.LocalPlayer != null && GameData.Instance != null;
        }
        catch { return false; }
    }

    private string Spawn()
    {
        if (!NocturneConfig.DummyEnabled.Value) return NocturneText.T("Манекены выключены.", "Dummies are off.");
        if (!CanSpawn()) return NocturneText.T("Только хост в лобби.", "Host in lobby only.");

        try
        {
            if (PlayerControl.AllPlayerControls.Count >= Cap) return NocturneText.T("Лобби заполнено.", "Lobby is full.");

            AmongUsClient client = AmongUsClient.Instance;
            PlayerControl prefab = client.PlayerPrefab;
            if (prefab == null) return NocturneText.T("Нет префаба.", "No prefab.");

            Vector3 pos = PlayerControl.LocalPlayer.transform.position + Vector3.right * 1.5f;
            byte color = (byte)Random.Range(0, Palette.PlayerColors.Length);
            string name = PickName();

            PlayerControl dm = Object.Instantiate(prefab, pos, Quaternion.identity);
            dm.PlayerId = _next;
            GameData.Instance.AddPlayerInfo(GameData.Instance.AddDummy(dm));
            ((InnerNetClient)client).Spawn((InnerNetObject)(object)dm, -2, (SpawnFlags)1);
            ((Behaviour)dm.NetTransform).enabled = true;

            string hat = RandomCosmo(0), skin = RandomCosmo(1), visor = RandomCosmo(2);
            dm.RpcSetColor(color);
            SetName(dm, name);
            dm.RpcSetHat(hat);
            dm.RpcSetSkin(skin);
            dm.RpcSetVisor(visor);
            dm.RpcSetPet(string.Empty);
            dm.RpcSetLevel((uint)Random.Range(1, 200));
            dm.RpcSetNamePlate(string.Empty);

            NocturneForceRoles.Assign(dm, RoleTypes.Crewmate, true);
            NocturneForceRoles.RegisterDefault(dm.PlayerId, RoleTypes.Crewmate);

            _bots[_next] = new Dummy { Pc = dm, Name = name, Color = color, Hat = hat, Skin = skin, Visor = visor };
            _pending.Add((dm.PlayerId, Time.unscaledTime + 3f));
            if (++_next > 200) _next = 100;
            return NocturneText.T("Создан: ", "Spawned: ") + name;
        }
        catch (System.Exception e)
        {
            NocturnePlugin.Logger?.LogWarning((object)("Dummy spawn failed: " + e));
            return NocturneText.T("Ошибка спавна.", "Spawn failed.");
        }
    }

    private static void SetName(PlayerControl pc, string name)
    {
        AmongUsClient client = AmongUsClient.Instance;
        if (client != null)
        {
            foreach (PlayerControl other in PlayerControl.AllPlayerControls)
            {
                if (other == null || other == PlayerControl.LocalPlayer) continue;
                try
                {
                    int cid = ((InnerNetClient)client).GetClientIdFromCharacter(other);
                    MessageWriter w = ((InnerNetClient)client).StartRpcImmediately(((InnerNetObject)(object)pc).NetId, 6, (SendOption)0, cid);
                    w.Write(((InnerNetObject)(object)pc).NetId);
                    w.Write(name);
                    ((InnerNetClient)client).FinishRpcImmediately(w);
                }
                catch { }
            }
        }
        try { pc.SetName(name); } catch { }
        try { if (pc.cosmetics != null && pc.cosmetics.nameText != null) ((TMP_Text)pc.cosmetics.nameText).text = name; } catch { }
    }

    private static string RandomCosmo(int kind)
    {
        try
        {
            HatManager hm = DestroyableSingleton<HatManager>.Instance;
            if (hm == null) return string.Empty;
            if (kind == 0) { var a = hm.allHats; return a.Length > 0 ? a[Random.Range(0, a.Length)].ProdId : string.Empty; }
            if (kind == 1) { var a = hm.allSkins; return a.Length > 0 ? a[Random.Range(0, a.Length)].ProdId : string.Empty; }
            var v = hm.allVisors; return v.Length > 0 ? v[Random.Range(0, v.Length)].ProdId : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static readonly string[] Ru =
    {
        "Мурзик", "Тучка", "Пельмень", "Барсик", "Ромашка", "Ёжик", "Зефирка", "Кекс",
        "Бублик", "Снежок", "Огонёк", "Василёк", "Пушок", "Ягодка", "Лучик", "Комета",
    };

    private static string PickName()
    {
        if (NocturneText.IsRussian) return Ru[Random.Range(0, Ru.Length)];
        const string abc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var c = new char[6];
        for (int i = 0; i < c.Length; i++) c[i] = abc[Random.Range(0, abc.Length)];
        return new string(c);
    }

    private static void Restamp()
    {
        bool host = false;
        try { host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; } catch { }
        foreach (var kv in _bots)
        {
            Dummy d = kv.Value;
            if (d?.Pc == null) continue;
            if (host)
            {
                try { d.Pc.RpcSetColor(d.Color); } catch { }
                SetName(d.Pc, d.Name);
                try { d.Pc.RpcSetHat(d.Hat); } catch { }
                try { d.Pc.RpcSetSkin(d.Skin); } catch { }
                try { d.Pc.RpcSetVisor(d.Visor); } catch { }
            }
            else
            {
                try { if (d.Pc.cosmetics != null && d.Pc.cosmetics.nameText != null) ((TMP_Text)d.Pc.cosmetics.nameText).text = d.Name; } catch { }
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
internal static class NocturneDummyKillPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null || !NocturneDummies.IsDummy(target.PlayerId)) return true;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
            if (target.Data.IsDead) return false;
            __instance.RpcMurderPlayer(target, true);
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NocturneDummyResetPatch
{
    public static void Postfix() => NocturneDummies.Forget();
}
