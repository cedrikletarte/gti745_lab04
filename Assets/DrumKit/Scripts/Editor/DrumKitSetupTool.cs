using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DrumKit.Audio;
using DrumKit.Pieces;

namespace DrumKit.EditorTools
{
    /// <summary>
    /// Guided, click-driven setup for the drum kit: creating sound bank assets, wiring
    /// generated placeholder clips into them, and turning the named pieces of the
    /// Drum Set_sketchfab model into working DrumPiece hit-zones. Exists because the model's
    /// sub-mesh names carry no per-piece meaning on their own - this tool relies on the
    /// hierarchy names the pieces were given in the scene instead of guessing geometry.
    /// </summary>
    static class DrumKitSetupTool
    {
        struct PieceConfig
        {
            public string HierarchyName;
            public string SoundBankName;
            public bool IsChokeable;
            public float PitchOffsetSemitones;
        }

        static readonly PieceConfig[] k_Pieces =
        {
            new PieceConfig { HierarchyName = "Bass Drum", SoundBankName = "BassDrum", IsChokeable = false, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Snare Drum", SoundBankName = "SnareDrum", IsChokeable = false, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Left Tom-Tom", SoundBankName = "RackTom", IsChokeable = false, PitchOffsetSemitones = 3f },
            new PieceConfig { HierarchyName = "Right Tom-Tom", SoundBankName = "RackTom", IsChokeable = false, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Floor Tom", SoundBankName = "FloorTom", IsChokeable = false, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Hi-Hat Cymbals", SoundBankName = "HiHat", IsChokeable = true, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Left Crash Cymbal", SoundBankName = "Crash", IsChokeable = true, PitchOffsetSemitones = 0f },
            new PieceConfig { HierarchyName = "Right Crash Cymbal", SoundBankName = "Crash", IsChokeable = true, PitchOffsetSemitones = 0f },
        };

        const string SoundBankFolder = "Assets/DrumKit/Audio/SoundBanks";
        const string GeneratedClipsFolder = "Assets/DrumKit/Audio/Generated";

        // ---------------------------------------------------------------
        // Step 1: sound bank assets
        // ---------------------------------------------------------------

        [MenuItem("Tools/Drum Kit/1) Create Default Sound Bank Assets")]
        static void CreateDefaultSoundBanks()
        {
            EnsureFolder(SoundBankFolder);

            var uniqueBankNames = k_Pieces.Select(p => p.SoundBankName).Distinct();
            var brightPieces = new HashSet<string> { "HiHat", "Crash" };

            int created = 0;
            foreach (string bankName in uniqueBankNames)
            {
                string path = $"{SoundBankFolder}/{bankName}.asset";
                if (AssetDatabase.LoadAssetAtPath<DrumPieceSoundBank>(path) != null)
                {
                    continue;
                }

                var bank = ScriptableObject.CreateInstance<DrumPieceSoundBank>();
                bank.layers = new[]
                {
                    new VelocityLayer
                    {
                        minIntensity = 0f,
                        maxIntensity = 0.55f,
                        volumeRange = new Vector2(0.5f, 0.75f),
                        pitchSemitoneJitter = new Vector2(-0.5f, 0.5f),
                    },
                    new VelocityLayer
                    {
                        minIntensity = 0.55f,
                        maxIntensity = 1f,
                        volumeRange = new Vector2(0.85f, 1f),
                        pitchSemitoneJitter = new Vector2(-0.3f, 0.3f),
                    },
                };
                bank.chokeFadeSeconds = 0.08f;
                bank.edgeBrightnessSemitones = brightPieces.Contains(bankName) ? 2f : 0f;

                AssetDatabase.CreateAsset(bank, path);
                created++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Drum Kit] Created {created} sound bank asset(s) in {SoundBankFolder}. " +
                      $"({uniqueBankNames.Count() - created} already existed.)");
        }

        // ---------------------------------------------------------------
        // Step 2: wire up generated placeholder clips by naming convention
        // "{BankName}_Soft_*" -> low velocity layer, "{BankName}_Hard_*" -> high velocity layer
        // ---------------------------------------------------------------

        [MenuItem("Tools/Drum Kit/2) Auto-Assign Generated Clips To Sound Banks")]
        static void AutoAssignGeneratedClips()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedClipsFolder))
            {
                Debug.LogWarning($"[Drum Kit] No folder at {GeneratedClipsFolder} - generate placeholder clips first.");
                return;
            }

            var uniqueBankNames = k_Pieces.Select(p => p.SoundBankName).Distinct();
            int assignedBanks = 0;

            foreach (string bankName in uniqueBankNames)
            {
                string bankPath = $"{SoundBankFolder}/{bankName}.asset";
                var bank = AssetDatabase.LoadAssetAtPath<DrumPieceSoundBank>(bankPath);
                if (bank == null)
                {
                    Debug.LogWarning($"[Drum Kit] Sound bank '{bankName}' not found at {bankPath} - run step 1 first.");
                    continue;
                }

                if (bank.layers.Length < 2)
                {
                    Debug.LogWarning($"[Drum Kit] Sound bank '{bankName}' does not have the expected 2 velocity layers - skipping.");
                    continue;
                }

                AudioClip[] softClips = FindClips($"{bankName}_Soft");
                AudioClip[] hardClips = FindClips($"{bankName}_Hard");

                bank.layers[0].clips = softClips;
                bank.layers[1].clips = hardClips;
                EditorUtility.SetDirty(bank);

                Debug.Log($"[Drum Kit] '{bankName}': {softClips.Length} soft clip(s), {hardClips.Length} hard clip(s).");
                assignedBanks++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Drum Kit] Assigned generated clips to {assignedBanks} sound bank(s).");
        }

        static AudioClip[] FindClips(string namePrefix)
        {
            string[] guids = AssetDatabase.FindAssets($"{namePrefix} t:AudioClip", new[] { GeneratedClipsFolder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(namePrefix))
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(clip => clip != null)
                .ToArray();
        }

        // ---------------------------------------------------------------
        // Step 3: turn the named pieces under the selected Drum Set root into hit-zones
        // ---------------------------------------------------------------

        [MenuItem("Tools/Drum Kit/3) Auto-Configure Selected Drum Set")]
        static void AutoConfigureSelectedDrumSet()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError("[Drum Kit] Select the Drum Set_sketchfab root in the Hierarchy first.");
                return;
            }

            var missing = new List<string>();
            int configured = 0;
            int skipped = 0;

            foreach (PieceConfig config in k_Pieces)
            {
                Transform piece = FindChildRecursive(root.transform, config.HierarchyName);
                if (piece == null)
                {
                    missing.Add(config.HierarchyName);
                    continue;
                }

                Transform existingHitZone = piece.Find("HitZone");
                if (existingHitZone != null)
                {
                    Debug.Log($"[Drum Kit] '{config.HierarchyName}' already has a HitZone - skipping (delete it to reconfigure).");
                    skipped++;
                    continue;
                }

                ConfigurePiece(piece, config);
                configured++;
            }

            Debug.Log($"[Drum Kit] Auto-configure done: {configured} configured, {skipped} already set up, {missing.Count} not found." +
                      (missing.Count > 0 ? $" Missing: {string.Join(", ", missing)}" : string.Empty));
        }

        static void ConfigurePiece(Transform piece, PieceConfig config)
        {
            Bounds bounds = ComputeRendererBounds(piece);

            var hitZone = new GameObject("HitZone");
            Undo.RegisterCreatedObjectUndo(hitZone, "Create Drum Hit Zone");
            hitZone.transform.SetParent(piece, false);
            hitZone.transform.position = bounds.center;
            hitZone.transform.rotation = Quaternion.identity;

            var box = Undo.AddComponent<BoxCollider>(hitZone);
            box.isTrigger = true;
            box.size = bounds.size * 1.05f; // slight padding so a stick registers before visually clipping the mesh

            Undo.AddComponent<DrumVoicePool>(hitZone);
            var drumPiece = Undo.AddComponent<DrumPiece>(hitZone);

            var bank = AssetDatabase.LoadAssetAtPath<DrumPieceSoundBank>($"{SoundBankFolder}/{config.SoundBankName}.asset");
            if (bank == null)
            {
                Debug.LogWarning($"[Drum Kit] Sound bank '{config.SoundBankName}' not found for '{piece.name}' - run step 1 first, then reassign manually.");
            }

            var serialized = new SerializedObject(drumPiece);
            serialized.FindProperty("soundBank").objectReferenceValue = bank;
            serialized.FindProperty("pitchOffsetSemitones").floatValue = config.PitchOffsetSemitones;
            serialized.FindProperty("isChokeable").boolValue = config.IsChokeable;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[Drum Kit] Configured '{piece.name}' (bank: {config.SoundBankName}, chokeable: {config.IsChokeable}). " +
                      "Strike axis defaults to this hit-zone's local up - verify with the Scene view gizmo, especially for Bass Drum.");
        }

        // ---------------------------------------------------------------
        // Step 6: swap whatever primitive collider a piece has for an exact-fit
        // convex MeshCollider built from its own mesh. A mesh reference can't be
        // guessed from a text file, so unlike the hand radii this genuinely needs
        // to run inside Unity.
        // ---------------------------------------------------------------

        [MenuItem("Tools/Drum Kit/6) Upgrade Hit-Zone Colliders To Mesh Colliders")]
        static void UpgradeCollidersToMesh()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError("[Drum Kit] Select the Drum Set_sketchfab root in the Hierarchy first.");
                return;
            }

            int upgraded = 0;
            int skipped = 0;

            foreach (PieceConfig config in k_Pieces)
            {
                Transform piece = FindChildRecursive(root.transform, config.HierarchyName);
                if (piece == null)
                {
                    continue;
                }

                DrumPiece drumPiece = piece.GetComponentInChildren<DrumPiece>();
                if (drumPiece == null)
                {
                    Debug.LogWarning($"[Drum Kit] '{config.HierarchyName}' has no DrumPiece yet - run step 3 first.");
                    continue;
                }

                GameObject hitZoneObject = drumPiece.gameObject;
                MeshFilter meshFilter = hitZoneObject.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"[Drum Kit] '{hitZoneObject.name}' (under '{config.HierarchyName}') has no MeshFilter on the same GameObject as " +
                                      "its DrumPiece, so an exact-fit MeshCollider can't be built there (the visible mesh lives on a different transform). " +
                                      "Left its existing collider untouched.");
                    skipped++;
                    continue;
                }

                // DrumPiece requires a Collider to be present at all times, so the new one has
                // to go on before the old one comes off - otherwise Unity refuses the removal.
                Collider existingCollider = hitZoneObject.GetComponent<Collider>();

                var meshCollider = Undo.AddComponent<MeshCollider>(hitZoneObject);
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
                meshCollider.isTrigger = true;

                if (existingCollider != null)
                {
                    Undo.DestroyObjectImmediate(existingCollider);
                }

                Debug.Log($"[Drum Kit] '{hitZoneObject.name}': collider upgraded to an exact-fit convex MeshCollider from mesh '{meshFilter.sharedMesh.name}'.");
                upgraded++;
            }

            Debug.Log($"[Drum Kit] Mesh collider upgrade done: {upgraded} upgraded, {skipped} skipped (see warnings above).");
        }

        // ---------------------------------------------------------------
        // Step 7: give cymbals a Rigidbody + ConfigurableJoint so they physically
        // swing/wobble on their stand when struck, instead of sitting rigid like the
        // drums. Reuses the IsChokeable flag as "this is a cymbal" - it already
        // identifies exactly the hi-hat and the two crashes.
        // ---------------------------------------------------------------

        [MenuItem("Tools/Drum Kit/7) Add Swing Physics To Cymbals")]
        static void AddCymbalSwingPhysics()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogError("[Drum Kit] Select the Drum Set_sketchfab root in the Hierarchy first.");
                return;
            }

            int configured = 0;

            foreach (PieceConfig config in k_Pieces)
            {
                if (!config.IsChokeable)
                {
                    continue;
                }

                Transform piece = FindChildRecursive(root.transform, config.HierarchyName);
                if (piece == null)
                {
                    continue;
                }

                DrumPiece drumPiece = piece.GetComponentInChildren<DrumPiece>();
                if (drumPiece == null)
                {
                    Debug.LogWarning($"[Drum Kit] '{config.HierarchyName}' has no DrumPiece yet - run step 3 first.");
                    continue;
                }

                GameObject go = drumPiece.gameObject;

                var rigidbody = go.GetComponent<Rigidbody>();
                if (rigidbody == null)
                {
                    rigidbody = Undo.AddComponent<Rigidbody>(go);
                }
                rigidbody.mass = 0.15f;
                rigidbody.linearDamping = 0.5f;
                rigidbody.angularDamping = 1f;
                rigidbody.useGravity = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var joint = go.GetComponent<ConfigurableJoint>();
                if (joint == null)
                {
                    joint = Undo.AddComponent<ConfigurableJoint>(go);
                }
                joint.connectedBody = null; // anchored to the world, not to another Rigidbody
                joint.anchor = Vector3.zero; // pivot at the cymbal's own origin (its mount point)
                // Auto-configure so the connected anchor is computed from wherever this instance
                // actually sits at runtime, instead of baking today's world position into the
                // prefab - a hand-picked connectedAnchor breaks the moment the same prefab is
                // placed at a different position/rotation in another scene.
                joint.autoConfigureConnectedAnchor = true;
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;

                var tiltLimit = new SoftJointLimit { limit = 35f, bounciness = 0.15f };
                joint.lowAngularXLimit = new SoftJointLimit { limit = -35f, bounciness = 0.15f };
                joint.highAngularXLimit = tiltLimit;
                joint.angularZLimit = tiltLimit;
                joint.angularYLimit = new SoftJointLimit { limit = 40f, bounciness = 0.15f };

                // Gentle spring back toward resting flat - stands in for the felt washer
                // friction that keeps a real cymbal roughly level between hits. Kept soft
                // so the swing itself stays visible instead of snapping back immediately.
                var centeringDrive = new JointDrive { positionSpring = 4f, positionDamper = 0.5f, maximumForce = 50f };
                joint.angularXDrive = centeringDrive;
                joint.angularYZDrive = centeringDrive;

                Debug.Log($"[Drum Kit] '{go.name}': added swing physics (Rigidbody + ConfigurableJoint).");
                configured++;
            }

            Debug.Log($"[Drum Kit] Swing physics added to {configured} cymbal(s).");
        }

        static Bounds ComputeRendererBounds(Transform piece)
        {
            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(piece.position, Vector3.one * 0.1f);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
