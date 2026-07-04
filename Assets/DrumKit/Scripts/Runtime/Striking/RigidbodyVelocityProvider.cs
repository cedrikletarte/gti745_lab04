using UnityEngine;

namespace DrumKit.Striking
{
    /// <summary>
    /// Reads velocity straight from a physically simulated Rigidbody. This is the
    /// implementation to use once the striker tip belongs to a real, grabbed
    /// (non-kinematic) drumstick instead of a directly-tracked controller/hand transform.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyVelocityProvider : MonoBehaviour, IVelocityProvider
    {
        Rigidbody m_Rigidbody;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        public Vector3 GetVelocity() => m_Rigidbody.linearVelocity;
    }
}
