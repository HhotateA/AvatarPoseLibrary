using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.hhotatea.avatar_pose_library.editor
{
    public class CacheSave
    {
        const string folderPath = "Packages/com.hhotatea.avatar_pose_library/_Cache";
        string fileName;
        string filePath;
        ScriptableObject mainAsset;
        ScriptableObject[] assets;

        public CacheSave(string guid)
        {
            fileName = Path.ChangeExtension(guid, "asset");
            filePath = EnsureFilePath(fileName);
            mainAsset = EnsureMainAsset(filePath);
            assets = EnsureAssets(filePath);
        }

        public T LoadAsset<T>(string name) where T : Object
        {
            foreach (var obj in assets)
            {
                if (obj.name == name)
                    return obj as T;
            }
            return null;
        }

        public bool SaveAsset(string name, Object asset)
        {
            var obj = LoadAsset<Object>(name);
            if (obj)
            {
                return false;
            }
            AssetDatabase.AddObjectToAsset(asset, filePath);
            EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssets();
            return true;
        }

        static string EnsureFilePath(string fileName)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath,fileName);
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

        static ScriptableObject[] EnsureAssets(string filePath)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(filePath);
            return System.Array.ConvertAll(objs, o => (ScriptableObject)o);
        }
    }
}