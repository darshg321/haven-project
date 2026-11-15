using System.Collections.Generic;
using UnityEngine;

namespace HavenDuel
{
    public static class Sfx
    {
        const float Master = 0.5f;
        const float MinGap = 0.08f;

        static AudioSource ui;
        static readonly Dictionary<AudioClip, float> lastPlayed = new();

        static bool TooSoon(AudioClip clip)
        {
            if (!clip) return true;
            if (lastPlayed.TryGetValue(clip, out float t) && Time.time - t < MinGap) return true;
            lastPlayed[clip] = Time.time;
            return false;
        }

        public static void At(AudioClip clip, Vector3 pos, float volume = 1f, float spatial = 1f)
        {
            if (TooSoon(clip)) return;
            var s = new GameObject("Sfx").AddComponent<AudioSource>();
            s.transform.position = pos;
            s.clip = clip;
            s.volume = volume * Master * (spatial < 0.5f ? 0.6f : 1f);
            s.spatialBlend = spatial;
            s.pitch = Random.Range(0.94f, 1.06f);
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = 50f;
            s.dopplerLevel = 0f;
            s.Play();
            Object.Destroy(s.gameObject, Mathf.Min(clip.length, 1.5f) / s.pitch + 0.1f);
        }

        public static void Ui(AudioClip clip, float volume = 1f)
        {
            if (TooSoon(clip)) return;
            if (!ui) ui = new GameObject("UiAudio").AddComponent<AudioSource>();
            ui.PlayOneShot(clip, volume * Master);
        }
    }
}
