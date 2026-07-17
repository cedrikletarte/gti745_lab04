using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using DrumKit.Pieces;

namespace DrumKit.Striking
{
    /// <summary>
    /// Sits on a striker tip (controller-mounted mallet today, a physical drumstick tip
    /// later). Each physics step it sweeps its own collider from where it was to where it
    /// now is and asks any DrumPiece along that path to register a strike - a plain
    /// OnTriggerEnter would miss fast VR swings that tunnel through a piece entirely
    /// between two physics steps, since the tip is small and can easily move further than
    /// its own radius in one step. Also debounces re-entries so a single physical hit
    /// can't fire twice while still overlapping a piece across a couple of physics frames.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class DrumStriker : MonoBehaviour
    {
        [SerializeField, Tooltip("Minimum time before the same piece can be re-triggered by this striker.")]
        float retriggerCooldown = 0.05f;

        [SerializeField, Tooltip("Duration (seconds) of the haptic pulse sent on a full-intensity strike.")]
        float maxHapticDuration = 0.08f;

        IVelocityProvider m_VelocityProvider;
        SphereCollider m_Collider;
        HapticImpulsePlayer m_HapticPlayer;
        Vector3 m_PreviousPosition;
        bool m_HasPreviousPosition;
        readonly Dictionary<DrumPiece, float> m_LastHitTime = new Dictionary<DrumPiece, float>();
        readonly RaycastHit[] m_SweepHits = new RaycastHit[8];

        public Vector3 CurrentVelocity => m_VelocityProvider != null ? m_VelocityProvider.GetVelocity() : Vector3.zero;

        void Awake()
        {
            m_VelocityProvider = GetComponent<IVelocityProvider>();
            m_Collider = GetComponent<SphereCollider>();
            m_HapticPlayer = GetComponentInParent<HapticImpulsePlayer>();

            if (m_VelocityProvider == null)
            {
                Debug.LogError($"{nameof(DrumStriker)} on '{name}' requires a component implementing " + $"{nameof(IVelocityProvider)}.", this);
            }
        }

        void OnEnable()
        {
            m_HasPreviousPosition = false;
        }

        void FixedUpdate()
        {
            Vector3 currentPosition = transform.position;

            if (!m_HasPreviousPosition)
            {
                m_PreviousPosition = currentPosition;
                m_HasPreviousPosition = true;
                return;
            }

            Vector3 delta = currentPosition - m_PreviousPosition;
            float distance = delta.magnitude;

            if (distance > 0.005f)
            {
                Vector3 direction = delta / distance;

                // Use the largest scale axis so a non-uniformly scaled striker
                // does not accidentally use a radius that is too small.
                Vector3 scale = transform.lossyScale;
                float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

                float radius = m_Collider.radius * largestScale;

                int hitCount = Physics.SphereCastNonAlloc(m_PreviousPosition, radius, direction, m_SweepHits, distance, ~0, QueryTriggerInteraction.Collide);

                DrumPiece closestPiece = null;
                Vector3 closestPoint = default;
                float closestDistance = float.MaxValue;

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = m_SweepHits[i];

                    // This also works when the collider is on a child object.
                    DrumPiece piece = hit.collider.GetComponentInParent<DrumPiece>();

                    if (piece == null || hit.distance >= closestDistance)
                    {
                        continue;
                    }

                    Vector3 contactPoint = hit.point;

                    // A cast that starts overlapped can return distance 0 and an
                    // unusable point. Fall back to the collider's closest point.
                    if (hit.distance <= Mathf.Epsilon)
                    {
                        contactPoint = hit.collider.ClosestPoint(currentPosition);
                    }

                    closestPiece = piece;
                    closestPoint = contactPoint;
                    closestDistance = hit.distance;
                }

                if (closestPiece != null)
                {
                    // This is the exact world-space position used by DrumPiece
                    // for audio, scoring, physics, and the VFX spawn.
                    closestPiece.RegisterStrike(this, closestPoint);
                }
            }

            m_PreviousPosition = currentPosition;
        }

        public void PlayHapticImpulse(float intensity01)
        {
            if (m_HapticPlayer == null)
            {
                return;
            }

            float intensity = Mathf.Clamp01(intensity01);

            m_HapticPlayer.SendHapticImpulse(intensity, maxHapticDuration * intensity);
        }

        public void ResetTrackedPosition()
        {
            m_PreviousPosition = transform.position;
            m_HasPreviousPosition = true;
        }

        /// <summary>
        /// Returns true if enough time has passed since the last strike this
        /// striker landed on the given piece, and records the attempt.
        /// </summary>
        public bool TryConsumeCooldown(DrumPiece piece)
        {
            if (m_LastHitTime.TryGetValue(piece, out float lastTime) && Time.time - lastTime < retriggerCooldown)
            {
                return false;
            }

            m_LastHitTime[piece] = Time.time;
            return true;
        }
    }
}