using System;
using System.Collections.Generic;
using System.Linq;
using com.hhotatea.avatar_pose_library.editor;
using UnityEngine;
using UnityEditor.Animations;
using com.hhotatea.avatar_pose_library.model;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using TrackingType = com.hhotatea.avatar_pose_library.logic.AnimationLayerBuilder.TrackingType;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class AnimatorBuilder
    {
        public static AnimatorController BuildLocomotionAnimator(AvatarPoseData poseLibrary)
        {
            var result = BaseAnimator(poseLibrary);
            
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = $"{ConstVariables.MotionAnimatorPrefix}_{poseLibrary.Guid}",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };
            result.AddLayer(layer);

            // 空のステート（default）
            var defaultState = layer.stateMachine.AddState("Default");
            defaultState.writeDefaultValues = false;
            defaultState.motion = new AnimationClip();

            // ポーズのレイヤー追加
            foreach (var category in poseLibrary.categories)
            {
                foreach (var pose in category.poses)
                {
                    AnimationLayerBuilder.AddLocomotionLayer(pose,layer,defaultState,
                        poseLibrary.enableHeightParam,poseLibrary.enableSpeedParam,poseLibrary.enableMirrorParam,
                        poseLibrary.Guid);
                }
            }

            return result;
        }
        public static AnimatorController BuildFxAnimator(AvatarPoseData poseLibrary)
        {
            var result = BaseAnimator(poseLibrary);

            // レイヤー作成
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer
                {
                    name = $"{ConstVariables.ParamAnimatorPrefix}_{poseLibrary.Guid}",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine(),
                    blendingMode = AnimatorLayerBlendingMode.Override
                };
                result.AddLayer(layer);

                // 空のステート（default）
                var defaultState = layer.stateMachine.AddState("Default");
                defaultState.writeDefaultValues = false;
                defaultState.motion = new AnimationClip();

                // ポーズのレイヤー追加
                foreach (var category in poseLibrary.categories)
                {
                    foreach (var pose in category.poses)
                    {
                        AnimationLayerBuilder.AddParamLayer(layer, pose, defaultState, poseLibrary.Guid);
                    }
                }
            }

            // レイヤー作成
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer
                {
                    name = $"{ConstVariables.FxAnimatorPrefix}_{poseLibrary.Guid}",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine(),
                    blendingMode = AnimatorLayerBlendingMode.Override
                };
                result.AddLayer(layer);

                // 空のステート（default）
                var defaultState = layer.stateMachine.AddState("Default");
                defaultState.writeDefaultValues = true;
                defaultState.motion = new AnimationClip();

                // ポーズのレイヤー追加
                foreach (var category in poseLibrary.categories)
                {
                    foreach (var pose in category.poses)
                    {
                        AnimationLayerBuilder.AddFxLayer(pose,layer,defaultState,
                            poseLibrary.enableHeightParam,poseLibrary.enableSpeedParam,poseLibrary.enableMirrorParam,
                            poseLibrary.Guid);
                    }
                }
            }

            // その他の変数レイヤー
            result.AddLayer(AnimationLayerBuilder.TrackingLayer(TrackingType.Base,$"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}"));
            result.AddLayer(AnimationLayerBuilder.TrackingLayer(TrackingType.Head,$"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}"));
            result.AddLayer(AnimationLayerBuilder.TrackingLayer(TrackingType.Arm,$"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}"));
            result.AddLayer(AnimationLayerBuilder.TrackingLayer(TrackingType.Foot,$"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}"));
            result.AddLayer(AnimationLayerBuilder.TrackingLayer(TrackingType.Finger,$"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}"));
            result.AddLayer(AnimationLayerBuilder.ResetLayer($"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",poseLibrary));

            return result;
        }
        
        static AnimatorController BaseAnimator(AvatarPoseData poseLibrary)
        {
            var result = new AnimatorController();
            result.AddLayer(new AnimatorControllerLayer
            {
                name = "null",
                stateMachine = new AnimatorStateMachine(),
            });
            
            var heightParam = new AnimatorControllerParameter
            {
                name = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.5f,
            };
            result.AddParameter(heightParam);
            
            var speedParam = new AnimatorControllerParameter
            {
                name = $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.5f,
            };
            result.AddParameter(speedParam);
            
            var mirrorParam = new AnimatorControllerParameter
            {
                name = $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            };
            result.AddParameter(mirrorParam);
            
            foreach (var param in poseLibrary.Parameters)
            {
                // パラメーター追加
                result.AddParameter(param, AnimatorControllerParameterType.Int);
            }

            // Tracking制御ノード
            result.AddParameter($"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter($"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter($"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter($"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter($"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter($"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            
            for (int i = 0; i < ConstVariables.BoolFlagCount; i++)
            {
                result.AddParameter($"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}", AnimatorControllerParameterType.Bool);
            }

            return result;
        }

    }
}