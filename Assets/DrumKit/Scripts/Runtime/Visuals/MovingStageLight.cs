using UnityEngine;

namespace DrumKit.Visuals
{
    /// <summary>
    /// A concert "moving head": a Spot Light that sweeps left-right and continuously cycles its
    /// colour through the hue wheel. Purely cosmetic and self-contained - no music/beat
    /// reaction, just steady movement and colour. Pair it with LightBeamCone, which reads this
    /// light's live colour so the beam cone cycles along with it.
    ///
    /// The sweep is applied around world-up on top of whatever aim the light was placed with,
    /// so you point the spot in the editor and this just wags it back and forth.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MovingStageLight : MonoBehaviour
    {
        [Header("Sweep (left-right)")]
        [Tooltip("Half-angle of the left-right sweep, in degrees, around the light's placed aim.")]
        [SerializeField] float panAmplitudeDegrees = 22f;
        [Tooltip("Sweeps per second.")]
        [SerializeField] float panSpeed = 0.12f;
        [Range(0f, 1f)]
        [Tooltip("Phase offset (0-1 of a full cycle) so several lights don't sweep in lockstep.")]
        [SerializeField] float panPhase;

        [Header("Colour cycle")]
        [Tooltip("Full trips around the colour wheel per second.")]
        [SerializeField] float hueCycleSpeed = 0.06f;
        [Range(0f, 1f)]
        [Tooltip("Starting hue (0-1). Give paired lights different values so they aren't the same colour at the same time.")]
        [SerializeField] float hueStart;
        [Range(0f, 1f)]
        [SerializeField] float saturation = 0.9f;
        [Range(0f, 1f)]
        [SerializeField] float brightness = 1f;

        Light m_Light;
        Quaternion m_BaseLocalRotation;

        void Awake()
        {
            m_Light = GetComponent<Light>();
            m_BaseLocalRotation = transform.localRotation;
        }

        void Update()
        {
            float time = Time.time;

            // Sweep: wag around world-up on top of the placed aim.
            float pan = Mathf.Sin((time * panSpeed + panPhase) * Mathf.PI * 2f) * panAmplitudeDegrees;
            transform.localRotation = m_BaseLocalRotation;
            transform.rotation = Quaternion.AngleAxis(pan, Vector3.up) * transform.rotation;

            // Colour: rotate the hue continuously.
            float hue = Mathf.Repeat(hueStart + time * hueCycleSpeed, 1f);
            m_Light.color = Color.HSVToRGB(hue, saturation, brightness);
        }
    }
}
