using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Nocturne;

public enum NocturneNotifyKind { Info, Success, Warning, Danger }

public sealed class NocturneToast : MonoBehaviour
{
    private sealed class Entry
    {
        internal string Title;
        internal string Detail;
        internal float CreatedAt;
        internal float Duration;
        internal NocturneNotifyKind Kind;
    }

    private const int MaxEntries = 4;
    private const float Width = 392f;
    private const float Top = 84f;
    private const float Gap = 8f;
    private const float SlideInDistance = 56f;
    private const float SlideInSeconds = 0.30f;

    private static readonly List<Entry> Entries = new List<Entry>(MaxEntries);

    private bool _built;
    private GUIStyle _title;
    private GUIStyle _titleShadow;
    private GUIStyle _detail;
    private GUIStyle _ttl;

    internal static void Push(string text, float life = 3.6f)
    {
        Push(text, null, life, NocturneNotifyKind.Info);
    }

    internal static void Push(string title, string detail, float life, NocturneNotifyKind kind)
    {
        if (NocturneConfig.Toasts != null && !NocturneConfig.Toasts.Value) return;

        string t = Clean(title, 120);
        string d = Clean(detail, 160);
        if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(d)) return;

        while (Entries.Count >= MaxEntries) Entries.RemoveAt(0);
        Entries.Add(new Entry
        {
            Title = t,
            Detail = d,
            CreatedAt = Time.unscaledTime,
            Duration = Mathf.Clamp(life, 1.25f, 8f),
            Kind = kind,
        });
    }

    public void Update()
    {
        float now = Time.unscaledTime;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i] == null || now - Entries[i].CreatedAt > Entries[i].Duration)
                Entries.RemoveAt(i);
        }
    }

    internal void DrawGui()
    {
        if (Entries.Count == 0) return;
        EnsureStyles();

        int prevDepth = GUI.depth;
        GUI.depth = -12000;
        Color prevColor = GUI.color;
        float now = Time.unscaledTime;
        float scaleUi = NocturneConfig.HudScale != null ? Mathf.Clamp(NocturneConfig.HudScale.Value, 0.6f, 2f) : 1f;
        float width = Mathf.Min(Width * scaleUi, Screen.width - 24f);
        float x = (Screen.width - width) * 0.5f;
        float y = Top;

        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry e = Entries[i];
            float age = now - e.CreatedAt;
            float alpha = FadeAlpha(age, e.Duration);
            float height = (string.IsNullOrEmpty(e.Detail) ? 54f : 72f) * scaleUi;

            float slideT = EaseOutCubic(age / SlideInSeconds);
            float slideY = y - (1f - slideT) * SlideInDistance;
            float scale = Mathf.Lerp(0.96f, 1f, slideT);
            float sw = width * scale;
            float sh = height * scale;
            float dx = x + (width - sw) * 0.5f;
            float dy = slideY + (height - sh) * 0.5f;

            DrawCard(new Rect(dx, dy, sw, sh), e, alpha, now);
            y += height + Gap;
        }

        GUI.color = prevColor;
        GUI.depth = prevDepth;
    }

    [HideFromIl2Cpp]
    private void DrawCard(Rect rect, Entry e, float alpha, float now)
    {
        NocturnePalette p = NocturneStyle.Current;
        const int r = 18;

        Color card = Color.Lerp(p.Window, p.Panel, 0.32f);
        card.a = 0.99f * alpha;
        Color border = Color.Lerp(p.Accent, Color.white, 0.40f);
        NocturneStyle.FillRounded(rect, card, r);
        NocturneStyle.FillRounded(rect, A(p.Accent, 0.06f * alpha), r);
        NocturneStyle.StrokeRounded(rect, A(border, 0.9f * alpha), r, 2);
        NocturneStyle.Fill(new Rect(rect.x + 18f, rect.y + 2f, rect.width - 36f, 1f), A(Color.white, 0.09f * alpha));

        GUI.color = Color.white;

        float textX = rect.x + 20f;
        float textW = Mathf.Max(16f, rect.xMax - 50f - textX);
        float titleY = rect.y + (string.IsNullOrEmpty(e.Detail) ? 15f : 9f);

        _titleShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f * alpha);
        GUI.Label(new Rect(textX + 1f, titleY + 1f, textW, 24f), e.Title, _titleShadow);
        _title.normal.textColor = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(textX, titleY, textW, 24f), e.Title, _title);

        float remaining = Mathf.Max(0f, e.Duration - (now - e.CreatedAt));
        if (remaining > 0.05f)
        {
            _ttl.normal.textColor = A(p.Accent, 0.60f * alpha);
            GUI.Label(new Rect(rect.xMax - 44f, rect.y + 8f, 36f, 16f), $"{remaining:F1}s", _ttl);
        }

        if (!string.IsNullOrEmpty(e.Detail))
        {
            _detail.normal.textColor = A(Color.Lerp(p.Muted, Color.white, 0.85f), alpha);
            GUI.Label(new Rect(textX, rect.y + 39f, rect.width - 34f - (textX - rect.x), 22f), e.Detail, _detail);
        }

        Color bar = e.Kind switch
        {
            NocturneNotifyKind.Success => new Color(0.22f, 0.82f, 0.38f),
            NocturneNotifyKind.Danger => new Color(0.90f, 0.28f, 0.28f),
            NocturneNotifyKind.Warning => new Color(0.95f, 0.72f, 0.10f),
            _ => p.Accent,
        };
        float trackX = rect.x + 14f;
        float trackY = rect.yMax - 9f;
        float trackW = rect.width - 28f;
        NocturneStyle.FillRounded(new Rect(trackX, trackY, trackW, 2.5f), A(Color.white, 0.12f * alpha), 1);
        float fillW = trackW * (1f - Mathf.Clamp01((now - e.CreatedAt) / e.Duration));
        if (fillW > 1f)
            NocturneStyle.FillRounded(new Rect(trackX, trackY, fillW, 2.5f), A(bar, 0.95f * alpha), 1);
    }

    private void EnsureStyles()
    {
        if (_built) return;
        _built = true;

        _title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            richText = true,
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
        };
        _titleShadow = new GUIStyle(_title);
        _detail = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            richText = true,
            wordWrap = false,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft,
        };
        _ttl = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleRight,
        };
    }

    private static float FadeAlpha(float age, float duration)
    {
        float fadeIn = Mathf.Clamp01(age / 0.22f);
        float fadeOut = Mathf.Clamp01((duration - age) / 0.45f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static string Clean(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string s = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, Math.Max(0, max - 1)) + "…";
    }
}
