using UnityEngine;
using UnityEditor;
using System;
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
        private readonly string fileName;
        private readonly string filePath;
        private CacheModel cacheAsset;

        public CacheSave(string guid)
        {
            fileName = guid;
            filePath = EnsureFilePath(Path.ChangeExtension(guid, "asset"));
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
            if (CheckAnimatorError(cacheAsset.paramLayer)) return false;
            return true;
        }

        /// <summary>
        /// アニメーターの改竄をチェックする。
        /// 本来は必要ないが、他ツールによってTrackingControlが無効化される場合があり、その対策。
        /// </summary>
        /// <param name="fxAnim"></param>
        /// <returns></returns>
        static bool CheckAnimatorError(AnimatorController fxAnim)
        {
            foreach (var layer in fxAnim.layers)
            {
                if (!layer.name.Contains(ConstVariables.HeadParamPrefix))
                {
                    continue;
                }
                bool isError = true;
                foreach (var s in layer.stateMachine.states)
                {
                    foreach (var beh in s.state.behaviours)
                    {
                        if (beh is VRCAnimatorTrackingControl ctrl)
                        {
                            Debug.LogWarning(layer.name);
                            if (ctrl.trackingHead == VRC_AnimatorTrackingControl.TrackingType.NoChange)
                            {
                                // TrackingがNoChangeになっていたら異常
                                Debug.Log($"AssetPoseLibrary.CacheSave: Ditect animator's error");
                                return true;
                            }
                            else
                            {
                                // 一つでも見つけないと異常なので、ここで代入
                                isError = false;
                            }
                        }
                    }
                }
                return isError;
            }
            return true;
        }

        public CacheModel LoadAsset()
        {
            if (!IsCacheValid())
            {
                return null;
            }
            var asset = ScriptableObject.CreateInstance<CacheModel>();
            asset.locomotionLayer = cacheAsset.locomotionLayer;
            asset.paramLayer = cacheAsset.paramLayer;
            asset.trackingLayer = cacheAsset.trackingLayer;
            asset.menuObject = Object.Instantiate(cacheAsset.menuObject);
            asset.menuObject.name = cacheAsset.libraryName;
            asset.paramObject = Object.Instantiate(cacheAsset.paramObject);
            return asset;
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
                AssetDatabase.ImportAsset(filePath);
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
