using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using DrumKit.Striking;

namespace DrumKit.EditorTools
{
    /// <summary>
    /// Builds the striker tip prefab (small trigger collider + kinematic Rigidbody +
    /// velocity tracking + DrumStriker) and attaches it to both hand controllers in the
    /// open scene. Kept separate from the physical drumstick so that swapping to a real,
    /// grabbed stick later is a matter of re-parenting this same tip and swapping its
    /// IVelocityProvider - see RigidbodyVelocityProvider.
    /// </summary>
    static class DrumTipSetupTool
    {
        const string PrefabPath = "Assets/DrumKit/Prefabs/DrumTip.prefab";
        const float TipRadius = 0.015f;
        static readonly Vector3 k_TipLocalOffset = new Vector3(0f, 0f, 0.12f);

        [MenuItem("Tools/Drum Kit/4) Create Drum Tip Prefab")]
        static void CreateDrumTipPrefab()
        {
            EnsureFolder("Assets/DrumKit/Prefabs");

            var tip = new GameObject("DrumTip");
            try
            {
                var sphere = tip.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = TipRadius;

                var rigidbody = tip.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                tip.AddComponent<TransformVelocityTracker>();
                tip.AddComponent<DrumStriker>();

                PrefabUtility.SaveAsPrefabAsset(tip, PrefabPath, out bool success);
                Debug.Log(success
                    ? $"[Drum Kit] Drum tip prefab created at {PrefabPath}."
                    : $"[Drum Kit] Failed to save drum tip prefab at {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(tip);
            }
        }

        [MenuItem("Tools/Drum Kit/5) Attach Drum Tips To XR Controllers")]
        static void AttachDrumTipsToControllers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Drum Kit] No prefab at {PrefabPath} - run step 4 first.");
                return;
            }

            AttachTip(prefab, "Left Controller");
            AttachTip(prefab, "Right Controller");
        }

        static void AttachTip(GameObject prefab, string controllerName)
        {
            Transform controller = FindInOpenScenes(controllerName);
            if (controller == null)
            {
                Debug.LogError($"[Drum Kit] Could not find a '{controllerName}' transform in the open scene(s).");
                return;
            }

            if (controller.Find("DrumTip") != null)
            {
                Debug.Log($"[Drum Kit] '{controllerName}' already has a DrumTip - skipping.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller);
            Undo.RegisterCreatedObjectUndo(instance, "Attach Drum Tip");
            instance.transform.localPosition = k_TipLocalOffset;
            instance.transform.localRotation = Quaternion.identity;

            Debug.Log($"[Drum Kit] Attached DrumTip to '{controllerName}'.");
        }

        static Transform FindInOpenScenes(string name)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    var queue = new Queue<Transform>();
                    queue.Enqueue(root.transform);

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
