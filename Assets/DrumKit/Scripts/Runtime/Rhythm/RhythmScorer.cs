using System;
using System.Collections.Generic;
using UnityEngine;
using DrumKit.Pieces;

namespace DrumKit.Rhythm
{
    public enum Judgement
    {
        Perfect,
        Good,
        Miss,
    }

    /// <summary>
    /// Judges real strikes (via DrumPiece.OnStruck) against the chart's expected notes,
    /// comparing timestamps on Conductor.SongPositionSeconds (never Time.time). A pure
    /// observer - never calls into DrumPiece/DrumStriker beyond subscribing to the one
    /// event they already expose.
    /// </summary>
    public class RhythmScorer : MonoBehaviour
    {
        [SerializeField] Conductor conductor;
        [SerializeField] RhythmPieceRegistry registry;
        [Tooltip("Max time (seconds) from a note's exact time to still count as Perfect.")]
        [SerializeField] float perfectWindow = 0.05f;
        [Tooltip("Max time (seconds) from a note's exact time to still count as Good (wider than Perfect).")]
        [SerializeField] float goodWindow = 0.12f;

        readonly Dictionary<DrumPieceId, List<ChartNote>> m_NotesByPiece = new();
        readonly Dictionary<DrumPieceId, int> m_NextIndex = new();

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        public event Action<DrumPieceId, Judgement> OnNoteJudged;

        void Start()
        {
            BuildNoteLookup();

            foreach (DrumPieceId id in registry.KnownPieceIds)
            {
                if (registry.TryGetPiece(id, out DrumPiece piece))
                {
                    piece.OnStruck += HandleStrike;
                }
            }
        }

        void OnDestroy()
        {
            foreach (DrumPieceId id in registry.KnownPieceIds)
            {
                if (registry.TryGetPiece(id, out DrumPiece piece))
                {
                    piece.OnStruck -= HandleStrike;
                }
            }
        }

        void BuildNoteLookup()
        {
            foreach (ChartNote note in conductor.Chart.notes)
            {
                if (!m_NotesByPiece.TryGetValue(note.targetPiece, out List<ChartNote> list))
                {
                    list = new List<ChartNote>();
                    m_NotesByPiece[note.targetPiece] = list;
                }

                list.Add(note);
                m_NextIndex.TryAdd(note.targetPiece, 0);
            }
        }

        void Update()
        {
            if (!conductor.IsStarted)
            {
                return;
            }

            // Any note whose Good window has fully elapsed without a matching strike is a Miss.
            foreach (DrumPieceId id in new List<DrumPieceId>(m_NotesByPiece.Keys))
            {
                List<ChartNote> notes = m_NotesByPiece[id];
                int idx = m_NextIndex[id];
                if (idx < notes.Count && conductor.SongPositionSeconds > notes[idx].timeSeconds + goodWindow)
                {
                    Judge(id, Judgement.Miss);
                }
            }
        }

        void HandleStrike(DrumPiece piece, float intensity01, Vector3 worldContactPoint)
        {
            if (!registry.TryGetId(piece, out DrumPieceId id) || !m_NotesByPiece.TryGetValue(id, out List<ChartNote> notes))
            {
                return;
            }

            int idx = m_NextIndex[id];
            if (idx >= notes.Count)
            {
                return;
            }

            float delta = Mathf.Abs(conductor.SongPositionSeconds - notes[idx].timeSeconds);
            if (delta <= perfectWindow)
            {
                Judge(id, Judgement.Perfect);
            }
            else if (delta <= goodWindow)
            {
                Judge(id, Judgement.Good);
            }
            // Otherwise: off-chart/extra hit, ignored (no penalty in MVP).
        }

        void Judge(DrumPieceId id, Judgement judgement)
        {
            m_NextIndex[id]++;

            switch (judgement)
            {
                case Judgement.Perfect:
                    Score += 100;
                    Combo++;
                    break;
                case Judgement.Good:
                    Score += 50;
                    Combo++;
                    break;
                case Judgement.Miss:
                    Combo = 0;
                    break;
            }

            MaxCombo = Mathf.Max(MaxCombo, Combo);
            OnNoteJudged?.Invoke(id, judgement);
        }
    }
}
