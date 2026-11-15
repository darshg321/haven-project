using UnityEngine;

namespace HavenDuel.Combat
{
    /// One-shot sounds. 3D ones fall off with distance; UI ones play flat.
    public static class Sfx
    {
        static AudioSource ui;

        /// spatial: 1 = fully 3D at pos, 0 = flat (your own throws).
        public static void At(AudioClip clip, Vector3 pos, float volume = 1f, float spatial = 1f, float pitchJitter = 0.06f)
        {
            if (!clip) return;
            var go = new GameObject("Sfx " + clip.name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.spatialBlend = spatial;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 2f; s.maxDistance = 60f; s.dopplerLevel = 0f;
            s.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, s.pitch) + 0.1f);
        }

        public static void Ui(AudioClip clip, float volume = 1f)
        {
            if (!clip) return;
            if (!ui)
            {
                var go = new GameObject("UiAudio");
                Object.DontDestroyOnLoad(go);
                ui = go.AddComponent<AudioSource>();
                ui.spatialBlend = 0f;
            }
            ui.PlayOneShot(clip, volume);
        }
    }
}
