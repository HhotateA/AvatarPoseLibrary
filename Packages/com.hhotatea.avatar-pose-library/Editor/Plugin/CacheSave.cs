using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Animations;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using Object = UnityEngine.Object;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.editor
{
    public class CacheSave
    {
        /// <summary>
        /// Upper bound for a single cache asset. Body rotation multiplies the generated
        /// clips by BodyRotationSteps^3 and the height/speed parameters multiply them
        /// again, so a large library can serialize to several gigabytes of YAML. Such a
        /// cache is slower to clone than to rebuild and exhausts both disk and memory,
        /// so oversized caches are discarded instead of being written.
        /// </summary>
        private const long MaxCacheFileBytes = 512L * 1024 * 1024;

        private readonly string fileName;
        private readonly string filePath;
        private readonly string markerPath;
        private CacheModel cacheAsset;

        /// <summary>
        /// Set when a cache was rejected because it exceeded <see cref="MaxCacheFileBytes"/>.
        /// Callers use this to stop retrying the cache for the same library.
        /// </summary>
        public bool ExceededSizeLimit { get; private set; }

        public CacheSave(string guid)
        {
            fileName = guid;
            filePath = EnsureFilePath(Path.ChangeExtension(guid, "asset"));
            markerPath = EnsureFilePath($".{guid}.nocache");
            // A marker is left behind when a previous build generated a graph larger than
            // the cache limit. Regenerating it would write several gigabytes to disk again.
            ExceededSizeLimit = File.Exists(markerPath);
            if (ExceededSizeLimit)
            {
                Debug.LogWarning(
                    $"AvatarPoseLibrary.CacheSave: Cache disabled for {fileName} because a previous build " +
                    $"exceeded {CacheMenu.FormatBytes(MaxCacheFileBytes, "unknown")}. " +
                    "Run Tools/Avatar Pose Library/Clear All Caches after shrinking the library to re-enable it.");
            }

            cacheAsset = AssetDatabase.LoadAssetAtPath<CacheModel>(filePath);
            if (cacheAsset == null)
            {
                Debug.Log($"AssetPoseLibrary.CacheSave: Load cache failed at {filePath}");
            }
            else
            {
                Debug.Log($"AssetPoseLibrary.CacheSave: Load cache success at {filePath}");
            }
        }

        public void Delete()
        {
            if (cacheAsset == null)
            {
                return;
            }
            DeleteAsset(cacheAsset.menuObject);
            DeleteAsset(cacheAsset.paramObject);
            DeleteAsset(cacheAsset);
            AssetDatabase.Refresh();
            cacheAsset = null;
        }

        [Obsolete("Use Delete instead.")]
        public void Deleate()
        {
            Delete();
        }

        private static void DeleteAsset(Object asset)
        {
            if (asset == null) return;
            var existingPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(existingPath)) return;
            if (AssetDatabase.DeleteAsset(existingPath))
            {
                Debug.Log($"AvatarPoseLibrary.CacheSave: Deleted cache at {existingPath}");
            }
            else
            {
                Debug.LogWarning($"AssetPoseLibrary.CacheSave: Failed to delete cache at {existingPath}");
            }
        }

        private void Create(CacheModel asset)
        {
            asset.name = Path.GetFileNameWithoutExtension(filePath);
            AssetDatabase.CreateAsset(asset, filePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"AssetPoseLibrary.CacheSave: Create cache at {filePath}");
            cacheAsset = asset;
        }

        private bool IsCacheValid()
        {
            if (!cacheAsset) return false;
            if (!cacheAsset.locomotionLayer) return false;
            if (!cacheAsset.paramLayer) return false;
            if (!cacheAsset.trackingLayer) return false;
            if (!cacheAsset.menuObject) return false;
            if (!cacheAsset.paramObject) return false;
            if (cacheAsset.version != DynamicVariables.CurrentVersion.ToString()) return false;
            if (HasInvalidTrackingControl(cacheAsset.paramLayer)) return false;
            return true;
        }

        /// <summary>
        /// Detects cached tracking controls that were removed or disabled after generation.
        /// </summary>
        private static bool HasInvalidTrackingControl(AnimatorController fxAnimator)
        {
            // Keep rejecting caches that were corrupted before cached animator controllers were
            // isolated from downstream build plugins.
            foreach (var layer in fxAnimator.layers)
            {
                if (!layer.name.Contains(ConstVariables.HeadParamPrefix))
                {
                    continue;
                }

                bool foundTrackingControl = false;
                foreach (var state in layer.stateMachine.states)
                {
                    foreach (var behaviour in state.state.behaviours)
                    {
                        if (behaviour is not VRCAnimatorTrackingControl control)
                        {
                            continue;
                        }

                        Debug.LogWarning(layer.name);
                        if (control.trackingHead == VRC_AnimatorTrackingControl.TrackingType.NoChange)
                        {
                            Debug.Log("AvatarPoseLibrary.CacheSave: Invalid tracking control detected.");
                            return true;
                        }

                        foundTrackingControl = true;
                    }
                }

                return !foundTrackingControl;
            }

            return true;
        }

        public CacheModel LoadAsset()
        {
            if (RejectIfOversized())
            {
                return null;
            }

            if (!IsCacheValid())
            {
                return null;
            }
            var clonedObjects = new Dictionary<Object, Object>();
            var asset = ScriptableObject.CreateInstance<CacheModel>();
            asset.locomotionLayer = CloneAnimatorForBuild(cacheAsset.locomotionLayer, clonedObjects);
            asset.paramLayer = CloneAnimatorForBuild(cacheAsset.paramLayer, clonedObjects);
            asset.trackingLayer = CloneAnimatorForBuild(cacheAsset.trackingLayer, clonedObjects);
            asset.menuObject = Object.Instantiate(cacheAsset.menuObject);
            asset.menuObject.name = cacheAsset.libraryName;
            asset.paramObject = Object.Instantiate(cacheAsset.paramObject);
            return asset;
        }

        /// <summary>
        /// Creates a transient animator graph for the current build. Only objects stored in this
        /// cache asset are cloned; user-authored motions and other external assets stay shared.
        /// </summary>
        private AnimatorController CloneAnimatorForBuild(
            AnimatorController animator,
            Dictionary<Object, Object> clonedObjects)
        {
            return CloneCachedObject(animator, clonedObjects) as AnimatorController;
        }

        private Object CloneCachedObject(Object source, Dictionary<Object, Object> clonedObjects)
        {
            if (!source)
            {
                return null;
            }

            if (clonedObjects.TryGetValue(source, out var existingClone))
            {
                return existingClone;
            }

            var sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            if (sourcePath != NormalizeAssetPath(filePath) || !IsAnimatorGraphObject(source))
            {
                return source;
            }

            var clone = Object.Instantiate(source);
            clone.name = source.name;
            clonedObjects.Add(source, clone);

            var serializedClone = new SerializedObject(clone);
            var property = serializedClone.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                var referencedObject = property.objectReferenceValue;
                if (!referencedObject || referencedObject == clone)
                {
                    continue;
                }

                property.objectReferenceValue = CloneCachedObject(referencedObject, clonedObjects);
            }
            serializedClone.ApplyModifiedPropertiesWithoutUndo();

            return clone;
        }

        private static bool IsAnimatorGraphObject(Object asset)
        {
            return asset is AnimatorController ||
                   asset is AnimatorStateMachine ||
                   asset is AnimatorState ||
                   asset is AnimatorTransitionBase ||
                   asset is StateMachineBehaviour ||
                   asset is Motion ||
                   asset is AvatarMask;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        public bool SaveAsset(CacheModel asset)
        {
            if (asset == null)
            {
                Debug.LogError("AvatarPoseLibrary.CacheSave: Cannot save a null cache asset.");
                return false;
            }

            try
            {
                Delete();
                Create(asset);

                asset.version = DynamicVariables.CurrentVersion.ToString();
                asset.libraryName = asset.menuObject.name;
                SaveAnimator(asset.locomotionLayer, filePath);
                SaveAnimator(asset.paramLayer, filePath);
                SaveAnimator(asset.trackingLayer, filePath);
                asset.menuObject = SaveGameObject(asset.menuObject);
                asset.paramObject = SaveGameObject(asset.paramObject);

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                // Reject before the forced synchronous import: importing a multi-gigabyte
                // cache is the most expensive part of the save.
                if (RejectIfOversized())
                {
                    return false;
                }

                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport);

                // ImportAsset can replace the imported main asset and its sub-assets. Do not
                // keep using the instances that were assigned before the import.
                cacheAsset = AssetDatabase.LoadAssetAtPath<CacheModel>(filePath);
                if (!IsCacheValid())
                {
                    Debug.LogWarning($"AvatarPoseLibrary.CacheSave: Saved cache is invalid at {filePath}");
                    return false;
                }

                Debug.Log($"AvatarPoseLibrary.CacheSave: Save cache to {fileName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"AvatarPoseLibrary.GetAssetCache: Save cache error \n {e}");
                return false;
            }

        }

        private GameObject SaveGameObject(GameObject gameObject)
        {
            var prefabName = System.Guid.NewGuid().ToString("N").Substring(0, ConstVariables.HashLong);
            var prefabPath = EnsureFilePath(Path.ChangeExtension(prefabName, "prefab"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
            Object.DestroyImmediate(gameObject);
            return prefab;
        }

        private static void SaveAnimator(AnimatorController controller, string path)
        {
            if (controller == null) return;
            AddAsset(controller, path);
            foreach (var layer in controller.layers)
            {
                SaveStateMachine(layer.stateMachine, path);
            }
        }

        private static void SaveStateMachine(AnimatorStateMachine stateMachine, string path)
        {
            if (stateMachine == null) return;
            AddAsset(stateMachine, path);
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                AddAsset(transition, path);
            }
            foreach (var transition in stateMachine.entryTransitions)
            {
                AddAsset(transition, path);
            }
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                SaveStateMachine(childStateMachine.stateMachine, path);
            }
            foreach (var childState in stateMachine.states)
            {
                SaveState(childState.state, path);
            }
            foreach (var behaviour in stateMachine.behaviours)
            {
                AddAsset(behaviour, path);
            }
        }

        private static void SaveState(AnimatorState st, string path)
        {
            if (st == null) return;
            AddAsset(st, path);
            foreach (var m in st.behaviours)
            {
                AddAsset(m, path);
            }
            foreach (var t in st.transitions)
            {
                AddAsset(t, path);
            }
            SaveMotion(st.motion, path);
        }

        private static void SaveMotion(Motion mt, string path)
        {
            if (mt == null) return;
            AddAsset(mt, path);
            if (mt is BlendTree bt)
            {
                foreach (var cm in bt.children)
                {
                    SaveMotion(cm.motion, path);
                }
            }
        }

        private static void AddAsset(Object o, string path)
        {
            if (o == null) return;
            var existingPath = AssetDatabase.GetAssetPath(o);
            if (!string.IsNullOrEmpty(existingPath)) return;

            AssetDatabase.AddObjectToAsset(o, path);
            EditorUtility.SetDirty(o);
        }

        /// <summary>
        /// Discards a cache that grew past <see cref="MaxCacheFileBytes"/>.
        /// A cache this large costs more to deserialize and clone than it saves, and it
        /// forces Unity into several gigabytes of disk writes plus paging on every entry
        /// into Play Mode, so it is deleted rather than kept.
        /// </summary>
        private bool RejectIfOversized()
        {
            var size = GetCacheFileSize();
            if (size <= MaxCacheFileBytes)
            {
                return false;
            }

            ExceededSizeLimit = true;
            Debug.LogError(
                $"AvatarPoseLibrary.CacheSave: Cache size {CacheMenu.FormatBytes(size, "unknown")} " +
                $"exceeds the {CacheMenu.FormatBytes(MaxCacheFileBytes, "unknown")} limit and was deleted: {filePath}\n" +
                "The Height/Speed parameters multiply the generated clips. Reduce the pose count or " +
                "disable those parameters, then rebuild.");
            WriteSizeLimitMarker();
            Delete();
            return true;
        }

        private long GetCacheFileSize()
        {
            try
            {
                var info = new FileInfo(filePath);
                return info.Exists ? info.Length : 0L;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AvatarPoseLibrary.CacheSave: Failed to read cache size at {filePath}: {exception.Message}");
                return 0L;
            }
        }

        /// <summary>
        /// Remembers that this library produces a cache larger than the limit. The marker
        /// is a dot-file so Unity's asset pipeline ignores it, and it lives next to the
        /// cache so "Clear All Caches" removes it.
        /// </summary>
        private void WriteSizeLimitMarker()
        {
            try
            {
                File.WriteAllText(
                    markerPath,
                    "AvatarPoseLibrary skipped caching because the generated graph exceeded " +
                    $"{MaxCacheFileBytes} bytes ({DateTime.UtcNow:o}).\n" +
                    "Delete this file, or run Tools/Avatar Pose Library/Clear All Caches, to try caching again.\n");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AvatarPoseLibrary.CacheSave: Failed to write the size limit marker at {markerPath}: {exception.Message}");
            }
        }

        private static string EnsureFilePath(string fileName)
        {
            var folderPath = DynamicVariables.Settings.cachePath;
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath, fileName);
        }
    }
}
