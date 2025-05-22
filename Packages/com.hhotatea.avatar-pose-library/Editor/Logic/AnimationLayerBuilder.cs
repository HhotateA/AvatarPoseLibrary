using System;
using System.Collections.Generic;
using System.Linq;
using com.hhotatea.avatar_pose_library.editor;
using UnityEngine;
using UnityEditor.Animations;
using com.hhotatea.avatar_pose_library.model;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class AnimationLayerBuilder
    {

        public static AnimatorControllerLayer ResetLayer(string param,AvatarPoseData poseLibrary)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            var noneClip = MotionBuilder.NoneAnimation();
            // ステートの初期化
            var defaultState = layer.stateMachine.AddState("Default");
            defaultState.writeDefaultValues = false;
            defaultState.motion = noneClip;
            
            var resetState = layer.stateMachine.AddState("Reset");
            resetState.writeDefaultValues = false;
            resetState.motion = noneClip;
            
            var paramReset = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            foreach (var parameter in poseLibrary.Parameters)
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = parameter,
                    value = 0,
                });
            }
            foreach (var parameter in new String[]{
                         ConstVariables.HeadParamPrefix,
                         ConstVariables.ArmParamPrefix,
                         ConstVariables.FootParamPrefix,
                         ConstVariables.FingerParamPrefix,
                         ConstVariables.BaseParamPrefix,
                         ConstVariables.MirrorParamPrefix})
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0,
                });
            }
            foreach (var parameter in new String[]{
                         ConstVariables.SpeedParamPrefix,
                         ConstVariables.HeightParamPrefix,})
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0.5f,
                });
            }
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = param,
                value = 0,
            });
            
            // 遷移の設定
            var resetTransition = defaultState.AddTransition(resetState);
            resetTransition.canTransitionToSelf = false;
            resetTransition.hasExitTime = true;
            resetTransition.exitTime = 0f;
            resetTransition.hasFixedDuration = true;
            resetTransition.duration = 0.0f;
            resetTransition.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = param,
                }
            };
            
            var defaultTransition = resetState.AddTransition(defaultState);
            defaultTransition.canTransitionToSelf = false;
            defaultTransition.hasExitTime = true;
            defaultTransition.exitTime = 0f;
            defaultTransition.hasFixedDuration = true;
            defaultTransition.duration = 0.0f;

            return layer;
        }

        public enum TrackingType
        {
            Base,
            Head,
            Arm,
            Foot,
            Finger
        }

        public static AnimatorControllerLayer TrackingLayer(
            TrackingType type,string param)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            var noneClip = MotionBuilder.NoneAnimation(0.3f);
            
            // ステートの初期化
            var offIdleState = layer.stateMachine.AddState("OffIdle");
            offIdleState.writeDefaultValues = false;
            offIdleState.motion = noneClip;
            
            var offConState = layer.stateMachine.AddState("OffConState");
            offConState.writeDefaultValues = false;
            offConState.motion = noneClip;

            var onIdleState = layer.stateMachine.AddState("OnIdle");
            onIdleState.writeDefaultValues = false;
            onIdleState.motion = noneClip;
            
            var onConState = layer.stateMachine.AddState("OnConState");
            onConState.writeDefaultValues = false;
            onConState.motion = noneClip;
            
            // コンポーネント
            switch (type)
            {
                case TrackingType.Base:
                    var locoOn = offConState.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
                    locoOn.disableLocomotion = false;
                    var locoOff = onConState.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
                    locoOff.disableLocomotion = true;
                    break;

                case TrackingType.Head:
                    ApplyTrackingLayer(offConState, onConState,
                        off => off.trackingHead = VRC_AnimatorTrackingControl.TrackingType.Tracking,
                        on => on.trackingHead = VRC_AnimatorTrackingControl.TrackingType.Animation);
                    break;

                case TrackingType.Arm:
                    ApplyTrackingLayer(offConState, onConState,
                        off => {
                            off.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on => {
                            on.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                        });
                    break;

                case TrackingType.Foot:
                    ApplyTrackingLayer(offConState, onConState,
                        off => {
                            off.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on => {
                            on.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Animation;
                        });
                    break;

                case TrackingType.Finger:
                    var gestureOff = offConState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    gestureOff.layer = VRC_PlayableLayerControl.BlendableLayer.Gesture;
                    gestureOff.goalWeight = 1f;
                    var gestureOn = onConState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    gestureOn.layer = VRC_PlayableLayerControl.BlendableLayer.Gesture;
                    gestureOn.goalWeight = 0f;
                    ApplyTrackingLayer(offConState, onConState,
                        off => {
                            off.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on => {
                            on.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                        });
                    break;
            }
            
            // 遷移の設定
            var fromOffToOn = offIdleState.AddTransition(onConState);
            fromOffToOn.canTransitionToSelf = false;
            fromOffToOn.hasExitTime = true;
            fromOffToOn.exitTime = 0f;
            fromOffToOn.hasFixedDuration = true;
            fromOffToOn.duration = 0.0f;
            fromOffToOn.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = param,
                }
            };
            
            var fromOnToOn = onConState.AddTransition(onIdleState);
            fromOnToOn.canTransitionToSelf = false;
            fromOnToOn.hasExitTime = true;
            fromOnToOn.exitTime = 0f;
            fromOnToOn.hasFixedDuration = true;
            fromOnToOn.duration = 0.0f;
            
            var fromOnToOff = onIdleState.AddTransition(offConState);
            fromOnToOff.canTransitionToSelf = false;
            fromOnToOff.hasExitTime = true;
            fromOnToOff.exitTime = 0f;
            fromOnToOff.hasFixedDuration = true;
            fromOnToOff.duration = 0.0f;
            fromOnToOff.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = param,
                }
            };
            
            var fromOffToOff = offConState.AddTransition(offIdleState);
            fromOffToOff.canTransitionToSelf = false;
            fromOffToOff.hasExitTime = true;
            fromOffToOff.exitTime = 0f;
            fromOffToOff.hasFixedDuration = true;
            fromOffToOff.duration = 0.0f;
            
            // Off設定を維持する
            var loopTransition = onIdleState.AddTransition(onConState);
            loopTransition.canTransitionToSelf = false;
            loopTransition.hasExitTime = true;
            loopTransition.exitTime = 0f;
            loopTransition.hasFixedDuration = true;
            loopTransition.duration = 0.0f;

            return layer;
        }
        
        private static void ApplyTrackingLayer(
            AnimatorState offState, AnimatorState onState,
            Action<VRCAnimatorTrackingControl> configureOff,
            Action<VRCAnimatorTrackingControl> configureOn)
        {
            var offCon = offState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            var onCon = onState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            configureOff(offCon);
            configureOn(onCon);
        }

        public static void AddParamLayer(
            AnimatorControllerLayer layer, PoseEntry pose, 
            List<string> parameters,
            AnimatorState defaultState,string guid)
        {
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => (PreReset =>) Reset => Default
             * のループ。
             * 
             * Reserveでパラメーターの同期、Resetでパラメーターの初期化を行う
             * Loopアニメーションの場合は、再生終了後にPreResetを経由する
             */
            
            // トラッキング設定用のオブジェクト
            var trackingMap = new (bool enabled, string prefix)[]
            {
                (pose.tracking.head, ConstVariables.HeadParamPrefix),
                (pose.tracking.arm, ConstVariables.ArmParamPrefix),
                (pose.tracking.foot, ConstVariables.FootParamPrefix),
                (pose.tracking.finger, ConstVariables.FingerParamPrefix),
                (pose.tracking.locomotion, ConstVariables.BaseParamPrefix)
            };
            var noneClip = MotionBuilder.NoneAnimation();
            
            // 準備ステートの作成
            var reserveState = layer.stateMachine.AddState("Reserve_"+pose.Value.ToString());
            reserveState.motion = noneClip;
            reserveState.writeDefaultValues = false;
            {
                var trackingOnParam = reserveState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                foreach (var (enabled, prefix) in trackingMap)
                {
                    if (!enabled) continue;

                    trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{prefix}_{guid}",
                        value = 1,
                    });
                }
                trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.SpeedParamPrefix}_{guid}",
                    value = pose.tracking.motionSpeed == 0f ?  0f : 0.5f,
                });
                trackingOnParam.parameters.AddRange(
                    pose.GetAnimatorFlag().Select((flag, index) => new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.FlagParamPrefix}_{guid}_{index}",
                        value = flag ? 1 : 0
                    })
                );
                var additive = reserveState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                additive.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additive.goalWeight = 1f;
            }
            
            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_"+pose.Value.ToString());
            poseState.writeDefaultValues = false;
            poseState.motion = MotionBuilder.PartAnimation(pose.animationClip)[2];

            // トラッキングリセット用のステート
            var resetState = layer.stateMachine.AddState("Reset"+pose.Value.ToString());
            resetState.motion = noneClip;
            resetState.writeDefaultValues = false;
            {
                var trackingOffParam = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                foreach (var (enabled, prefix) in trackingMap)
                {
                    if (!enabled) continue;

                    trackingOffParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{prefix}_{guid}",
                        value = 0,
                    });
                }
                trackingOffParam.parameters.AddRange(
                    pose.GetAnimatorFlag().Select((flag, index) => new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.FlagParamPrefix}_{guid}_{index}",
                        value = 0
                    })
                );
            }
            
            // 変数リセット用のステート
            var preResetState = layer.stateMachine.AddState("PreReset"+pose.Value.ToString());
            preResetState.motion = noneClip;
            preResetState.writeDefaultValues = false;
            var resetParam = preResetState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            {
                resetParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = pose.Parameter,
                    value = 0,
                });
            }
            
            // 遷移を作成
            var reTransition = defaultState.AddTransition(reserveState);
            reTransition.canTransitionToSelf = false;
            reTransition.hasExitTime = false;
            reTransition.hasFixedDuration = true;
            reTransition.duration = 0.0f;
            reTransition.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition()
                {
                    mode = AnimatorConditionMode.Equals,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                }
            };
            
            // メインへの移行
            var mainTransition = reserveState.AddTransition(poseState);
            mainTransition.canTransitionToSelf = false;
            mainTransition.hasExitTime = true;
            mainTransition.exitTime = 0f;
            mainTransition.hasFixedDuration = true;
            mainTransition.duration = 0.0f;
            
            // Preからリセットへの遷移
            var bypassTransition = preResetState.AddTransition(resetState);
            bypassTransition.canTransitionToSelf = false;
            bypassTransition.hasExitTime = false;
            bypassTransition.hasFixedDuration = true;
            bypassTransition.duration = 0.0f;
            bypassTransition.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition()
                {
                    mode = AnimatorConditionMode.NotEqual,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                }
            };
            
            // リセットへの遷移
            var resetTransition = poseState.AddTransition(resetState);
            resetTransition.canTransitionToSelf = false;
            resetTransition.hasExitTime = false;
            resetTransition.hasFixedDuration = true;
            resetTransition.duration = 0.0f;
            resetTransition.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition()
                {
                    mode = AnimatorConditionMode.NotEqual,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                }
            };
    
            // パラメーターリストを作成
            parameters.Remove(pose.Parameter);
            foreach (var p in parameters)
            {
                // プレリセットへの遷移
                var preResetTransition = poseState.AddTransition(preResetState);
                preResetTransition.canTransitionToSelf = false;
                preResetTransition.hasExitTime = false;
                preResetTransition.hasFixedDuration = true;
                preResetTransition.duration = 0.0f;
                preResetTransition.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = p,
                        threshold = 0
                    }
                };
            }
    
            // デフォルトへの遷移
            var defaultTransition = resetState.AddTransition(defaultState);
            defaultTransition.canTransitionToSelf = false;
            defaultTransition.hasExitTime = false;
            defaultTransition.hasFixedDuration = true;
            defaultTransition.duration = 0.0f;
            defaultTransition.conditions = new AnimatorCondition[]
            {
                new AnimatorCondition()
                {
                    mode = AnimatorConditionMode.NotEqual,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                }
            };

            if (!pose.tracking.loop)
            {
                var endTransition = poseState.AddTransition(preResetState);
                endTransition.canTransitionToSelf = false;
                endTransition.hasExitTime = true;
                endTransition.exitTime = 0f;
                endTransition.hasFixedDuration = true;
                endTransition.duration = 0.0f;
            }
        }
        
        public static void AddFxLayer(
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState,
            bool height, bool speed, bool mirror,
            string guid)
        {
            var noneClip = MotionBuilder.NoneAnimation();
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => Reset => Default
             * のループ。
             * 
             * ReserveでActionの有効化、ResetでActionの無効化を行う
             */
            
            // 準備ステートの作成
            var reserveState = layer.stateMachine.AddState("Reserve_"+pose.Value.ToString());
            reserveState.motion = noneClip;
            reserveState.writeDefaultValues = false;
            var additiveOn = reserveState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
            additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            additiveOn.goalWeight = 1f;

            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_"+pose.Value.ToString());
            poseState.writeDefaultValues = false;
            if (mirror)
            {
                poseState.mirrorParameterActive = true;
                poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            }
            
            // 初期化ステートの作成
            var resetState = layer.stateMachine.AddState("Reset_"+pose.Value.ToString());
            resetState.motion = noneClip;
            resetState.writeDefaultValues = false;
            var additiveOff = resetState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
            additiveOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            additiveOff.goalWeight = 0f;

            // blendTree
            var anim = MotionBuilder.SetAnimationLoop(pose.animationClip,pose.tracking.loop);
            // Transform以外のAnimationを抽出
            poseState.motion = MotionBuilder.PartAnimation(anim)[1];
            if (MotionBuilder.IsMoveAnimation(anim))
            {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }
            
            var flags = pose.GetAnimatorFlag();
            
            // 遷移を作成
            var reTransition = defaultState.AddTransition(reserveState);
            reTransition.canTransitionToSelf = false;
            reTransition.hasExitTime = false;
            reTransition.hasFixedDuration = true;
            reTransition.duration = 0.0f;
            reTransition.conditions = flags.Select((flag, i) => new AnimatorCondition
                {
                    mode = flag ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}"
                })
                .ToArray();
            
            var mainTransition = reserveState.AddTransition(poseState);
            mainTransition.canTransitionToSelf = false;
            mainTransition.hasExitTime = true;
            mainTransition.hasFixedDuration = true;
            mainTransition.duration = 0.0f;
            
            for (int i = 0; i < flags.Length; i++)
            {
                // Preからリセットへの遷移
                var bypassTransition = poseState.AddTransition(resetState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = false;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.0f;
                bypassTransition.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition
                    {
                        mode = flags[i] ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                    }
                };
            }
            
            var resetTransition = resetState.AddTransition(defaultState);
            resetTransition.canTransitionToSelf = false;
            resetTransition.hasExitTime = true;
            resetTransition.hasFixedDuration = true;
            resetTransition.duration = 0.0f;
        }

        public static void AddLocomotionLayer(
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState,
            bool height, bool speed, bool mirror,
            string guid)
        {
            var noneClip = MotionBuilder.NoneAnimation();
            
            // 準備ステートの作成
            var reserveState = layer.stateMachine.AddState("Reserve_"+pose.Value.ToString());
            reserveState.motion = noneClip;
            reserveState.writeDefaultValues = false;
            
            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_"+pose.Value.ToString());
            poseState.writeDefaultValues = false;
            if (mirror)
            {
                poseState.mirrorParameterActive = true;
                poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            }

            // blendTree
            var anim = MotionBuilder.SetAnimationLoop(pose.animationClip,pose.tracking.loop);
            // Transform関係のAnimation抽出
            anim = MotionBuilder.PartAnimation(anim)[0];
            if (MotionBuilder.IsMoveAnimation(anim))
            {
                var blendTree = new BlendTree();
                
                // アニメーションの生成
                AnimationClip motionClip0 = height ? MotionBuilder.BuildMotionLevel(anim, +DynamicVariables.Settings.minMaxHeight) : anim;
                AnimationClip motionClip1 = height ? MotionBuilder.BuildMotionLevel(anim, -DynamicVariables.Settings.minMaxHeight) : anim;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.AddChild(motionClip0, 0);
                blendTree.AddChild(motionClip1, 1);
                poseState.motion = blendTree;
                
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }
            else
            {
                var blendTree = new BlendTree();
                var motionClip0 = speed ? MotionBuilder.IdleAnimation(anim,0f) : anim;
                var motionClip1 = speed ? MotionBuilder.IdleAnimation(anim,DynamicVariables.Settings.motionNoiseScale) : anim;
                var motionClip00 = height ? MotionBuilder.BuildMotionLevel(motionClip0,+DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip01 = height ? MotionBuilder.BuildMotionLevel(motionClip0,-DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip10 = height ? MotionBuilder.BuildMotionLevel(motionClip1,+DynamicVariables.Settings.minMaxHeight) : motionClip1;
                var motionClip11 = height ? MotionBuilder.BuildMotionLevel(motionClip1,-DynamicVariables.Settings.minMaxHeight) : motionClip1;
                blendTree.blendType = BlendTreeType.FreeformCartesian2D;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.blendParameterY = $"{ConstVariables.SpeedParamPrefix}_{guid}";
                blendTree.AddChild(motionClip00 , new Vector2(0f,0f));
                blendTree.AddChild(motionClip01, new Vector2(1f,0f));
                blendTree.AddChild(motionClip10, new Vector2(0f,1f));
                blendTree.AddChild(motionClip11, new Vector2(1f,1f));
                poseState.motion = blendTree;
            }
            
            var flags = pose.GetAnimatorFlag();
            
            // 遷移を作成
            var reTransition = defaultState.AddTransition(poseState);
            reTransition.canTransitionToSelf = false;
            reTransition.hasExitTime = false;
            reTransition.hasFixedDuration = true;
            reTransition.duration = 0.0f;
            reTransition.conditions = flags.Select((flag, i) => new AnimatorCondition
                {
                    mode = flag ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}"
                })
                .ToArray();
            
            for (int i = 0; i < flags.Length; i++)
            {
                // Preからリセットへの遷移
                var bypassTransition = poseState.AddTransition(defaultState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = false;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.0f;
                bypassTransition.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition
                    {
                        mode = flags[i] ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                    }
                };
            }
        }

    }
}