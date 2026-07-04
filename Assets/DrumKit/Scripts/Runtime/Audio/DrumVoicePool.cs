using System.Collections;
using UnityEngine;

namespace DrumKit.Audio
{
    /// <summary>
    /// Owns a small pool of pre-configured AudioSources for a single drum piece, so
    /// fast repeated hits (flams, rolls) overlap instead of cutting each other off.
    /// Also implements Choke(): a fast fade + low-pass sweep on this piece's currently
    /// ringing voices, imitating a hand muting a cymbal instead of a hard stop.
    /// </summary>
    [DisallowMultipleComponent]
    public class DrumVoicePool : MonoBehaviour
    {
        [SerializeField, Range(1, 8), Tooltip("Number of overlapping voices this piece can play at once.")]
        int voiceCount = 4;

        [SerializeField, Tooltip("3D spatial blend applied to every pooled voice (0 = 2D, 1 = fully 3D).")]
        [Range(0f, 1f)] float spatialBlend = 1f;

        [SerializeField] float minDistance = 0.3f;
        [SerializeField] float maxDistance = 8f;

        AudioSource[] m_Voices;
        AudioLowPassFilter[] m_LowPassFilters;
        Coroutine[] m_FadeRoutines;
        int m_NextVoiceIndex;

        const float OpenLowPassCutoff = 22000f;

        void Awake()
        {
            m_Voices = new AudioSource[voiceCount];
            m_LowPassFilters = new AudioLowPassFilter[voiceCount];
            m_FadeRoutines = new Coroutine[voiceCount];

            for (int i = 0; i < voiceCount; i++)
            {
                GameObject voiceObject = new GameObject($"Voice_{i}");
                voiceObject.transform.SetParent(transform, false);

                AudioSource source = voiceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = spatialBlend;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.rolloffMode = AudioRolloffMode.Logarithmic;

                AudioLowPassFilter lowPass = voiceObject.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = OpenLowPassCutoff;

                m_Voices[i] = source;
                m_LowPassFilters[i] = lowPass;
            }
        }

        /// <summary>Plays a clip on the next available voice, stealing the oldest voice if the pool is full.</summary>
        public void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (clip == null)
            {
                return;
            }

            int index = m_NextVoiceIndex;
            m_NextVoiceIndex = (m_NextVoiceIndex + 1) % m_Voices.Length;

            if (m_FadeRoutines[index] != null)
            {
                StopCoroutine(m_FadeRoutines[index]);
                m_FadeRoutines[index] = null;
            }

            AudioSource source = m_Voices[index];
            m_LowPassFilters[index].cutoffFrequency = OpenLowPassCutoff;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        /// <summary>Fades out and low-pass-mutes every voice of this piece that is currently ringing.</summary>
        public void Choke(float fadeSeconds)
        {
            for (int i = 0; i < m_Voices.Length; i++)
            {
                if (!m_Voices[i].isPlaying)
                {
                    continue;
                }

                if (m_FadeRoutines[i] != null)
                {
                    StopCoroutine(m_FadeRoutines[i]);
                }

                m_FadeRoutines[i] = StartCoroutine(FadeAndStop(i, fadeSeconds));
            }
        }

        IEnumerator FadeAndStop(int voiceIndex, float fadeSeconds)
        {
            AudioSource source = m_Voices[voiceIndex];
            AudioLowPassFilter lowPass = m_LowPassFilters[voiceIndex];

            float startVolume = source.volume;
            float elapsed = 0f;
            fadeSeconds = Mathf.Max(fadeSeconds, 0.01f);

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeSeconds;
                source.volume = Mathf.Lerp(startVolume, 0f, t);
                lowPass.cutoffFrequency = Mathf.Lerp(OpenLowPassCutoff, 400f, t);
                yield return null;
            }

            source.Stop();
            source.volume = startVolume;
            lowPass.cutoffFrequency = OpenLowPassCutoff;
            m_FadeRoutines[voiceIndex] = null;
        }
    }
}
