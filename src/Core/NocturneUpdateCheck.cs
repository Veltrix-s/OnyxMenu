using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

internal enum UpState { Idle, Checking, Found, Loading, Done, Fail }

public sealed class NocturneUpdateCheck : MonoBehaviour
{
    private const string Site = "https://onyxmenu.kawas-set.workers.dev";
    private const string VersionUrl = Site + "/version.json";

    private static readonly HttpClient Http = Make();

    internal static UpState State { get; private set; } = UpState.Idle;
    internal static string Latest { get; private set; } = "";
    internal static string Err { get; private set; } = "";
    private static string _url = "";

    private Task<string> _check;
    private Task<byte[]> _load;
    private bool _started;
    private bool _shown;
    private float _at;

    internal static NocturneUpdateCheck Instance { get; private set; }
    public void Awake() => Instance = this;
    public void Start() => _at = Time.unscaledTime + 10f;

    private static HttpClient Make()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        h.DefaultRequestHeaders.Add("User-Agent", "NocturneMenu");
        return h;
    }

    public void Update()
    {
        if (!_started && Time.unscaledTime >= _at)
        {
            _started = true;
            BeginCheck();
        }

        if (_check != null && _check.IsCompleted)
        {
            Task<string> t = _check;
            _check = null;
            if (t.IsFaulted || t.IsCanceled) Fail(ErrOf(t));
            else Apply(t.Result);
        }

        if (_load != null && _load.IsCompleted)
        {
            Task<byte[]> t = _load;
            _load = null;
            if (t.IsFaulted || t.IsCanceled)
            {
                Fail(ErrOf(t));
                NocturneToast.Push(NocturneText.T("Обновление", "Update"), NocturneText.T("Не скачалось: ", "Download failed: ") + Err, 6f, NocturneNotifyKind.Danger);
            }
            else Install(t.Result);
        }

        if (!_shown && State == UpState.Found)
        {
            _shown = true;
            NocturneToast.Push(NocturneText.T("Доступно обновление", "Update available"),
                NocturneText.T("Вышла v", "Version v") + Latest + NocturneText.T(" — качается из меню.", " — download it from the menu."),
                9f, NocturneNotifyKind.Success);
        }
    }

    internal static void Recheck()
    {
        if (Instance == null || State == UpState.Checking || State == UpState.Loading) return;
        Instance._shown = false;
        Instance.BeginCheck();
    }

    private void BeginCheck()
    {
        Err = "";
        State = UpState.Checking;
        try { _check = Task.Run(() => Fetch()); }
        catch (Exception e) { Fail(e.Message); _check = null; }
    }

    internal static void Download()
    {
        if (Instance == null || State != UpState.Found || Instance._load != null) return;
        if (string.IsNullOrWhiteSpace(_url))
        {
            try { GUIUtility.systemCopyBuffer = Site; } catch { }
            try { Application.OpenURL(Site); } catch { }
            return;
        }

        Err = "";
        State = UpState.Loading;
        try { Instance._load = Http.GetByteArrayAsync(_url); }
        catch (Exception e) { Fail(e.Message); Instance._load = null; }
    }

    private static void Fail(string e)
    {
        Err = e ?? "";
        State = UpState.Fail;
    }

    private static string ErrOf(Task t)
    {
        Exception e = t.Exception != null ? t.Exception.GetBaseException() : null;
        if (e == null) return t.IsCanceled ? "canceled" : "unknown";
        return e.GetType().Name + ": " + e.Message;
    }

    [HideFromIl2Cpp]
    private static string Fetch()
    {
        string bust = VersionUrl + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Http.GetStringAsync(bust).GetAwaiter().GetResult();
    }

    [HideFromIl2Cpp]
    private static void Apply(string json)
    {
        try
        {
            string ver = Grab(json, "\"version\"");
            if (string.IsNullOrEmpty(ver)) { State = UpState.Idle; return; }

            Latest = ver.TrimStart('v', 'V').Trim();
            _url = Grab(json, "\"url\"") ?? "";
            State = Newer(Latest, NocturnePlugin.PluginVersion) ? UpState.Found : UpState.Idle;
        }
        catch (Exception e) { Fail(e.Message); }
    }

    [HideFromIl2Cpp]
    private static void Install(byte[] data)
    {
        if (data == null || data.Length < 1024) { Fail(NocturneText.T("пустой файл", "empty file")); return; }
        try
        {
            string cur = Assembly.GetExecutingAssembly().Location;
            string tmp = cur + ".new";
            string bak = cur + ".bak";

            File.WriteAllBytes(tmp, data);
            if (File.Exists(bak)) File.Delete(bak);
            File.Move(cur, bak);
            if (File.Exists(cur)) File.Delete(cur);
            File.Move(tmp, cur);

            State = UpState.Done;
            NocturneToast.Push(NocturneText.T("Обновление", "Update"), NocturneText.T("Установлено. Перезапусти игру.", "Installed. Restart the game."), 9f, NocturneNotifyKind.Success);
        }
        catch (Exception e)
        {
            Fail(e.GetType().Name + ": " + e.Message);
            NocturneToast.Push(NocturneText.T("Обновление", "Update"), NocturneText.T("Не установилось: ", "Install failed: ") + Err, 6f, NocturneNotifyKind.Danger);
        }
    }

    internal static void Restart()
    {
        try { Application.Quit(); } catch { }
    }

    [HideFromIl2Cpp]
    private static string Grab(string json, string key)
    {
        int i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf(':', i);
        if (i < 0) return null;
        int q1 = json.IndexOf('"', i + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        return q2 < 0 ? null : json.Substring(q1 + 1, q2 - q1 - 1);
    }

    [HideFromIl2Cpp]
    private static bool Newer(string latest, string current)
    {
        try
        {
            int[] a = Parts(latest), b = Parts(current);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
        }
        catch { }
        return false;
    }

    [HideFromIl2Cpp]
    private static int[] Parts(string v)
    {
        var r = new int[3];
        string[] p = v.Trim().Split('.', '-', '+');
        for (int i = 0; i < 3 && i < p.Length; i++)
        {
            var sb = new StringBuilder();
            foreach (char c in p[i])
            {
                if (!char.IsDigit(c)) break;
                sb.Append(c);
            }
            int.TryParse(sb.ToString(), out r[i]);
        }
        return r;
    }
}
