using System;
using System.IO;
using AmongUs.Data;
using Il2CppInterop.Runtime;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne;

public sealed class NocturneBugRooms : MonoBehaviour
{
    private enum Step { Off, Wait, Started, Ending, Reading, Out, Back }

    internal static NocturneBugRooms Instance { get; private set; }
    internal static string Stage = "";
    internal static int Runs;
    internal static int Hits;
    internal static int Lvl;
    internal static int Was => Instance == null ? 0 : Instance._was;

    private Step _s;
    private float _at;
    private int _code;
    private float _gap;
    private float _cut;
    private float _poke;
    private float _look;
    private float _firm;
    private string _tail = "";
    private int _was;
    private bool _done;
    private readonly System.Collections.Generic.HashSet<int> _seen = new System.Collections.Generic.HashSet<int>();

    private static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "Nocturne");
    private static string Txt => Path.Combine(Dir, "BugRooms.txt");

    public void Awake() => Instance = this;

    internal static int LocalLevel()
    {
        try
        {
            uint l = DataManager.Player.Stats.Level;
            if (l < 10000u) return (int)l + 1;
        }
        catch { }
        return 0;
    }

    private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;

    private static bool EndScreen()
    {
        try { return Object.FindObjectOfType<EndGameManager>() != null; }
        catch { return false; }
    }

    private static bool Intro()
    {
        try
        {
            IntroCutscene c = Object.FindObjectOfType<IntroCutscene>();
            if (c != null && ((Component)c).gameObject.activeInHierarchy) return true;
        }
        catch { }
        try
        {
            ShhhBehaviour s = Object.FindObjectOfType<ShhhBehaviour>();
            if (s != null && ((Component)s).gameObject.activeInHierarchy) return true;
        }
        catch { }
        return false;
    }

    private static InnerNetClient Net()
    {
        try { return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance; }
        catch { return null; }
    }

    private static int Gid(InnerNetClient c)
    {
        try { return c == null ? 0 : c.GameId; }
        catch { return 0; }
    }

    private static bool Launch()
    {
        try
        {
            if (LobbyBehaviour.Instance == null) return false;
            GameStartManager g = DestroyableSingleton<GameStartManager>.InstanceExists
                ? DestroyableSingleton<GameStartManager>.Instance
                : Object.FindObjectOfType<GameStartManager>();
            if (g == null) return false;
            g.MinPlayers = 1;
            g.startState = GameStartManager.StartingStates.Countdown;
            g.countDownTimer = 0f;
            return true;
        }
        catch { return false; }
    }

    private static void Finish()
    {
        try { GameManager.Instance.RpcEndGame((GameOverReason)1, false); } catch { }
    }

    private static void Quit()
    {
        try { AmongUsClient.Instance.ExitGame((DisconnectReasons)0); } catch { }
    }

    private static void Enter(int code)
    {
        try
        {
            AmongUsClient c = AmongUsClient.Instance;
            if (c == null || code == 0) return;
            ((InnerNetClient)c).GameId = code;
            var it = c.CoJoinOnlineGameFromCode(code, false);
            if (it != null) ((MonoBehaviour)c).StartCoroutine(it);
        }
        catch { }
    }

    private static bool Shoo()
    {
        try
        {
            DisconnectPopup pop = Object.FindObjectOfType<DisconnectPopup>();
            if (pop == null || !((Component)pop).gameObject.activeInHierarchy) return false;
            pop.Close();
            return true;
        }
        catch { return false; }
    }

    private void Nudge()
    {
        if (Time.unscaledTime < _poke) return;
        _poke = Time.unscaledTime + 0.9f;
        Poke();
    }

    private static void Poke()
    {
        if (Shoo()) return;

        CreateGameOptions opt = Sheet();
        if (opt != null)
        {
            Say("Confirm");
            try { opt.Confirm(); } catch { }
            return;
        }

        try
        {
            MainMenuManager m = Menu();
            if (m != null)
            {
                if (Tap(m.createGameButton)) { Say("createGameButton"); return; }
                if (Tap(m.PlayOnlineButton)) { Say("PlayOnlineButton"); return; }
                if (Tap(m.playButton)) { Say("playButton"); return; }
            }
            else Say("нет MainMenuManager");

            if (Wild()) return;
            Say("нечего жать");
        }
        catch (Exception e) { Say("сбой: " + e.Message); }
    }

    private static bool _dump;

    private static bool Wild()
    {
        int n = -1;
        try { n = Object.FindObjectsOfType<PassiveButton>().Length; }
        catch (Exception e) { Say("СКАН УПАЛ: " + e.Message); }
        Say("СБОРКА-4 кнопок в сцене: " + n);

        if (Hunt("create game", "createbutton", "create")) return true;
        if (Hunt("online")) return true;
        if (Hunt("play")) return true;

        if (!_dump && n > 0)
        {
            _dump = true;
            try
            {
                foreach (PassiveButton b in Object.FindObjectsOfType<PassiveButton>())
                    if (b != null) Say("кнопка: " + Label(b));
            }
            catch (Exception e) { Say("дамп упал: " + e.Message); }
        }
        return false;
    }

    private static bool Hunt(params string[] want)
    {
        try
        {
            foreach (PassiveButton b in Object.FindObjectsOfType<PassiveButton>())
            {
                if (b == null) continue;
                string t = Label(b);
                if (t.Contains("back") || t.Contains("cancel") || t.Contains("enter code") || t.Contains("find game")) continue;

                bool ok = false;
                for (int i = 0; i < want.Length && !ok; i++) ok = t.Contains(want[i]);
                if (!ok || !Tap(b)) continue;

                Say("жму: " + t);
                return true;
            }
        }
        catch (Exception e) { Say("поиск кнопок: " + e.Message); }
        return false;
    }

    private static string Label(PassiveButton b)
    {
        string t = ((Object)b).name.ToLowerInvariant();
        try
        {
            foreach (TMPro.TMP_Text x in ((Component)b).GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (x != null && !string.IsNullOrEmpty(x.text)) t += " " + x.text.ToLowerInvariant();
        }
        catch { }
        return t;
    }

    internal static MainMenuManager Held;

    private static MainMenuManager Menu()
    {
        try { if (Patches.NocturneMainArtDriver.Live != null) return Patches.NocturneMainArtDriver.Live; }
        catch { }

        try { if (Held != null && ((Component)Held).gameObject != null) return Held; }
        catch { }

        try
        {
            MainMenuManager m = Object.FindObjectOfType<MainMenuManager>();
            if (m != null) return m;
        }
        catch { }

        try
        {
            foreach (Object o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MainMenuManager>()))
            {
                MainMenuManager m = o == null ? null : o.TryCast<MainMenuManager>();
                if (m != null) return m;
            }
        }
        catch (Exception e) { Say("поиск меню: " + e.Message); }

        return null;
    }

    private static void Say(string what)
    {
        NocturnePlugin.Logger?.LogInfo((object)("[BugRooms] " + what));
    }

    private static CreateGameOptions Sheet()
    {
        try
        {
            ConfirmCreatePopUp pop = Object.FindObjectOfType<ConfirmCreatePopUp>();
            if (pop != null && ((Component)pop).gameObject.activeInHierarchy)
            {
                CreateGameOptions own = null;
                try { own = pop.createGameOptions; } catch { }
                if (own != null) return own;
            }
        }
        catch { }

        try
        {
            CreateGameOptions o = Object.FindObjectOfType<CreateGameOptions>();
            return o != null && ((Component)o).gameObject.activeInHierarchy ? o : null;
        }
        catch { return null; }
    }

    private static bool Tap(PassiveButton b)
    {
        if (b == null) return false;
        try
        {
            if (!((Component)b).gameObject.activeInHierarchy || !((Behaviour)b).isActiveAndEnabled) return false;
        }
        catch { return false; }

        bool hit = false;
        try { if (b.OnClick != null) { b.OnClick.Invoke(); hit = true; } } catch { }
        try
        {
            ((PassiveUiElement)b).ReceiveClickDown();
            ((PassiveUiElement)b).ReceiveClickUp();
            hit = true;
        }
        catch { }
        return hit;
    }

    private static void Keep(int code)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string name = GameCode.IntToGameName(code);
            if (string.IsNullOrEmpty(name)) return;
            foreach (string line in File.Exists(Txt) ? File.ReadAllLines(Txt) : new string[0])
                if (line.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return;
            File.AppendAllText(Txt, name + "  " + DateTime.Now.ToString("dd.MM HH:mm") + Environment.NewLine);
        }
        catch { }
    }

    internal static string Tail(int code)
    {
        try
        {
            string s = GameCode.IntToGameName(code);
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(8);
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            string t = sb.ToString();
            return t.Length <= 4 ? t : t.Substring(t.Length - 4);
        }
        catch { return ""; }
    }

    private static bool Listed()
    {
        string v = NocturneConfig.BugRoomTargets.Value;
        return !string.IsNullOrWhiteSpace(v);
    }

    private static bool Wanted(string tail)
    {
        if (tail.Length < 4) return false;
        foreach (string p in NocturneConfig.BugRoomTargets.Value.Split(',', ';', ' ', '\n', '\r', '\t'))
        {
            string q = p.Trim();
            if (q.Length > 0 && string.Equals(q, tail, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void Tally(float now)
    {
        Runs++;
        _done = true;

        bool got = Lvl == 1 && _code != 0;
        if (got)
        {
            if (NocturneConfig.BugRoomLog.Value) Keep(_code);
            Hits++;
            NocturneToast.Push(NocturneText.T("Баг-комната", "Bug room"),
                GameCode.IntToGameName(_code) + NocturneText.T(" — остаюсь тут", " — staying here"), 5f, NocturneNotifyKind.Success);
        }

        if (got && NocturneConfig.BugRoomHunt.Value)
        {
            NocturneConfig.BugRoomHunt.Value = false;
            _s = Step.Wait;
            _at = now + _gap;
            Stage = NocturneText.T("нашёл", "found");
            return;
        }

        _s = Step.Out;
        _at = now + 1f;
        Stage = NocturneText.T("выхожу", "leaving");
    }

    public void Update()
    {
        if (!NocturneConfig.BugRoomCycle.Value)
        {
            if (_s != Step.Off) { _s = Step.Off; _at = 0f; _poke = 0f; _done = false; Stage = ""; }
            return;
        }

        float now = Time.unscaledTime;
        InnerNetClient net = Net();
        int gid = Gid(net);

        if (Held == null && now >= _look)
        {
            _look = now + 1f;
            try { Held = Object.FindObjectOfType<MainMenuManager>(); } catch { }
        }

        _gap = Mathf.Clamp(NocturneConfig.BugRoomDelay.Value, 1f, 10f);

        if (Shoo())
        {
            _s = Step.Out;
            _at = now + 1.5f;
            Stage = NocturneText.T("сорвалось, заново", "dropped, retry");
            return;
        }

        if (EndScreen())
        {
            if (_done) { Stage = NocturneText.T("итоги", "results"); return; }
            if (_s != Step.Reading) { _s = Step.Reading; _at = now + 1f; _cut = now + 8f; }
            if (now < _at) { Stage = NocturneText.T("итоги", "results"); return; }

            Lvl = LocalLevel();
            if (now < _cut && (Lvl == 0 || Lvl == _was)) { Stage = NocturneText.T("жду уровень", "waiting level"); return; }
            Tally(now);
            return;
        }

        if (ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
        {
            if (_s != Step.Ending) { _s = Step.Ending; _at = now + 1f; _cut = now + 10f; _done = false; }
            if (Intro()) { _at = now + 1f; Stage = NocturneText.T("вступление", "intro"); return; }
            if (now < _at) { Stage = NocturneText.T("в игре", "in game"); return; }
            Finish();
            _at = now + 2.5f;
            Stage = NocturneText.T("завершаю", "ending");
            return;
        }

        if (LobbyBehaviour.Instance != null && gid != 0 && _s != Step.Out)
        {
            if ((_s == Step.Ending || _s == Step.Reading) && !_done)
            {
                _code = gid;
                Lvl = LocalLevel();
                if (now < _cut && (Lvl == 0 || Lvl == _was)) { Stage = NocturneText.T("жду уровень", "waiting level"); return; }
                Tally(now);
                return;
            }

            _done = false;
            if (_code != gid)
            {
                _code = gid;
                _tail = "";
                _s = Step.Wait;
                _at = now + _gap;
                if (NocturneConfig.BugRoomHunt.Value && !_seen.Add(_code))
                {
                    _s = Step.Out;
                    _at = now;
                    Stage = NocturneText.T("уже была", "seen");
                    return;
                }
            }
            if (NocturneConfig.BugRoomHunt.Value && Listed())
            {
                string tail = Tail(gid);
                if (tail.Length < 4) { _tail = ""; Stage = NocturneText.T("жду код", "waiting code"); return; }

                if (tail != _tail) { _tail = tail; _firm = now + 0.6f; }
                if (now < _firm) { Stage = NocturneText.T("читаю код ", "reading code ") + tail; return; }

                if (!Wanted(tail))
                {
                    _s = Step.Out;
                    _at = now;
                    Stage = NocturneText.T("мимо: ", "skip: ") + tail;
                    return;
                }

                NocturneConfig.BugRoomHunt.Value = false;
                Hits++;
                if (NocturneConfig.BugRoomLog.Value) Keep(gid);
                NocturneToast.Push(NocturneText.T("Баг-комната", "Bug room"),
                    GameCode.IntToGameName(gid) + NocturneText.T(" — совпало", " — matched"), 6f, NocturneNotifyKind.Success);
                Stage = NocturneText.T("нашёл ", "found ") + tail;
            }

            if (net == null || !net.AmHost) { Stage = NocturneText.T("не хост", "not host"); return; }
            if (_s == Step.Started)
            {
                if (now < _at) { Stage = NocturneText.T("старт", "starting"); return; }
                _s = Step.Wait;
                _at = now;
            }
            if (_s != Step.Wait) { _s = Step.Wait; _at = now + _gap; }
            if (now < _at) { Stage = NocturneText.T("пауза", "idle"); return; }
            if (Launch()) { _was = LocalLevel(); _s = Step.Started; _at = now + 8f; Stage = NocturneText.T("старт", "starting"); }
            else _at = now + 1f;
            return;
        }

        bool hunt = NocturneConfig.BugRoomHunt.Value;

        if (_s == Step.Out)
        {
            if (InRoom() || gid != 0)
            {
                if (now >= _at) { Quit(); _at = now + 1.5f; }
                Stage = NocturneText.T("выхожу", "leaving");
                return;
            }
            if (now < _at) return;
            if (hunt)
            {
                Nudge();
                _s = Step.Back;
                _at = now + 20f;
                Stage = NocturneText.T("новая комната", "new room");
            }
            else
            {
                Enter(_code);
                _s = Step.Back;
                _at = now + 20f;
                Stage = NocturneText.T("захожу", "rejoining");
            }
            return;
        }

        if (_s == Step.Back)
        {
            if (InRoom()) { _s = Step.Wait; _at = now + _gap; Stage = NocturneText.T("в лобби", "in lobby"); return; }
            if (now < _at)
            {
                if (gid != 0) { Stage = NocturneText.T("подключаюсь", "connecting"); return; }
                if (hunt) Nudge();
                return;
            }
            if (hunt) { _s = Step.Out; _at = now + 1f; return; }
            NocturneConfig.BugRoomCycle.Value = false;
            try { GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code); } catch { }
            NocturneToast.Push(NocturneText.T("Баг-комнаты", "Bug rooms"), NocturneText.T("Не зашёл, код скопирован.", "Rejoin failed, code copied."), 5f, NocturneNotifyKind.Danger);
            _s = Step.Off;
            return;
        }

        if (gid != 0) { Stage = NocturneText.T("жду лобби", "waiting lobby"); return; }
        if (now < _at) { Stage = NocturneText.T("пауза", "idle"); return; }

        if (!hunt && _code != 0)
        {
            Enter(_code);
            _s = Step.Back;
            _at = now + 20f;
            Stage = NocturneText.T("захожу", "rejoining");
            return;
        }

        Nudge();
        Stage = NocturneText.T("создаю комнату", "creating room");
    }
}

[HarmonyLib.HarmonyPatch(typeof(MainMenuManager), "Start")]
internal static class NocturneBugRoomsMenuHook
{
    public static void Postfix(MainMenuManager __instance) => NocturneBugRooms.Held = __instance;
}

