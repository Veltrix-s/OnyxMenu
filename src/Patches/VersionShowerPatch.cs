using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneStamp
{
    private static TMP_Text _text;
    private static string _theme;

    internal static void Bind(TMP_Text t)
    {
        _text = t;
        _theme = null;
        Tint();
    }

    internal static void Tint()
    {
        if (_text == null) return;
        try
        {
            NocturnePalette p = NocturneStyle.Current;
            if (_theme == p.Id) return;
            _theme = p.Id;
            _text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(p.Accent) + "><b>NocturneMenu</b> by Kawasaki</color>";
        }
        catch { }
    }
}

public sealed class NocturneStampDriver : MonoBehaviour
{
    private float _at;

    public void Update()
    {
        if (Time.unscaledTime < _at) return;
        _at = Time.unscaledTime + 0.4f;
        NocturneStamp.Tint();
    }
}

[HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
internal static class NocturneVersionShowerPatch
{
    public static void Postfix(VersionShower __instance)
    {
        try
        {
            if (__instance == null || __instance.text == null) return;
            NocturneStamp.Bind(__instance.text);
        }
        catch { }
    }
}
