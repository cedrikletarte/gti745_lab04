using UnityEngine;

namespace DrumKit.Visuals
{
    /// <summary>
    /// Fake volumetric beam for a Spot Light: a translucent additive cone, built to match the
    /// light's aim, that reads as a coloured shaft of light cutting through the fog - the
    /// concert "moving head" / laser look URP can't do with real volumetrics. The cone is
    /// tinted to the Light's current colour every frame, so it cycles right along with
    /// MovingStageLight. Runs in edit mode too (ExecuteAlways) so the beam is visible while
    /// tuning; the generated child is hidden and never saved into the scene.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public class LightBeamCone : MonoBehaviour
    {
        [Tooltip("How far the visible beam reaches, in metres (independent of the light's range).")]
        [SerializeField] float beamLength = 9f;
        [Tooltip("Cone width relative to the light's spot angle (0.7 = a bit tighter than the lit pool, for a beam-y look).")]
        [SerializeField, Range(0.2f, 1.2f)] float widthScale = 0.75f;
        [Tooltip("Overall brightness of the beam. Additive, so keep it low.")]
        [SerializeField, Range(0f, 2f)] float strength = 0.6f;
        [Tooltip("Beam opacity right at the light; it fades to zero along its length.")]
        [SerializeField, Range(0f, 1f)] float apexAlpha = 0.5f;
        [SerializeField] int segments = 24;

        static readonly int k_Color = Shader.PropertyToID("_Color");

        Light m_Light;
        Transform m_Beam;
        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_Block;

        void OnEnable()
        {
            m_Light = GetComponent<Light>();
            EnsureBeam();
        }

        void OnDisable()
        {
            if (m_Beam != null)
            {
                DestroyImmediate(m_Beam.gameObject);
                m_Beam = null;
            }
        }

        void LateUpdate()
        {
            if (m_Beam == null || m_Renderer == null)
            {
                return;
            }

            // Tint to the live light colour (this is what makes the beam cycle with the music).
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_Color, m_Light.color);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        void EnsureBeam()
        {
            if (m_Beam != null)
            {
                return;
            }

            var material = LoadMaterial();
            if (material == null)
            {
                return;
            }

            var go = new GameObject("Beam (generated)")
            {
                // Kept out of the saved scene and the hierarchy - purely a runtime/edit visual.
                hideFlags = HideFlags.HideAndDontSave
            };
            m_Beam = go.transform;
            m_Beam.SetParent(transform, false); // inherits the light's aim, so it sweeps with it

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildConeMesh();

            m_Renderer = go.AddComponent<MeshRenderer>();
            m_Renderer.sharedMaterial = material;
            m_Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Renderer.receiveShadows = false;
            m_Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            m_Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            m_Block = new MaterialPropertyBlock();
        }

        Material LoadMaterial()
        {
            var shader = Shader.Find("DrumKit/AdditiveBeam");
            if (shader == null)
            {
                Debug.LogWarning($"{nameof(LightBeamCone)}: shader 'DrumKit/AdditiveBeam' not found.", this);
                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "AdditiveBeam (instance)" };
        }

        /// <summary>Open cone: apex at the light origin, opening along local +Z (the spot's aim),
        /// with a per-vertex alpha that fades from the apex to the far rim.</summary>
        Mesh BuildConeMesh()
        {
            float halfAngle = Mathf.Deg2Rad * m_Light.spotAngle * 0.5f * widthScale;
            float radius = beamLength * Mathf.Tan(halfAngle);

            var verts = new Vector3[segments + 1];
            var colors = new Color[segments + 1];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            colors[0] = new Color(1f, 1f, 1f, apexAlpha);

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, beamLength);
                colors[i + 1] = new Color(1f, 1f, 1f, 0f); // fades to nothing at the far rim
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = next + 1;
            }

            var mesh = new Mesh { name = "LightBeam (procedural)" };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
