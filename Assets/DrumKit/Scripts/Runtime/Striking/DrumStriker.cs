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
                Debug.LogError($"{nameof(DrumStriker)} on '{name}' requires a component implementing {nameof(IVelocityProvider)} " +
                                $"(e.g. {nameof(TransformVelocityTracker)} or {nameof(RigidbodyVelocityProvider)}).", this);
            }
        }

        /// <summary>Sends a short haptic pulse to this striker's controller, if one is found in its parent chain.</summary>
        public void PlayHapticImpulse(float intensity01)
        {
            if (m_HapticPlayer != null)
            {
                m_HapticPlayer.SendHapticImpulse(Mathf.Clamp01(intensity01), maxHapticDuration * Mathf.Clamp01(intensity01));
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
                float radius = m_Collider.radius * transform.lossyScale.x;
                int hitCount = Physics.SphereCastNonAlloc(m_PreviousPosition, radius, delta / distance, m_SweepHits, distance,
                    ~0, QueryTriggerInteraction.Collide);

                DrumPiece closestPiece = null;
                Vector3 closestPoint = default;
                float closestDistance = float.MaxValue;

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = m_SweepHits[i];
                    DrumPiece piece = hit.collider.GetComponent<DrumPiece>();
                    if (piece != null && hit.distance < closestDistance)
                    {
                        closestPiece = piece;
                        closestPoint = hit.point;
                        closestDistance = hit.distance;
                    }
                }

                if (closestPiece != null)
                {
                    closestPiece.RegisterStrike(this, closestPoint);
                }
            }

            m_PreviousPosition = currentPosition;
        }

        /// <summary>
        /// Re-syncs the tracked position to where the tip is right now, without sweeping
        /// for a hit along the way there. For use when something external (e.g. a
        /// drumstick collision clamp releasing after being held at a surface) teleports the
        /// tip in a way that isn't a real swing - without this, the next FixedUpdate would
        /// sweep across that jump and could register a spurious second strike.
        /// </summary>
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
