using System.Collections.Generic;
using UnityEngine;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Single source of truth for the per-instrument colour coding. Both the physical piece
    /// tint (RhythmPieceRegistry) and the approaching cue rings (CueRingVisual, via
    /// RhythmCueSpawner) read their colour from here, so an anneau always matches the drum it
    /// tells the player to strike - the whole point of the colour cue.
    ///
    /// Only the drum shells (which have a paintable "Main Color" material) are colour-coded.
    /// The cymbals - hi-hat and the two crashes - are deliberately left out: they keep their
    /// natural metal look and their cue rings keep the CueRingMaterial's original colour.
    /// Keyed by the stable DrumPieceId enum so the mapping holds identically across the Solo
    /// and Rythme scenes.
    /// </summary>
    public static class DrumPalette
    {
        static readonly Dictionary<DrumPieceId, Color> k_Colors = new()
        {
            { DrumPieceId.BassDrum,  new Color(1.00f, 0.20f, 0.20f) }, // red
            { DrumPieceId.SnareDrum, new Color(1.00f, 0.55f, 0.10f) }, // orange
            { DrumPieceId.LeftTom,   new Color(1.00f, 0.90f, 0.20f) }, // yellow
            { DrumPieceId.RightTom,  new Color(0.20f, 0.85f, 0.40f) }, // green
            { DrumPieceId.FloorTom,  new Color(0.20f, 0.55f, 1.00f) }, // blue
        };

        /// <summary>
        /// The colour coding for a piece. Returns false for pieces that are deliberately not
        /// colour-coded (the cymbals) - callers should then leave that instrument and its rings
        /// with their original look. The colour is opaque (alpha 1); callers needing
        /// translucency (the rings) apply their own alpha.
        /// </summary>
        public static bool TryGetColor(DrumPieceId id, out Color color) => k_Colors.TryGetValue(id, out color);
    }
}
