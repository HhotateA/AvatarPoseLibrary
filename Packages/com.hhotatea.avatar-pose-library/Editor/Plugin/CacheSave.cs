using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using Object = UnityEngine.Object;

namespace com.hhotatea.avatar_pose_library.editor
{
    public class CacheSave
    {
        string fileName;
        string filePath;
        CacheModel cacheAsset;

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

        public void Deleate()
        {
            if (cacheAsset == null)
            {
                return;
            }
            DeleateAsset(cacheAsset.menuObject);
            DeleateAsset(cacheAsset.paramObject);
            DeleateAsset(cacheAsset);
            AssetDatabase.Refresh();
            cacheAsset = null;
        }

        void DeleateAsset(Object o)
        {
            if (o == null) return;
            var existingPath = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(existingPath)) return;
            if (AssetDatabase.DeleteAsset(existingPath))
            {
                Debug.Log($"AssetPoseLibrary.CacheSave: Deleate cache at {existingPath}");
            }
            else
            {
                Debug.LogWarning($"AssetPoseLibrary.CacheSave: Failed to delete cache at {existingPath}");
            }
        }

        void Create(CacheModel asset)
        {
            asset.name = Path.GetFileNameWithoutExtension(filePath);
            AssetDatabase.CreateAsset(asset, filePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"AssetPoseLibrary.CacheSave: Create cache at {filePath}");
            cacheAsset = asset;
        }

        bool IsEnableCache()
        {
            if (!cacheAsset) return false;
            if (!cacheAsset.locomotionLayer) return false;
            if (!cacheAsset.paramLayer) return false;
            if (!cacheAsset.trackingLayer) return false;
            if (!cacheAsset.menuObject) return false;
            if (!cacheAsset.paramObject) return false;
            return true;
        }

        public CacheModel LoadAsset()
        {
            if (!IsEnableCache())
            {
                return null;
            }
            CacheModel asset = new CacheModel();
            asset.locomotionLayer = Object.Instantiate(cacheAsset.locomotionLayer);
            asset.paramLayer = Object.Instantiate(cacheAsset.paramLayer);
            asset.trackingLayer = Object.Instantiate(cacheAsset.trackingLayer);
            asset.menuObject = GameObject.Instantiate(cacheAsset.menuObject);
            asset.menuObject.name = cacheAsset.libraryName;
            asset.paramObject = GameObject.Instantiate(cacheAsset.paramObject);
            return asset;
        }

        public bool SaveAsset(CacheModel asset)
        {
            if (asset == null)
            {
                Debug.LogError($"AvatarPoseLibrary.CacheSave: Asset is null {asset.name}");
                return false;
            }

            Deleate();
            Create(asset);

            asset.libraryName = asset.menuObject.name;
            SaveAnimator(asset.locomotionLayer,filePath);
            SaveAnimator(asset.paramLayer,filePath);
            SaveAnimator(asset.trackingLayer,filePath);
            asset.menuObject = SaveGameObject(asset.menuObject,filePath);
            asset.paramObject = SaveGameObject(asset.paramObject,filePath);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(filePath);
            Debug.Log($"AvatarPoseLibrary.CacheSave: Save cache to {fileName}");
            return true;
        }

        GameObject SaveGameObject(GameObject go, string path)
        {
            var prefabName = System.Guid.NewGuid ().ToString ("N").Substring (0, ConstVariables.HashLong);
            var prefabPath = EnsureFilePath(Path.ChangeExtension(prefabName, "prefab"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            GameObject.DestroyImmediate(go);
            return prefab;
        }

        void SaveAnimator(AnimatorController ac, string path)
        {
            if (ac == null) return;
            AddAsset(ac, path);
            foreach (var l in ac.layers)
            {
                SaveStateMachine(l.stateMachine, path);
            }
        }

        void SaveStateMachine(AnimatorStateMachine st, string path)
        {
            if (st == null) return;
            AddAsset(st, path);
            foreach (var t in st.anyStateTransitions)
            {
                AddAsset(t, path);
            }
            foreach (var t in st.entryTransitions)
            {
                AddAsset(t, path);
            }
            foreach (var s in st.stateMachines)
            {
                SaveStateMachine(s.stateMachine, path);
            }
            foreach (var s in st.states)
            {
                SaveState(s.state,path);
            }
            foreach (var t in st.anyStateTransitions)
            {
                AddAsset(t, path);
            }
            foreach (var t in st.entryTransitions)
            {
                AddAsset(t, path);
            }
            foreach (var b in st.behaviours)
            {
                AddAsset(b, path);
            }
        }

        void SaveState(AnimatorState st, string path)
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

        void SaveMotion(Motion mt, string path)
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

        void AddAsset(Object o, string path)
        {
            if (o == null) return;
            var existingPath = AssetDatabase.GetAssetPath(o);
            if (!string.IsNullOrEmpty(existingPath)) return;

            AssetDatabase.AddObjectToAsset(o, path);
            EditorUtility.SetDirty(o);
        }

        string EnsureFilePath(string fileName)
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