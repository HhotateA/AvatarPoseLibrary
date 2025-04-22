using System;
using com.hhotatea.avatar_pose_library.logic;
using nadena.dev.ndmf;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(AutoThumbnailPlugin))]
namespace com.hhotatea.avatar_pose_library.editor
{
    public class AutoThumbnailPlugin : Plugin<AutoThumbnailPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("AvatarPose: Replace thumbnail...", ctx =>
                {
                    var settings = ctx.AvatarRootObject.GetComponentsInChildren<AvatarPoseLibrary>();
                    foreach (var setting in settings)
                    {
                        using (var capture = new ThumbnailGenerator(ctx.AvatarRootObject))
                        {
                            foreach (var category in setting.data.categories)
                            {
                                foreach (var pose in category.poses)
                                {
                                    if(!pose.autoThumbnail) continue;
                                    var c = SearchMenu(ctx.AvatarDescriptor.expressionsMenu,pose);
                                    if (c == null)
                                    {
                                        continue;
                                    }
                                    c.icon = capture.Capture(pose.animationClip);
                                }
                            }
                        }
                    }
                });
        }

        VRCExpressionsMenu.Control SearchMenu(VRCExpressionsMenu menu,PoseEntry pose)
        {
            foreach (var control in menu.controls)
            {
                var c = SearchMenuRecursively(control,pose);
                if (c != null) return c; 
            }

            return null;
        }

        VRCExpressionsMenu.Control SearchMenuRecursively(VRCExpressionsMenu.Control control,PoseEntry pose)
        {
            if (control == null) return null;
            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    foreach (var c in control.subMenu.controls)
                    {
                        var sc = SearchMenuRecursively(c,pose);
                        if (sc != null) return sc;
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    if (control.parameter == null) return null;
                    if (control.parameter.name == pose.parameter &&
                        Math.Abs(control.value - pose.value) < 0.01f)
                    {
                        return control;
                    }
                    break;
            }
            return null;
        }

        /// <summary>
        /// サムネイルの更新
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        Texture2D UpdateThumbnail(GameObject obj,AnimationClip pose)
        {
            var avatar = obj.GetComponentInParent<VRCAvatarDescriptor>();
            if(avatar == null) return null;

            using var capture = new ThumbnailGenerator(obj);
            return capture.Capture(pose);
        }
    }
}