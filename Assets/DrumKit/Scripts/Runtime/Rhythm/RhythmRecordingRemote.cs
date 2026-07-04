using UnityEngine;
using UnityEngine.XR;
using TMPro;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// VR transport controls for RhythmChartRecorder. Both hands are occupied by drumsticks
    /// during a recording pass, so this reads the controllers' face buttons directly
    /// (bypassing the project's Input Action bindings entirely, to avoid touching whatever
    /// they're already wired to) rather than requiring a menu: left controller
    /// pauses/rewinds, right controller fast-forwards or stops early. Status is shown on a
    /// world-space text so the player never needs to leave the headset to see it.
    /// </summary>
    public class RhythmRecordingRemote : MonoBehaviour
    {
        [SerializeField] RhythmChartRecorder recorder;
        [SerializeField] Conductor conductor;
        [SerializeField] TMP_Text statusText;
        [Tooltip("How far (seconds) a rewind/fast-forward press jumps.")]
        [SerializeField] float seekSeconds = 5f;
        [Tooltip("Playback speed presets the left trigger cycles through, in order. Index 0 is applied as soon as the scene starts.")]
        [SerializeField] float[] speedPresets = { 0.5f, 0.3f, 1f };

        bool m_LeftPrimaryWasPressed;
        bool m_LeftSecondaryWasPressed;
        bool m_RightPrimaryWasPressed;
        bool m_RightSecondaryWasPressed;
        bool m_LeftTriggerWasPressed;
        bool m_RightTriggerWasPressed;
        int m_SpeedPresetIndex;

        void Start()
        {
            if (speedPresets.Length > 0)
            {
                conductor.SetPlaybackSpeed(speedPresets[m_SpeedPresetIndex]);
            }
        }

        void Update()
        {
            if (!conductor.IsStarted)
            {
                // Waiting for the player to get into position - nothing else does anything
                // useful yet, so only poll the "I'm ready" trigger.
                PollButton(XRNode.RightHand, CommonUsages.triggerButton, ref m_RightTriggerWasPressed, conductor.StartSong);
                UpdateStatusText();
                return;
            }

            PollButton(XRNode.LeftHand, CommonUsages.primaryButton, ref m_LeftPrimaryWasPressed, recorder.TogglePause);
            PollButton(XRNode.LeftHand, CommonUsages.secondaryButton, ref m_LeftSecondaryWasPressed, () => recorder.Rewind(seekSeconds));
            PollButton(XRNode.RightHand, CommonUsages.primaryButton, ref m_RightPrimaryWasPressed, () => recorder.FastForward(seekSeconds));
            PollButton(XRNode.RightHand, CommonUsages.secondaryButton, ref m_RightSecondaryWasPressed, recorder.StopRecordingAndSaveChart);
            PollButton(XRNode.LeftHand, CommonUsages.triggerButton, ref m_LeftTriggerWasPressed, CyclePlaybackSpeed);

            UpdateStatusText();
        }

        void CyclePlaybackSpeed()
        {
            if (speedPresets.Length == 0)
            {
                return;
            }

            m_SpeedPresetIndex = (m_SpeedPresetIndex + 1) % speedPresets.Length;
            conductor.SetPlaybackSpeed(speedPresets[m_SpeedPresetIndex]);
        }

        static void PollButton(XRNode node, InputFeatureUsage<bool> usage, ref bool wasPressed, System.Action onPressed)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool isPressed = device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;

            if (isPressed && !wasPressed)
            {
                onPressed();
            }

            wasPressed = isPressed;
        }

        void UpdateStatusText()
        {
            if (statusText == null)
            {
                return;
            }

            if (!conductor.IsStarted)
            {
                statusText.text = "Get into position...\nRight Trigger: start recording when ready";
                return;
            }

            string state = conductor.IsPaused ? "PAUSED" : "Recording";
            statusText.text = $"{state} - {conductor.SongPositionSeconds:F1}s @ {conductor.PlaybackSpeed:F1}x\n" +
                               $"Left X: pause/resume   Left Y: rewind {seekSeconds:F0}s   Left Trigger: speed\n" +
                               $"Right A: forward {seekSeconds:F0}s   Right B: save now";
        }
    }
}
