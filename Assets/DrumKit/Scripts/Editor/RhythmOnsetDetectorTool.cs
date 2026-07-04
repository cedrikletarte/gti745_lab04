using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DrumKit.Rhythm;

namespace DrumKit.EditorTools
{
    /// <summary>
    /// Semi-automated chart authoring: scans an isolated drum stem for percussive onsets
    /// (sudden jumps in short-time energy - simple, but reliable on a clean, already-
    /// isolated drums track) and writes one ChartNote per detected hit into a new SongChart,
    /// all defaulted to the same placeholder piece (SnareDrum).
    ///
    /// This turns full manual transcription into a review pass: open the resulting asset,
    /// scrub the isolated stem (in Audacity/Sonic Visualiser, or Unity's own asset preview)
    /// to each listed Time Seconds, and pick the real Target Piece from the dropdown Unity's
    /// default array Inspector already provides for the notes list - no custom UI needed.
    /// For a moment where several pieces hit together, duplicate that entry and give the
    /// copy a different targetPiece; SongChart already supports several notes sharing one
    /// timeSeconds value.
    /// </summary>
    public static class RhythmOnsetDetectorTool
    {
        const float FrameSeconds = 0.01f; // ~10ms analysis frames
        const float Sensitivity = 1.6f; // how far above the local-mean flux a peak must rise
        const float MinFluxFloor = 0.01f; // absolute floor so near-silence can't false-trigger
        const float MinIntervalSeconds = 0.08f; // debounce: ignore a second peak this soon after the last
        const int ThresholdWindowFrames = 20; // ~200ms of local context either side, for the adaptive threshold

        [MenuItem("Tools/Drum Kit/Detect Drum Onsets From Selected Clip")]
        static void DetectFromSelection()
        {
            var clip = Selection.activeObject as AudioClip;
            if (clip == null)
            {
                Debug.LogError("[Drum Kit] Select an AudioClip (the isolated drums stem) in the Project window first, then re-run this command.");
                return;
            }

            DetectAndSaveChart(clip);
        }

        /// <summary>Callable directly (e.g. from other editor tooling/automation) without going through Selection.</summary>
        public static SongChart DetectAndSaveChart(AudioClip clip)
        {
            List<float> onsets = DetectOnsets(clip);
            Debug.Log($"[Drum Kit] Detected {onsets.Count} onset(s) in '{clip.name}' ({clip.length:F1}s).");

            if (onsets.Count == 0)
            {
                Debug.LogWarning("[Drum Kit] No onsets detected - the clip may be silent, or too quiet for the current sensitivity.");
                return null;
            }

            const string chartFolder = "Assets/DrumKit/Rhythm/Charts";
            EnsureFolder(chartFolder);

            var chart = ScriptableObject.CreateInstance<SongChart>();
            chart.song = clip;
            chart.notes = onsets.Select(t => new ChartNote { timeSeconds = t, targetPiece = DrumPieceId.SnareDrum }).ToArray();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{chartFolder}/{clip.name}_DetectedOnsets.asset");
            AssetDatabase.CreateAsset(chart, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Drum Kit] Saved to '{path}'. Every note defaults to SnareDrum as a placeholder - " +
                      "open the asset, scrub the clip to each Time Seconds, and set the real Target Piece " +
                      "(duplicate an entry for simultaneous hits).", chart);
            Selection.activeObject = chart;
            return chart;
        }

        static List<float> DetectOnsets(AudioClip clip)
        {
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }

            int channels = Mathf.Max(1, clip.channels);
            int sampleRate = clip.frequency;
            var samples = new float[clip.samples * channels];
            clip.GetData(samples, 0);

            int frameSize = Mathf.Max(1, Mathf.RoundToInt(sampleRate * FrameSeconds));
            int frameCount = samples.Length / channels / frameSize;
            if (frameCount < 3)
            {
                return new List<float>();
            }

            var energy = new float[frameCount];
            for (int f = 0; f < frameCount; f++)
            {
                double sumSquares = 0;
                int baseIndex = f * frameSize * channels;
                int sampleCount = frameSize * channels;
                for (int i = 0; i < sampleCount; i++)
                {
                    int index = baseIndex + i;
                    if (index >= samples.Length)
                    {
                        break;
                    }

                    float s = samples[index];
                    sumSquares += s * (double)s;
                }

                energy[f] = (float)Math.Sqrt(sumSquares / sampleCount);
            }

            // Onset strength: only count energy *increases* frame-to-frame (a hit's attack),
            // not decreases (a hit's decay/ring-out shouldn't itself register as a new onset).
            var flux = new float[frameCount];
            for (int f = 1; f < frameCount; f++)
            {
                flux[f] = Mathf.Max(0f, energy[f] - energy[f - 1]);
            }

            var onsets = new List<float>();
            float lastOnsetTime = float.NegativeInfinity;

            for (int f = 1; f < frameCount - 1; f++)
            {
                int lo = Mathf.Max(0, f - ThresholdWindowFrames);
                int hi = Mathf.Min(frameCount - 1, f + ThresholdWindowFrames);
                float sum = 0f;
                for (int k = lo; k <= hi; k++)
                {
                    sum += flux[k];
                }

                float localMean = sum / (hi - lo + 1);
                float threshold = Mathf.Max(localMean * Sensitivity, MinFluxFloor);

                bool isLocalPeak = flux[f] >= flux[f - 1] && flux[f] >= flux[f + 1];
                if (!isLocalPeak || flux[f] <= threshold)
                {
                    continue;
                }

                float time = f * frameSize / (float)sampleRate;
                if (time - lastOnsetTime < MinIntervalSeconds)
                {
                    continue;
                }

                onsets.Add(time);
                lastOnsetTime = time;
            }

            return onsets;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
