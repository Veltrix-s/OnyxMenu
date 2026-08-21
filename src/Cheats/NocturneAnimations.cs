using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Nocturne;

internal enum NocturneAnim { ClimbUp, ClimbDown, EnterVent, ExitVent, Jump, Spawn }

internal static class NocturneAnimations
{
    private static AudioClip _slam;
    private static AudioClip _eject;
    private static MushroomMixupScreenTint _tint;

    private static readonly Dictionary<NocturneAnim, float> _loops = new Dictionary<NocturneAnim, float>();
    private static readonly List<NocturneAnim> _due = new List<NocturneAnim>(6);

    private static PlayerControl Me => PlayerControl.LocalPlayer;

    private static PlayerAnimations Anim
    {
        get { PlayerControl me = Me; return me != null && me.MyPhysics != null ? me.MyPhysics.Animations : null; }
    }

    private static bool Flip
    {
        get { PlayerControl me = Me; return me != null && me.cosmetics != null && me.cosmetics.FlipX; }
    }

    internal static bool Active(NocturneAnim a) => _loops.ContainsKey(a);
    internal static bool ClimbHeld => _loops.ContainsKey(NocturneAnim.ClimbUp) || _loops.ContainsKey(NocturneAnim.ClimbDown);
    internal static bool ClimbDownHeld => _loops.ContainsKey(NocturneAnim.ClimbDown);

    internal static void Toggle(NocturneAnim a)
    {
        if (_loops.Remove(a)) { if (_loops.Count == 0) RestoreIdle(); return; }
        if (a == NocturneAnim.ClimbUp) _loops.Remove(NocturneAnim.ClimbDown);
        else if (a == NocturneAnim.ClimbDown) _loops.Remove(NocturneAnim.ClimbUp);
        _loops[a] = 0f;
        Play(a);
    }

    internal static void ResetAll()
    {
        _loops.Clear();
        RestoreIdle();
    }

    internal static void Tick()
    {
        if (_loops.Count == 0) return;
        float now = Time.time;
        _due.Clear();
        foreach (KeyValuePair<NocturneAnim, float> kv in _loops)
            if (now >= kv.Value) _due.Add(kv.Key);
        for (int i = 0; i < _due.Count; i++)
        {
            NocturneAnim a = _due[i];
            if (!_loops.ContainsKey(a)) continue;
            Play(a);
            _loops[a] = now + Interval(a);
        }
    }

    internal static void ForceClimb(PlayerAnimations a)
    {
        if (a == null) return;
        try { if (!a.IsPlayingClimbAnimation()) a.PlayClimbAnimation(ClimbDownHeld); } catch { }
    }

    private static float Interval(NocturneAnim a)
    {
        switch (a)
        {
            case NocturneAnim.Jump: return 1f;
            case NocturneAnim.EnterVent:
            case NocturneAnim.ExitVent:
            case NocturneAnim.Spawn: return 1.5f;
            default: return 2f;
        }
    }

    private static void Play(NocturneAnim a)
    {
        switch (a)
        {
            case NocturneAnim.ClimbUp: RawClimb(false); break;
            case NocturneAnim.ClimbDown: RawClimb(true); break;
            case NocturneAnim.EnterVent: RawVent(true); break;
            case NocturneAnim.ExitVent: RawVent(false); break;
            case NocturneAnim.Jump: RawJump(); break;
            case NocturneAnim.Spawn: RawSpawn(); break;
        }
    }

    private static void RestoreIdle()
    {
        PlayerAnimations a = Anim;
        if (a != null) try { a.PlayIdleAnimation(); } catch { }
    }

    private static void RawClimb(bool down) { PlayerAnimations a = Anim; if (a != null) try { a.PlayClimbAnimation(down); } catch { } }
    private static void RawVent(bool enter) { PlayerAnimations a = Anim; if (a != null) Run(enter ? a.CoPlayEnterVentAnimation(0) : a.CoPlayExitVentAnimation()); }
    private static void RawJump() { PlayerAnimations a = Anim; if (a != null) Run(a.CoPlayJumpAnimation()); }
    private static void RawSpawn() { PlayerAnimations a = Anim; if (a != null) Run(a.CoPlaySpawnAnimation(Flip)); }

    private static void Run(IEnumerator co)
    {
        PlayerControl me = Me;
        if (me == null || me.MyPhysics == null || co == null) return;
        try { me.MyPhysics.StartCoroutine(co); } catch { }
    }

    internal static void AlertFlash()
    {
        try
        {
            HudManager h = HudManager.Instance;
            if (h != null && h.AlertFlash != null && h.AlertFlash.animator != null)
                h.AlertFlash.animator.SetTrigger("OnFlash");
        }
        catch { }
    }

    internal static void MushroomIn() { MushroomMixupScreenTint t = Tint(); if (t != null) try { t.Activate(); } catch { } }
    internal static void MushroomOut() { MushroomMixupScreenTint t = Tint(); if (t != null) try { t.Deactivate(); } catch { } }

    internal static void MeetingSting()
    {
        AudioClip c = Slam();
        SoundManager s = SoundManager.Instance;
        if (c != null && s != null) try { s.PlaySound(c, false, 0.7f, (AudioMixerGroup)null); } catch { }
    }

    internal static void EjectSfx()
    {
        AudioClip c = Eject();
        SoundManager s = SoundManager.Instance;
        if (c != null && s != null) try { s.PlaySoundImmediate(c, false, 0.8f, 1f, (AudioMixerGroup)null); } catch { }
    }

    private static MushroomMixupScreenTint Tint()
    {
        if (_tint != null) return _tint;
        try
        {
            HudManager h = HudManager.Instance;
            if (h != null)
            {
                MushroomMixupScreenTint t = h.GetComponentInChildren<MushroomMixupScreenTint>(true);
                if (t != null) return _tint = t;
            }
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MushroomMixupScreenTint>());
            if (all != null)
                for (int i = 0; i < all.Length; i++)
                {
                    MushroomMixupScreenTint t = all[i] != null ? all[i].TryCast<MushroomMixupScreenTint>() : null;
                    if (t != null) return _tint = t;
                }
        }
        catch { }
        return null;
    }

    private static AudioClip Slam()
    {
        if (_slam != null) return _slam;
        try
        {
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MeetingIntroAnimation>());
            if (all != null)
                for (int i = 0; i < all.Length; i++)
                {
                    MeetingIntroAnimation m = all[i] != null ? all[i].TryCast<MeetingIntroAnimation>() : null;
                    if (m != null && m.PlayerDeadSound != null) return _slam = m.PlayerDeadSound;
                }
        }
        catch { }
        return null;
    }

    private static AudioClip Eject()
    {
        if (_eject != null) return _eject;
        try
        {
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ExileController>());
            if (all != null)
                for (int i = 0; i < all.Length; i++)
                {
                    ExileController x = all[i] != null ? all[i].TryCast<ExileController>() : null;
                    if (x != null && x.TextSound != null) return _eject = x.TextSound;
                }
        }
        catch { }
        return null;
    }
}
