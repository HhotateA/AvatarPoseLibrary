using System.Linq;
using com.hhotatea.avatar_pose_library.logic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using VRC.SDK3.Avatars.Components;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[assembly: ExportsPlugin(typeof(DataConvertPlugin))]
namespace com.hhotatea.avatar_pose_library.editor
{
    public class DataConvertPlugin : Plugin<DataConvertPlugin>
    {
        public override string DisplayName => "AvatarPoseLibrary";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("AvatarPose: Data Converting...", ctx =>
                {
                    var settings = ctx.AvatarRootObject.GetComponentsInChildren<AvatarPoseLibrary>();

                    // ターゲットメニューが指定されているものを個別処理
                    foreach (var setting in settings.Where(e => e.target != null))
                    {
                        var d = setting.data.UpdateParameter();
                        var go = new GameObject(d.Guid);
                        go.transform.SetParent(setting.transform);
    
                        BuildRuntimeAnimator(go, d);
                        BuildRuntimeMenu(go, d, setting.target);
                        BuildRuntimeParameter(go, d);
                    }

                    // ターゲット未指定のデータを統合して処理
                    var combinedData = AvatarPoseData.Combine(
                        settings.Where(e => e.target == null)
                            .Select(e => e.data)
                            .ToArray());

                    foreach (var d in combinedData)
                    {
                        var go = new GameObject(d.Guid);
                        var root = settings.FirstOrDefault(e => e.data.name == d.name);
                        go.transform.SetParent(root?.transform ?? ctx.AvatarRootObject.transform);
    
                        BuildRuntimeAnimator(go, d);
                        BuildRuntimeMenu(go, d);
                        BuildRuntimeParameter(go, d);
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
            var result = new GameObject();

            var locomotion = AnimatorBuilder.BuildLocomotionAnimator(data);
            
            var ma_base = result.AddComponent<ModularAvatarMergeAnimator>();
            ma_base.animator = locomotion;
            ma_base.pathMode = MergeAnimatorPathMode.Absolute;
            ma_base.matchAvatarWriteDefaults = true;
            ma_base.layerType = VRCAvatarDescriptor.AnimLayerType.Base;
            
            var ma_action = result.AddComponent<ModularAvatarMergeAnimator>();
            ma_action.animator = locomotion;
            ma_action.pathMode = MergeAnimatorPathMode.Absolute;
            ma_action.matchAvatarWriteDefaults = true;
            ma_action.layerType = VRCAvatarDescriptor.AnimLayerType.Action;
            
            var ma_fx = result.AddComponent<ModularAvatarMergeAnimator>();
            ma_fx.animator = AnimatorBuilder.BuildFxAnimator(data);
            ma_fx.pathMode = MergeAnimatorPathMode.Absolute;
            ma_fx.matchAvatarWriteDefaults = true;
            ma_fx.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            
            var ma_tracking = result.AddComponent<ModularAvatarMergeAnimator>();
            ma_tracking.animator = AnimatorBuilder.BuildTrackingAnimator(data);
            ma_tracking.pathMode = MergeAnimatorPathMode.Absolute;
            ma_tracking.matchAvatarWriteDefaults = true;
            ma_tracking.layerType = VRCAvatarDescriptor.AnimLayerType.Base;
            
            result.transform.SetParent(obj.transform);
        }

        /// <summary>
        /// メニューの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        /// <param name="target"></param>
        void BuildRuntimeMenu(GameObject obj,AvatarPoseData data,VRCExpressionsMenu target = null)
        {
            var result = MenuBuilder.BuildPoseMenu(data);

            var installer = result.AddComponent<ModularAvatarMenuInstaller>();
            installer.installTargetMenu = target;
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