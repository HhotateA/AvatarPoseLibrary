using System.Collections.Generic;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using TrackingType = com.hhotatea.avatar_pose_library.logic.AnimationLayerBuilder.TrackingType;

namespace com.hhotatea.avatar_pose_library.logic {
    public static class AnimatorBuilder {
        public static AnimatorController BuildTrackingAnimator (AvatarPoseData poseLibrary, bool writeDefault) {
            var result = BaseAnimator (poseLibrary, writeDefault);
            var builder = new AnimationLayerBuilder (writeDefault);

            // フルトラ以外の場合は、アクションレイヤーを無効化する。
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Action, $"{ConstVariables.ActionParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));

            return result;
        }

        public static AnimatorController BuildLocomotionAnimator (AvatarPoseData poseLibrary, bool writeDefault) {
            var result = BaseAnimator (poseLibrary, writeDefault);
            var builder = new AnimationLayerBuilder (writeDefault);

            // レイヤー作成
            if (poseLibrary.enableLocomotionAnimator)
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer {
                    name = $"{ConstVariables.MotionAnimatorPrefix}_{poseLibrary.Guid}",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine (),
                    blendingMode = AnimatorLayerBlendingMode.Override,
                };
                result.AddLayer (layer);

                // 空のステート（default）
                var defaultState = layer.stateMachine.AddState ("Default");
                defaultState.writeDefaultValues = writeDefault;
                defaultState.motion = MotionBuilder.NoneAnimation;

                // リセットへの遷移
                var resetTransition = layer.stateMachine.AddAnyStateTransition (defaultState);
                resetTransition.canTransitionToSelf = false;
                resetTransition.hasExitTime = false;
                resetTransition.hasFixedDuration = true;
                resetTransition.duration = 0.1f;
                resetTransition.conditions = new AnimatorCondition[] {
                    new AnimatorCondition () {
                    mode = AnimatorConditionMode.If,
                    parameter = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",
                    }
                };

                // ポーズのレイヤー追加
                foreach (var category in poseLibrary.categories) {
                    foreach (var pose in category.poses) {
                        builder.AddLocomotionLayer (pose, layer, defaultState,
                            poseLibrary.enableHeightParam, poseLibrary.enableSpeedParam, poseLibrary.enableMirrorParam,
                            poseLibrary.Guid);
                    }
                }
            }

            return result;
        }

        public static AnimatorController BuildFxAnimator (AvatarPoseData poseLibrary, bool writeDefault) {
            var result = BaseAnimator (poseLibrary, writeDefault);
            var builder = new AnimationLayerBuilder (writeDefault);

            // レイヤー作成
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer
                {
                    name = $"{ConstVariables.ParamAnimatorPrefix}_{poseLibrary.Guid}",
                    defaultWeight = 0f,
                    stateMachine = new AnimatorStateMachine(),
                    blendingMode = AnimatorLayerBlendingMode.Override
                };
                result.AddLayer(layer);

                // 空のステート（default）
                var defaultState = layer.stateMachine.AddState("Default");
                defaultState.writeDefaultValues = writeDefault;
                defaultState.motion = MotionBuilder.NoneAnimation;

                // トラッキングリセット用のステート
                var resetState = layer.stateMachine.AddState("Reset");
                resetState.writeDefaultValues = writeDefault;
                resetState.motion = MotionBuilder.FrameAnimation;
                {
                    var trackingOffParam = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    for (int i = 0; i < ConstVariables.PoseFlagCount; i++)
                    {
                        trackingOffParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            name = $"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}",
                            value = 0
                        });
                    }
                    trackingOffParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",
                        value = 0f,
                    });
                    trackingOffParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}",
                        value = 0f,
                    });
                }
                // デフォルトへの遷移
                var defaultTransition = resetState.AddTransition(defaultState);
                defaultTransition.canTransitionToSelf = false;
                defaultTransition.hasExitTime = true;
                defaultTransition.hasFixedDuration = true;
                defaultTransition.duration = 0.0f;

                Dictionary<string, AnimatorState> preResets = new();
                foreach (var param in poseLibrary.Parameters)
                {
                    // 変数リセット用のステート
                    var preResetState = layer.stateMachine.AddState("PreReset" + param);
                    preResetState.writeDefaultValues = writeDefault;
                    preResetState.motion = MotionBuilder.FrameAnimation;
                    {
                        var resetParam = preResetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                        resetParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                        {
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            name = param,
                            value = 0,
                        });
                    }
                    // Preからリセットへの遷移
                    var bypassTransition = preResetState.AddTransition(resetState);
                    bypassTransition.canTransitionToSelf = false;
                    bypassTransition.hasExitTime = true;
                    bypassTransition.hasFixedDuration = true;
                    bypassTransition.duration = 0.0f;
                    // Dictionaryに登録
                    preResets.Add(param, preResetState);
                }

                // リセットへの遷移
                var resetTransition = layer.stateMachine.AddAnyStateTransition(defaultState);
                resetTransition.canTransitionToSelf = false;
                resetTransition.hasExitTime = false;
                resetTransition.hasFixedDuration = true;
                resetTransition.duration = 0.1f;
                resetTransition.conditions = new AnimatorCondition[] {
                    new AnimatorCondition () {
                    mode = AnimatorConditionMode.If,
                    parameter = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",
                    }
                };

                // ポーズのレイヤー追加
                foreach (var category in poseLibrary.categories)
                {
                    foreach (var pose in category.poses)
                    {
                        builder.AddParamLayer(
                            layer, pose, poseLibrary.Parameters, poseLibrary.Guid,
                            defaultState, resetState, preResets[pose.Parameter]);
                    }
                }
            }

            // レイヤー作成
            if (poseLibrary.enableFxAnimator)
            {
                AnimatorControllerLayer layer = new AnimatorControllerLayer {
                    name = $"{ConstVariables.FxAnimatorPrefix}_{poseLibrary.Guid}",
                    defaultWeight = 0f,
                    stateMachine = new AnimatorStateMachine (),
                    blendingMode = AnimatorLayerBlendingMode.Override
                };
                result.AddLayer (layer);

                // 空のステート（default）
                var defaultState = layer.stateMachine.AddState ("Default");
                defaultState.writeDefaultValues = writeDefault;
                defaultState.motion = MotionBuilder.NoneAnimation;

                // 初期化ステートの作成
                var resetState = layer.stateMachine.AddState ("Reset");
                resetState.writeDefaultValues = writeDefault;
                resetState.motion = MotionBuilder.FrameAnimation;
                var trackingOffParam = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver> ();
                foreach (var prefix in new [] {
                        ConstVariables.HeadParamPrefix,
                            ConstVariables.ArmParamPrefix,
                            ConstVariables.FootParamPrefix,
                            ConstVariables.FingerParamPrefix,
                            ConstVariables.BaseParamPrefix
                    }) {
                    trackingOffParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                            name = $"{prefix}_{poseLibrary.Guid}",
                            value = 0f,
                    });
                }
                trackingOffParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.ActionParamPrefix}_{poseLibrary.Guid}",
                        value = 0f,
                });
                var additiveOff = resetState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
                additiveOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additiveOff.goalWeight = 0f;

                // デフォルトへの遷移
                var leftTransition = resetState.AddTransition (defaultState);
                leftTransition.canTransitionToSelf = false;
                leftTransition.hasExitTime = true;
                leftTransition.hasFixedDuration = true;
                leftTransition.duration = 0.0f;

                // リセットへの遷移
                var resetTransition = layer.stateMachine.AddAnyStateTransition (defaultState);
                resetTransition.canTransitionToSelf = false;
                resetTransition.hasExitTime = false;
                resetTransition.hasFixedDuration = true;
                resetTransition.duration = 0.1f;
                resetTransition.conditions = new AnimatorCondition[] {
                    new AnimatorCondition () {
                    mode = AnimatorConditionMode.If,
                    parameter = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",
                    }
                };

                // ポーズのレイヤー追加
                foreach (var category in poseLibrary.categories) {
                    foreach (var pose in category.poses) {
                        builder.AddFxLayer (pose, layer,
                            defaultState, resetState,
                            poseLibrary.Guid);
                    }
                }
            }

            // その他の変数レイヤー
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Base, $"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Head, $"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Arm, $"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Foot, $"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ConstantTrackingLayer (TrackingType.Finger, $"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ActiveTrackingLayer (TrackingType.Face, $"{ConstVariables.FaceParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ActiveTrackingLayer(TrackingType.Space, $"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}", poseLibrary.Guid));
            result.AddLayer (builder.ResetLayer($"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}", poseLibrary));
            
            if(poseLibrary.EnableAudioMode)
            {
                result.AddLayer (builder.AudioVolumeLayer ($"{ConstVariables.VolumeParamPrefix}_{poseLibrary.Guid}", $"{ConstVariables.AudioParamPrefix}_{poseLibrary.Guid}", DynamicVariables.Settings.audioVolume));
            }

            return result;
        }

        static AnimatorController BaseAnimator (AvatarPoseData poseLibrary, bool writeDefault) {
            var result = new AnimatorController ();
            var builder = new AnimationLayerBuilder (writeDefault);
            /*result.AddLayer(new AnimatorControllerLayer
            {
                name = "null",
                stateMachine = new AnimatorStateMachine(),
            });*/

            var heightParam = new AnimatorControllerParameter {
                name = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.5f,
            };
            result.AddParameter (heightParam);

            var speedParam = new AnimatorControllerParameter {
                name = $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.5f,
            };
            result.AddParameter (speedParam);

            var volumeParam = new AnimatorControllerParameter {
                name = $"{ConstVariables.VolumeParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f,
            };
            result.AddParameter (volumeParam);

            var mirrorParam = new AnimatorControllerParameter {
                name = $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.Guid}",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false,
            };
            result.AddParameter (mirrorParam);

            foreach (var param in poseLibrary.Parameters) {
                // パラメーター追加
                result.AddParameter (param, AnimatorControllerParameterType.Int);
            }

            // Tracking制御ノード
            result.AddParameter ($"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.FaceParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.ActionParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);
            result.AddParameter ($"{ConstVariables.DummyParamPrefix}_{poseLibrary.Guid}", AnimatorControllerParameterType.Bool);

            for (int i = 0; i < ConstVariables.PoseFlagCount; i++) {
                result.AddParameter ($"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}", AnimatorControllerParameterType.Int);
            }

            return result;
        }
    }
}