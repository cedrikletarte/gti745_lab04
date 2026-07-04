using System.Collections.Generic;
using UnityEngine;
using DrumKit.Pieces;

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
            }
        }

        public bool TryGetPiece(DrumPieceId id, out DrumPiece piece) => m_PiecesById.TryGetValue(id, out piece);

        public bool TryGetId(DrumPiece piece, out DrumPieceId id) => m_IdsByPiece.TryGetValue(piece, out id);

        public IEnumerable<DrumPieceId> KnownPieceIds => m_PiecesById.Keys;

        /// <summary>
        /// The piece's own local-space axis pointing through its thinnest dimension - i.e.
        /// the visual "flat face" normal (a drum/cymbal is a short disc: two large
        /// dimensions plus one short "thickness" one). This is purely geometric and
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

                    Vector3 extents = max - min;
                    if (extents.x <= extents.y && extents.x <= extents.z) return Vector3.right;
                    if (extents.y <= extents.x && extents.y <= extents.z) return Vector3.up;
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
