using InnerNet;
using UnityEngine;

namespace Nocturne;

internal static class MapTilt
{
    private static GameObject _container;
    private static bool _applied;
    private static float _angle;

    internal static bool Applied => _applied;

    private static bool Want()
    {
        return NocturneConfig.WorldTilt.Value
            && ShipStatus.Instance != null
            && LobbyBehaviour.Instance == null
            && MeetingHud.Instance == null;
    }

    internal static void Tick()
    {
        if (!Want())
        {
            if (_applied) Disable();
            return;
        }

        float angle = Mathf.Clamp(NocturneConfig.WorldTiltAngle.Value, -180f, 180f);

        if (!_applied)
        {
            Enable(angle);
            return;
        }

        if (!Mathf.Approximately(angle, _angle) && _container != null)
        {
            _angle = angle;
            _container.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private static void Enable(float angle)
    {
        ShipStatus ship = ShipStatus.Instance;
        if (ship == null) return;

        _container = new GameObject("NocturneTilt");
        _container.hideFlags = HideFlags.HideAndDontSave;
        _container.transform.position = Vector3.zero;
        _container.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        ((Component)ship).transform.SetParent(_container.transform, true);

        _angle = angle;
        _applied = true;
    }

    private static void Disable()
    {
        ShipStatus ship = ShipStatus.Instance;
        if (ship != null)
            ((Component)ship).transform.SetParent(null, true);

        if (_container != null)
            Object.Destroy(_container);

        _container = null;
        _applied = false;
    }

    // чужого игрока двигаем под наклон карты; локального и тень не трогаем
    internal static void PlaceOther(PlayerControl pc)
    {
        if (!_applied || pc == null || ((InnerNetObject)pc).AmOwner)
            return;

        Transform tr = ((Component)pc).transform;
        Vector3 p = tr.position;

        float rad = _angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        tr.position = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos, p.z);
    }
}
