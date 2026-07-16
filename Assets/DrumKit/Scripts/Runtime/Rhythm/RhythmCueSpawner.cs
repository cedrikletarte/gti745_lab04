using System.Collections.Generic;
using UnityEngine;
using DrumKit.Pieces;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Watches the chart for upcoming notes and spawns a CueRingVisual timed to close on
    /// each one, positioned on the target piece. Pools rings since rhythm gameplay can
    /// fire many in quick succession.
    /// </summary>
    public class RhythmCueSpawner : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmPieceRegistry registry;
        [SerializeField] CueRingVisual ringPrefab;
        [Tooltip("How long (seconds) before a note's time the ring starts closing in.")]
        [SerializeField] float approachSeconds = 0.9f;

        readonly Queue<CueRingVisual> m_Pool = new();
        readonly List<CueRingVisual> m_Active = new();
        int m_NextNoteIndex;

        public int ActiveRingCount => m_Active.Count;
        public int PooledRingCount => m_Pool.Count;
        public int NextNoteIndex => m_NextNoteIndex;

        void Update()
        {
            if (!conductor.IsStarted)
            {
                return;
            }

            SongChart chart = conductor.Chart;
            float now = conductor.SongPositionSeconds;

            while (m_NextNoteIndex < chart.notes.Length && chart.notes[m_NextNoteIndex].timeSeconds - approachSeconds <= now)
            {
                SpawnCue(chart.notes[m_NextNoteIndex]);
                m_NextNoteIndex++;
            }

            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                if (m_Active[i].IsFinished)
                {
                    m_Pool.Enqueue(m_Active[i]);
                    m_Active.RemoveAt(i);
                }
            }
        }

        void SpawnCue(ChartNote note)
        {
            if (!registry.TryGetPiece(note.targetPiece, out DrumPiece piece))
            {
                return;
            }

            if (!registry.TryGetLocalFlatAxis(piece, out Vector3 localFlatAxis))
            {
                localFlatAxis = Vector3.up;
            }

            // Geometric flat-face normal (see RhythmPieceRegistry), not DrumPiece.SurfaceUp:
            // that axis is tuned for hit-detection dot products and can point in an
            // import-artifact direction that has nothing to do with the visual "flat top".
            Vector3 worldFlatNormal = piece.transform.TransformDirection(localFlatAxis);
            float diameter = 2f * Vector3.ProjectOnPlane(piece.GetComponent<Collider>().bounds.extents, worldFlatNormal).magnitude;

            CueRingVisual ring = m_Pool.Count > 0 ? m_Pool.Dequeue() : Instantiate(ringPrefab, transform);
            ring.Begin(conductor, note.timeSeconds, piece, localFlatAxis, diameter, DrumPalette.GetColor(note.targetPiece));
            m_Active.Add(ring);
        }
    }
}
