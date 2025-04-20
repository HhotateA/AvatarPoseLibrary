using System.Linq;
using com.hhotatea.avatar_pose_library.logic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using UnityEngine;

[assembly: ExportsPlugin(typeof(DataConvertPlugin))]
namespace com.hhotatea.avatar_pose_library.editor
{
    public class DataConvertPlugin : Plugin<DataConvertPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("AvatarPose: Data Converting...", ctx =>
                {
                    var settings = ctx.AvatarRootObject.GetComponentsInChildren<AvatarPoseLibrarySettings>();

                    // 全てのコンポーネントを統合する。
                    var data = AvatarPoseData.Combine(
                        settings.Select(e => e.data).ToArray());

                    foreach (var d in data)
                    {
                        BuildRuntimeAnimator(ctx.AvatarRootObject,d);
                        BuildRuntimeMenu(ctx.AvatarRootObject,d);
                        BuildRuntimeParameter(ctx.AvatarRootObject,d);
                    }
                });
        }

        /// <summary>
        /// アニメーターの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        void BuildRuntimeAnimator(GameObject obj,AvatarPoseData data)
        {
            var result = AnimatorBuilder.BuildPoseAnimator(data);

            var ma = obj.gameObject.AddComponent<ModularAvatarMergeAnimator>();
            ma.animator = result;
            ma.layerType = VRCAvatarDescriptor.AnimLayerType.Base;
        }

        /// <summary>
        /// メニューの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        void BuildRuntimeMenu(GameObject obj,AvatarPoseData data)
        {
            var result = MenuBuilder.BuildPoseMenu(data);

            result.AddComponent<ModularAvatarMenuInstaller>();
            result.transform.SetParent(obj.transform);
        }

        /// <summary>
        /// パラメーターの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        void BuildRuntimeParameter(GameObject obj,AvatarPoseData data)
        {
            var result = ParameterBuilder.BuildPoseParameter(data);

            result.transform.SetParent(obj.transform);
        }
    }
}