using System.Collections.Generic;
using UnityEngine;
using DrumKit.Pieces;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Editor-testing/authoring tool: while its own Conductor plays a reference track
    /// (typically an isolated drums stem - easier to tap along to than the full mix), every
    /// real strike on a registered DrumPiece is recorded as a ChartNote at the song's
    /// current position. Auto-saves as a new SongChart once the reference track finishes
    /// playing, so a VR session doesn't need to alt-tab out to stop it.
    ///
    /// The saved chart's song is deliberately a separate field from what Conductor plays
    /// during recording: stems from tools like Moises share the exact same timeline as the
    /// source mix, so timestamps captured against an isolated drums track transfer directly
    /// to the full song, which is what should actually play during real gameplay.
    /// </summary>
    public class RhythmChartRecorder : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmPieceRegistry registry;

        [Header("Resulting chart")]
        [Tooltip("What the saved SongChart's song should be - usually the full original mix, since the recording clip's timestamps line up with it exactly.")]
        [SerializeField] AudioClip finalGameplaySong;
        [SerializeField] float bpm = 120f;
        [SerializeField] float startOffsetSeconds;
        [SerializeField] string outputFolder = "Assets/DrumKit/Rhythm/Charts";
        [SerializeField] string outputName = "RecordedChart";

        readonly List<ChartNote> m_RecordedNotes = new();
        bool m_Recording;

        void Start()
        {
            foreach (DrumPieceId id in registry.KnownPieceIds)
            {
                if (registry.TryGetPiece(id, out DrumPiece piece))
                {
                    piece.OnStruck += HandleStrike;
                }
            }

            m_Recording = true;
            Debug.Log($"{nameof(RhythmChartRecorder)}: recording started - strike pieces along with the beat.");
        }

        void OnDestroy()
        {
            if (registry == null)
            {
                return;
            }

            foreach (DrumPieceId id in registry.KnownPieceIds)
            {
                if (registry.TryGetPiece(id, out DrumPiece piece))
                {
                    piece.OnStruck -= HandleStrike;
                }
            }
        }

        void Update()
        {
            // Past the lead-in, not paused, and no longer playing == the reference track
            // reached its natural end (a manual Pause() also stops IsPlayingClip, so that
            // must be excluded here or every pause would be mistaken for "finished").
            if (m_Recording && !conductor.IsPaused && conductor.SongPositionSeconds > 1f && !conductor.IsPlayingClip)
            {
                StopRecordingAndSaveChart();
            }
        }

        void HandleStrike(DrumPiece piece, float intensity01, Vector3 worldContactPoint)
        {
            if (!m_Recording || !conductor.IsStarted || conductor.IsPaused)
            {
                return;
            }

            if (!registry.TryGetId(piece, out DrumPieceId id))
            {
                return;
            }

            m_RecordedNotes.Add(new ChartNote { timeSeconds = conductor.SongPositionSeconds, targetPiece = id });
            Debug.Log($"{nameof(RhythmChartRecorder)}: recorded {id} at {conductor.SongPositionSeconds:F3}s (total {m_RecordedNotes.Count}).");
        }

        public void TogglePause()
        {
            if (conductor.IsPaused)
            {
                conductor.Resume();
            }
            else
            {
                conductor.Pause();
            }
        }

        /// <summary>Jumps backward to redo a section - discards any notes already recorded at/after the rewound-to position, since the redo will re-record them.</summary>
        public void Rewind(float seconds)
        {
            float newPosition = Mathf.Max(0f, conductor.SongPositionSeconds - seconds);
            int removed = m_RecordedNotes.RemoveAll(n => n.timeSeconds >= newPosition);
            conductor.SeekTo(newPosition);
            Debug.Log($"{nameof(RhythmChartRecorder)}: rewound to {newPosition:F2}s, discarded {removed} note(s) for redo (total now {m_RecordedNotes.Count}).");
        }

        /// <summary>Skips forward without discarding any notes - there's nothing to redo ahead of where you already were.</summary>
        public void FastForward(float seconds)
        {
            conductor.SeekRelative(seconds);
            Debug.Log($"{nameof(RhythmChartRecorder)}: skipped ahead to {conductor.SongPositionSeconds:F2}s.");
        }

        [ContextMenu("Stop Recording And Save Chart")]
        public void StopRecordingAndSaveChart()
        {
            if (!m_Recording)
            {
                return;
            }

            m_Recording = false;
            m_RecordedNotes.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));

#if UNITY_EDITOR
            var chart = ScriptableObject.CreateInstance<SongChart>();
            chart.song = finalGameplaySong;
            chart.bpm = bpm;
            chart.startOffsetSeconds = startOffsetSeconds;
            chart.notes = m_RecordedNotes.ToArray();

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                Debug.LogError($"{nameof(RhythmChartRecorder)}: output folder '{outputFolder}' does not exist.", this);
                return;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{outputName}.asset");
            AssetDatabase.CreateAsset(chart, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(RhythmChartRecorder)}: saved {m_RecordedNotes.Count} notes to '{path}'.", chart);
#else
            Debug.LogWarning($"{nameof(RhythmChartRecorder)}: saving is Editor-only.");
#endif
        }
    }
}
