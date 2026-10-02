using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using UnityEngine;

namespace Nocturne;

internal sealed class RpcBatch
{
    private const byte TagData = 1;
    private const byte TagRpc = 2;
    private const byte TagGameData = 5;
    private const byte TagGameDataTo = 6;

    private const int VanillaByteCap = 1201;

    private static readonly Dictionary<byte, ushort> _seq = new Dictionary<byte, ushort>();

    private readonly InnerNetClient _net;
    private readonly MessageWriter _w;
    private readonly bool _echo;
    private readonly bool _wire;

    private int _count;
    private bool _sent;

    private RpcBatch(InnerNetClient net, int target)
    {
        _net = net;

        if (target < 0)
        {
            _echo = true;
            _wire = true;
        }
        else
        {
            bool self = target == net.ClientId;
            _echo = self;
            _wire = !self;
        }

        _w = MessageWriter.Get(SendOption.Reliable);

        if (target < 0)
        {
            _w.StartMessage(TagGameData);
            _w.Write(net.GameId);
        }
        else
        {
            _w.StartMessage(TagGameDataTo);
            _w.Write(net.GameId);
            _w.WritePacked(target);
        }
    }

    internal static RpcBatch All()
    {
        return new RpcBatch((InnerNetClient)AmongUsClient.Instance, -1);
    }

    internal static RpcBatch To(int clientId)
    {
        return new RpcBatch((InnerNetClient)AmongUsClient.Instance, clientId);
    }

    internal static RpcBatch ToOwner(PlayerControl pc)
    {
        return new RpcBatch((InnerNetClient)AmongUsClient.Instance, pc != null ? pc.OwnerId : -1);
    }

    private RpcBatch Emit(uint netId, byte call, Action<MessageWriter> body, Action mirror)
    {
        if (_echo) mirror?.Invoke();

        if (!_wire)
            return this;

        _w.StartMessage(TagRpc);
        _w.WritePacked(netId);
        _w.Write(call);
        body?.Invoke(_w);
        _w.EndMessage();
        _count++;
        return this;
    }

    internal RpcBatch Raw(uint netId, byte call, Action<MessageWriter> body)
    {
        return Emit(netId, call, body, null);
    }

    internal RpcBatch DataFlag(uint netId, MessageWriter body)
    {
        if (!_wire)
            return this;

        _w.StartMessage(TagData);
        _w.WritePacked(netId);
        _w.Write(body, false);
        _w.EndMessage();
        _count++;
        return this;
    }

    internal RpcBatch Color(PlayerControl pc, byte color)
    {
        if (pc == null) return this;

        return Emit(pc.NetId, 8,
            w =>
            {
                w.Write(((InnerNetObject)pc.Data).NetId);
                w.Write(color);
            },
            () => pc.SetColor((int)color));
    }

    internal RpcBatch Name(PlayerControl pc, string name)
    {
        if (pc == null)
            return this;

        return Emit(pc.NetId, 6,
            w =>
            {
                w.Write(pc.NetId);
                w.Write(name);
            },
            () => pc.SetName(name));
    }

    internal RpcBatch Role(PlayerControl pc, RoleTypes role, bool canOverride = false)
    {
        if (pc == null) return this;

        return Emit(pc.NetId, 44,
            w =>
            {
                w.Write((ushort)role);
                w.Write(canOverride);
            },
            () => pc.StartCoroutine(pc.CoSetRole(role, canOverride)));
    }

    internal RpcBatch Morph(PlayerControl pc, PlayerControl target, bool animate)
    {
        if (pc == null || target == null)
            return this;

        return Emit(pc.NetId, 46,
            w =>
            {
                w.WriteNetObject(target);
                w.Write(animate);
            },
            () => pc.Shapeshift(target, animate));
    }

    internal RpcBatch Vanish(PlayerControl pc)
    {
        if (pc == null)
            return this;

        return Emit(pc.NetId, 63, null,
            () =>
            {
                pc.SetRoleInvisibility(true, true, false);
                pc.HandleServerVanish();
            });
    }

    internal RpcBatch Appear(PlayerControl pc, bool animate = true)
    {
        if (pc == null) return this;

        return Emit(pc.NetId, 65, w => w.Write(animate), () => pc.HandleServerAppear(animate));
    }

    internal RpcBatch Cosmetic(PlayerControl pc, byte call, string id, byte seq, Action mirror)
    {
        if (pc == null)
            return this;

        return Emit(pc.NetId, call,
            w =>
            {
                w.Write(id);
                w.Write(seq);
            },
            mirror);
    }

    internal RpcBatch Hat(PlayerControl pc, string id, byte seq)
    {
        return Cosmetic(pc, 39, id, seq, pc != null ? (Action)(() => pc.SetHat(id, pc.Data.DefaultOutfit.ColorId)) : null);
    }

    internal RpcBatch Skin(PlayerControl pc, string id, byte seq)
    {
        return Cosmetic(pc, 40, id, seq, pc != null ? (Action)(() => pc.SetSkin(id, pc.Data.DefaultOutfit.ColorId)) : null);
    }

    internal RpcBatch Visor(PlayerControl pc, string id, byte seq)
    {
        return Cosmetic(pc, 42, id, seq, pc != null ? (Action)(() => pc.SetVisor(id, pc.Data.DefaultOutfit.ColorId)) : null);
    }

    internal RpcBatch Pet(PlayerControl pc, string id, byte seq)
    {
        return Cosmetic(pc, 41, id, seq, pc != null ? (Action)(() => pc.SetPet(id, pc.Data.DefaultOutfit.ColorId)) : null);
    }

    internal RpcBatch Murder(PlayerControl pc, PlayerControl target, MurderResultFlags flags)
    {
        if (pc == null || target == null) return this;

        return Raw(pc.NetId, 12,
            w =>
            {
                w.WritePacked(target.NetId);
                w.Write((int)flags);
            });
    }

    internal RpcBatch Snap(PlayerControl pc, Vector2 pos)
    {
        if (pc == null || pc.NetTransform == null)
            return this;

        ushort id = NextSeq(pc.PlayerId);
        return Raw(pc.NetTransform.NetId, 21,
            w =>
            {
                NetHelpers.WriteVector2(pos, w);
                w.Write(id);
            });
    }

    internal RpcBatch System(SystemTypes system, PlayerControl source, byte amount)
    {
        if (ShipStatus.Instance == null) return this;

        return Raw(((InnerNetObject)ShipStatus.Instance).NetId, 35,
            w =>
            {
                w.Write((byte)system);
                w.WriteNetObject(source);
                w.Write(amount);
            });
    }

    internal RpcBatch System(SystemTypes system, PlayerControl source, MessageWriter body)
    {
        if (ShipStatus.Instance == null) return this;

        return Raw(((InnerNetObject)ShipStatus.Instance).NetId, 35,
            w =>
            {
                w.Write((byte)system);
                w.WriteNetObject(source);
                w.Write(body, false);
            });
    }

    internal RpcBatch CloseDoors(SystemTypes door)
    {
        if (ShipStatus.Instance == null)
            return this;

        return Raw(((InnerNetObject)ShipStatus.Instance).NetId, 27, w => w.Write((byte)door));
    }

    internal RpcBatch CompleteTask(PlayerControl pc, uint index)
    {
        if (pc == null)
            return this;

        return Emit(pc.NetId, 1, w => w.WritePacked(index), () => pc.CompleteTask(index));
    }

    internal RpcBatch ChatNote(PlayerControl pc, byte forPlayer, ChatNoteTypes note)
    {
        if (pc == null) return this;

        return Raw(pc.NetId, 16,
            w =>
            {
                w.Write(forPlayer);
                w.Write((byte)note);
            });
    }

    internal bool Send()
    {
        if (_sent) return false;

        _sent = true;
        try
        {
            _w.EndMessage();

            if (_count == 0)
                return true;

            if (_w.Length > VanillaByteCap)
            {
                NocturnePlugin.Logger?.LogWarning((object)("RpcBatch " + _w.Length + "b > " + VanillaByteCap));
                return false;
            }

            int cap = _net.GetMaxMessagePackingLimit();
            if (_count > cap)
            {
                NocturnePlugin.Logger?.LogWarning((object)("RpcBatch " + _count + " msgs > " + cap));
                return false;
            }

            _net.SendOrDisconnect(_w);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _w.Recycle();
        }
    }

    private static ushort NextSeq(byte pid)
    {
        _seq.TryGetValue(pid, out ushort v);
        if (v < 10000) v = 10000;
        v++;
        _seq[pid] = v;
        return v;
    }
}
