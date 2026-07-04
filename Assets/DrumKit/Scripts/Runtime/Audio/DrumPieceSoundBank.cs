using System;
using UnityEngine;

namespace DrumKit.Audio
{
    /// <summary>
    /// One velocity-layered sample set (e.g. "soft" vs "hard" hits). Clips within a
    /// layer are chosen round-robin to avoid an obviously repeating "machine-gun" sound.
    /// </summary>
    [Serializable]
    public class VelocityLayer
    {
        [Tooltip("Normalized strike intensity (0..1) range this layer covers.")]
        [Range(0f, 1f)] public float minIntensity;
        [Range(0f, 1f)] public float maxIntensity = 1f;

        [Tooltip("Candidate clips for this layer, picked round-robin on each hit.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        public Vector2 volumeRange = new Vector2(0.9f, 1f);
        public Vector2 pitchSemitoneJitter = new Vector2(-0.5f, 0.5f);

        public bool Contains(float intensity01) => intensity01 >= minIntensity && intensity01 <= maxIntensity;
    }

    /// <summary>
    /// Data-driven description of how a single drum piece sounds. New instruments are
    /// added by creating a new asset, not by writing code - DrumPiece/DrumVoicePool never
    /// need to know which physical piece they belong to.
    /// </summary>
    [CreateAssetMenu(menuName = "Drum Kit/Drum Piece Sound Bank", fileName = "NewDrumPieceSoundBank")]
    public class DrumPieceSoundBank : ScriptableObject
    {
        [Tooltip("Velocity layers, ordered low-intensity to high-intensity. Must cover the full 0..1 range.")]
        public VelocityLayer[] layers = Array.Empty<VelocityLayer>();

        [Tooltip("How long a Choke() fade-out takes, in seconds.")]
        public float chokeFadeSeconds = 0.08f;

        [Tooltip("Optional: how much brighter (positive semitones) an edge hit sounds vs. a center hit. " +
                 "Leave at 0 for pieces where impact position doesn't matter.")]
        public float edgeBrightnessSemitones;

        int m_RoundRobinIndex;

        /// <summary>
        /// Picks a clip for the given normalized intensity (0..1) and radial impact
        /// position (0 = center, 1 = edge), returning the playback volume and pitch to use.
        /// </summary>
        public bool TryPickClip(float intensity01, float radialPosition01, out AudioClip clip, out float volume, out float pitch)
        {
            VelocityLayer layer = SelectLayer(intensity01);
            if (layer == null || layer.clips.Length == 0)
            {
                clip = null;
                volume = 0f;
                pitch = 1f;
                return false;
            }

            m_RoundRobinIndex = (m_RoundRobinIndex + 1) % layer.clips.Length;
            clip = layer.clips[m_RoundRobinIndex];

            volume = UnityEngine.Random.Range(layer.volumeRange.x, layer.volumeRange.y);

            float jitterSemitones = UnityEngine.Random.Range(layer.pitchSemitoneJitter.x, layer.pitchSemitoneJitter.y);
            float brightnessSemitones = edgeBrightnessSemitones * Mathf.Clamp01(radialPosition01);
            pitch = SemitonesToPitch(jitterSemitones + brightnessSemitones);
            return true;
        }

        VelocityLayer SelectLayer(float intensity01)
        {
            VelocityLayer closest = null;
            float closestDistance = float.MaxValue;

            foreach (VelocityLayer layer in layers)
            {
                if (layer.Contains(intensity01))
                {
                    return layer;
                }

                float distance = intensity01 < layer.minIntensity ? layer.minIntensity - intensity01 : intensity01 - layer.maxIntensity;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = layer;
                }
            }

            return closest;
        }

        public static float SemitonesToPitch(float semitones) => Mathf.Pow(2f, semitones / 12f);
    }
}
