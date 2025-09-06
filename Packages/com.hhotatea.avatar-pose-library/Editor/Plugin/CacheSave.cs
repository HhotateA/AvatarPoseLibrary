using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;

namespace com.hhotatea.avatar_pose_library.editor
{
    public class CacheSave
    {
        const string folderPath = "Assets/_APLCache";
        string fileName;
        string filePath;
        ScriptableObject mainAsset;
        Object[] assets;

        public CacheSave(string guid)
        {
            fileName = Path.ChangeExtension(guid, "asset");
            filePath = EnsureFilePath(fileName);
            mainAsset = EnsureMainAsset(filePath);
            assets = EnsureAssets(filePath);
        }

        public void Deleate()
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (AssetDatabase.DeleteAsset(filePath))
            {
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning($"Failed to delete asset at {filePath}");
            }
        }

        public T LoadAsset<T>(string name) where T : Object
        {
            foreach (var obj in assets)
            {
                if (obj.name == name)
                {
                    Debug.Log($"AssetPoseLibrary.CacheSave: Get Cache {fileName} _ {name}");
                    return obj as T;
                }
            }
            return null;
        }

        public bool SaveAsset(string name, Object asset)
        {
            if (asset == null)
            {
                Debug.LogError("AvatarPoseLibrary.CacheSave: Asset is Null");
                return false;
            }
            var obj = LoadAsset<Object>(name);
            if (obj)
            {
                Undo.DestroyObjectImmediate(obj);
            }
            asset.name = name;
            AddAsset(asset, filePath);

            if (asset is AnimatorController ac)
            {
                SaveAnimator(ac,filePath);
            }

            EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(filePath);
            return true;
        }

        static void SaveAnimator(AnimatorController ac, string path)
        {
            if (ac == null) return;
            foreach (var l in ac.layers)
            {
                SaveStateMachine(l.stateMachine, path);
            }
        }

        static void SaveStateMachine(AnimatorStateMachine st, string path)
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

        static void SaveState(AnimatorState st, string path)
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

        static void SaveMotion(Motion mt, string path)
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

        static void AddAsset(Object o, string path)
        {
            if (o == null) return;
            var existingPath = AssetDatabase.GetAssetPath(o);
            if (!string.IsNullOrEmpty(existingPath)) return;

            AssetDatabase.AddObjectToAsset(o, path);
            EditorUtility.SetDirty(o);
        }

        static string EnsureFilePath(string fileName)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath, fileName);
        }

        static ScriptableObject EnsureMainAsset(string filePath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(filePath);

            if (asset == null)
            {
                var root = ScriptableObject.CreateInstance<ScriptableObject>();
                root.name = Path.GetFileNameWithoutExtension(filePath);
                AssetDatabase.CreateAsset(root, filePath);
                AssetDatabase.SaveAssets();
                asset = root;
            }
            return asset;
        }

        static Object[] EnsureAssets(string filePath)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(filePath);
            return System.Array.ConvertAll(objs, o => (Object)o);
        }
    }
}