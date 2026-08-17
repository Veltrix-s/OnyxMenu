using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneGuiHost : MonoBehaviour
{
    private Action[] _draw;

    private void Bind()
    {
        if (_draw != null) return;

        var menu = GetComponent<NocturneMenu>();
        var lobby = GetComponent<NocturneLobby>();
        var tracers = GetComponent<NocturneTracers>();
        var over = GetComponent<NocturneOverheadChat>();
        var radar = GetComponent<NocturneRadar>();
        var bar = GetComponent<NocturneLobbyBar>();
        var music = GetComponent<NocturneMusicPlayer>();
        var radial = GetComponent<NocturneRadial>();
        var log = GetComponent<NocturneEventLog>();
        var replay = GetComponent<NocturneReplay>();
        var chat = GetComponent<NocturneChatWindow>();
        var toast = GetComponent<NocturneToast>();
        var btn = GetComponent<NocturneMenuButton>();

        var order = new List<Action>(13);
        if (menu != null) order.Add(menu.DrawGui);
        if (lobby != null) order.Add(lobby.DrawGui);
        if (tracers != null) order.Add(tracers.DrawGui);
        if (over != null) order.Add(over.DrawGui);
        if (radar != null) order.Add(radar.DrawGui);
        if (bar != null) order.Add(bar.DrawGui);
        if (music != null) order.Add(music.DrawGui);
        if (radial != null) order.Add(radial.DrawGui);
        if (log != null) order.Add(log.DrawGui);
        if (replay != null) order.Add(replay.DrawGui);
        if (chat != null) order.Add(chat.DrawGui);
        if (toast != null) order.Add(toast.DrawGui);
        if (btn != null) order.Add(btn.DrawGui);
        _draw = order.ToArray();
    }

    public void OnGUI()
    {
        Bind();
        if (_draw == null) return;

        Matrix4x4 m = GUI.matrix;
        Color c = GUI.color;

        for (int i = 0; i < _draw.Length; i++)
        {
            try { _draw[i](); }
            catch { }
            GUI.matrix = m;
            GUI.color = c;
        }
    }
}
