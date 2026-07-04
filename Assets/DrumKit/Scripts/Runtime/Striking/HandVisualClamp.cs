using UnityEngine;
using DrumKit.Pieces;

namespace DrumKit.Striking
{
    /// <summary>
    /// Purely cosmetic: stops the rendered hand/controller model from visually clipping
    /// through a drum surface by nudging it back out each frame. Does not touch the real
    /// tracked controller transform - DrumTip/DrumStriker keep following the player's
    /// actual hand exactly as before, so audio and haptic timing are unaffected. Only
    /// what gets rendered is corrected, which is why this can't (and shouldn't) live on
    /// the same GameObject as DrumStriker.
    ///
    /// Attach directly to the GameObject that renders the hand/controller mesh.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HandVisualClamp : MonoBehaviour
    {
        [SerializeField, Tooltip("Approximate radius of the hand/controller model, used for the surface push-back check.")]
        float handRadius = 0.06f;
        [SerializeField, Tooltip("How quickly the visual correction is applied - higher snaps back faster.")]
        float correctionSpeed = 40f;

        SphereCollider m_Collider;
        Vector3 m_CurrentCorrection;
        readonly Collider[] m_OverlapResults = new Collider[8];

        void Awake()
        {
            m_Collider = GetComponent<SphereCollider>();
            m_Collider.isTrigger = true;
            m_Collider.radius = handRadius;
        }

        void LateUpdate()
        {
            Vector3 targetCorrection = Vector3.zero;
            int count = Physics.OverlapSphereNonAlloc(transform.position, handRadius, m_OverlapResults, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider other = m_OverlapResults[i];
                if (other.GetComponent<DrumPiece>() == null)
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                        m_Collider, transform.position, transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    targetCorrection += direction * distance;
                }
            }

            m_CurrentCorrection = Vector3.Lerp(m_CurrentCorrection, targetCorrection, Time.deltaTime * correctionSpeed);
            transform.position += m_CurrentCorrection;
        }
    }
}
