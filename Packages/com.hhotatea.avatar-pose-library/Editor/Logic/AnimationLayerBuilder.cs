using System;
using System.Collections.Generic;
using System.Linq;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.logic {
    public class AnimationLayerBuilder {
        bool writeDefault_;

        public AnimationLayerBuilder (bool writeDefault) {
            writeDefault_ = writeDefault;
        }

        public AnimatorControllerLayer ResetLayer (string param, AvatarPoseData poseLibrary) {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine (),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };

            // ステートの初期化
            var defaultState = layer.stateMachine.AddState ("Default");
            defaultState.writeDefaultValues = writeDefault_;
            defaultState.motion = MotionBuilder.FrameAnimation;

            var resetState = layer.stateMachine.AddState ("Reset");
            resetState.writeDefaultValues = writeDefault_;
            resetState.motion = MotionBuilder.FrameAnimation;

            var paramReset = resetState.AddStateMachineBehaviour<VRCAvatarParameterDriver> ();
            foreach (var parameter in poseLibrary.Parameters) {
                paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = parameter,
                    value = 0,
                });
            }
            foreach (var parameter in new String[] {
                    ConstVariables.OnPlayParamPrefix,
                    ConstVariables.HeadParamPrefix,
                    ConstVariables.ArmParamPrefix,
                    ConstVariables.FootParamPrefix,
                    ConstVariables.FingerParamPrefix,
                    ConstVariables.BaseParamPrefix,
                    ConstVariables.MirrorParamPrefix,
                    ConstVariables.ActionParamPrefix,
                    ConstVariables.FaceParamPrefix,
                }) {
                paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0,
                });
            }
            paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = $"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}",
                value = DynamicVariables.Settings.poseSpaceMenu ? 1 : 0,
            });
            foreach (var parameter in new String[] {
                    ConstVariables.SpeedParamPrefix,
                        ConstVariables.HeightParamPrefix,
                }) {
                paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0.5f,
                });
            }
            paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = param,
                value = 0,
            });

            for (int i = 0; i < ConstVariables.PoseFlagCount; i++) {
                paramReset.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}",
                    value = 0,
                });
            }

            var additive = resetState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
            additive.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            additive.goalWeight = 0f;

            var gesture = resetState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
            gesture.layer = VRC_PlayableLayerControl.BlendableLayer.Gesture;
            gesture.goalWeight = 1f;

            // 遷移の設定
            var resetTransition = defaultState.AddTransition (resetState);
            resetTransition.canTransitionToSelf = false;
            resetTransition.hasExitTime = true;
            resetTransition.exitTime = 0f;
            resetTransition.hasFixedDuration = true;
            resetTransition.duration = 0.0f;
            resetTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            var defaultTransition = resetState.AddTransition (defaultState);
            defaultTransition.canTransitionToSelf = false;
            defaultTransition.hasExitTime = true;
            defaultTransition.exitTime = 0f;
            defaultTransition.hasFixedDuration = true;
            defaultTransition.duration = 0.0f;

            return layer;
        }

        public enum TrackingType {
            Base,
            Head,
            Arm,
            Foot,
            Finger,
            Face,
            Action,
            Space
        }

        public AnimatorControllerLayer ActiveTrackingLayer(TrackingType type, string param, string playParam)
        {
            return TrackingLayer(type,param,
                (onTo,offTo,onState,offState) => {
                    var off_1 = offTo;
                    var off_2 = DuplicateTransition(offTo,onState);
                    onTo.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                            mode = AnimatorConditionMode.If,
                            parameter = param
                        },
                        new AnimatorCondition {
                            mode = AnimatorConditionMode.If,
                            parameter = playParam
                        }
                    };
                    off_1.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                            mode = AnimatorConditionMode.IfNot,
                            parameter = param
                        }
                    };
                    off_2.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                            mode = AnimatorConditionMode.IfNot,
                            parameter = playParam
                        }
                    };
                });
        }

        public AnimatorControllerLayer ConstantTrackingLayer(TrackingType type, string param)
        {
            return TrackingLayer(type,param,null);
        }

        private AnimatorControllerLayer TrackingLayer(
            TrackingType type, string param,
            Action<AnimatorStateTransition, AnimatorStateTransition, AnimatorState, AnimatorState> onSetTrasitions)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            var noneClip = MotionBuilder.CreateFrameAnimation(0.3f);

            // ステートの初期化
            var offIdleState = layer.stateMachine.AddState("OffIdle");
            offIdleState.writeDefaultValues = writeDefault_;
            offIdleState.motion = noneClip;

            var offConState = layer.stateMachine.AddState("OffConState");
            offConState.writeDefaultValues = writeDefault_;
            offConState.motion = noneClip;

            var onIdleState = layer.stateMachine.AddState("OnIdle");
            onIdleState.writeDefaultValues = writeDefault_;
            onIdleState.motion = noneClip;

            var onConState = layer.stateMachine.AddState("OnConState");
            onConState.writeDefaultValues = writeDefault_;
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
                        off =>
                        {
                            off.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on =>
                        {
                            on.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
                        });
                    break;

                case TrackingType.Foot:
                    ApplyTrackingLayer(offConState, onConState,
                        off =>
                        {
                            off.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on =>
                        {
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
                        off =>
                        {
                            off.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                            off.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                        },
                        on =>
                        {
                            on.trackingLeftFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                            on.trackingRightFingers = VRC_AnimatorTrackingControl.TrackingType.Animation;
                        });
                    break;

                case TrackingType.Face:
                    var fxOff = offConState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    fxOff.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    fxOff.layer = 1;
                    fxOff.goalWeight = 0f;
                    var fxOn = onConState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    fxOn.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    fxOn.layer = 1;
                    fxOn.goalWeight = 1f;
                    break;

                case TrackingType.Action:
                    var additiveOff = offConState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    additiveOff.layer = VRC_PlayableLayerControl.BlendableLayer.Additive;
                    additiveOff.goalWeight = 1f;
                    var additiveOn = onConState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Additive;
                    additiveOn.goalWeight = 0f;

                    var actionOff = onConState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    actionOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    actionOff.goalWeight = 0f;

                    break;

                case TrackingType.Space:
                    var spaceEnter = onConState.AddStateMachineBehaviour<VRCAnimatorTemporaryPoseSpace>();
                    spaceEnter.enterPoseSpace = true;
                    var spaceExit = offConState.AddStateMachineBehaviour<VRCAnimatorTemporaryPoseSpace>();
                    spaceExit.enterPoseSpace = false;

                    break;
            }

            // 遷移の設定
            var fromOffToOn = offIdleState.AddTransition(onConState);
            fromOffToOn.canTransitionToSelf = false;
            fromOffToOn.hasExitTime = true;
            fromOffToOn.exitTime = 0f;
            fromOffToOn.hasFixedDuration = true;
            fromOffToOn.duration = 0.0f;
            fromOffToOn.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
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
            fromOnToOff.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
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

            // PoseSpaceの設定用（Loopより優先するためにここで挿し込む）
            onSetTrasitions?.Invoke(fromOffToOn, fromOnToOff, onIdleState, offIdleState);

            // Off設定を維持する
            var loopTransition = onIdleState.AddTransition(onConState);
            loopTransition.canTransitionToSelf = false;
            loopTransition.hasExitTime = true;
            loopTransition.exitTime = 0f;
            loopTransition.hasFixedDuration = true;
            loopTransition.duration = 0.0f;
            return layer;
        }

        private void ApplyTrackingLayer (
            AnimatorState offState, AnimatorState onState,
            Action<VRCAnimatorTrackingControl> configureOff,
            Action<VRCAnimatorTrackingControl> configureOn) {
            var offCon = offState.AddStateMachineBehaviour<VRCAnimatorTrackingControl> ();
            var onCon = onState.AddStateMachineBehaviour<VRCAnimatorTrackingControl> ();
            configureOff (offCon);
            configureOn (onCon);
        }

        public void AddParamLayer (
            AnimatorControllerLayer layer, PoseEntry pose,
            List<string> parameters, string guid,
            AnimatorState defaultState, AnimatorState resetState, AnimatorState preResetState) {
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => (PreReset =>) Reset => Default
             * のループ。
             * 
             * Reserveでパラメーターの同期、Resetでパラメーターの初期化を行う
             * Loopアニメーションの場合は、再生終了後にPreResetを経由する
             */
            bool isMove = MotionBuilder.IsMoveAnimation (pose.animationClip);
            var anim = MotionBuilder.SafeAnimation (pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // 準備ステートの作成
            var reserveState = layer.stateMachine.AddState ("Reserve_" + pose.Value.ToString ());
            reserveState.motion = MotionBuilder.FrameAnimation;
            reserveState.writeDefaultValues = writeDefault_; 
            {
                var trackingOnParam = reserveState.AddStateMachineBehaviour<VRCAvatarParameterDriver> (); 
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.SpeedParamPrefix}_{guid}",
                        value = pose.tracking.motionSpeed == 0f ? 0f : 0.5f,
                });
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.ActionParamPrefix}_{guid}",
                        value = 1f,
                });
                trackingOnParam.parameters.AddRange (
                    pose.GetAnimatorFlag ().Select ((flag, index) => new VRC_AvatarParameterDriver.Parameter {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.FlagParamPrefix}_{guid}_{index}",
                        value = flag
                    })
                );
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.OnPlayParamPrefix}_{guid}",
                    value = 1f,
                });
                var additive = reserveState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
                additive.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additive.goalWeight = 1f;
            }

            // メインステートの作成
            var poseState = layer.stateMachine.AddState ("Pose_" + pose.Value.ToString ());
            poseState.writeDefaultValues = writeDefault_;
            poseState.motion = MotionBuilder.PartAnimation (anim, MotionBuilder.AnimationPart.None);
            if (isMove) {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }

            // 遷移を作成
            var joinTransition = defaultState.AddTransition (reserveState);
            joinTransition.canTransitionToSelf = false;
            joinTransition.hasExitTime = false;
            joinTransition.hasFixedDuration = true;
            joinTransition.duration = 0.0f;
            joinTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition () {
                mode = AnimatorConditionMode.Equals,
                parameter = pose.Parameter,
                threshold = pose.Value
                }
            };

            // 侵入経路
            if (pose.beforeAnimationClip) {
                // メインステートの作成
                var beforeState = layer.stateMachine.AddState ("Before_" + pose.Value.ToString ());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = MotionBuilder.PartAnimation (pose.beforeAnimationClip, MotionBuilder.AnimationPart.None);

                // バイパスの作成
                var bypassTransition = reserveState.AddTransition (beforeState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = true;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.1f;

                // メインへの移行
                var mainTransition = beforeState.AddTransition (poseState);
                mainTransition.canTransitionToSelf = false;
                mainTransition.hasExitTime = true;
                mainTransition.exitTime = 0f;
                mainTransition.hasFixedDuration = true;
                mainTransition.duration = 0.0f;
            } else {
                // メインへの移行
                var mainTransition = reserveState.AddTransition (poseState);
                mainTransition.canTransitionToSelf = false;
                mainTransition.hasExitTime = true;
                mainTransition.exitTime = 0f;
                mainTransition.hasFixedDuration = true;
                mainTransition.duration = 0.0f;
            }

            // 脱出経路
            if (true) {
                // リセットへの遷移
                var leftTransition = poseState.AddTransition (resetState);
                leftTransition.canTransitionToSelf = false;
                leftTransition.hasExitTime = false;
                leftTransition.hasFixedDuration = true;
                leftTransition.duration = 0.0f;
                leftTransition.conditions = new AnimatorCondition[] {
                    new AnimatorCondition () {
                    mode = AnimatorConditionMode.NotEqual,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                    }
                };

                // パラメーターリストを作成
                parameters.Remove (pose.Parameter);
                foreach (var p in parameters) {
                    // プレリセットへの遷移
                    var preResetTransition = poseState.AddTransition (preResetState);
                    preResetTransition.canTransitionToSelf = false;
                    preResetTransition.hasExitTime = false;
                    preResetTransition.hasFixedDuration = true;
                    preResetTransition.duration = 0.0f;
                    preResetTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = p,
                        threshold = 0
                        }
                    };
                }

                if (!pose.tracking.loop) {
                    var endTransition = poseState.AddTransition (preResetState);
                    endTransition.canTransitionToSelf = false;
                    endTransition.hasExitTime = true;
                    endTransition.exitTime = 0f;
                    endTransition.hasFixedDuration = true;
                    endTransition.duration = 0.0f;
                }
            }
        }

        public void AddFxLayer (
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState, AnimatorState resetState,
            string guid) {
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => Reset => Default
             * のループ。
             * 
             * ReserveでActionの有効化、ResetでActionの無効化を行う
             */
            bool isMove = MotionBuilder.IsMoveAnimation (pose.animationClip);
            var flags = pose.GetAnimatorFlag ();
            var anim = MotionBuilder.SafeAnimation (pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // トラッキング設定用のオブジェクト
            var trackingMap = new (bool enabled, string prefix) [] {
                (pose.tracking.head, ConstVariables.HeadParamPrefix),
                (pose.tracking.arm, ConstVariables.ArmParamPrefix),
                (pose.tracking.foot, ConstVariables.FootParamPrefix),
                (pose.tracking.finger, ConstVariables.FingerParamPrefix),
                (pose.tracking.locomotion, ConstVariables.BaseParamPrefix),
                (pose.tracking.fx, ConstVariables.FaceParamPrefix)
            };

            // メインステートの作成
            var poseState = layer.stateMachine.AddState ("Pose_" + pose.Value.ToString ());
            poseState.writeDefaultValues = writeDefault_;
            poseState.mirrorParameterActive = true;
            poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            poseState.motion = MakeFxAnim (anim, pose.tracking.loop);
            var trackingOnParam = poseState.AddStateMachineBehaviour<VRCAvatarParameterDriver> ();
            foreach (var (enabled, prefix) in trackingMap) {
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{prefix}_{guid}",
                        value = enabled ? 1f : 0f,
                });
            }
            if (isMove) {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }

            // 侵入経路
            if (pose.beforeAnimationClip) {
                // メインステートの作成
                var beforeState = layer.stateMachine.AddState ("Before_" + pose.Value.ToString ());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = MakeFxAnim (pose.beforeAnimationClip, pose.tracking.loop);

                // 遷移を作成
                var joinTransition = defaultState.AddTransition (beforeState);
                joinTransition.canTransitionToSelf = false;
                joinTransition.hasExitTime = false;
                joinTransition.hasFixedDuration = true;
                joinTransition.duration = 0.0f;
                joinTransition.conditions = flags.Select ((flag, i) => new AnimatorCondition {
                        mode = AnimatorConditionMode.Equals,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flag,
                    })
                    .ToArray ();

                // バイパスの作成
                var bypassTransition = beforeState.AddTransition (poseState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = true;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.1f;

                // レイヤーの設定
                var additiveOn = beforeState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
                additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additiveOn.goalWeight = 1f;
            } else {
                // 遷移を作成
                var joinTransition = defaultState.AddTransition (poseState);
                joinTransition.canTransitionToSelf = false;
                joinTransition.hasExitTime = false;
                joinTransition.hasFixedDuration = true;
                joinTransition.duration = 0.0f;
                joinTransition.conditions = flags.Select ((flag, i) => new AnimatorCondition {
                        mode = AnimatorConditionMode.Equals,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flag,
                    })
                    .ToArray ();

                // レイヤーの設定
                var additiveOn = poseState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
                additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additiveOn.goalWeight = 1f;
            }

            // 脱出経路
            if (pose.afterAnimationClip) {
                // メインステートの作成
                var afterState = layer.stateMachine.AddState ("After_" + pose.Value.ToString ());
                afterState.writeDefaultValues = writeDefault_;
                afterState.mirrorParameterActive = true;
                afterState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                afterState.motion = MakeFxAnim (pose.afterAnimationClip, pose.tracking.loop);

                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    // Preからリセットへの遷移
                    var leftTransition = poseState.AddTransition (afterState);
                    leftTransition.canTransitionToSelf = false;
                    leftTransition.hasExitTime = false;
                    leftTransition.hasFixedDuration = true;
                    leftTransition.duration = 0.0f;
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }

                // バイパスを作成
                var bypassTransition = afterState.AddTransition (resetState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = true;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    // Preからリセットへの遷移
                    var leftTransition = poseState.AddTransition (resetState);
                    leftTransition.canTransitionToSelf = false;
                    leftTransition.hasExitTime = false;
                    leftTransition.hasFixedDuration = true;
                    leftTransition.duration = 0.0f;
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }
            }
        }

        public void AddLocomotionLayer (
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState,
            bool height, bool speed, bool mirror,
            string guid) {
            bool isMove = MotionBuilder.IsMoveAnimation (pose.animationClip);
            var flags = pose.GetAnimatorFlag ();
            var anim = MotionBuilder.SafeAnimation (pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // メインステートの作成
            var poseState = layer.stateMachine.AddState ("Pose_" + pose.Value.ToString ());
            poseState.writeDefaultValues = writeDefault_;
            poseState.mirrorParameterActive = true;
            poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            poseState.motion = MakeLocomotionAnim (anim, pose.tracking.loop, height, speed, guid);
            if (isMove) {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }

            // 侵入経路
            if (pose.beforeAnimationClip) {
                var beforeState = layer.stateMachine.AddState ("Before_" + pose.Value.ToString ());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = MakeLocomotionAnim (pose.beforeAnimationClip, pose.tracking.loop, height, speed, guid);

                // 遷移を作成
                var joinTransition = defaultState.AddTransition (beforeState);
                joinTransition.canTransitionToSelf = false;
                joinTransition.hasExitTime = false;
                joinTransition.hasFixedDuration = true;
                joinTransition.duration = 0.0f;
                joinTransition.conditions = flags.Select ((flag, i) => new AnimatorCondition {
                        mode = AnimatorConditionMode.Equals,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flag
                    })
                    .ToArray ();

                // バイパスの作成
                var bypassTransition = beforeState.AddTransition (poseState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = true;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                var joinTransition = defaultState.AddTransition (poseState);
                joinTransition.canTransitionToSelf = false;
                joinTransition.hasExitTime = false;
                joinTransition.hasFixedDuration = true;
                joinTransition.duration = 0.0f;
                joinTransition.conditions = flags.Select ((flag, i) => new AnimatorCondition {
                        mode = AnimatorConditionMode.Equals,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flag
                    })
                    .ToArray ();
            }

            // 脱出経路
            if (pose.afterAnimationClip) {
                // メインステートの作成
                var afterState = layer.stateMachine.AddState ("After_" + pose.Value.ToString ());
                afterState.writeDefaultValues = writeDefault_;
                afterState.mirrorParameterActive = true;
                afterState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                afterState.motion = MakeLocomotionAnim (pose.afterAnimationClip, pose.tracking.loop, height, speed, guid);

                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    var leftTransition = poseState.AddTransition (afterState);
                    leftTransition.canTransitionToSelf = false;
                    leftTransition.hasExitTime = false;
                    leftTransition.hasFixedDuration = true;
                    leftTransition.duration = 0.0f;
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }

                // バイパスを作成
                var bypassTransition = afterState.AddTransition (defaultState);
                bypassTransition.canTransitionToSelf = false;
                bypassTransition.hasExitTime = true;
                bypassTransition.hasFixedDuration = true;
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    var leftTransition = poseState.AddTransition (defaultState);
                    leftTransition.canTransitionToSelf = false;
                    leftTransition.hasExitTime = false;
                    leftTransition.hasFixedDuration = true;
                    leftTransition.duration = 0.0f;
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }
            }
        }

        Motion MakeFxAnim (AnimationClip anim, bool loop) {
            // blendTree
            anim = MotionBuilder.SetAnimationLoop (anim, loop);
            // Transform以外のAnimationを抽出
            anim = MotionBuilder.PartAnimation (anim, MotionBuilder.AnimationPart.Fx);

            return anim;
        }

        Motion MakeLocomotionAnim (AnimationClip anim, bool loop, bool height, bool speed, string guid) {
            var blendTree = new BlendTree ();

            if (MotionBuilder.IsMoveAnimation (anim)) {
                anim = MotionBuilder.SetAnimationLoop (anim, loop);
                anim = MotionBuilder.PartAnimation (anim, MotionBuilder.AnimationPart.Locomotion);
                // アニメーションの生成
                AnimationClip motionClip0 = height ? MotionBuilder.BuildMotionLevel (anim, +DynamicVariables.Settings.minMaxHeight) : anim;
                AnimationClip motionClip1 = height ? MotionBuilder.BuildMotionLevel (anim, -DynamicVariables.Settings.minMaxHeight) : anim;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.AddChild (motionClip0, 0);
                blendTree.AddChild (motionClip1, 1);
            } else {
                anim = MotionBuilder.SetAnimationLoop (anim, loop);
                anim = MotionBuilder.PartAnimation (anim, MotionBuilder.AnimationPart.Locomotion);
                // アニメーションの生成
                var motionClip0 = speed ? MotionBuilder.IdleAnimation (anim, 0f) : anim;
                var motionClip1 = speed ? MotionBuilder.IdleAnimation (anim, DynamicVariables.Settings.motionNoiseScale) : anim;
                var motionClip00 = height ? MotionBuilder.BuildMotionLevel (motionClip0, +DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip01 = height ? MotionBuilder.BuildMotionLevel (motionClip0, -DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip10 = height ? MotionBuilder.BuildMotionLevel (motionClip1, +DynamicVariables.Settings.minMaxHeight) : motionClip1;
                var motionClip11 = height ? MotionBuilder.BuildMotionLevel (motionClip1, -DynamicVariables.Settings.minMaxHeight) : motionClip1;
                blendTree.blendType = BlendTreeType.FreeformCartesian2D;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.blendParameterY = $"{ConstVariables.SpeedParamPrefix}_{guid}";
                blendTree.AddChild (motionClip00, new Vector2 (0f, 0f));
                blendTree.AddChild (motionClip01, new Vector2 (1f, 0f));
                blendTree.AddChild (motionClip10, new Vector2 (0f, 1f));
                blendTree.AddChild (motionClip11, new Vector2 (1f, 1f));
            }

            return blendTree;
        }

        public static AnimatorStateTransition DuplicateTransition(AnimatorStateTransition source, AnimatorState state)
        {
            var dest = state.AddTransition(source.destinationState);
            dest.hasExitTime = source.hasExitTime;
            dest.exitTime = source.exitTime;
            dest.hasFixedDuration = source.hasFixedDuration;
            dest.duration = source.duration;
            dest.offset = source.offset;
            dest.interruptionSource = source.interruptionSource;
            dest.orderedInterruption = source.orderedInterruption;
            dest.canTransitionToSelf = source.canTransitionToSelf;

            // 条件のコピー
            foreach (var condition in source.conditions)
            {
                dest.AddCondition(condition.mode, condition.threshold, condition.parameter);
            }

            return dest;
        }

    }
}