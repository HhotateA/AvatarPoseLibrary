using System.IO;
using UnityEditor;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class PackageReplaceHelper
    {
        private const string MenuForceReplace = "Tools/Avatar Pose Library/Force Replace Install (Silent Overwrite)";
        private const string PackageFolder = "Packages/com.hhotatea.avatar-pose-library";

        [MenuItem(MenuForceReplace, false, 300)]
        private static void ForceReplaceInstall()
        {
            var path = EditorUtility.OpenFilePanel("Select AvatarPoseLibrary_Mod.unitypackage", "", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            ForceInstallPackage(path);
        }

        public static void ForceInstallPackage(string unitypackagePath)
        {
            if (string.IsNullOrEmpty(unitypackagePath) || !File.Exists(unitypackagePath))
            {
                EditorUtility.DisplayDialog("Avatar Pose Library", "unitypackage not found:\n" + unitypackagePath, "OK");
                return;
            }
            // Delete old package folder first so ImportPackage won't show "already exists" - silent replace
            if (AssetDatabase.IsValidFolder(PackageFolder))
            {
                // Use AssetDatabase to keep meta handling correct, fallback to IO if needed
                if (!AssetDatabase.DeleteAsset(PackageFolder))
                {
                    try { Directory.Delete(PackageFolder, true); } catch {}
                    FileUtil.DeleteFileOrDirectory(PackageFolder);
                    FileUtil.DeleteFileOrDirectory(PackageFolder + ".meta");
                }
                AssetDatabase.Refresh();
            }
            // false = non-interactive, overwrites without dialog
            AssetDatabase.ImportPackage(unitypackagePath, false);
            Debug.Log("AvatarPoseLibrary: Force replaced " + PackageFolder + " from " + unitypackagePath);
        }
    }
}
