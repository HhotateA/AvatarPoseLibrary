using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using com.hhotatea.avatar_pose_library.component;

namespace com.hhotatea.avatar_pose_library.editor
{
    public class CacheSave
    {
        string fileName;
        string filePath;
        CacheModel cacheAsset;

        public CacheSave(string guid)
        {
            fileName = Path.ChangeExtension(guid, "asset");
            filePath = EnsureFilePath(fileName);
            cacheAsset = AssetDatabase.LoadAssetAtPath<CacheModel>(filePath);
        }

        public void Deleate()
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (AssetDatabase.DeleteAsset(filePath))
            {
                Debug.Log($"AssetPoseLibrary.CacheSave: Deleate cache at {filePath}");
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning($"AssetPoseLibrary.CacheSave: Failed to delete cache at {filePath}");
            }
            cacheAsset = null;
        }

        void Create(CacheModel asset)
        {
            asset.name = Path.GetFileNameWithoutExtension(filePath);
            AssetDatabase.CreateAsset(asset, filePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"AssetPoseLibrary.CacheSave: Create cache at {filePath}");
            cacheAsset = asset;
        }

        public CacheModel LoadAsset()
        {
            return cacheAsset;
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

            SaveAnimator(asset.locomotionLayer,filePath);
            SaveAnimator(asset.paramLayer,filePath);
            SaveAnimator(asset.trackingLayer,filePath);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(filePath);
            Debug.Log($"AvatarPoseLibrary.CacheSave: Save cache to {fileName}");
            return true;
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