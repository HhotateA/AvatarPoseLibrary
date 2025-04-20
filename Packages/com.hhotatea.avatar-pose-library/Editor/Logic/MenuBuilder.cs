using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class MenuBuilder
    {
        public static GameObject BuildPoseMenu(AvatarPoseData poseLibrary)
        {
            var result = new GameObject(poseLibrary.name);
            var mResult = result.AddComponent<ModularAvatarMenuItem>();
            mResult.MenuSource = SubmenuSource.Children;
            mResult.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

            var settings = new GameObject(DynamicVariables.Settings.settingMenu.title);
            settings.transform.SetParent(result.transform);
            var mSettings = settings.AddComponent<ModularAvatarMenuItem>();
            mSettings.MenuSource = SubmenuSource.Children;
            mSettings.Control.icon = DynamicVariables.Settings.settingMenu.thumbnail;
            mSettings.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            SettingsMenu(mSettings.transform,poseLibrary);

            // メニューの構造を作る
            foreach (var category in poseLibrary.categories)
            {
                var folder = new GameObject(category.name);
                folder.transform.SetParent(result.transform);
                var mFolder = folder.AddComponent<ModularAvatarMenuItem>();
                mFolder.MenuSource = SubmenuSource.Children;
                mSettings.Control.icon = category.thumbnail;
                mFolder.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

                // 各ポーズステート
                foreach (var pose in category.poses)
                {
                    var item = new GameObject(pose.name);
                    item.transform.SetParent(folder.transform);
                    var mItem = item.AddComponent<ModularAvatarMenuItem>();
                    mItem.Control.icon = pose.thumbnail;
                    mItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    mItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = pose.parameter
                    };
                    mItem.Control.value = pose.value;
                }
            }

            return result;
        }

        /// <summary>
        /// セッティング用のメニュー追加
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="poseLibrary"></param>
        static void SettingsMenu(Transform parent, AvatarPoseData poseLibrary)
        {
            // --- Radialメニューを追加（例: 身長・速度） ---
            CreateRadialMenu(
                parent,
                DynamicVariables.Settings.heightMenu.title,
                DynamicVariables.Settings.heightMenu.thumbnail,
                $"{ConstVariables.HeightParamPrefix}_{poseLibrary.guid}"
            );

            CreateRadialMenu(
                parent,
                DynamicVariables.Settings.speedMenu.title,
                DynamicVariables.Settings.speedMenu.thumbnail,
                $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.guid}"
            );
            
            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.mirrorMenu.title,
                DynamicVariables.Settings.mirrorMenu.thumbnail,
                $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.guid}"
            );

            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.resetMenu.title,
                DynamicVariables.Settings.resetMenu.thumbnail,
                $"{ConstVariables.ResetParamPrefix}_{poseLibrary.guid}"
            );

            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.locomotionMenu.title,
                DynamicVariables.Settings.locomotionMenu.thumbnail,
                $"{ConstVariables.BaseParamPrefix}_{poseLibrary.guid}"
            );
            
            var tracking = new GameObject("TrackingMenu");
            tracking.transform.SetParent(parent.transform);
            var mTracking = tracking.AddComponent<ModularAvatarMenuItem>();
            mTracking.MenuSource = SubmenuSource.Children;
            mTracking.Control.name = DynamicVariables.Settings.trackingMenu.title;
            mTracking.Control.icon = DynamicVariables.Settings.trackingMenu.thumbnail;
            mTracking.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            TrackingMenu(mTracking.transform,poseLibrary);
        }

        static void TrackingMenu(Transform parent, AvatarPoseData poseLibrary)
        {
            // --- トグルメニューを追加（例: トラッキング制御） ---
            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.headMenu.title,
                DynamicVariables.Settings.headMenu.thumbnail,
                $"{ConstVariables.HeadParamPrefix}_{poseLibrary.guid}"
            );

            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.armMenu.title,
                DynamicVariables.Settings.armMenu.thumbnail,
                $"{ConstVariables.ArmParamPrefix}_{poseLibrary.guid}"
            );

            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.footMenu.title,
                DynamicVariables.Settings.footMenu.thumbnail,
                $"{ConstVariables.FootParamPrefix}_{poseLibrary.guid}"
            );

            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.fingerMenu.title,
                DynamicVariables.Settings.fingerMenu.thumbnail,
                $"{ConstVariables.FingerParamPrefix}_{poseLibrary.guid}"
            );
        }

        /// <summary>
        /// Radial型のメニュー項目を作成します（例: 身長、速度）
        /// </summary>
        static void CreateRadialMenu(Transform parent, string name, Texture2D icon, string parameterName)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);

            var item = obj.AddComponent<ModularAvatarMenuItem>();
            item.Control.icon = icon;
            item.Control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            item.Control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = parameterName }
            };
        }

        /// <summary>
        /// トグル型のメニュー項目を作成します（例: 各部位のトラッキング切り替え）
        /// </summary>
        static void CreateToggleMenu(Transform parent, string name, Texture2D icon, string parameterName)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);

            var item = obj.AddComponent<ModularAvatarMenuItem>();
            item.Control.icon = icon;
            item.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            item.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName };
            item.Control.value = 1;
        }
    }
}