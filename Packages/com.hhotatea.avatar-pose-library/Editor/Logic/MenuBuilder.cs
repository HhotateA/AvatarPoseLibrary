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
            if (poseLibrary.target != null)
            {
                var installer = result.AddComponent<ModularAvatarMenuInstaller>();
                installer.installTargetMenu = poseLibrary.target;
            }

            var mResult = result.AddComponent<ModularAvatarMenuItem>();
            mResult.MenuSource = SubmenuSource.Children;
            mResult.Control.icon = poseLibrary.thumbnail;
            mResult.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

            if (poseLibrary.enableHeightParam ||
                poseLibrary.enableSpeedParam ||
                poseLibrary.enableMirrorParam ||
                poseLibrary.enablePoseSpace ||
                poseLibrary.enableTrackingParam)
            {
                // 設定メニュー
                var settings = new GameObject(DynamicVariables.Settings.Menu.setting.title);
                settings.transform.SetParent(result.transform);
                if (poseLibrary.settings != null)
                {
                    var installer = settings.AddComponent<ModularAvatarMenuInstaller>();
                    installer.installTargetMenu = poseLibrary.settings;
                }

                var mSettings = settings.AddComponent<ModularAvatarMenuItem>();
                mSettings.MenuSource = SubmenuSource.Children;
                mSettings.Control.icon = DynamicVariables.Settings.Menu.setting.thumbnail;
                mSettings.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

                SettingsMenu(mSettings.transform, poseLibrary);
            }
            else
            {
                // 設定メニューが必要ない場合
                // リセットだけは作る
                CreateToggleMenu(
                    result.transform,
                    DynamicVariables.Settings.Menu.reset.title,
                    DynamicVariables.Settings.Menu.reset.thumbnail,
                    $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}"
                );
            }


            if (poseLibrary.enableVariationSlider)
            {
                // Variation mode: if single category, use category name (+count) like "Pose 1 (10)" so it matches inspector's [Category Name]
                // If multiple categories, create one radial per category so each radial name == category name
                var varMenu = DynamicVariables.Settings.Menu.variation;
                if (poseLibrary.categories != null && poseLibrary.categories.Count == 1)
                {
                    var cat = poseLibrary.categories[0];
                    string catTitle = cat != null && !string.IsNullOrWhiteSpace(cat.name) ? cat.name : (varMenu != null && !string.IsNullOrWhiteSpace(varMenu.title) ? varMenu.title : "Pose Variation");
                    // Append count if user hasn't already typed it like "Pose 1 (10)"
                    if (cat != null && cat.poses != null && catTitle.IndexOf("(") < 0)
                        catTitle = $"{catTitle} ({cat.poses.Count})";
                    Texture2D thumb = varMenu != null ? varMenu.thumbnail : null;
                    if (thumb == null) thumb = cat != null && cat.thumbnail ? cat.thumbnail : (poseLibrary.thumbnail ? poseLibrary.thumbnail : DynamicVariables.Settings.Menu.pose.thumbnail);
                    string varParam = $"{ConstVariables.VariationParamPrefix}_{poseLibrary.Guid}";
                    CreateRadialMenu(result.transform, catTitle, thumb, varParam);
                }
                else if (poseLibrary.categories != null && poseLibrary.categories.Count > 1)
                {
                    // Multiple categories: keep single global radial named after library (all poses flattened). Per-category radials would duplicate same param.
                    string varTitle = !string.IsNullOrWhiteSpace(poseLibrary.name) ? poseLibrary.name : (varMenu != null && !string.IsNullOrWhiteSpace(varMenu.title) ? varMenu.title : "Pose Variation");
                    Texture2D varThumb = varMenu != null ? varMenu.thumbnail : null;
                    if (varThumb == null) varThumb = poseLibrary.thumbnail ? poseLibrary.thumbnail : DynamicVariables.Settings.Menu.pose.thumbnail;
                    string varParam = $"{ConstVariables.VariationParamPrefix}_{poseLibrary.Guid}";
                    CreateRadialMenu(result.transform, varTitle, varThumb, varParam);
                }
                else
                {
                    string varTitle = !string.IsNullOrWhiteSpace(poseLibrary.name) ? poseLibrary.name : (varMenu != null && !string.IsNullOrWhiteSpace(varMenu.title) ? varMenu.title : "Pose Variation");
                    Texture2D varThumb = varMenu != null ? varMenu.thumbnail : null;
                    if (varThumb == null) varThumb = poseLibrary.thumbnail ? poseLibrary.thumbnail : DynamicVariables.Settings.Menu.pose.thumbnail;
                    string varParam = $"{ConstVariables.VariationParamPrefix}_{poseLibrary.Guid}";
                    CreateRadialMenu(result.transform, varTitle, varThumb, varParam);
                }
            }
            else
            {
                // メニューの構造を作る
                foreach (var category in poseLibrary.categories)
                {
                    var folder = new GameObject(category.name);
                    folder.transform.SetParent(result.transform);
                    if (category.target != null)
                    {
                        var installer = folder.AddComponent<ModularAvatarMenuInstaller>();
                        installer.installTargetMenu = category.target;
                    }

                    var mFolder = folder.AddComponent<ModularAvatarMenuItem>();
                    mFolder.MenuSource = SubmenuSource.Children;
                    mFolder.Control.icon = category.thumbnail;
                    mFolder.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;

                    // 各ポーズステート
                    foreach (var pose in category.poses)
                    {
                        var item = new GameObject(pose.GetDisplayName(DynamicVariables.Settings.Menu.pose.title));
                        item.transform.SetParent(folder.transform);
                        if (pose.target != null)
                        {
                            var installer = item.AddComponent<ModularAvatarMenuInstaller>();
                            installer.installTargetMenu = pose.target;
                        }

                        var mItem = item.AddComponent<ModularAvatarMenuItem>();
                        mItem.Control.icon = pose.thumbnail;
                        mItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                        mItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                        {
                            name = pose.Parameter
                        };
                        mItem.Control.value = pose.Value;
                    }
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
            // リセットメニュー
            CreateToggleMenu(
                parent,
                DynamicVariables.Settings.Menu.reset.title,
                DynamicVariables.Settings.Menu.reset.thumbnail,
                $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}"
            );

            // --- Radialメニューを追加 ---
            if (poseLibrary.enableHeightParam)
            {
                CreateRadialMenu(
                    parent,
                    DynamicVariables.Settings.Menu.height.title,
                    DynamicVariables.Settings.Menu.height.thumbnail,
                    $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}",
                    $"{ConstVariables.HeightUpdateParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.enablePoseSpace)
            {
                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.poseSpace.title,
                    DynamicVariables.Settings.Menu.poseSpace.thumbnail,
                    $"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.enableSpeedParam)
            {
                CreateRadialMenu(
                    parent,
                    DynamicVariables.Settings.Menu.speed.title,
                    DynamicVariables.Settings.Menu.speed.thumbnail,
                    $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.enableMirrorParam)
            {
                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.mirror.title,
                    DynamicVariables.Settings.Menu.mirror.thumbnail,
                    $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.EnableAudioMode)
            {
                CreateRadialMenu(
                    parent,
                    DynamicVariables.Settings.Menu.volume.title,
                    DynamicVariables.Settings.Menu.volume.thumbnail,
                    $"{ConstVariables.AudioParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.enableTrackingParam)
            {
                var tracking = new GameObject(DynamicVariables.Settings.Menu.tracking.title);
                tracking.transform.SetParent(parent.transform);
                var mTracking = tracking.AddComponent<ModularAvatarMenuItem>();
                mTracking.MenuSource = SubmenuSource.Children;
                mTracking.Control.icon = DynamicVariables.Settings.Menu.tracking.thumbnail;
                mTracking.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                TrackingMenu(mTracking.transform, poseLibrary);
            }
        }

        static void TrackingMenu(Transform parent, AvatarPoseData poseLibrary)
        {
            // --- トグルメニューを追加 ---

            if (poseLibrary.enableFxAnimator)
            {
                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.face.title,
                    DynamicVariables.Settings.Menu.face.thumbnail,
                    $"{ConstVariables.FaceParamPrefix}_{poseLibrary.Guid}"
                );
            }

            if (poseLibrary.enableLocomotionAnimator)
            {
                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.head.title,
                    DynamicVariables.Settings.Menu.head.thumbnail,
                    $"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}"
                );

                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.arm.title,
                    DynamicVariables.Settings.Menu.arm.thumbnail,
                    $"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}"
                );

                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.finger.title,
                    DynamicVariables.Settings.Menu.finger.thumbnail,
                    $"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}"
                );

                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.foot.title,
                    DynamicVariables.Settings.Menu.foot.thumbnail,
                    $"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}"
                );

                CreateToggleMenu(
                    parent,
                    DynamicVariables.Settings.Menu.locomotion.title,
                    DynamicVariables.Settings.Menu.locomotion.thumbnail,
                    $"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}"
                );
            }
        }

        /// <summary>
        /// Radial型のメニュー項目を作成します（例: 身長、速度）
        /// </summary>
        static void CreateRadialMenu(Transform parent, string name, Texture2D icon, string subParameterName, string parameterName = "")
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);

            var item = obj.AddComponent<ModularAvatarMenuItem>();
            item.Control.icon = icon;
            item.Control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                item.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName };
            }
            item.Control.subParameters = new VRCExpressionsMenu.Control.Parameter[]
            {
                new VRCExpressionsMenu.Control.Parameter { name = subParameterName }
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