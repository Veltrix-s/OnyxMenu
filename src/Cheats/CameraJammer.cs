using System.Collections.Generic;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace Nocturne;

public sealed class CameraJammer : MonoBehaviour
{
    private const float Interval = 0.5f;
    private const byte SecuritySystem = 11;
    private const byte CommsSystem = 14;

    private static readonly HashSet<byte> _cur = new HashSet<byte>();
    private static readonly HashSet<byte> _prev = new HashSet<byte>();

    private float _next;

    public void Update()
    {
        if (Time.unscaledTime < _next)
            return;

        _next = Time.unscaledTime + Interval;

        InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
        bool active = NocturneConfig.CameraJam.Value
            && net != null && net.AmHost
            && ShipStatus.Instance != null && MeetingHud.Instance == null;

        if (!active)
        {
            FixAll();
            return;
        }

        SecurityCameraSystemType cams = Cameras();
        if (cams == null || !CommsSupported())
            return;

        _cur.Clear();

        var it = cams.PlayersUsing.GetEnumerator();
        while (it.MoveNext())
        {
            byte pid = it.Current;
            PlayerControl pc = Utils.ById(pid);
            if (pc == null)
                continue;
            if (pc == PlayerControl.LocalPlayer || pc.OwnerId == net.HostId)
                continue;
            _cur.Add(pid);
        }

        foreach (byte pid in _cur)
            if (!_prev.Contains(pid))
                SetComms(Utils.ById(pid), true);

        foreach (byte pid in _prev)
            if (!_cur.Contains(pid)) SetComms(Utils.ById(pid), false);

        _prev.Clear();
        foreach (byte pid in _cur)
            _prev.Add(pid);
    }

    private static void FixAll()
    {
        if (_prev.Count == 0)
            return;

        foreach (byte pid in _prev)
            SetComms(Utils.ById(pid), false);

        _prev.Clear();
        _cur.Clear();
    }

    private static void SetComms(PlayerControl pc, bool broken)
    {
        if (pc == null || ShipStatus.Instance == null) return;

        MessageWriter body = MessageWriter.Get(SendOption.Reliable);
        try
        {
            body.StartMessage(CommsSystem);
            body.Write((byte)(broken ? 1 : 0));
            body.EndMessage();

            RpcBatch.ToOwner(pc)
                .DataFlag(((InnerNetObject)ShipStatus.Instance).NetId, body)
                .Send();
        }
        finally
        {
            body.Recycle();
        }
    }

    private static SecurityCameraSystemType Cameras()
    {
        try
        {
            ISystemType sys = ShipStatus.Instance.Systems[(SystemTypes)SecuritySystem];
            return sys != null ? ((Il2CppObjectBase)sys).TryCast<SecurityCameraSystemType>() : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool CommsSupported()
    {
        try
        {
            ISystemType sys = ShipStatus.Instance.Systems[(SystemTypes)CommsSystem];
            return sys != null && ((Il2CppObjectBase)sys).TryCast<HudOverrideSystemType>() != null;
        }
        catch
        {
            return false;
        }
    }
}
