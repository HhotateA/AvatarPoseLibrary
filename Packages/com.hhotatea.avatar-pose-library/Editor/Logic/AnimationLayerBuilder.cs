using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;

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

            var paramReset = resetState.AddSafeParameterDriver ();
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
                value = 0,
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
                name = $"{ConstVariables.AudioParamPrefix}_{poseLibrary.Guid}",
                value = 1f,
            });
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
            var resetTransition = defaultState.MakeTransition (resetState,true);
            resetTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            var defaultTransition = resetState.MakeTransition (defaultState,true);

            return layer;
        }

        public enum TrackingType
        {
            Base,
            Head,
            Arm,
            Foot,
            Finger,
            Face,
            Action,
            Space
        }

        public AnimatorControllerLayer PoseTrackingLayer(TrackingType type, string param, string guid)
        {
            return TrackingLayer(type, param, guid,
                (layer, offToOn, onToOff, onState, offState) =>
                {
                    AnimatorUtility.CreateActiveTransition(onState, offToOn, onToOff, param, $"{ConstVariables.OnPlayParamPrefix}_{guid}");
                    
                    
                    var pose_reload = onState.AddSafeParameterDriver();
                    pose_reload.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.PoseReloadParamPrefix}_{guid}",
                        value = 0
                    });
                    AnimatorUtility.CreateLoopTransition(onState, $"{ConstVariables.PoseReloadParamPrefix}_{guid}", false);

                    var onLoopState = layer.stateMachine.AddState("OnLoop");
                    onLoopState.writeDefaultValues = onState.writeDefaultValues;
                    onLoopState.motion = onState.motion;
                    var height_reload = onLoopState.AddSafeParameterDriver();
                    height_reload.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.HeightUpdateParamPrefix}_{guid}",
                        value = 1
                    });
                    height_reload.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.PoseReloadParamPrefix}_{guid}",
                        value = 1
                    });

                    var loop_enter = onState.MakeTransition(onLoopState,false);
                    loop_enter.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.IfNot,
                        parameter = $"{ConstVariables.HeightUpdateParamPrefix}_{guid}",
                        }
                    };
                    var loop_exit = onLoopState.MakeTransition(onState,false);
                    loop_exit.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.If,
                        parameter = $"{ConstVariables.PoseReloadParamPrefix}_{guid}",
                        }
                    };

                    var spaceEnter = onLoopState.AddStateMachineBehaviour<VRCAnimatorTemporaryPoseSpace>();
                    spaceEnter.enterPoseSpace = false;
                });
        }

        public AnimatorControllerLayer ActiveTrackingLayer(TrackingType type, string param, string guid)
        {
            return TrackingLayer(type,param,guid,
                (layer, offToOn, onToOff, onState, offState) => {
                    AnimatorUtility.CreateActiveTransition(onState,offToOn,onToOff,param,$"{ConstVariables.OnPlayParamPrefix}_{guid}");
                    AnimatorUtility.CreateLoopTransition(onState,$"{ConstVariables.DummyParamPrefix}_{guid}",true);
                });
        }

        public AnimatorControllerLayer ConstantTrackingLayer(TrackingType type, string param, string guid)
        {
            return TrackingLayer(type, param, guid, (layer, offToOn, onToOff, onState, offState) =>
            {
                AnimatorUtility.CreateLoopTransition(onState,$"{ConstVariables.DummyParamPrefix}_{guid}",true);
            });
        }

        private AnimatorControllerLayer TrackingLayer(
            TrackingType type, string param, string guid,
            Action<AnimatorControllerLayer, AnimatorStateTransition, AnimatorStateTransition, AnimatorState, AnimatorState> onSetTrasitions)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            // ステートの初期化
            var offIdleState = layer.stateMachine.AddState("OffIdle");
            offIdleState.writeDefaultValues = writeDefault_;
            offIdleState.motion = MotionBuilder.NoneAnimation;

            var offConState = layer.stateMachine.AddState("OnToOff");
            offConState.writeDefaultValues = writeDefault_;
            offConState.motion = MotionBuilder.NoneAnimation;

            // var onIdleState = layer.stateMachine.AddState("OnIdle");
            // onIdleState.writeDefaultValues = writeDefault_;
            // onIdleState.motion = noneClip;

            var onConState = layer.stateMachine.AddState("OnIdle");
            onConState.writeDefaultValues = writeDefault_;
            onConState.motion = MotionBuilder.NoneAnimation;

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
            var fromOffToOn = offIdleState.MakeTransition (onConState,false);
            fromOffToOn.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            var fromOnToOff = onConState.MakeTransition (offConState,false);
            fromOnToOff.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.IfNot,
                parameter = param,
                }
            };

            var fromOffToOff = offConState.MakeTransition (offIdleState,false);
            fromOffToOff.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.IfNot,
                parameter = $"{ConstVariables.DummyParamPrefix}_{guid}",
                }
            };

            // PoseSpaceの設定用（Loopより優先するためにここで挿し込む）
            onSetTrasitions?.Invoke(layer, fromOffToOn, fromOnToOff, onConState, offIdleState);

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
                var trackingOnParam = reserveState.AddSafeParameterDriver ();
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
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.PoseReloadParamPrefix}_{guid}",
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
            if (pose.audioClip)
            {
                var vapa = poseState.AddStateMachineBehaviour<VRCAnimatorPlayAudio>();
                vapa.SourcePath = $"{ConstVariables.AudioParamPrefix}_{guid}";
                vapa.VolumeApplySettings = VRC_AnimatorPlayAudio.ApplySettings.NeverApply;
                vapa.PitchApplySettings = VRC_AnimatorPlayAudio.ApplySettings.NeverApply;
                vapa.ClipsApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                vapa.Clips = new AudioClip[] { pose.audioClip };
                vapa.LoopApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                vapa.Loop = pose.tracking.loop;
                vapa.PlayOnEnter = true;
                vapa.StopOnEnter = false;
                vapa.PlayOnExit = false;
                vapa.StopOnExit = true;
            }

            // 遷移を作成
            var joinTransition = defaultState.MakeTransition (reserveState,false);
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
                var bypassTransition = reserveState.MakeTransition (beforeState,true);
                bypassTransition.duration = 0.1f;

                // メインへの移行
                var mainTransition = beforeState.MakeTransition (poseState,true);
            } else {
                // メインへの移行
                var mainTransition = reserveState.MakeTransition (poseState,true);
            }

            // 脱出経路
            if (true) {
                // リセットへの遷移
                var leftTransition = poseState.MakeTransition (resetState,false);
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
                    var preResetTransition = poseState.MakeTransition (preResetState,false);
                    preResetTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = p,
                        threshold = 0
                        }
                    };
                }

                if (!pose.tracking.loop) {
                    var endTransition = poseState.MakeTransition (preResetState,true);
                }
            }
        }

        public AnimatorControllerLayer AudioVolumeLayer(string param,string obj,float volume)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override
            };

            // ステートの初期化
            var defaultState = layer.stateMachine.AddState ("Default");
            defaultState.writeDefaultValues = writeDefault_;
            defaultState.motion = MotionBuilder.FrameAnimation;
            var noneClip = MotionBuilder.CreateFrameAnimation(DynamicVariables.Settings.paramRate);

            var count = 100;
            var step = 1f / (float)count;
            var shift = 1f / 512f;
            for (int i = -1; i < count + 1; i++)
            {
                float v = (float)(i) / (float)count;
                // メインステートの作成
                var poseState = layer.stateMachine.AddState("Volume_" + i.ToString());
                poseState.writeDefaultValues = writeDefault_;
                poseState.motion = noneClip;

                var vapa = poseState.AddStateMachineBehaviour<VRCAnimatorPlayAudio>();
                vapa.SourcePath = obj;
                vapa.VolumeApplySettings = VRC_AnimatorPlayAudio.ApplySettings.AlwaysApply;
                vapa.Volume = new Vector2(v * volume, v * volume);
                vapa.PitchApplySettings = VRC_AnimatorPlayAudio.ApplySettings.NeverApply;
                vapa.ClipsApplySettings = VRC_AnimatorPlayAudio.ApplySettings.NeverApply;
                vapa.LoopApplySettings = VRC_AnimatorPlayAudio.ApplySettings.NeverApply;
                vapa.PlayOnEnter = true;
                vapa.StopOnEnter = false;
                vapa.PlayOnExit = false;
                vapa.StopOnExit = false;

                var joinTransition = defaultState.MakeTransition (poseState,false);
                joinTransition.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Greater,
                        parameter = param,
                        threshold = v - shift,
                    },
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Less,
                        parameter = param,
                        threshold = v + step + shift,
                    }
                };

                // var leftTransition = poseState.AddTransition(defaultState);
                // leftTransition.canTransitionToSelf = false;
                // leftTransition.hasExitTime = true;
                // leftTransition.hasFixedDuration = true;
                // leftTransition.duration = 0.0f;

                var leftTransition_1 = poseState.MakeTransition (defaultState,false);
                leftTransition_1.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Less,
                        parameter = param,
                        threshold = v - shift,
                    }
                };

                var leftTransition_2 = poseState.MakeTransition(defaultState,false);
                leftTransition_2.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Greater,
                        parameter = param,
                        threshold = v + step + shift,
                    }
                };
            }

            return layer;
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
            poseState.motion = anim.MakeFxAnim (pose.tracking.loop);
            var trackingOnParam = poseState.AddSafeParameterDriver ();
            foreach (var (enabled, prefix) in trackingMap) {
                trackingOnParam.parameters.Add (new VRC_AvatarParameterDriver.Parameter {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{prefix}_{guid}",
                        value = enabled ? 1f : 0f,
                });
            }
            if (isMove)
            {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }

            var inTransition = flags.Select((flag, i) => new AnimatorCondition
            {
                mode = AnimatorConditionMode.Equals,
                parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                threshold = flag,
            }).ToList();

            // 侵入経路
            if (pose.beforeAnimationClip) {
                // メインステートの作成
                var beforeState = layer.stateMachine.AddState ("Before_" + pose.Value.ToString ());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = pose.beforeAnimationClip.MakeFxAnim (pose.tracking.loop);

                // 遷移を作成
                var joinTransition = defaultState.MakeTransition (beforeState,false);
                joinTransition.conditions = inTransition.ToArray ();

                // バイパスの作成
                var bypassTransition = beforeState.MakeTransition (poseState,true);
                bypassTransition.duration = 0.1f;

                // レイヤーの設定
                var additiveOn = beforeState.AddStateMachineBehaviour<VRCPlayableLayerControl> ();
                additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                additiveOn.goalWeight = 1f;
            } else {
                // 遷移を作成
                var joinTransition = defaultState.MakeTransition (poseState,false);
                joinTransition.conditions = inTransition.ToArray ();

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
                afterState.motion = pose.afterAnimationClip.MakeFxAnim (pose.tracking.loop);

                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    // Preからリセットへの遷移
                    var leftTransition = poseState.MakeTransition (afterState,false);
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }

                // バイパスを作成
                var bypassTransition = afterState.MakeTransition (resetState,true);
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    // Preからリセットへの遷移
                    var leftTransition = poseState.MakeTransition (resetState,false);
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
            poseState.motion = anim.MakeLocomotionAnim (pose.tracking.loop, height, speed, guid);
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
                beforeState.motion = pose.beforeAnimationClip.MakeLocomotionAnim (pose.tracking.loop, height, speed, guid);

                // 遷移を作成
                var joinTransition = defaultState.MakeTransition (beforeState,false);
                joinTransition.conditions = flags.Select ((flag, i) => new AnimatorCondition {
                        mode = AnimatorConditionMode.Equals,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flag
                    })
                    .ToArray ();

                // バイパスの作成
                var bypassTransition = beforeState.MakeTransition(poseState, true);
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                var joinTransition = defaultState.MakeTransition (poseState,false);
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
                afterState.motion = pose.afterAnimationClip.MakeLocomotionAnim (pose.tracking.loop, height, speed, guid);

                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    var leftTransition = poseState.MakeTransition (afterState,false);
                    leftTransition.conditions = new AnimatorCondition[] {
                        new AnimatorCondition {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flags[i],
                        }
                    };
                }

                // バイパスを作成
                var bypassTransition = afterState.MakeTransition (defaultState,true);
                bypassTransition.duration = 0.1f;
            } else {
                // 遷移を作成
                for (int i = 0; i < flags.Length; i++) {
                    var leftTransition = poseState.MakeTransition (defaultState,false);
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
    }
}