using System.Collections.Generic;
using UnityEngine;
using DrumKit.Pieces;
using DrumKit.Visuals;

namespace DrumKit.Rhythm
{
    /// <summary>
    /// Resolves the stable DrumPieceId enum used by data assets (SongChart) to the actual
    /// DrumPiece components in the scene, by hierarchy name - the same names
    /// DrumKitSetupTool uses at edit time to configure the kit. Requires no changes to the
    /// Drum prefab: identity is derived purely from names that already exist.
    /// </summary>
    public class RhythmPieceRegistry : MonoBehaviour
    {
        [Tooltip("Root of the drum kit hierarchy (the Drum prefab instance in the scene).")]
        [SerializeField] Transform drumKitRoot;

        [Tooltip("Tint each instrument with its DrumPalette colour at startup, so a piece and the rings that point to it read as the same colour.")]
        [SerializeField] bool tintPieces = true;

        [Tooltip("Only the sub-mesh using a material with this exact name is recoloured (the drum shell's 'Main Color'). Chrome hardware, rims and heads keep their own materials. Cymbals have no such material and are left untouched.")]
        [SerializeField] string mainColorMaterialName = "Main Color";

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Color = Shader.PropertyToID("_Color");

        static readonly (DrumPieceId id, string hierarchyName)[] k_PieceNames =
        {
            (DrumPieceId.BassDrum, "Bass Drum"),
            (DrumPieceId.SnareDrum, "Snare Drum"),
            (DrumPieceId.LeftTom, "Left Tom-Tom"),
            (DrumPieceId.RightTom, "Right Tom-Tom"),
            (DrumPieceId.FloorTom, "Floor Tom"),
            (DrumPieceId.HiHat, "Hi-Hat Cymbals"),
            (DrumPieceId.LeftCrash, "Left Crash Cymbal"),
            (DrumPieceId.RightCrash, "Right Crash Cymbal"),
        };

        readonly Dictionary<DrumPieceId, DrumPiece> m_PiecesById = new();
        readonly Dictionary<DrumPiece, DrumPieceId> m_IdsByPiece = new();
        readonly Dictionary<DrumPiece, Vector3> m_LocalFlatAxisByPiece = new();

        void Awake()
        {
            if (drumKitRoot == null)
            {
                Debug.LogError($"{nameof(RhythmPieceRegistry)} on '{name}' has no drumKitRoot assigned.", this);
                return;
            }

            foreach ((DrumPieceId id, string hierarchyName) in k_PieceNames)
            {
                Transform found = FindChildRecursive(drumKitRoot, hierarchyName);
                if (found == null)
                {
                    Debug.LogWarning($"{nameof(RhythmPieceRegistry)}: could not find '{hierarchyName}' under '{drumKitRoot.name}'.", this);
                    continue;
                }

                DrumPiece piece = found.GetComponent<DrumPiece>();
                if (piece == null)
                {
                    Debug.LogWarning($"{nameof(RhythmPieceRegistry)}: '{hierarchyName}' has no DrumPiece component.", this);
                    continue;
                }

                m_PiecesById[id] = piece;
                m_IdsByPiece[piece] = id;
                m_LocalFlatAxisByPiece[piece] = ComputeLocalFlatAxis(piece.GetComponent<Collider>());

                if (tintPieces && DrumPalette.TryGetColor(id, out Color pieceColor))
                {
                    TintPiece(piece, pieceColor);
                }
            }
        }

        public bool TryGetPiece(DrumPieceId id, out DrumPiece piece) => m_PiecesById.TryGetValue(id, out piece);

        public bool TryGetId(DrumPiece piece, out DrumPieceId id) => m_IdsByPiece.TryGetValue(piece, out id);

        /// <summary>The colour coding for a resolved piece - the same colour its cue rings use.
        /// Returns false for pieces that aren't colour-coded (the cymbals).</summary>
        public bool TryGetColor(DrumPiece piece, out Color color)
        {
            if (m_IdsByPiece.TryGetValue(piece, out DrumPieceId id))
            {
                return DrumPalette.TryGetColor(id, out color);
            }

            color = Color.white;
            return false;
        }

        /// <summary>
        /// Recolours only the piece's "Main Color" sub-mesh (the paintable drum shell you see
        /// in the inspector) to its palette colour, via a per-sub-mesh MaterialPropertyBlock -
        /// so chrome hardware, rims and heads keep their own materials and the shared "Main
        /// Color" material asset stays untouched (other drums sharing it are unaffected). Skips
        /// PedalButtonPrompt renderers, and leaves cymbals (which have no such material) as they
        /// are - their cue rings still carry the piece's colour.
        /// </summary>
        void TintPiece(DrumPiece piece, Color color)
        {
            color.a = 1f;
            var block = new MaterialPropertyBlock();

            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<PedalButtonPrompt>() != null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int subMesh = 0; subMesh < materials.Length; subMesh++)
                {
                    if (materials[subMesh] == null || materials[subMesh].name != mainColorMaterialName)
                    {
                        continue;
                    }

                    renderer.GetPropertyBlock(block, subMesh);
                    block.SetColor(k_BaseColor, color);
                    block.SetColor(k_Color, color);
                    renderer.SetPropertyBlock(block, subMesh);
                }
            }
        }

        public IEnumerable<DrumPieceId> KnownPieceIds => m_PiecesById.Keys;

        /// <summary>
        /// The piece's own local-space cylinder axis - the one running through both drum
        /// heads, i.e. the visual "flat face" normal (a drum/cymbal is a disc: two equal
        /// diameter dimensions plus one "depth" one along this axis). This is purely geometric and
        /// deliberately independent of DrumPiece.SurfaceUp, which is tuned for hit-detection
        /// math and can point in an entirely different, import-artifact direction that
        /// happens to still work for velocity dot-products but looks wrong for a cosmetic
        /// overlay. Combine with the piece's *current* rotation (not cached) so a swinging
        /// cymbal keeps the ring aligned to its live orientation.
        /// </summary>
        public bool TryGetLocalFlatAxis(DrumPiece piece, out Vector3 axis) => m_LocalFlatAxisByPiece.TryGetValue(piece, out axis);

        static Vector3 ComputeLocalFlatAxis(Collider collider)
        {
            if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                Vector3[] verts = meshCollider.sharedMesh.vertices;
                if (verts.Length > 0)
                {
                    Vector3 min = verts[0];
                    Vector3 max = verts[0];
                    foreach (Vector3 v in verts)
                    {
                        min = Vector3.Min(min, v);
                        max = Vector3.Max(max, v);
                    }

                    Vector3 e = max - min;

                    // A drum is a cylinder: two of its three local extents are ~equal (the round
                    // head diameter) and the third is the odd one out - the axis running through
                    // both heads, i.e. the playing-surface normal we want. That axis is the
                    // SHORTEST extent only for a shallow drum (snare); for a deep one (floor tom,
                    // bass drum) it is the LONGEST, so the old "smallest extent" rule flipped deep
                    // drums onto their side and their rings faced sideways (vertical). Instead pick
                    // the axis whose two perpendicular extents are closest to equal - the roundest
                    // cross-section belongs to the cylinder axis, whatever its length.
                    float roundnessAboutX = Mathf.Abs(e.y - e.z); // cross-section perpendicular to X
                    float roundnessAboutY = Mathf.Abs(e.x - e.z); // ... perpendicular to Y
                    float roundnessAboutZ = Mathf.Abs(e.x - e.y); // ... perpendicular to Z
                    if (roundnessAboutX <= roundnessAboutY && roundnessAboutX <= roundnessAboutZ) return Vector3.right;
                    if (roundnessAboutY <= roundnessAboutX && roundnessAboutY <= roundnessAboutZ) return Vector3.up;
                    return Vector3.forward;
                }
            }

            return Vector3.up;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                if (current.name == name)
                {
                    return current;
                }

                foreach (Transform child in current)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }
    }
}
