using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nocturne.Patches;

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
internal static class LobbyMusicMutePatch
{
    private sealed class AudioState
    {
        public AudioSource Source;
        public float Volume;
        public bool Mute;
    }

    private struct Verdict
    {
        public int Clip;
        public bool Music;
    }

    private const float ScanIntervalSeconds = 0.15f;
    private static readonly Dictionary<int, AudioState> mutedSources = new Dictionary<int, AudioState>();
    private static readonly Dictionary<int, Verdict> verdicts = new Dictionary<int, Verdict>();
    private static readonly HashSet<int> activeIds = new HashSet<int>();
    private static float nextScanAt;

    public static void Postfix(LobbyBehaviour __instance)
    {
        RefreshNow(__instance);
    }

    internal static void RefreshNow(LobbyBehaviour lobby, bool force = false)
    {
        try
        {
            if (lobby == null || !NocturneConfig.MuteLobbyMusic.Value || !IsLobbyScene())
            {
                RestoreAll();
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (!force && now < nextScanAt)
            {
                ReapplyMute();
                return;
            }

            nextScanAt = now + ScanIntervalSeconds;
            AudioSource[] sources = Object.FindObjectsOfType<AudioSource>(includeInactive: true);
            if (sources == null || sources.Length == 0)
            {
                return;
            }

            if (verdicts.Count > 512)
            {
                verdicts.Clear();
            }

            activeIds.Clear();
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null)
                {
                    continue;
                }

                int instanceId = source.GetInstanceID();
                AudioClip clip = source.clip;
                int clipId = clip != null ? clip.GetInstanceID() : 0;
                if (!verdicts.TryGetValue(instanceId, out Verdict verdict) || verdict.Clip != clipId)
                {
                    verdict = new Verdict { Clip = clipId, Music = ShouldMuteLobbyAudio(source) };
                    verdicts[instanceId] = verdict;
                }

                if (!verdict.Music)
                {
                    continue;
                }

                activeIds.Add(instanceId);
                if (!mutedSources.ContainsKey(instanceId))
                {
                    mutedSources[instanceId] = new AudioState
                    {
                        Source = source,
                        Volume = source.volume,
                        Mute = source.mute,
                    };
                }

                ApplyMute(source);
            }

            TrimMutedSources(activeIds);
        }
        catch (Exception error)
        {
            NocturnePlugin.Logger?.LogWarning($"Lobby music mute failed: {error.Message}");
        }
    }

    internal static void RestoreAll()
    {
        if (mutedSources.Count == 0)
        {
            nextScanAt = 0f;
            return;
        }

        foreach (KeyValuePair<int, AudioState> pair in mutedSources)
        {
            AudioState state = pair.Value;
            AudioSource source = state?.Source;
            if (source == null)
            {
                continue;
            }

            source.mute = state.Mute;
            source.volume = state.Volume;
        }

        mutedSources.Clear();
        nextScanAt = 0f;
    }

    private static bool IsLobbyScene()
    {
        return LobbyBehaviour.Instance != null && ShipStatus.Instance == null;
    }

    private static void ReapplyMute()
    {
        if (mutedSources.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<int, AudioState> pair in mutedSources)
        {
            AudioSource source = pair.Value?.Source;
            if (source != null)
            {
                ApplyMute(source);
            }
        }
    }

    private static void ApplyMute(AudioSource source)
    {
        source.mute = true;
        source.volume = 0f;
    }

    private static bool ShouldMuteLobbyAudio(AudioSource source)
    {
        if (source == null)
        {
            return false;
        }

        string identity = AudioId(source);
        if (identity.Contains("ambient") || identity.Contains("ambience"))
        {
            return false;
        }

        return identity.Contains("music")
            || identity.Contains("theme")
            || identity.Contains("bgm")
            || identity.Contains("song")
            || identity.Contains("soundtrack")
            || identity.Contains("title");
    }

    private static string AudioId(AudioSource source)
    {
        string sourceName = (source.name ?? string.Empty).ToLowerInvariant();
        string clipName = source.clip != null ? (source.clip.name ?? string.Empty).ToLowerInvariant() : string.Empty;
        string path = string.Empty;
        Transform transform = source.transform;
        int depth = 0;
        while (transform != null && depth < 4)
        {
            string name = transform.name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                path += " " + name.ToLowerInvariant();
            }

            transform = transform.parent;
            depth++;
        }

        return sourceName + " " + clipName + path;
    }

    private static void TrimMutedSources(HashSet<int> activeIds)
    {
        if (mutedSources.Count == 0)
        {
            return;
        }

        List<int> staleIds = null;
        foreach (KeyValuePair<int, AudioState> pair in mutedSources)
        {
            AudioState state = pair.Value;
            AudioSource source = state?.Source;
            bool stillActive = activeIds != null && activeIds.Contains(pair.Key);
            if (source != null && stillActive)
            {
                continue;
            }

            if (source != null)
            {
                source.mute = state.Mute;
                source.volume = state.Volume;
            }

            staleIds ??= new List<int>();
            staleIds.Add(pair.Key);
        }

        if (staleIds == null)
        {
            return;
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            mutedSources.Remove(staleIds[i]);
        }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class LobbyMusicStartPatch
{
    public static void Postfix(LobbyBehaviour __instance)
    {
        LobbyMusicMutePatch.RefreshNow(__instance, force: true);
    }
}
