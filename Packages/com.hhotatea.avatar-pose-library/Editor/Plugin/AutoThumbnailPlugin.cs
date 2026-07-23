using System;
using com.hhotatea.avatar_pose_library.logic;
using nadena.dev.ndmf;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(AutoThumbnailPlugin))]
namespace com.hhotatea.avatar_pose_library.editor
{
    public class AutoThumbnailPlugin : Plugin<AutoThumbnailPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .AfterPlugin("nadena.dev.modular-avatar")
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .Run("AvatarPose: Replace thumbnail...", ctx =>
                {
                    var settings = ctx.AvatarRootObject.GetComponentsInChildren<AvatarPoseLibrary>();
                    try
                    {
                        foreach (var setting in settings)
                        {
                            var data = setting.data;
                            if (data != null)
                            {
                                var cameraSettings = DynamicVariables.GetCameraSettings(data);
                                using (var capture = new ThumbnailGenerator(ctx.AvatarRootObject))
                                {
                                    if (data.categories != null)
                                    {
                                        foreach (var category in data.categories)
                                        {
                                            if (category?.poses == null)
                                            {
                                                continue;
                                            }

                                            foreach (var pose in category.poses)
                                            {
                                                if (pose == null || !pose.autoThumbnail)
                                                {
                                                    continue;
                                                }

                                                var control = SearchMenu(ctx.AvatarDescriptor.expressionsMenu, pose);
                                                if (control != null)
                                                {
                                                    control.icon = capture.Capture(pose.animationClip, cameraSettings);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            Object.DestroyImmediate(setting);
                        }
                        APLTelemetry.CompleteBuild(ctx.AvatarRootObject);
                    }
                    catch (Exception exception)
                    {
                        APLTelemetry.FailBuild(
                            ctx.AvatarRootObject,
                            settings,
                            exception,
                            "auto_thumbnail");
                        throw;
                    }
                });
        }

        private static VRCExpressionsMenu.Control SearchMenu(VRCExpressionsMenu menu, PoseEntry pose)
        {
            if (menu?.controls == null)
            {
                return null;
            }

            foreach (var control in menu.controls)
            {
                var match = SearchMenuRecursively(control, pose);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static VRCExpressionsMenu.Control SearchMenuRecursively(
            VRCExpressionsMenu.Control control,
            PoseEntry pose)
        {
            if (control == null)
            {
                return null;
            }

            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    if (control.subMenu?.controls == null)
                    {
                        return null;
                    }

                    foreach (var child in control.subMenu.controls)
                    {
                        var match = SearchMenuRecursively(child, pose);
                        if (match != null)
                        {
                            return match;
                        }
                    }
                    break;

                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    if (control.parameter?.name == pose.Parameter
                        && Math.Abs(control.value - pose.Value) < 0.01f)
                    {
                        return control;
                    }
                    break;
            }

            return null;
        }
    }
}
