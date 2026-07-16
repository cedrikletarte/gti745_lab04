using UnityEngine;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Single source of truth for the per-instrument colour coding. Both the physical piece
    /// tint (RhythmPieceRegistry) and the approaching cue rings (CueRingVisual, via
    /// RhythmCueSpawner) read their colour from here, so an anneau always matches the drum it
    /// tells the player to strike - the whole point of the colour cue.
    ///
    /// Keyed by the stable DrumPieceId enum rather than a scene reference, so the mapping holds
    /// identically across the Solo and Rythme scenes.
    /// </summary>
    public static class DrumPalette
    {
        // Eight well-separated hues so neighbouring pieces never read as the same colour.
        static readonly Color[] k_Colors =
        {
            new Color(1.00f, 0.20f, 0.20f), // BassDrum   - red
            new Color(1.00f, 0.55f, 0.10f), // SnareDrum  - orange
            new Color(0.65f, 0.90f, 0.20f), // LeftTom    - lime
            new Color(0.20f, 0.85f, 0.40f), // RightTom   - green
            new Color(0.20f, 0.55f, 1.00f), // FloorTom   - blue
            new Color(1.00f, 0.90f, 0.20f), // HiHat      - yellow
            new Color(1.00f, 0.30f, 0.80f), // LeftCrash  - magenta
            new Color(0.65f, 0.35f, 1.00f), // RightCrash - purple
        };

        static readonly Color k_Fallback = Color.white;

        /// <summary>The colour assigned to a given drum piece. Opaque (alpha 1); callers that
        /// need translucency (the rings) apply their own alpha.</summary>
        public static Color GetColor(DrumPieceId id)
        {
            int index = (int)id;
            return index >= 0 && index < k_Colors.Length ? k_Colors[index] : k_Fallback;
        }
    }
}
