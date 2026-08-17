using System.Collections.Generic;
using AmongUs.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nocturne;

internal static class NocturneOutfits
{
    private const char Sep = '\t';

    internal static int MaxColor()
    {
        try { return Palette.PlayerColors != null ? Mathf.Max(0, Palette.PlayerColors.Length - 1) : 11; }
        catch { return 11; }
    }

    internal static bool Usable(PlayerControl p) => p != null && p.Data != null && !p.Data.Disconnected && p.PlayerId < 100;

    internal static string Capture(PlayerControl src)
    {
        if (!Usable(src)) return string.Empty;
        try
        {
            var o = src.Data.DefaultOutfit;
            return string.Join(Sep.ToString(), new[]
            {
                Mathf.Clamp(o.ColorId, 0, MaxColor()).ToString(),
                Clean(o.HatId), Clean(o.SkinId), Clean(o.VisorId), Clean(o.NamePlateId), Clean(o.PetId),
            });
        }
        catch { return string.Empty; }
    }

    internal static bool Apply(PlayerControl target, string data)
    {
        if (!Usable(target) || string.IsNullOrWhiteSpace(data)) return false;
        string[] p = data.Split(Sep);
        if (p.Length < 6 || !int.TryParse(p[0], out int col)) return false;

        NocturneOutfitApplier.Reset(target);
        NocturneOutfitApplier.Push(target, 0, null, col);
        NocturneOutfitApplier.Push(target, 1, p[1]);
        NocturneOutfitApplier.Push(target, 2, p[2]);
        NocturneOutfitApplier.Push(target, 3, p[3]);
        NocturneOutfitApplier.Push(target, 4, p[5]);
        NocturneOutfitApplier.Push(target, 5, p[4]);
        return true;
    }

    internal static void SetColor(PlayerControl p, int col)
    {
        if (p == null) return;
        byte c = (byte)Mathf.Clamp(col, 0, MaxColor());
        bool host = false;
        try { host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
        catch { }

        try
        {
            if (host) p.RpcSetColor(c);
            else if (p.AmOwner) p.CmdCheckColor(c);
        }
        catch { }
    }

    internal static string Summary(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return string.Empty;
        string[] p = data.Split(Sep);
        if (p.Length < 6) return string.Empty;
        int n = 0;
        for (int i = 1; i < 6; i++) if (!string.IsNullOrEmpty(p[i])) n++;
        return NocturneText.T($"цвет +{n}", $"color +{n}");
    }

    private static string Clean(string v) => (v ?? string.Empty).Replace(Sep.ToString(), string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);

    internal static string RandomHat() => RandomCosmetic(0);
    internal static string RandomSkin() => RandomCosmetic(1);
    internal static string RandomVisor() => RandomCosmetic(2);

    internal static string RandomPet()
    {
        try
        {
            HatManager hm = DestroyableSingleton<HatManager>.Instance;
            if (hm == null) return string.Empty;
            var all = (Il2CppArrayBase<PetData>)(object)hm.allPets;
            int n = all.Count;
            return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
        }
        catch { return string.Empty; }
    }

    internal static string Randomize(PlayerControl target)
    {
        if (!Usable(target)) return NocturneText.T("нет игрока", "no player");

        int col = Random.Range(0, MaxColor() + 1);
        string hat = RandomHat(), skin = RandomSkin(), visor = RandomVisor(), pet = RandomPet();

        NocturneOutfitApplier.Reset(target);
        NocturneOutfitApplier.Push(target, 0, null, col);
        NocturneOutfitApplier.Push(target, 1, hat);
        NocturneOutfitApplier.Push(target, 2, skin);
        NocturneOutfitApplier.Push(target, 3, visor);
        NocturneOutfitApplier.Push(target, 4, pet);

        if (NocturneConfig.RandomOutfitSave.Value) SaveProfile(col, hat, skin, visor, pet);
        return NocturneText.T("рандом надет", "random applied");
    }

    private static void SaveProfile(int col, string hat, string skin, string visor, string pet)
    {
        try
        {
            var c = DataManager.Player.Customization;
            c.Color = (byte)col;
            c.Hat = hat;
            c.Skin = skin;
            c.Visor = visor;
            c.Pet = pet;
            ((AbstractSaveData)DataManager.Player).Save();
        }
        catch { }
    }

    private static string RandomCosmetic(int kind)
    {
        try
        {
            HatManager hm = DestroyableSingleton<HatManager>.Instance;
            if (hm == null) return string.Empty;
            switch (kind)
            {
                case 0:
                {
                    var all = (Il2CppArrayBase<HatData>)(object)hm.allHats;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
                case 1:
                {
                    var all = (Il2CppArrayBase<SkinData>)(object)hm.allSkins;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
                default:
                {
                    var all = (Il2CppArrayBase<VisorData>)(object)hm.allVisors;
                    int n = all.Count;
                    return n <= 0 ? string.Empty : ((CosmeticData)all[Random.Range(0, n)]).ProdId;
                }
            }
        }
        catch { return string.Empty; }
    }
}

public sealed class NocturneOutfitApplier : MonoBehaviour
{
    private struct Step
    {
        internal PlayerControl Pc;
        internal int Kind;
        internal string Val;
        internal int Col;
    }

    private const float StepDelay = 0.25f;

    private static readonly List<Step> _queue = new List<Step>(8);
    private static float _next;

    internal static void Reset(PlayerControl pc)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
            if (_queue[i].Pc == pc) _queue.RemoveAt(i);
    }

    internal static void Push(PlayerControl pc, int kind, string val, int col = 0)
    {
        if (pc == null) return;
        _queue.Add(new Step { Pc = pc, Kind = kind, Val = val ?? string.Empty, Col = col });
    }

    public void Update()
    {
        if (_queue.Count == 0) return;
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + StepDelay;

        Step s = _queue[0];
        _queue.RemoveAt(0);

        PlayerControl pc = s.Pc;
        if (!NocturneOutfits.Usable(pc)) return;

        try
        {
            switch (s.Kind)
            {
                case 0: NocturneOutfits.SetColor(pc, s.Col); break;
                case 1: pc.RpcSetHat(s.Val); break;
                case 2: pc.RpcSetSkin(s.Val); break;
                case 3: pc.RpcSetVisor(s.Val); break;
                case 4: pc.RpcSetPet(s.Val); break;
                case 5: pc.RpcSetNamePlate(s.Val); break;
            }
        }
        catch { }
    }
}
