using UnityEngine;

namespace Flynn.Npc
{
    // Simple synthesized audio for codex/dialogue feedback. No audio clip assets
    // needed — tones are generated procedurally and cached.
    public static class CodexAudio
    {
        private static AudioSource _source;
        private static AudioClip _typewriterClip;
        private static AudioClip _chimeClip;
        private static AudioClip _trustUpClip;
        private static AudioClip _secretUnlockClip;

        private static AudioSource Source
        {
            get
            {
                if (_source == null)
                {
                    var go = new GameObject("[CodexAudio]");
                    Object.DontDestroyOnLoad(go);
                    _source = go.AddComponent<AudioSource>();
                    _source.playOnAwake = false;
                    _source.spatialBlend = 0f; // 2D
                }
                return _source;
            }
        }

        // Very short tick for typewriter — soft, low volume.
        public static void PlayTypewriterTick()
        {
            if (_typewriterClip == null)
                _typewriterClip = CreateTone(800f, 0.02f, 0.04f);
            Source.PlayOneShot(_typewriterClip, 0.3f);
        }

        // Gentle high chime for new codex entry.
        public static void PlayCodexChime()
        {
            if (_chimeClip == null)
                _chimeClip = CreateTone(1318f, 0.35f, 0.15f); // E6
            Source.PlayOneShot(_chimeClip, 0.5f);
        }

        // Soft ascending two-tone for trust increase.
        public static void PlayTrustUp()
        {
            if (_trustUpClip == null)
                _trustUpClip = CreateTwoTone(523f, 784f, 0.08f, 0.12f, 0.12f); // C5 → G5
            Source.PlayOneShot(_trustUpClip, 0.4f);
        }

        // Deeper resonant tone for secret unlock.
        public static void PlaySecretUnlock()
        {
            if (_secretUnlockClip == null)
                _secretUnlockClip = CreateTwoTone(392f, 523f, 0.15f, 0.4f, 0.15f); // G4 → C5
            Source.PlayOneShot(_secretUnlockClip, 0.6f);
        }

        // ── Procedural clip generation ────────────────────────────────────────

        private static AudioClip CreateTone(float frequency, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.Max(1, (int)(sampleRate * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / duration); // linear fade
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }
            var clip = AudioClip.Create("tone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateTwoTone(float freq1, float freq2, float dur1, float dur2, float volume)
        {
            int sampleRate = 44100;
            int samples1 = (int)(sampleRate * dur1);
            int samples2 = (int)(sampleRate * dur2);
            int total = samples1 + samples2;
            float[] data = new float[total];
            for (int i = 0; i < samples1; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / dur1);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq1 * t) * volume * envelope;
            }
            for (int i = 0; i < samples2; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (t / dur2);
                data[samples1 + i] = Mathf.Sin(2f * Mathf.PI * freq2 * t) * volume * envelope;
            }
            var clip = AudioClip.Create("twotone", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
