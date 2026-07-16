using UnityEngine;
using DrumKit.Pieces;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Purely cosmetic: a hollow ring that shrinks down onto a drum piece, closing to
    /// exactly the piece's own diameter right on the beat - the physical-kit equivalent of
    /// Guitar Hero's "note reaches the strike line", except the line is the piece itself.
    /// Driven entirely by Conductor.SongPositionSeconds (the same clock audio and scoring
    /// read) so it can't drift independently of the music. Finishes whether or not the
    /// note was actually hit - it never talks to scoring, RhythmCueSpawner recycles it.
    ///
    /// Builds its own flat annulus mesh at Awake (outer radius 0.5, i.e. diameter 1 in
    /// local units) so a plain uniform localScale directly equals the ring's world-space
    /// diameter - no custom shader, just geometry.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CueRingVisual : MonoBehaviour
    {
        [Tooltip("How many times larger than the piece's own diameter the ring starts at.")]
        [SerializeField] float approachSizeMultiplier = 2.5f;
        [Range(0.5f, 0.98f)]
        [SerializeField] float innerRadiusRatio = 0.85f;
        [SerializeField] int segments = 48;
        [Range(0f, 1f)]
        [Tooltip("Opacity the piece's colour is drawn at, so the ring stays a translucent overlay.")]
        [SerializeField] float ringAlpha = 0.7f;

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Color = Shader.PropertyToID("_Color");

        static Mesh s_SharedRingMesh;

        MeshRenderer m_Renderer;
        MaterialPropertyBlock m_PropertyBlock;

        Conductor m_Conductor;
        DrumPiece m_TargetPiece;
        Collider m_TargetCollider;
        Vector3 m_LocalFlatAxis;
        float m_SpawnTimeSeconds;
        float m_NoteTimeSeconds;
        float m_TargetDiameter;

        public bool IsFinished { get; private set; } = true;

        void Awake()
        {
            if (s_SharedRingMesh == null)
            {
                s_SharedRingMesh = BuildRingMesh(innerRadiusRatio, segments);
            }

            GetComponent<MeshFilter>().sharedMesh = s_SharedRingMesh;
            m_Renderer = GetComponent<MeshRenderer>();
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// targetDiameter is the piece's own real-world size (metres) - the ring closes to
        /// exactly that size. localFlatAxis is the piece's LOCAL-space "thin" axis (its
        /// visual flat-face normal, from RhythmPieceRegistry - independent of
        /// DrumPiece.SurfaceUp, which is tuned for hit-detection, not visuals). Position and
        /// orientation are re-derived from targetPiece every frame (not cached) so a
        /// physically swinging cymbal keeps the ring aligned to its live pose.
        /// </summary>
        /// <summary>
        /// pieceColor is the target piece's colour code, or null for a piece that isn't
        /// colour-coded (the cymbals) - in which case the ring keeps CueRingMaterial's own
        /// original colour.
        /// </summary>
        public void Begin(Conductor conductor, float noteTimeSeconds, DrumPiece targetPiece, Vector3 localFlatAxis, float targetDiameter, Color? pieceColor)
        {
            m_Conductor = conductor;
            m_TargetPiece = targetPiece;
            m_TargetCollider = targetPiece.GetComponent<Collider>();
            m_LocalFlatAxis = localFlatAxis;
            m_SpawnTimeSeconds = conductor.SongPositionSeconds;
            m_NoteTimeSeconds = noteTimeSeconds;
            m_TargetDiameter = targetDiameter;
            ApplyColor(pieceColor);
            IsFinished = false;
            gameObject.SetActive(true);
            UpdatePose();
        }

        /// <summary>
        /// Tints this ring to the target piece's colour via a MaterialPropertyBlock, so every
        /// pooled ring can show a different colour off the one shared (translucent) material -
        /// no per-ring material instances, no edits leaking back into the shared asset. A null
        /// colour clears the override so a recycled ring reverts to the material's own colour
        /// (used by cymbal cues, which aren't colour-coded).
        /// </summary>
        void ApplyColor(Color? pieceColor)
        {
            m_PropertyBlock.Clear();
            if (pieceColor.HasValue)
            {
                Color color = pieceColor.Value;
                color.a = ringAlpha;
                m_PropertyBlock.SetColor(k_BaseColor, color);
                m_PropertyBlock.SetColor(k_Color, color);
            }

            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        void Update()
        {
            if (IsFinished)
            {
                return;
            }

            UpdatePose();

            float t = Mathf.InverseLerp(m_SpawnTimeSeconds, m_NoteTimeSeconds, m_Conductor.SongPositionSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(m_TargetDiameter * approachSizeMultiplier, m_TargetDiameter, t);

            if (t >= 1f)
            {
                IsFinished = true;
                gameObject.SetActive(false);
            }
        }

        void UpdatePose()
        {
            Vector3 worldFlatNormal = m_TargetPiece.transform.TransformDirection(m_LocalFlatAxis);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, worldFlatNormal);
            transform.SetPositionAndRotation(m_TargetCollider.bounds.center, rotation);
        }

        static Mesh BuildRingMesh(float innerRadiusRatio, int segments)
        {
            const float outerRadius = 0.5f;
            float innerRadius = outerRadius * innerRadiusRatio;

            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var triangles = new int[segments * 12]; // 2 quads (front+back) * 2 tris * 3 indices, per segment

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
                uvs[i * 2] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                uvs[i * 2 + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int outerA = i * 2;
                int innerA = i * 2 + 1;
                int outerB = next * 2;
                int innerB = next * 2 + 1;

                // Top winding (visible from above) and bottom winding (visible from below) - double-sided.
                triangles[t++] = outerA; triangles[t++] = outerB; triangles[t++] = innerA;
                triangles[t++] = innerA; triangles[t++] = outerB; triangles[t++] = innerB;

                triangles[t++] = innerA; triangles[t++] = outerB; triangles[t++] = outerA;
                triangles[t++] = innerB; triangles[t++] = outerB; triangles[t++] = innerA;
            }

            var mesh = new Mesh { name = "CueRing (procedural)" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
