using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneStamp
{
    private static TMP_Text _text;
    private static string _last;

    internal static void Bind(TMP_Text t)
    {
        _text = t;
        _last = null;
        Tint();
    }

    internal static void Tint()
    {
        if (_text == null) return;
        try
        {
            string line = NocturneHud.StampLine();
            if (line == _last) return;
            _last = line;
            _text.richText = true;
            _text.text = line;
        }
        catch { }
    }
}

public sealed class NocturneStampDriver : MonoBehaviour
{
    private float _at;

    public void Update()
    {
        if (Time.unscaledTime < _at)
            return;
        _at = Time.unscaledTime + 0.05f;
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
