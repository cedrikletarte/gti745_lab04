using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DrumKit.Striking
{
    /// <summary>
    /// Purely cosmetic: attach directly to a controller root (e.g. "Left Controller").
    /// Automatically finds every direct child branch that renders something - the
    /// controller body, buttons, poke-interactor reticle, whatever else is under there -
    /// and stops each branch, independently, from visually clipping through a drum
    /// surface. No manual per-object assignment needed.
    ///
    /// Each qualifying child is treated as one rigid block: a single combined bounding
    /// volume covering everything under it is sampled, and if it overlaps a piece the
    /// whole branch is nudged back by one correction - this keeps buttons/thumbstick/etc.
    /// aligned with their own base instead of drifting independently, and avoids opposite
    /// push directions from different sample points cancelling out to roughly nothing
    /// (which happens easily, since a controller's neutral pose already sits among several
    /// pieces at once).
    ///
    /// The one child branch that also carries a DrumStriker (the invisible strike-detection
    /// tip) is always skipped, and this component never moves its own transform (the
    /// controller root itself) - only the child branches get corrected. That tip has to
    /// keep following the real tracked pose exactly, unmodified, or audio/haptic timing
    /// would desync from the player's actual hand.
    ///
    /// The rest pose of each branch is recomputed fresh from the live tracked root every
    /// frame rather than accumulated, so pulling the hand back out of a piece always
    /// returns the model to exactly where real tracking says it is - no residual offset.
    /// </summary>
    public class HandVisualClampGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Extra margin added around each branch's combined bounding volume before it's considered 'touching'.")]
        float padding = 0.015f;
        [SerializeField, Tooltip("How quickly the visual correction is applied/released - higher snaps faster in both directions.")]
        float correctionSpeed = 40f;
        [SerializeField, Tooltip("Layers to check against. Leave as Everything unless you've put drum pieces on a dedicated layer.")]
        LayerMask drumLayers = ~0;

        class ClampedBranch
        {
            public Transform Target;
            public Vector3 RestLocalPosition;
            public Quaternion RestLocalRotation;
            public Vector3 LossyScale;
            public Vector3[] LocalSamplePoints;
            public Vector3 CurrentCorrection;
            public Vector3 LastSetPosition;
            public bool HasLastSetPosition;
        }

        readonly List<ClampedBranch> m_Branches = new();
        SphereCollider m_Probe;
        readonly Collider[] m_OverlapResults = new Collider[16];

        void Awake()
        {
            var probeObject = new GameObject("HandVisualClampGroup Probe") { hideFlags = HideFlags.HideInHierarchy };
            probeObject.transform.SetParent(transform, false);
            m_Probe = probeObject.AddComponent<SphereCollider>();
            m_Probe.isTrigger = true;
            m_Probe.radius = padding;

            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<DrumStriker>(true) != null)
                {
                    // The strike-detection tip: must keep following real tracking exactly.
                    continue;
                }

                if (child.GetComponentInChildren<IXRInteractor>(true) != null || child.GetComponentInChildren<IXRInteractable>(true) != null)
                {
                    // Confirmed the hard way: moving a poke/ray/teleport interactor's
                    // transform from the outside breaks its own per-frame assumptions -
                    // it throws NullReferenceExceptions and corrupts UI raycasting every
                    // frame it's overlapping a piece. Not a style choice, a stability one.
                    Debug.Log($"[Drum Kit] '{child.name}': skipped - carries interaction logic that can't safely be moved externally.");
                    continue;
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    continue;
                }

                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }

                Vector3 lossyScale = child.lossyScale;
                Vector3 localCenter = child.InverseTransformPoint(combined.center);
                var localExtents = new Vector3(
                    combined.extents.x / Mathf.Max(lossyScale.x, 0.0001f),
                    combined.extents.y / Mathf.Max(lossyScale.y, 0.0001f),
                    combined.extents.z / Mathf.Max(lossyScale.z, 0.0001f));

                m_Branches.Add(new ClampedBranch
                {
                    Target = child,
                    RestLocalPosition = child.localPosition,
                    RestLocalRotation = child.localRotation,
                    LossyScale = lossyScale,
                    LocalSamplePoints = BuildSamplePoints(localCenter, localExtents),
                });
            }

            Debug.Log($"[Drum Kit] '{name}': HandVisualClampGroup is clamping {m_Branches.Count} branch(es): " +
                      string.Join(", ", m_Branches.ConvertAll(b => b.Target.name)));
        }

        static Vector3[] BuildSamplePoints(Vector3 center, Vector3 extents)
        {
            var points = new Vector3[9];
            points[0] = center;
            int i = 1;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        points[i++] = center + Vector3.Scale(extents, new Vector3(sx, sy, sz));
                    }
                }
            }

            return points;
        }

        void OnEnable()
        {
            Application.onBeforeRender += ApplyCorrections;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= ApplyCorrections;
        }

        int m_LastAppliedFrame = -1;

        // Runs after every Update/LateUpdate in the frame - including whatever "before
        // render" pose sync updates the controller model itself for minimum visual latency.
        // Anything earlier (LateUpdate included) gets silently overwritten by that sync
        // before the frame is actually drawn, which is why the correction has to live here.
        // Stereo rendering can fire this event more than once per frame (once per eye) - the
        // frame guard makes sure the correction (and its Lerp smoothing) only runs once.
        void ApplyCorrections()
        {
            if (Time.frameCount == m_LastAppliedFrame)
            {
                return;
            }
            m_LastAppliedFrame = Time.frameCount;

            foreach (ClampedBranch branch in m_Branches)
            {
                // Always rebuild from the rest local pose relative to this (live, tracked)
                // root - never from last frame's corrected position, or the correction would
                // bake in permanently instead of releasing once nothing overlaps anymore.
                Vector3 restWorldPosition = transform.TransformPoint(branch.RestLocalPosition);
                Quaternion restWorldRotation = transform.rotation * branch.RestLocalRotation;
                Matrix4x4 restMatrix = Matrix4x4.TRS(restWorldPosition, restWorldRotation, branch.LossyScale);

                Vector3 strongestCorrection = Vector3.zero;
                float strongestDistance = 0f;
                float broadPhaseRadius = padding + 0.15f;

                foreach (Vector3 localPoint in branch.LocalSamplePoints)
                {
                    Vector3 worldPoint = restMatrix.MultiplyPoint3x4(localPoint);
                    m_Probe.transform.position = worldPoint;

                    int count = Physics.OverlapSphereNonAlloc(worldPoint, broadPhaseRadius, m_OverlapResults, drumLayers, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < count; i++)
                    {
                        Collider other = m_OverlapResults[i];
                        if (other.GetComponentInParent<Pieces.DrumPiece>() == null)
                        {
                            continue;
                        }

                        if (Physics.ComputePenetration(
                                m_Probe, worldPoint, Quaternion.identity,
                                other, other.transform.position, other.transform.rotation,
                                out Vector3 direction, out float distance)
                            && distance > strongestDistance)
                        {
                            strongestCorrection = direction * distance;
                            strongestDistance = distance;
                        }
                    }
                }

                if (strongestDistance > 0.001f)
                {
                    Debug.Log($"[DrumKit DEBUG] '{branch.Target.name}' penetration detected: distance={strongestDistance:F3}m correction={strongestCorrection}");
                }

                branch.CurrentCorrection = Vector3.Lerp(branch.CurrentCorrection, strongestCorrection, Time.deltaTime * correctionSpeed);
                Vector3 finalPosition = restWorldPosition + branch.CurrentCorrection;
                branch.Target.SetPositionAndRotation(finalPosition, restWorldRotation);
            }
        }
    }
}
