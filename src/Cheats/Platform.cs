using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

internal static class Platform
{
    private static ShipStatus _ship;
    private static MovingPlatformBehaviour _plat;
    private static AirshipStatus _air;
    private static Il2CppArrayBase<PlatformConsole> _consoles;
    private static float _nextFix;

    internal static bool OnMap => Plat() != null;

    internal static bool IsLeft
    {
        get
        {
            MovingPlatformBehaviour p = Plat();
            return p != null && p.IsLeft;
        }
    }

    internal static bool Locked
    {
        get
        {
            MovingPlatformBehaviour p = Plat();
            if (p == null) return false;
            if (_air != null && _air.outOfOrderPlat != null && _air.outOfOrderPlat.activeSelf) return true;
            return !((Behaviour)p).enabled || !((Component)p).gameObject.activeSelf;
        }
    }

    private static MovingPlatformBehaviour Plat()
    {
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null)
        {
            _ship = null; _plat = null; _air = null; _consoles = null;
            return null;
        }
        try { if (_ship == ship) return _plat; } catch { _ship = null; }

        _ship = ship;
        _consoles = null;
        _air = ((Il2CppObjectBase)ship).TryCast<AirshipStatus>();
        if (_air != null && _air.GapPlatform != null) _plat = _air.GapPlatform;
        else _plat = Object.FindObjectOfType<MovingPlatformBehaviour>();
        return _plat;
    }

    internal static void Tick()
    {
        if (!NocturneConfig.PlatformUnlock.Value || Time.time < _nextFix) return;
        _nextFix = Time.time + 1f;

        MovingPlatformBehaviour p = Plat();
        if (p != null) Free(p);
    }

    private static void Free(MovingPlatformBehaviour p)
    {
        if (ShipStatus.Instance == null) return;

        ((Component)p).gameObject.SetActive(true);
        ((Behaviour)p).enabled = true;
        if (_air != null && _air.outOfOrderPlat != null) _air.outOfOrderPlat.SetActive(false);

        if (_consoles == null) _consoles = ((Component)ShipStatus.Instance).GetComponentsInChildren<PlatformConsole>(true);
        foreach (PlatformConsole c in _consoles)
        {
            if (c == null) continue;
            ((Component)c).gameObject.SetActive(true);
            ((Behaviour)c).enabled = true;
        }

        if (p.Target != null) return;

        Transform t = ((Component)p).transform;
        if ((t.localPosition - p.DisabledPosition).sqrMagnitude > 0.01f) return;
        t.localPosition = p.IsLeft ? p.LeftPosition : p.RightPosition;
    }

    internal static string Unlock()
    {
        if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");

        MovingPlatformBehaviour p = Plat();
        if (p == null) return NocturneText.T("Платформы нет — только Airship.", "No platform here — Airship only.");

        Free(p);
        return NocturneText.T("Разблокирована.", "Unlocked.");
    }

    internal static string Move(bool left)
    {
        if (ShipStatus.Instance == null) return NocturneText.T("Только в матче.", "In match only.");

        MovingPlatformBehaviour p = Plat();
        if (p == null) return NocturneText.T("Платформы нет — только Airship.", "No platform here — Airship only.");

        try { p.SetSide(left); }
        catch { return NocturneText.T("Не удалось.", "Failed."); }

        return left ? NocturneText.T("Уехала влево.", "Moved left.") : NocturneText.T("Уехала вправо.", "Moved right.");
    }

    internal static string Toggle()
    {
        MovingPlatformBehaviour p = Plat();
        if (p == null) return NocturneText.T("Платформы нет — только Airship.", "No platform here — Airship only.");
        return Move(!p.IsLeft);
    }
}
