using UnityEngine;

namespace Nocturne;

public sealed class NocturneLobby : MonoBehaviour
{
    private GUIStyle _mark;
    private Texture2D _moonTex;
    private string _moonAccent;
    private GUIStyle _moonStyle;

    internal void DrawGui()
    {
        if (NocturneConfig.LobbyBrand == null || !NocturneConfig.LobbyBrand.Value) return;
        if (LobbyBehaviour.Instance == null) return;
        if (Event.current.type != EventType.Repaint) return;

        EnsureStyles();
        NocturnePalette p = NocturneStyle.Current;
        Color accent = Patches.NocturneLobbyTheme.LobbyAccent(p.Accent);

        var badge = new Rect(22f, 16f, 214f, 48f);
        NocturneStyle.FillRounded(new Rect(badge.x - 2f, badge.y + 3f, badge.width + 4f, badge.height + 5f), new Color(0f, 0f, 0f, 0.32f), 15);
        NocturneStyle.FillRounded(badge, new Color(p.Window.r, p.Window.g, p.Window.b, 0.82f), 13);
        NocturneStyle.FillRounded(badge, A(accent, 0.07f), 13);
        NocturneStyle.FillRounded(new Rect(badge.x, badge.y, badge.width, badge.height * 0.5f), A(Color.white, 0.04f), 13);
        NocturneStyle.StrokeRounded(badge, A(accent, 0.5f), 13, 1);
        NocturneStyle.Fill(new Rect(badge.x + 12f, badge.y + 1f, badge.width - 24f, 1f), A(Color.white, 0.10f));
        NocturneStyle.FillRounded(new Rect(badge.x + 9f, badge.y + 11f, 3f, badge.height - 22f), accent, 2);

        DrawMoon(new Rect(badge.x + 20f, badge.center.y - 15f, 30f, 30f), accent, p.Id);

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Label(new Rect(badge.x + 60f, badge.y + 1f, badge.width - 62f, badge.height), "NOCTURNE", _mark);
        GUI.color = p.Text;
        GUI.Label(new Rect(badge.x + 59f, badge.y, badge.width - 62f, badge.height), "NOCTURNE", _mark);
        GUI.color = Color.white;
    }

    private void DrawMoon(Rect box, Color accent, string id)
    {
        int r = Mathf.RoundToInt(box.width / 2f);
        NocturneStyle.FillRounded(new Rect(box.center.x - box.width / 2f, box.center.y - box.height / 2f, box.width, box.height), A(accent, 0.14f), r);

        if (_moonTex == null || _moonAccent != id)
        {
            _moonTex = BuildMoon(accent);
            _moonAccent = id;
        }
        if (_moonStyle == null) _moonStyle = new GUIStyle();
        _moonStyle.normal.background = _moonTex;

        GUI.color = Color.white;
        GUI.Box(box, GUIContent.none, _moonStyle);

        NocturneStyle.FillRounded(new Rect(box.x + box.width * 0.66f, box.y + box.height * 0.18f, 3.5f, 3.5f), A(Color.white, 0.85f), 2);
    }

    private static Texture2D BuildMoon(Color accent)
    {
        const int n = 64;
        float cx = n * 0.5f, cy = n * 0.5f;
        float R = n * 0.33f;
        float r = n * 0.30f;
        float ox = n * 0.15f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float dOut = Mathf.Sqrt(dx * dx + dy * dy) - R;
                float ix = dx - ox;
                float dIn = Mathf.Sqrt(ix * ix + dy * dy) - r;
                float d = Mathf.Max(dOut, -dIn);
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                Color col = Color.Lerp(accent, Color.white, 0.20f * Mathf.Clamp01(-dOut * 0.4f));
                px[y * n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private void EnsureStyles()
    {
        if (_mark != null) return;
        _mark = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 26,
            wordWrap = false,
            clipping = TextClipping.Overflow
        };
        _mark.normal.textColor = Color.white;
    }
}
