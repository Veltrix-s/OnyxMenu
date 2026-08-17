using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneSabotage : MonoBehaviour
{
    private float _next;

    public void Update()
    {
        if (ShipStatus.Instance == null) return;
        NocturneDoors.KeepOpen = NocturneConfig.DoorKeepOpen.Value;
        NocturneDoors.Tick();

        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.6f;

        int m = Map();
        if (NocturneConfig.SabAutoLights.Value && m != 5) Lights();
        if (NocturneConfig.SabInfMushroom.Value && m == 5) Mush();
        if (NocturneConfig.SabSpamReactor.Value) Main();
        if (NocturneConfig.SabAutoFix.Value && !Mine() && AnyActive()) Fix();
    }

    private static readonly SystemTypes[] Watch =
    {
        SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory,
        SystemTypes.HeliSabotage, SystemTypes.LifeSupp, SystemTypes.Comms,
        SystemTypes.MushroomMixupSabotage
    };

    private static bool AnyActive()
    {
        ShipStatus ss = ShipStatus.Instance;
        if (ss == null || ss.Systems == null) return false;
        for (int i = 0; i < Watch.Length; i++)
        {
            try
            {
                if (!ss.Systems.ContainsKey(Watch[i])) continue;
                IActivatable a = ss.Systems[Watch[i]].TryCast<IActivatable>();
                if (a != null && a.IsActive) return true;
            }
            catch { }
        }
        return false;
    }

    private static bool Mine()
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        try { return me != null && me.Data != null && me.Data.Role != null && me.Data.Role.IsImpostor; }
        catch { return false; }
    }

    public static void Lights()
    {
        ShipStatus ss = ShipStatus.Instance;
        if (ss == null) return;
        try
        {
            SwitchSystem sys = ss.Systems[SystemTypes.Electrical].TryCast<SwitchSystem>();
            if (sys == null) return;
            int a = sys.ActualSwitches, e = sys.ExpectedSwitches;
            for (byte i = 0; i < 5; i++)
                if (((a >> i) & 1) == ((e >> i) & 1))
                    Up(SystemTypes.Electrical, i);
        }
        catch { }
    }

    public static void Main()
    {
        switch (Map())
        {
            case 2: Up(SystemTypes.Laboratory, 128); break;
            case 4: Up(SystemTypes.HeliSabotage, 128); break;
            default: Up(SystemTypes.Reactor, 128); break;
        }
    }

    public static void Oxygen() => Up(SystemTypes.LifeSupp, 128);
    public static void Comms() => Up(SystemTypes.Comms, 128);
    public static void Mush() => Up(SystemTypes.MushroomMixupSabotage, 1);

    public static void All()
    {
        int m = Map();
        if (m != 5) Lights();
        Main();
        if (m == 0 || m == 1 || m == 3) Oxygen();
        if (m == 5) Mush();
        Comms();
    }

    public static void Fix()
    {
        if (ShipStatus.Instance == null) return;
        FixLights();
        FixConsoles(SystemTypes.Reactor);
        FixConsoles(SystemTypes.Laboratory);
        FixConsoles(SystemTypes.HeliSabotage);
        FixConsoles(SystemTypes.LifeSupp);
        Up(SystemTypes.Comms, 0);
        FixConsoles(SystemTypes.Comms);
        Up(SystemTypes.MushroomMixupSabotage, 0);
    }

    private static void FixConsoles(SystemTypes sys)
    {
        Up(sys, 0x40);
        Up(sys, 0x41);
    }

    private static void FixLights()
    {
        ShipStatus ss = ShipStatus.Instance;
        try
        {
            SwitchSystem sys = ss.Systems[SystemTypes.Electrical].TryCast<SwitchSystem>();
            if (sys == null) return;
            int a = sys.ActualSwitches, e = sys.ExpectedSwitches;
            for (byte i = 0; i < 5; i++)
                if (((a >> i) & 1) != ((e >> i) & 1))
                    Up(SystemTypes.Electrical, i);
        }
        catch { }
    }

    public static void Random()
    {
        int m = Map();
        List<int> opts = new List<int> { 1, 3 };
        if (m != 5) opts.Add(0);
        if (m == 0 || m == 1 || m == 3) opts.Add(2);
        if (m == 5) opts.Add(4);
        switch (opts[UnityEngine.Random.Range(0, opts.Count)])
        {
            case 0: Lights(); break;
            case 1: Main(); break;
            case 2: Oxygen(); break;
            case 3: Comms(); break;
            case 4: Mush(); break;
        }
    }

    private static void Up(SystemTypes sys, byte amt)
    {
        try { if (ShipStatus.Instance != null) ShipStatus.Instance.RpcUpdateSystem(sys, amt); }
        catch { }
    }

    private static int Map() => NocturneNav.CurrentMapId();
}
