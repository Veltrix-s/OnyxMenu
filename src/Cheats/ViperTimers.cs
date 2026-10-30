using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Nocturne;

internal static class ViperTimers
{
    private static readonly List<ViperDeadBody> Bodies = new List<ViperDeadBody>(4);
    private static readonly Color Fresh = Ui.C(0.42f, 0.92f, 0.55f, 1f);
    private static readonly Color Amber = Ui.C(1f, 0.80f, 0.30f, 1f);
    private static readonly Color Dying = Ui.C(0.96f, 0.30f, 0.34f, 1f);

    private static string[] _secs = new string[200];
    private static GUIStyle _txt;
    private static int _h;
    private static int _size;
    private static float _sc = 1f;

    internal static void Track(ViperDeadBody body)
    {
        Bodies.RemoveAll(b => b == null);
        Bodies.Add(body);
    }

    internal static void Draw()
    {
        if (!NocturneConfig.ViperTimer.Value || !NocturneStyle.Painting || Bodies.Count == 0) return;
        if (ShipStatus.Instance == null || MeetingHud.Instance != null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (_txt == null)
        {
            _txt = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _txt.normal.textColor = Ui.White;
        }

        int size = NocturneConfig.ViperTimerSize.Value;
        if (_h != Screen.height || _size != size)
        {
            _h = Screen.height;
            _size = size;
            _sc = Ui.Clamp(_h / 1080f, 0.85f, 2.2f) * Ui.Clamp(size, 60, 200) / 100f;
            _txt.fontSize = Ui.RoundToInt(20f * _sc);
        }

        float w = 96f * _sc;
        float h = 30f * _sc;
        float gem = 8f * _sc;
        float inset = 3f * _sc;
        int rad = Ui.RoundToInt(h * 0.5f);
        int inRad = Ui.RoundToInt(h * 0.5f - inset);
        Color bg = NocturneStyle.Current.Window;
        bg.a = 0.92f;
        Matrix4x4 m = GUI.matrix;

        for (int i = 0; i < Bodies.Count; i++)
        {
            ViperDeadBody body = Bodies[i];
            if (body == null || !body.victimDissolving) continue;

            float left = body.dissolveCurrentTime;
            float full = body.maxDissolveTime;
            float a = Ui.Clamp01(Ui.Min((full - left) * 4f, left * 2f));
            if (a <= 0f) continue;

            Vector2 sp = NocturneTracers.ToScreen(cam, (Vector2)body.transform.position + Ui.Up2 * 0.4f);
            if (sp.x < -w || sp.x > Screen.width + w || sp.y < 0f || sp.y - h * 2f > Screen.height) continue;

            float k = left / full;
            Color col = k > 0.5f ? Ui.Mix(Amber, Fresh, (k - 0.5f) * 2f) : Ui.Mix(Dying, Amber, k * 2f);
            float pulse = left < 3f ? 0.5f + 0.5f * Ui.Sin(NocturneStyle.Now * 12f) : 0f;

            int tenths = (int)(left * 10f);
            if (tenths >= _secs.Length)
                Array.Resize(ref _secs, tenths + 100);
            string txt = _secs[tenths] ??= (tenths / 10f).ToString("0.0");

            float cx = Ui.Round(sp.x);
            float bottom = Ui.Round(sp.y - gem * 0.7f);
            var pill = Ui.R(Ui.Round(cx - w * 0.5f), bottom - h, w, h);
            var halo = Ui.R(pill.m_XMin - w * 0.25f, pill.m_YMin - h * 0.8f, w * 1.5f, h * 2.6f);
            var bar = Ui.R(pill.m_XMin + inset, pill.m_YMin + inset, (w - inset * 2f) * k, h - inset * 2f);

            NocturneStyle.Tint = Ui.C(1f, 1f, 1f, a);
            NocturneStyle.Glow(halo, Ui.C(col.r, col.g, col.b, 0.14f + 0.32f * pulse));

            GUIUtility.RotateAroundPivot(45f, Ui.V(cx, bottom));
            NocturneStyle.Fill(Ui.R(cx - gem * 0.5f, bottom - gem * 0.5f, gem, gem), Ui.C(col.r, col.g, col.b, 0.9f));
            GUI.matrix = m;

            NocturneStyle.FillRounded(pill, bg, rad);
            if (k > 0.03f)
                NocturneStyle.FillRounded(bar, Ui.C(col.r, col.g, col.b, 0.32f + 0.16f * pulse), inRad);
            NocturneStyle.StrokeRounded(pill, Ui.C(col.r, col.g, col.b, 0.9f), rad, 2);
            GUI.Label(pill, txt, _txt);
        }

        NocturneStyle.Tint = Ui.White;
    }
}

[HarmonyPatch(typeof(ViperDeadBody), nameof(ViperDeadBody.SetupViperInfo))]
internal static class ViperBodyPatch
{
    public static void Postfix(ViperDeadBody __instance) => ViperTimers.Track(__instance);
}
