using System;
using UnityEngine;

namespace DrumKit.Rhythm
{
    /// <summary>One timed hit the player is expected to land on a given piece.</summary>
    [Serializable]
    public struct ChartNote
    {
        [Tooltip("Time (seconds) from the start of the audio clip - directly comparable to Conductor.SongPositionSeconds.")]
        public float timeSeconds;
        public DrumPieceId targetPiece;
    }

    /// <summary>
    /// Data-driven description of a song's rhythm-mode chart: the track itself, its tempo,
    /// and the ordered list of notes the player must hit in time. Authored by hand in the
    /// Inspector for now (notes must stay sorted ascending by timeSeconds) - there is no
    /// beat-detection or external chart-format import in this project.
    /// </summary>
    [CreateAssetMenu(menuName = "Drum Kit/Rhythm/Song Chart", fileName = "NewSongChart")]
    public class SongChart : ScriptableObject
    {
        public AudioClip song;

        [Tooltip("Beats per minute - informational for now (notes are authored as absolute seconds), but kept for future beat-snapped tooling.")]
        public float bpm = 120f;

        [Tooltip("Informational for now: time (seconds) of the song's first true downbeat (many tracks have lead-in silence/a pickup phrase before beat 1). Not used in note timing math yet - notes are authored as raw clip-relative seconds, matching Conductor.SongPositionSeconds directly.")]
        public float startOffsetSeconds;

        [Tooltip("Must stay sorted ascending by timeSeconds.")]
        public ChartNote[] notes = Array.Empty<ChartNote>();
    }
}
