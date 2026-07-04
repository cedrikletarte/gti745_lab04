using UnityEngine;

namespace DrumKit.Striking
{
    /// <summary>
    /// Estimates world-space velocity from position deltas rather than physics.
    /// Required for XR controller/hand transforms: they are moved by the tracking
    /// system (kinematically), so a Rigidbody never accumulates a usable velocity.
    /// Averages over a short window to smooth out per-frame tracking jitter while
    /// staying responsive enough for a drum strike.
    /// </summary>
    public class TransformVelocityTracker : MonoBehaviour, IVelocityProvider
    {
        [SerializeField, Tooltip("Number of FixedUpdate samples averaged to smooth tracking jitter.")]
        [Range(2, 8)]
        int sampleCount = 4;

        Vector3[] m_PositionSamples;
        float[] m_TimeSamples;
        int m_SampleIndex;
        int m_FilledSamples;
        Vector3 m_LastVelocity;

        void Awake()
        {
            m_PositionSamples = new Vector3[sampleCount];
            m_TimeSamples = new float[sampleCount];
        }

        void OnEnable()
        {
            m_FilledSamples = 0;
            m_SampleIndex = 0;
            m_LastVelocity = Vector3.zero;
        }

        void FixedUpdate()
        {
            m_PositionSamples[m_SampleIndex] = transform.position;
            m_TimeSamples[m_SampleIndex] = Time.time;
            m_SampleIndex = (m_SampleIndex + 1) % sampleCount;
            m_FilledSamples = Mathf.Min(m_FilledSamples + 1, sampleCount);

            if (m_FilledSamples < 2)
            {
                m_LastVelocity = Vector3.zero;
                return;
            }

            // Oldest sample still in the buffer vs. the one just written.
            int oldestIndex = m_FilledSamples < sampleCount ? 0 : m_SampleIndex;
            int newestIndex = (m_SampleIndex - 1 + sampleCount) % sampleCount;

            float deltaTime = m_TimeSamples[newestIndex] - m_TimeSamples[oldestIndex];
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 deltaPosition = m_PositionSamples[newestIndex] - m_PositionSamples[oldestIndex];
            m_LastVelocity = deltaPosition / deltaTime;
        }

        public Vector3 GetVelocity() => m_LastVelocity;

        /// <summary>
        /// Clears the sample buffer so a teleport that isn't a real hand motion (e.g. a
        /// collision clamp releasing) doesn't get read back as a burst of velocity for the
        /// next few samples.
        /// </summary>
        public void ResetTracking()
        {
            m_FilledSamples = 0;
            m_SampleIndex = 0;
            m_LastVelocity = Vector3.zero;
        }
    }
}
