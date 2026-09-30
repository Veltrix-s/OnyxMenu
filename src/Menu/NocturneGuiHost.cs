using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneGuiHost : MonoBehaviour
{
    private Action[] _draw;

    private const float WarmDelay = 3f;
    private const float WarmGap = 0.05f;
    private const float SlowFrame = 0.033f;
    private const float StopWaiting = 10f;

    private float _warmAt = -1f;
    private float _warmNext;
    private bool _warmed;

    private void Awake() => useGUILayout = false;

    private void Update()
    {
        NocturneGate.Tick();

        if (_warmed) return;
        if (_warmAt < 0f)
        {
            _warmAt = Time.unscaledTime + WarmDelay;
            return;
        }

        float now = Time.unscaledTime;
        if (now < _warmAt || now < _warmNext)
            return;
        if (Time.unscaledDeltaTime > SlowFrame && now < _warmAt + StopWaiting)
            return;

        _warmNext = now + WarmGap;

        if (NocturneIcons.WarmStep()) return;
        if (NocturneStyle.WarmStep())
            return;
        if (NocturneMenuBg.WarmStep()) return;

        _warmed = true;
    }

    private void Bind()
    {
        if (_draw != null) return;

        var menu = GetComponent<NocturneMenu>();
        var tracers = GetComponent<NocturneTracers>();
        var over = GetComponent<NocturneOverheadChat>();
        var radar = GetComponent<NocturneRadar>();
        var radial = GetComponent<NocturneRadial>();
        var log = GetComponent<NocturneEventLog>();
        var replay = GetComponent<NocturneReplay>();
        var chat = GetComponent<NocturneChatWindow>();
        var toast = GetComponent<NocturneToast>();
        var btn = GetComponent<NocturneMenuButton>();

        var order = new List<Action>(13);
        if (menu != null)
            order.Add(menu.DrawGui);
        if (tracers != null) order.Add(tracers.DrawGui);
        if (over != null)
            order.Add(over.DrawGui);
        if (radar != null) order.Add(radar.DrawGui);
        if (radial != null)
            order.Add(radial.DrawGui);
        if (log != null) order.Add(log.DrawGui);
        if (replay != null) order.Add(replay.DrawGui);
        if (chat != null)
            order.Add(chat.DrawGui);
        if (toast != null) order.Add(toast.DrawGui);
        if (btn != null) order.Add(btn.DrawGui);
        _draw = order.ToArray();
    }

    public void OnGUI()
    {
        if (NocturneGate.Tripped) return;
        Bind();
        if (_draw == null) return;

        NocturneStyle.BeginPass();

        Matrix4x4 m = GUI.matrix;
        Color c = GUI.color;

        for (int i = 0; i < _draw.Length; i++)
        {
            try
            {
                _draw[i]();
            }
            catch { }
            GUI.matrix = m;
            GUI.color = c;
        }
    }
}
