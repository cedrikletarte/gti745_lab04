namespace DrumKit.Rhythm
{
    /// <summary>
    /// Stable identifier for a physical drum piece, used by data assets (SongChart) that
    /// can't hold a scene reference to the actual DrumPiece GameObject. Resolved to a real
    /// DrumPiece at runtime by RhythmPieceRegistry, which matches these names against the
    /// same hierarchy names DrumKitSetupTool uses to configure the kit.
    /// </summary>
    public enum DrumPieceId
    {
        BassDrum,
        SnareDrum,
        LeftTom,
        RightTom,
        FloorTom,
        HiHat,
        LeftCrash,
        RightCrash,
    }
}
