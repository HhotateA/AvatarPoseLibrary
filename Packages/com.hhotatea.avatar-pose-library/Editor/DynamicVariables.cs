using System;
using com.hhotatea.avatar_pose_library.component;
using UnityEditor;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class DynamicVariables
    {
        private const string settingsGuid = "ae5c4065be3495d418f9d2709e536100";
        private static AvatarPoseSettings settingsBuff;

        public static AvatarPoseSettings Settings
        {
            get
            {
                if (settingsBuff) return settingsBuff;

                var filePath = AssetDatabase.GUIDToAssetPath(settingsGuid);
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new NullReferenceException("Settingファイルが見つかりません。再インポートしてください。");
                }

                settingsBuff = AssetDatabase.LoadAssetAtPath<AvatarPoseSettings>(filePath);
                return settingsBuff;
            }
        }
    }
}