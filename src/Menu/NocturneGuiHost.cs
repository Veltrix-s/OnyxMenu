using UnityEngine;

namespace Nocturne;

public sealed class NocturneGuiHost : MonoBehaviour
{
    private NocturneMenu _menu;
    private NocturneLobby _lobby;
    private NocturneTracers _tracers;
    private NocturneOverheadChat _over;
    private NocturneRadar _radar;
    private NocturneLobbyBar _bar;
    private NocturneMusicPlayer _music;
    private NocturneRadial _radial;
    private NocturneEventLog _log;
    private NocturneReplay _replay;
    private NocturneChatWindow _chat;
    private NocturneToast _toast;
    private NocturneMenuButton _menuBtn;
    private bool _bound;

    private void Bind()
    {
        if (_bound) return;
        _bound = true;
        _menu = GetComponent<NocturneMenu>();
        _lobby = GetComponent<NocturneLobby>();
        _tracers = GetComponent<NocturneTracers>();
        _over = GetComponent<NocturneOverheadChat>();
        _radar = GetComponent<NocturneRadar>();
        _bar = GetComponent<NocturneLobbyBar>();
        _music = GetComponent<NocturneMusicPlayer>();
        _radial = GetComponent<NocturneRadial>();
        _log = GetComponent<NocturneEventLog>();
        _replay = GetComponent<NocturneReplay>();
        _chat = GetComponent<NocturneChatWindow>();
        _toast = GetComponent<NocturneToast>();
        _menuBtn = GetComponent<NocturneMenuButton>();
    }

    public void OnGUI()
    {
        Bind();
        Matrix4x4 m = GUI.matrix;
        Color c = GUI.color;

        if (_menu != null) { try { _menu.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_lobby != null) { try { _lobby.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_tracers != null) { try { _tracers.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_over != null) { try { _over.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_radar != null) { try { _radar.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_bar != null) { try { _bar.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_music != null) { try { _music.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_radial != null) { try { _radial.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_log != null) { try { _log.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_replay != null) { try { _replay.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_chat != null) { try { _chat.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_toast != null) { try { _toast.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
        if (_menuBtn != null) { try { _menuBtn.DrawGui(); } catch { } GUI.matrix = m; GUI.color = c; }
    }
}
