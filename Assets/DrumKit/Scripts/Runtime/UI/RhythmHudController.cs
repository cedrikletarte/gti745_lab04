using TMPro;
using UnityEngine;
using DrumKit.Rhythm;

namespace DrumKit.UI
{
    /// <summary>Minimal MVP HUD: score + combo text, refreshed on every judged note.</summary>
    public class RhythmHudController : MonoBehaviour
    {
        [SerializeField] RhythmScorer scorer;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text comboText;

        void OnEnable() => scorer.OnNoteJudged += HandleNoteJudged;
        void OnDisable() => scorer.OnNoteJudged -= HandleNoteJudged;

        void HandleNoteJudged(DrumPieceId id, Judgement judgement)
        {
            scoreText.text = $"Score: {scorer.Score}";
            comboText.text = judgement == Judgement.Miss ? "Combo: 0" : $"Combo: {scorer.Combo}";
        }
    }
}
