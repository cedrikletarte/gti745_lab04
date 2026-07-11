using UnityEngine;

namespace DrumKit.Visuals
{
    /// <summary>
    /// A small world-space "press this button" badge that hovers on a drum piece played by
    /// a controller button (bass drum / hi-hat). It billboards to face the camera and gently
    /// pulses in scale to draw the eye. Purely cosmetic and self-contained - it has no
    /// dependency on the rhythm system, so the same prefab behaves identically in the Rythme
    /// and Solo scenes.
    /// </summary>
    public class PedalButtonPrompt : MonoBehaviour
    {
        [SerializeField, Tooltip("Camera the badge turns to face. Leave empty to use Camera.main (the VR head camera).")]
        Camera targetCamera;

        [SerializeField, Tooltip("Rotate every frame to face the camera so the label is always readable.")]
        bool billboard = true;

        [SerializeField, Range(0f, 1f), Tooltip("Extra scale at the peak of the pulse (0.15 = grows 15% larger).")]
        float pulseAmount = 0.15f;

        [SerializeField, Tooltip("Roughly how many pulses per second.")]
        float pulseSpeed = 2.5f;

        Vector3 m_BaseScale;

        void Awake()
        {
            m_BaseScale = transform.localScale;
        }

        void OnDisable()
        {
            // Don't leave the badge frozen mid-pulse if it gets hidden.
            if (m_BaseScale != Vector3.zero)
            {
                transform.localScale = m_BaseScale;
            }
        }

        void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (billboard && cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
            }

            float t = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            transform.localScale = m_BaseScale * (1f + t * pulseAmount);
        }
    }
}
