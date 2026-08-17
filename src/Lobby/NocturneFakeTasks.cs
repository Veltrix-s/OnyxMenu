using UnityEngine;

namespace Nocturne;

public sealed class NocturneFakeTasks : MonoBehaviour
{
    private bool _scan;
    private bool _cams;

    public void Update()
    {
        if (!InMatch())
        {
            if (_scan) { Scan(false); _scan = false; }
            _cams = false;
            return;
        }

        bool scanWant = NocturneConfig.FakeScan.Value;
        if (scanWant != _scan) { Scan(scanWant); _scan = scanWant; }

        bool camsWant = NocturneConfig.FakeCams.Value && HasCams();
        if (camsWant != _cams) { Cams(camsWant); _cams = camsWant; }
    }

    internal static void Shields() => Anim(1);
    internal static void Asteroids() => Anim(6);
    internal static void Garbage() => Anim(10);

    internal static bool HasCams()
    {
        int m = NocturneNav.CurrentMapId();
        return m == 0 || m == 2 || m == 4;
    }

    private static bool InMatch() =>
        ShipStatus.Instance != null && PlayerControl.LocalPlayer != null && MeetingHud.Instance == null;

    private static void Anim(byte t)
    {
        try { if (InMatch()) PlayerControl.LocalPlayer.RpcPlayAnimation(t); }
        catch { }
    }

    private static void Scan(bool on)
    {
        try { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.RpcSetScanner(on); }
        catch { }
    }

    private static void Cams(bool on)
    {
        try { if (ShipStatus.Instance != null) ShipStatus.Instance.RpcUpdateSystem((SystemTypes)11, (byte)(on ? 1 : 0)); }
        catch { }
    }
}
