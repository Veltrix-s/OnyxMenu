using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

public sealed class NocturneOverheadChat : MonoBehaviour
{
    private sealed class Bubble
    {
        internal string Text;
        internal float At;
    }

    private const int MaxLen = 60;
    private const float PadX = 12f;
    private const float PadY = 7f;

    private static readonly Dictionary<byte, Bubble> _bubbles = new Dictionary<byte, Bubble>();
    private static readonly List<byte> _drop = new List<byte>(8);

    private GUIStyle _text;
    private GUIStyle _shadow;
    private readonly GUIContent _gc = new GUIContent();

    private static bool On => NocturneConfig.OverheadChat.Value;

    private static float Life => Ui.Clamp(NocturneConfig.OverheadChatTime.Value, 2, 15);

    internal static void Feed(PlayerControl src, string msg)
    {
        if (src == null || src.Data == null || string.IsNullOrWhiteSpace(msg)) return;
        if (msg.TrimStart().StartsWith("/")) return;

        string clean = NocturneNameColor.Strip(msg).Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (clean.Length == 0) return;
        if (clean.Length > MaxLen) clean = clean.Substring(0, MaxLen - 1) + "…";

        _bubbles[src.PlayerId] = new Bubble { Text = clean, At = Time.unscaledTime };
    }

    internal static void Clear() => _bubbles.Clear();

    internal void DrawGui()
    {
        if (!NocturneStyle.Painting) return;
        if (!On || _bubbles.Count == 0) return;
        if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
        if (MeetingHud.Instance != null || ExileController.Instance != null) return;
        if (HudManager.Instance != null && HudManager.Instance.Chat != null && HudManager.Instance.Chat.IsOpenOrOpening)
            return;

        bool inLobby = LobbyBehaviour.Instance != null;
        bool inMatch = !inLobby && ShipStatus.Instance != null;
        if (!inLobby && !inMatch) return;

        int where = NocturneConfig.OverheadChatWhere.Value;
        if ((where == 1 && !inMatch) || (where == 2 && !inLobby)) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        if (_text == null) Build();

        float now = Time.unscaledTime;
        float life = Life;
        _drop.Clear();

        float ppu = Ppu(cam);

        var it = PlayerControl.AllPlayerControls.GetEnumerator();
        while (it.MoveNext())
        {
            PlayerControl p = it.Current;
            if (p == null || p.Data == null || p.Data.Disconnected) continue;
            if (!_bubbles.TryGetValue(p.PlayerId, out Bubble b)) continue;

            float age = now - b.At;
            if (age >= life)
            {
                _drop.Add(p.PlayerId);
                continue;
            }

            Vector2 world;
            try
            {
                world = p.GetTruePosition();
            }
            catch
            {
                Vector3 t = p.transform.position;
                world = Ui.V(t.x, t.y);
            }

            Vector3 raw = cam.WorldToScreenPoint(Ui.V3(world.x, world.y, 0f));

            float sx = raw.x, sy = Screen.height - raw.y;
            if (sx < -200f || sx > Screen.width + 200f || sy < -120f || sy > Screen.height + 120f) continue;

            float fade = age > life - 0.6f ? Ui.Clamp01((life - age) / 0.6f) : 1f;
            float rise = Ui.Min(1f, age / 0.25f);
            Draw(sx, sy - ppu * 1.55f - (1f - rise) * 10f, b.Text, fade, BodyColor(p));
        }

        for (int i = 0; i < _drop.Count; i++)
            _bubbles.Remove(_drop[i]);
    }

    private void Draw(float sx, float sy, string msg, float fade, Color tint)
    {
        _gc.text = msg;
        float w = Ui.Clamp(msg.Length * 9.2f + PadX * 2f, 96f, 380f);
        float h = _text.CalcHeight(_gc, w - PadX * 2f) + PadY * 2f;

        var box = Ui.R(sx - w * 0.5f, sy - h, w, h);
        Color prev = NocturneStyle.Tint;
        NocturneStyle.Tint = Ui.C(1f, 1f, 1f, fade);

        NocturneStyle.FillRounded(Ui.R(box.m_XMin + 2f, box.m_YMin + 3f, box.m_Width, box.m_Height), Ui.C(0f, 0f, 0f, 0.35f), 10);
        NocturneStyle.FillRounded(box, Ui.C(0.05f, 0.06f, 0.09f, 0.93f), 10);
        NocturneStyle.StrokeRounded(box, Ui.C(tint.r, tint.g, tint.b, 0.85f), 10, 2);

        var inner = Ui.R(box.m_XMin + PadX, box.m_YMin + PadY, box.m_Width - PadX * 2f, box.m_Height - PadY * 2f);
        _shadow.normal.textColor = Ui.C(0f, 0f, 0f, 0.8f * fade);
        GUI.Label(Ui.R(inner.m_XMin + 1f, inner.m_YMin + 1f, inner.m_Width, inner.m_Height), msg, _shadow);
        _text.normal.textColor = Ui.C(0.96f, 0.97f, 1f, fade);
        GUI.Label(inner, msg, _text);

        NocturneStyle.Tint = prev;
    }

    private static float Ppu(Camera cam)
    {
        Vector3 a = cam.WorldToScreenPoint(Ui.Zero3);
        Vector3 b = cam.WorldToScreenPoint(Ui.V3(0f, 1f, 0f));
        float d = Ui.Abs(a.y - b.y);
        return d < 4f ? 42f : d;
    }

    private static Color BodyColor(PlayerControl p)
    {
        int id = p.CurrentOutfit.ColorId;
        if (id >= 0 && id < Palette.PlayerColors.Length) return Palette.PlayerColors[id];
        return NocturneStyle.Current.Accent;
    }

    private void Build()
    {
        _text = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = false,
        };
        _shadow = new GUIStyle(_text);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal static class NocturneOverheadChatFeed
{
    public static void Postfix(PlayerControl sourcePlayer, string chatText) => NocturneOverheadChat.Feed(sourcePlayer, chatText);
}
