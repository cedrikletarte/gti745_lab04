using UnityEngine;
using DrumKit.Pieces;

namespace DrumKit.Striking
{
    /// <summary>
    /// Keeps the visible drumstick from poking through a drum/cymbal surface. Each frame it
    /// tries to sit exactly at the hand's rest pose (its authored local offset from the
    /// controller), but if that path would sweep the tip into a DrumPiece, it stops at the
    /// surface instead - checked with a swept sphere cast (like DrumStriker's own hit sweep)
    /// rather than a same-frame overlap test, so a fast or a slow-but-continuous push can't
    /// tunnel the stick through between two frames. A depenetration pass then cleans up any
    /// residual overlap (e.g. resting pressed against a surface). The rest pose is
    /// recomputed fresh from the live tracked controller every frame rather than
    /// accumulated, so pulling the hand back out of a piece always snaps the stick back to
    /// the real hand position, with no residual offset.
    ///
    /// Attach directly to the drumstick root (the GameObject holding the visible mesh).
    /// DrumTip, nested under it, moves along for the ride - so a real hit still stops the
    /// stick and the tip at the same point, at the same time.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class DrumstickCollisionClamp : MonoBehaviour
    {
        [SerializeField, Tooltip("Local point (in this stick's own space) checked against drum surfaces. Auto-filled from a child named 'DrumTip' at Awake if left at zero.")]
        Vector3 tipLocalPoint;
        [SerializeField, Tooltip("Radius of the stick at the check point, used for the surface push-back test.")]
        float tipRadius = 0.012f;
        [SerializeField, Tooltip("Extra margin kept between the tip and a surface once stopped, so the two don't stay in exact contact (which can hide/re-trigger casts).")]
        float skin = 0.003f;

        Transform m_Parent;
        Vector3 m_RestLocalPosition;
        Quaternion m_RestLocalRotation;
        SphereCollider m_Probe;
        Vector3 m_LastTipWorldPoint;
        bool m_HasLastTipWorldPoint;
        bool m_WasConstrained;
        DrumStriker m_Striker;
        TransformVelocityTracker m_VelocityTracker;
        readonly Collider[] m_OverlapResults = new Collider[8];
        readonly RaycastHit[] m_SweepHits = new RaycastHit[8];

        void Awake()
        {
            m_Parent = transform.parent;
            m_RestLocalPosition = transform.localPosition;
            m_RestLocalRotation = transform.localRotation;

            if (tipLocalPoint == Vector3.zero)
            {
                Transform drumTip = transform.Find("DrumTip");
                if (drumTip != null)
                {
                    tipLocalPoint = drumTip.localPosition;
                }
            }

            m_Probe = GetComponent<SphereCollider>();
            m_Probe.isTrigger = true;
            m_Probe.radius = tipRadius;
            m_Probe.center = tipLocalPoint;

            m_Striker = GetComponentInChildren<DrumStriker>();
            m_VelocityTracker = GetComponentInChildren<TransformVelocityTracker>();
        }

        void OnEnable() => Application.onBeforeRender += ApplyCorrection;
        void OnDisable() => Application.onBeforeRender -= ApplyCorrection;

        int m_LastAppliedFrame = -1;

        // Runs after tracking pose updates so the check uses this frame's real hand pose,
        // and is frame-guarded since stereo rendering can fire onBeforeRender per eye.
        void ApplyCorrection()
        {
            if (Time.frameCount == m_LastAppliedFrame)
            {
                return;
            }
            m_LastAppliedFrame = Time.frameCount;

            Vector3 restWorldPosition = m_Parent.TransformPoint(m_RestLocalPosition);
            Quaternion restWorldRotation = m_Parent.rotation * m_RestLocalRotation;

            Vector3 tipOffset = restWorldRotation * Vector3.Scale(tipLocalPoint, transform.lossyScale);
            Vector3 desiredTipWorldPoint = restWorldPosition + tipOffset;

            if (!m_HasLastTipWorldPoint)
            {
                m_LastTipWorldPoint = desiredTipWorldPoint;
                m_HasLastTipWorldPoint = true;
            }

            Vector3 resolvedTipWorldPoint = SweepTip(m_LastTipWorldPoint, desiredTipWorldPoint);
            resolvedTipWorldPoint = DepenetrateTip(resolvedTipWorldPoint, restWorldRotation, tipOffset);

            bool constrainedThisFrame = (resolvedTipWorldPoint - desiredTipWorldPoint).sqrMagnitude > 0.0001f * 0.0001f;

            m_LastTipWorldPoint = resolvedTipWorldPoint;
            transform.SetPositionAndRotation(resolvedTipWorldPoint - tipOffset, restWorldRotation);

            // Just released from being held at a surface: the tip's own transform (read by
            // DrumStriker/TransformVelocityTracker) just jumped from the surface back to the
            // real hand position in a single step, not through a real swing. Left alone,
            // DrumStriker's next FixedUpdate would sweep across that jump and could fire a
            // spurious second strike on the way out - resync both trackers to this frame's
            // position instead so only genuine hand motion gets swept for hits.
            if (m_WasConstrained && !constrainedThisFrame)
            {
                m_Striker?.ResetTrackedPosition();
                m_VelocityTracker?.ResetTracking();
            }

            m_WasConstrained = constrainedThisFrame;
        }

        // Swept check along the whole path travelled this frame, so a fast (or a slow but
        // continuous) push can't skip past a piece between two same-frame position samples.
        Vector3 SweepTip(Vector3 from, Vector3 to)
        {
            Vector3 travel = to - from;
            float distance = travel.magnitude;
            if (distance < 0.0001f)
            {
                return to;
            }

            Vector3 direction = travel / distance;
            int hitCount = Physics.SphereCastNonAlloc(from, tipRadius, direction, m_SweepHits, distance, ~0, QueryTriggerInteraction.Collide);

            float closestDistance = float.MaxValue;
            bool foundPiece = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = m_SweepHits[i];
                if (hit.collider.GetComponentInParent<DrumPiece>() == null)
                {
                    continue;
                }

                // A distance-0 hit means the tip was already touching/embedded at the start
                // of this frame's move, not that it just entered along this path - blocking
                // on that would freeze the stick in place forever, including when trying to
                // pull back out. Only a genuine mid-path entry (distance > 0) blocks movement;
                // DepenetrateTip below is what resolves an existing overlap, in any direction.
                if (hit.distance > 0f && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    foundPiece = true;
                }
            }

            if (foundPiece)
            {
                return from + direction * Mathf.Max(0f, closestDistance - skin);
            }

            return to;
        }

        // Cleans up any residual overlap left after the sweep (e.g. the hand holding the
        // stick pressed steadily against a surface, where travel distance is near zero).
        Vector3 DepenetrateTip(Vector3 tipWorldPoint, Quaternion rotation, Vector3 tipOffset)
        {
            int count = Physics.OverlapSphereNonAlloc(tipWorldPoint, tipRadius, m_OverlapResults, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider other = m_OverlapResults[i];
                if (other.GetComponentInParent<DrumPiece>() == null)
                {
                    continue;
                }

                // m_Probe.center is a local offset (tipLocalPoint); ComputePenetration re-applies
                // it on top of positionA, so positionA must be the *origin*, not the tip itself.
                if (Physics.ComputePenetration(
                        m_Probe, tipWorldPoint - tipOffset, rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float pushDistance))
                {
                    tipWorldPoint += direction * (pushDistance + skin);
                }
            }

            return tipWorldPoint;
        }
    }
}
