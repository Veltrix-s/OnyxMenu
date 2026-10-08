using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Nocturne.Patches;

internal static class NocturneCopyFeedback
{
    private const float Dur = 1.35f;
    private const float Freq = 26f;

    private static PassiveButton copyBtn;
    private static TMP_Text codeText;
    private static int hooked = -1;
    private static int lastPane = -1;
    private static float start, until;
    private static float idleAt;
    private static bool animating;
    private static string code = "";

    internal static void Apply(LobbyInfoPane pane)
    {
        try
        {
            if (pane == null || !NocturneConfig.LobbyAnims.Value) return;

            float now = Time.time;
            if (!animating && now >= until && now < idleAt) return;
            idleAt = now + 0.2f;

            int id = pane.GetInstanceID();
            if (id != lastPane)
            {
                Reset();
                lastPane = id;
            }

            if (codeText == null) codeText = FindCode(pane);
            if (codeText == null)
                return;
            if (copyBtn == null)
                copyBtn = FindButton(pane, codeText);

            Hook();
            Tick();
        }
        catch { }
    }

    private static void Reset()
    {
        copyBtn = null;
        codeText = null;
        hooked = -1;
        start = until = 0f;
        animating = false;
        code = "";
    }

    private static void Hook()
    {
        if (copyBtn == null || copyBtn.OnClick == null) return;
        int id = copyBtn.GetInstanceID();
        if (id == hooked)
            return;
        copyBtn.OnClick.AddListener((Action)OnCopy);
        hooked = id;
    }

    private static void OnCopy()
    {
        start = Time.time;
        until = Time.time + Dur;
    }

    private static void Tick()
    {
        float now = Time.time;
        bool active = now < until;
        animating = active;

        if (copyBtn != null)
        {
            float s = 1f;
            if (active)
            {
                float t = Ui.Clamp01((now - start) / Dur);
                s = 1f + Ui.Abs(Ui.Sin(now * Freq)) * (0.16f + 0.14f * (1f - t));
            }
            copyBtn.transform.localScale = Ui.One3 * s;
        }

        if (codeText == null) return;
        string raw = codeText.text ?? "";
        string c = Extract(raw);
        if (!string.IsNullOrEmpty(c))
            code = c;

        if (active)
        {
            float t = Ui.Clamp01((now - start) / Dur);
            float bounce = 1f + Ui.Sin(t * 16f) * (1f - t) * 0.05f;
            codeText.transform.localScale = Ui.One3 * bounce;
            string want = code + "\n<size=50%><color=#4CA2FF><b>" + Nocturne.NocturneText.T("СКОПИРОВАНО", "COPIED") + "</b></color></size>";
            if (!string.Equals(raw, want, StringComparison.Ordinal)) codeText.text = want;
        }
        else
        {
            codeText.transform.localScale = Ui.One3;
            if (!string.IsNullOrEmpty(code) && raw.Contains('\n') && raw.StartsWith(code, StringComparison.Ordinal))
                codeText.text = code;
        }
    }

    private static TMP_Text FindCode(Component root)
    {
        TMP_Text best = null;
        float bestSize = 0f;
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t == null || string.IsNullOrEmpty(Extract(t.text ?? ""))) continue;
            if (t.fontSize > bestSize)
            {
                bestSize = t.fontSize;
                best = t;
            }
        }
        return best;
    }

    private static PassiveButton FindButton(Component root, TMP_Text codeLabel)
    {
        Vector3 cp = codeLabel.transform.position;
        PassiveButton best = null;
        float bestScore = float.MaxValue;
        foreach (PassiveButton b in root.GetComponentsInChildren<PassiveButton>(true))
        {
            if (b == null)
                continue;
            Vector3 bp = b.transform.position;
            float dx = bp.x - cp.x;
            float dy = Ui.Abs(bp.y - cp.y);
            if (dx < 0.1f || dy > 0.8f) continue;
            float score = dx * 0.4f + dy;
            string name = b.name.ToLowerInvariant();
            if (name.Contains("copy") || name.Contains("clipboard") || name.Contains("code")) score -= 0.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = b;
            }
        }
        return best;
    }

    private static string Extract(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        bool inTag = false;
        int run = 0, runStart = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<')
            {
                inTag = true;
                continue;
            }
            if (c == '>')
            {
                inTag = false;
                continue;
            }
            if (inTag) continue;

            if (c >= 'A' && c <= 'Z')
            {
                if (run == 0)
                    runStart = i;
                if (++run == 6)
                {
                    var sb = new System.Text.StringBuilder(6);
                    bool t2 = false;
                    for (int j = runStart; sb.Length < 6 && j < text.Length; j++)
                    {
                        char nc = text[j];
                        if (nc == '<')
                        {
                            t2 = true;
                            continue;
                        }
                        if (nc == '>')
                        {
                            t2 = false;
                            continue;
                        }
                        if (!t2 && nc >= 'A' && nc <= 'Z')
                            sb.Append(nc);
                    }
                    return sb.Length == 6 ? sb.ToString() : "";
                }
            }
            else
            {
                run = 0;
                runStart = -1;
            }
        }
        return "";
    }
}

[HarmonyPatch(typeof(LobbyInfoPane), nameof(LobbyInfoPane.Update))]
internal static class NocturneCopyFeedbackPatch
{
    public static void Postfix(LobbyInfoPane __instance) => NocturneCopyFeedback.Apply(__instance);
}
