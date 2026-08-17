using System.Collections.Generic;
using Hazel;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
internal static class NocturneAntiBan
{
    private static readonly Dictionary<int, float> _pending = new Dictionary<int, float>();

    private static bool Prefix(byte callId, MessageReader reader)
    {
        if (callId != 35 || reader == null) return true;

        InnerNetClient net;
        try { net = (InnerNetClient)AmongUsClient.Instance; }
        catch { return true; }
        if (net == null) return true;

        if (Inspect(reader, out PlayerControl src) != 2) return true;

        bool host = false;
        try { host = net.AmHost; } catch { }

        if (!host)
        {
            NocturneToast.Push(NocturneText.T("Анти-бан", "Anti-ban"), NocturneText.T("Заблокирован вент-кик.", "Vent-kick blocked."), 2.5f, NocturneNotifyKind.Warning);
            return false;
        }

        if (!NocturneConfig.AntiBanHost.Value) return true;

        if (src != null && src != PlayerControl.LocalPlayer)
        {
            int cid = ((InnerNetObject)src).OwnerId;
            if (cid >= 0 && cid != net.ClientId && cid != net.HostId)
                NocturneAccess.Act(net, cid, NocturneConfig.AntiBanHostAction.Value, NocturneAccess.ClientName(net, cid), NocturneText.T("вент-кик эксплойт", "vent-kick exploit"));
        }
        return false;
    }

    private static int Inspect(MessageReader reader, out PlayerControl src)
    {
        src = null;
        MessageReader r = null;
        try
        {
            r = MessageReader.Get(reader);
            if ((int)(SystemTypes)r.ReadByte() != 37) return 0;
            src = r.ReadNetObject<PlayerControl>();
            if (src == null || src == PlayerControl.LocalPlayer) return 0;
            int owner = ((InnerNetObject)src).OwnerId;
            if (owner < 0) return 0;

            ushort num = r.ReadUInt16();
            int op = r.ReadByte();
            int vid = r.ReadByte();

            if (op == 2 && num == 0 && vid == 0) { _pending[owner] = Time.realtimeSinceStartup; return 1; }
            if (op != 5 || num != 1 || vid != 0) return 0;
            if (!_pending.TryGetValue(owner, out float t)) return 0;
            _pending.Remove(owner);
            return Time.realtimeSinceStartup - t <= 1f ? 2 : 0;
        }
        catch { return 0; }
        finally { try { if (r != null) r.Recycle(); } catch { } }
    }
}
