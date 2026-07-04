using UnityEngine;

namespace DrumKit.Striking
{
    /// <summary>
    /// Abstracts how a striker (controller tip, hand joint, physical drumstick) reports its
    /// current world-space velocity. Swapping the implementation is the only change needed
    /// to move from VR controllers to a grabbed physical drumstick.
    /// </summary>
    public interface IVelocityProvider
    {
        Vector3 GetVelocity();
    }
}
