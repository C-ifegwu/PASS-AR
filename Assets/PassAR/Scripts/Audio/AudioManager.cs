using System;
using UnityEngine;

namespace PassAR
{
    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        public struct SoundEntry
        {
            public SoundId id;
            public AudioClip clip;
        }

        [SerializeField] private AudioSource effectsSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private SoundEntry[] sounds;

        private AudioClip Find(SoundId id)
        {
            foreach (SoundEntry entry in sounds)
                if (entry.id == id) return entry.clip;
            return null;
        }

        public void Play(SoundId id)
        {
            AudioClip clip = Find(id);
            if (clip != null) effectsSource.PlayOneShot(clip);
        }

        /// <summary>Starts or stops a looping sound (the spray hiss).</summary>
        public void SetLoop(SoundId id, bool on)
        {
            if (!on)
            {
                loopSource.Stop();
                return;
            }

            AudioClip clip = Find(id);
            if (clip == null) return;
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.Play();
        }

        /// <summary>Mutes everything, including the fire sound that lives on the fire itself (FR-20).</summary>
        public void SetMuted(bool muted)
        {
            AudioListener.volume = muted ? 0f : 1f;
        }
    }
}
