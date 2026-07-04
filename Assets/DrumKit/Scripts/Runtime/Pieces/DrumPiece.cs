using UnityEngine;
using DrumKit.Audio;
using DrumKit.Striking;

namespace DrumKit.Pieces
{
    /// <summary>
    /// Hit-zone for a single physical drum/cymbal. Lives on a dedicated trigger-collider
    /// GameObject (kept separate from the visual mesh, since the imported model has no
    /// colliders and its sub-mesh names carry no per-piece meaning on their own).
    ///
    /// Turns a DrumStriker contact into: a soft-touch/no-op, or a percussive hit with an
    /// intensity and impact position handed off to the piece's DrumVoicePool. Cymbals/hi-hat
    /// additionally choke (mute) when held still after ringing.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(DrumVoicePool))]
    public class DrumPiece : MonoBehaviour
    {
        [SerializeField] DrumPieceSoundBank soundBank;

        [Header("Strike sensitivity")]
        [Tooltip("Impact speed (m/s, along the surface normal) below which a touch is ignored entirely.")]
        [SerializeField] float minStrikeSpeed = 0.15f;
        [Tooltip("Impact speed (m/s) that maps to full intensity (loudest, richest layer).")]
        [SerializeField] float maxStrikeSpeed = 4f;
        [Tooltip("Defines the strike axis (its 'up'). Defaults to this transform if left empty.")]
        [SerializeField] Transform surfaceReference;
        [Tooltip("Tuning offset in semitones, so one sound bank can be shared across differently-tuned pieces (e.g. two toms).")]
        [SerializeField] float pitchOffsetSemitones;

        [Header("Choke (cymbals / hi-hat)")]
        [SerializeField] bool isChokeable;
        [Tooltip("Minimum time after a strike before a resting contact is allowed to choke it - lets the piece ring briefly first.")]
        [SerializeField] float chokeGraceSeconds = 0.15f;
        [Tooltip("A striker moving slower than this (m/s) while in contact counts as 'resting', not 'still swinging'.")]
        [SerializeField] float chokeSpeedThreshold = 0.15f;

        [Header("Physical reaction (optional)")]
        [Tooltip("How much of the strike's velocity gets imparted to this piece's Rigidbody, if it has one (e.g. a cymbal swinging on its stand). Has no effect on pieces without a Rigidbody.")]
        [SerializeField] float physicsImpulseMultiplier = 1.5f;

        Collider m_Collider;
        DrumVoicePool m_VoicePool;
        Rigidbody m_Rigidbody;
        float m_LastEventTime = float.NegativeInfinity;

        void Awake()
        {
            m_Collider = GetComponent<Collider>();
            m_VoicePool = GetComponent<DrumVoicePool>();
            m_Rigidbody = GetComponent<Rigidbody>();

            if (surfaceReference == null)
            {
                surfaceReference = transform;
            }
        }

        /// <summary>
        /// Called by a DrumStriker that swept through this piece this physics step (see
        /// DrumStriker.FixedUpdate). Driven by an explicit SphereCast sweep rather than
        /// Unity's OnTriggerEnter, which can miss fast-moving small colliders tunneling
        /// through a piece between two physics steps - a real problem at VR swing speeds.
        /// </summary>
        public void RegisterStrike(DrumStriker striker, Vector3 worldContactPoint)
        {
            if (!striker.TryConsumeCooldown(this))
            {
                return;
            }

            Vector3 up = surfaceReference.up;
            float impactSpeed = Mathf.Max(0f, Vector3.Dot(striker.CurrentVelocity, -up));

            m_LastEventTime = Time.time;

            if (impactSpeed < minStrikeSpeed)
            {
                // Deliberate no-op: resting a controller on a drum/cymbal must stay silent.
                return;
            }

            float intensity01 = Mathf.Clamp01(Mathf.InverseLerp(minStrikeSpeed, maxStrikeSpeed, impactSpeed));
            float radial01 = ComputeRadialPosition(worldContactPoint, up);

            if (soundBank != null &&
                soundBank.TryPickClip(intensity01, radial01, out AudioClip clip, out float volume, out float pitch))
            {
                m_VoicePool.PlayClip(clip, volume, pitch * DrumPieceSoundBank.SemitonesToPitch(pitchOffsetSemitones));
            }

            if (m_Rigidbody != null)
            {
                m_Rigidbody.AddForceAtPosition(striker.CurrentVelocity * physicsImpulseMultiplier, worldContactPoint, ForceMode.VelocityChange);
            }

            striker.PlayHapticImpulse(intensity01);
        }

        void OnTriggerStay(Collider other)
        {
            if (!isChokeable)
            {
                return;
            }

            DrumStriker striker = other.GetComponent<DrumStriker>();
            if (striker == null)
            {
                return;
            }

            if (Time.time - m_LastEventTime < chokeGraceSeconds || striker.CurrentVelocity.magnitude > chokeSpeedThreshold)
            {
                return;
            }

            m_VoicePool.Choke(soundBank != null ? soundBank.chokeFadeSeconds : 0.08f);
            // Throttle: don't restart the fade every single frame the hand stays put.
            m_LastEventTime = Time.time;
        }

        /// <summary>0 = impact at the piece's center, 1 = impact at its edge (only meaningful for round pieces like cymbals).</summary>
        float ComputeRadialPosition(Vector3 worldContactPoint, Vector3 up)
        {
            Vector3 offsetFromCenter = worldContactPoint - m_Collider.bounds.center;
            Vector3 horizontalOffset = Vector3.ProjectOnPlane(offsetFromCenter, up);

            float horizontalExtent = Vector3.ProjectOnPlane(m_Collider.bounds.extents, up).magnitude;
            if (horizontalExtent < 0.0001f)
            {
                horizontalExtent = m_Collider.bounds.extents.magnitude;
            }

            return horizontalExtent < 0.0001f ? 0f : Mathf.Clamp01(horizontalOffset.magnitude / horizontalExtent);
        }

        void OnDrawGizmosSelected()
        {
            Collider col = m_Collider != null ? m_Collider : GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            Gizmos.color = isChokeable ? new Color(1f, 0.6f, 0.1f, 0.6f) : new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.matrix = Matrix4x4.identity;
            Bounds bounds = col.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Vector3 up = (surfaceReference != null ? surfaceReference : transform).up;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(bounds.center, bounds.center + up * 0.1f);
        }
    }
}
