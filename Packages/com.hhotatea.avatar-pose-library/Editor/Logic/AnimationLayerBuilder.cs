using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.logic
{
    public class AnimationLayerBuilder
    {
        bool writeDefault_;
        AvatarPoseData poseLibrary_;

        public AnimationLayerBuilder(bool writeDefault, AvatarPoseData poseLibrary)
        {
            writeDefault_ = writeDefault;
            poseLibrary_ = poseLibrary;
        }

        public AnimatorControllerLayer ResetLayer(string param, AvatarPoseData poseLibrary)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };

            // ステートの初期化
            var defaultState = layer.stateMachine.AddState("Default");
            defaultState.writeDefaultValues = writeDefault_;
            defaultState.motion = MotionBuilder.FrameAnimation;

            var resetState = layer.stateMachine.AddState("Reset");
            resetState.writeDefaultValues = writeDefault_;
            resetState.motion = MotionBuilder.FrameAnimation;

            var paramReset = resetState.AddSafeParameterDriver();
            foreach (var parameter in poseLibrary.Parameters)
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
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
                })
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0,
                });
            }
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = $"{ConstVariables.MirrorCycleOffsetParamPrefix}_{poseLibrary.Guid}",
                value = 0,
            });
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = $"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}",
                value = 0,
            });
            foreach (var parameter in new String[] {
                    ConstVariables.SpeedParamPrefix,
                        ConstVariables.HeightParamPrefix,
                })
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{parameter}_{poseLibrary.Guid}",
                    value = 0.5f,
                });
            }
            if (poseLibrary.UseBodyRotationParam)
            {
                // 回転ラジアルも無回転に戻します（X/Z は左端 = 0 度、Y は中央 = 0 度）。
                // 戻さない場合、リセット後も最後に選んだ傾きが残ります。
                foreach (var axis in ConstVariables.BodyRotationAxes)
                {
                    paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{axis.ParameterPrefix}_{poseLibrary.Guid}",
                        value = axis.NeutralValue,
                    });
                }
            }
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = $"{ConstVariables.AudioParamPrefix}_{poseLibrary.Guid}",
                value = 1f,
            });
            if (poseLibrary.enableVariationSlider)
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.VariationParamPrefix}_{poseLibrary.Guid}",
                    value = 0f,
                });
            }
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = param,
                value = 0,
            });

            for (int i = 0; i < ConstVariables.PoseFlagCount; i++)
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}",
                    value = 0,
                });
            }

            var action = resetState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
            action.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            action.goalWeight = 0f;

            var gesture = resetState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
            gesture.layer = VRC_PlayableLayerControl.BlendableLayer.Gesture;
            gesture.goalWeight = 1f;

            // 遷移の設定
            var resetTransition = defaultState.MakeTransition(resetState, true);
            resetTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            var defaultTransition = resetState.MakeTransition(defaultState, true);

            return layer;
        }

        /// <summary>
        /// 回転ラジアルだけを無回転へ戻すレイヤーを作成します。
        /// ポーズの選択や他の設定は維持したまま、X/Y/Z の傾きだけをリセットします。
        /// </summary>
        public AnimatorControllerLayer BodyRotationResetLayer(string param, AvatarPoseData poseLibrary)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };

            // ステートの初期化
            var defaultState = layer.stateMachine.AddState("Default");
            defaultState.writeDefaultValues = writeDefault_;
            defaultState.motion = MotionBuilder.FrameAnimation;

            var resetState = layer.stateMachine.AddState("Reset");
            resetState.writeDefaultValues = writeDefault_;
            resetState.motion = MotionBuilder.FrameAnimation;

            var paramReset = resetState.AddSafeParameterDriver();
            foreach (var axis in ConstVariables.BodyRotationAxes)
            {
                paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{axis.ParameterPrefix}_{poseLibrary.Guid}",
                    value = axis.NeutralValue,
                });
            }

            // トリガー自身も戻し、メニューのトグルが押されたままにならないようにします。
            paramReset.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                name = param,
                value = 0,
            });

            // 遷移の設定
            var resetTransition = defaultState.MakeTransition(resetState, true);
            resetTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            resetState.MakeTransition(defaultState, true);

            return layer;
        }

        // ラジアル中央（無回転）付近とみなす許容幅。
        const float RotationEpsilon = 0.002f;

        /// <summary>
        /// 回転ラジアルが無回転から外れている間だけ IK を切り、クリップ側の姿勢をそのまま表示します。
        ///
        /// 傾きをアニメーションで与えても、VRChat の IK は頭・手足のトラッキング位置に合わせて
        /// 脊柱と脚を解き直します。そのためヨーが 90 度を超えたあたりで解が反転し、体が一瞬
        /// 別の向きへ飛んでから戻る（スナップする）ように見えます。角度を制限しても、この競合は
        /// 無くなりません。回転中だけ IK を切ることで、クリップ側の姿勢をそのまま出します。
        /// </summary>
        public AnimatorControllerLayer BodyRotationTrackingLayer(string param, AvatarPoseData poseLibrary)
        {
            // レイヤー作成
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = param,
                defaultWeight = 0f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };

            // ステートの初期化
            var neutralState = layer.stateMachine.AddState("Neutral");
            neutralState.writeDefaultValues = writeDefault_;
            neutralState.motion = MotionBuilder.NoneAnimation;

            var rotatedState = layer.stateMachine.AddState("Rotated");
            rotatedState.writeDefaultValues = writeDefault_;
            rotatedState.motion = MotionBuilder.NoneAnimation;

            // 回転中は姿勢をクリップに任せます（IK を切る）。
            // 無回転側には何も置かず、ユーザーがメニューで設定したトラッキングを尊重します。
            var tracking = rotatedState.AddSafeStateMachineBehaviour<VRCAnimatorTrackingControl>();
            tracking.trackingHip = VRC_AnimatorTrackingControl.TrackingType.Animation;
            tracking.trackingLeftFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
            tracking.trackingRightFoot = VRC_AnimatorTrackingControl.TrackingType.Animation;
            tracking.trackingLeftHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
            tracking.trackingRightHand = VRC_AnimatorTrackingControl.TrackingType.Animation;
            tracking.trackingHead = VRC_AnimatorTrackingControl.TrackingType.Animation;

            // 無回転の値は軸ごとに違います（X/Z は 0、Y は 0.5）。
            var rotationAxes = ConstVariables.BodyRotationAxes;

            // 無回転から外れたら IK を切ります。軸ごとに、無回転から離れる向きの遷移を張ります。
            // パラメーターは 0..1 なので、無回転が端にある軸（X/Z は左端）は片側だけになります。
            foreach (var axis in rotationAxes)
            {
                var parameter = $"{axis.ParameterPrefix}_{poseLibrary.Guid}";
                var neutral = axis.NeutralValue;
                if (neutral + RotationEpsilon < 1f)
                {
                    AddRotationCondition(neutralState, rotatedState, parameter,
                        AnimatorConditionMode.Greater, neutral + RotationEpsilon);
                }

                if (neutral - RotationEpsilon > 0f)
                {
                    AddRotationCondition(neutralState, rotatedState, parameter,
                        AnimatorConditionMode.Less, neutral - RotationEpsilon);
                }
            }

            // 3軸すべてが無回転へ戻ったときだけ IK を戻します。
            // 1軸でも傾いている間に戻すと、また競合してスナップします。
            var backTransition = rotatedState.MakeTransition(neutralState, false);
            backTransition.conditions = rotationAxes
                .SelectMany(axis => NeutralConditions(axis, poseLibrary.Guid))
                .ToArray();

            return layer;
        }

        static void AddRotationCondition(
            AnimatorState from, AnimatorState to, string parameter,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.MakeTransition(to, false);
            transition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                    mode = mode,
                    parameter = parameter,
                    threshold = threshold,
                }
            };
        }

        /// <summary>
        /// その軸が無回転の近傍にあることを表す条件を作ります。
        /// 無回転がパラメーター範囲の端にある軸（X/Z は左端 = 0）では、
        /// 到達し得ない側の条件を作りません。
        /// </summary>
        static AnimatorCondition[] NeutralConditions(
            ConstVariables.BodyRotationAxis axis, string guid)
        {
            var parameter = $"{axis.ParameterPrefix}_{guid}";
            var neutral = axis.NeutralValue;
            var conditions = new List<AnimatorCondition>(2);

            if (neutral - RotationEpsilon > 0f)
            {
                conditions.Add(new AnimatorCondition
                {
                    mode = AnimatorConditionMode.Greater,
                    parameter = parameter,
                    threshold = neutral - RotationEpsilon,
                });
            }

            if (neutral + RotationEpsilon < 1f)
            {
                conditions.Add(new AnimatorCondition
                {
                    mode = AnimatorConditionMode.Less,
                    parameter = parameter,
                    threshold = neutral + RotationEpsilon,
                });
            }

            return conditions.ToArray();
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
            Space,
            Mirror
        }

        public AnimatorControllerLayer ActiveTrackingLayer(TrackingType type, string param, string guid)
        {
            return TrackingLayer(type, param, guid,
                (layer, offToOn, onToOff, onState, offState) =>
                {
                    AnimatorUtility.CreateActiveTransition(onState, offToOn, onToOff, param, $"{ConstVariables.OnPlayParamPrefix}_{guid}");
                    AnimatorUtility.CreateLoopTransition(onState, $"{ConstVariables.DummyParamPrefix}_{guid}", true);
                });
        }

        public AnimatorControllerLayer ConstantTrackingLayer(TrackingType type, string param, string guid)
        {
            return TrackingLayer(type, param, guid, (layer, offToOn, onToOff, onState, offState) =>
            {
                AnimatorUtility.CreateLoopTransition(onState, $"{ConstVariables.DummyParamPrefix}_{guid}", true);
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
                    var locoOn = offConState.AddSafeStateMachineBehaviour<VRCAnimatorLocomotionControl>();
                    locoOn.disableLocomotion = false;
                    var locoOff = onConState.AddSafeStateMachineBehaviour<VRCAnimatorLocomotionControl>();
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
                    var gestureOff = offConState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                    gestureOff.layer = VRC_PlayableLayerControl.BlendableLayer.Gesture;
                    gestureOff.goalWeight = 1f;
                    var gestureOn = onConState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
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
                    var fxOff = offConState.AddSafeStateMachineBehaviour<VRCAnimatorLayerControl>();
                    fxOff.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    fxOff.layer = 1;
                    fxOff.goalWeight = 0f;
                    var fxOn = onConState.AddSafeStateMachineBehaviour<VRCAnimatorLayerControl>();
                    fxOn.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    fxOn.layer = 1;
                    fxOn.goalWeight = 1f;
                    break;

                case TrackingType.Action:
                    if (poseLibrary_.suppressAdditiveAnimator)
                    {
                        var additiveOff = offConState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                        additiveOff.layer = VRC_PlayableLayerControl.BlendableLayer.Additive;
                        additiveOff.goalWeight = 1f;
                        var additiveOn = onConState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                        additiveOn.layer = VRC_PlayableLayerControl.BlendableLayer.Additive;
                        additiveOn.goalWeight = 0f;
                    }

                    var actionOff = onConState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                    actionOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    actionOff.goalWeight = 0f;

                    break;

                case TrackingType.Space:
                    var spaceEnter = onConState.AddSafeStateMachineBehaviour<VRCAnimatorTemporaryPoseSpace>();
                    spaceEnter.enterPoseSpace = true;
                    var spaceExit = offConState.AddSafeStateMachineBehaviour<VRCAnimatorTemporaryPoseSpace>();
                    spaceExit.enterPoseSpace = false;

                    break;

                case TrackingType.Mirror:
                    var mirrorOffDriver = offConState.AddSafeParameterDriver();
                    mirrorOffDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.MirrorCycleOffsetParamPrefix}_{guid}",
                        value = 0f,
                    });

                    var mirrorOnDriver = onConState.AddSafeParameterDriver();
                    mirrorOnDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.MirrorCycleOffsetParamPrefix}_{guid}",
                        value = 0.5f,
                    });
                    break;
            }

            // 遷移の設定
            var fromOffToOn = offIdleState.MakeTransition(onConState, false);
            fromOffToOn.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.If,
                parameter = param,
                }
            };

            var fromOnToOff = onConState.MakeTransition(offConState, false);
            fromOnToOff.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                mode = AnimatorConditionMode.IfNot,
                parameter = param,
                }
            };

            var fromOffToOff = offConState.MakeTransition(offIdleState, false);
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

        private static void ApplyTrackingLayer(
            AnimatorState offState, AnimatorState onState,
            Action<VRCAnimatorTrackingControl> configureOff,
            Action<VRCAnimatorTrackingControl> configureOn)
        {
            var offCon = offState.AddSafeStateMachineBehaviour<VRCAnimatorTrackingControl>();
            var onCon = onState.AddSafeStateMachineBehaviour<VRCAnimatorTrackingControl>();
            configureOff(offCon);
            configureOn(onCon);
        }

        // Variation slider helpers
        private bool IsVariationMode => poseLibrary_ != null && poseLibrary_.enableVariationSlider;
        private string VariationParam(string guid) => $"{ConstVariables.VariationParamPrefix}_{guid}";
        private (float low, float high, float shift) GetVariationInterval(PoseEntry pose, string guid)
        {
            int total = Mathf.Max(1, poseLibrary_?.PoseCount ?? 1);
            float segment = 1f / total;
            float shift = 1f / 512f;
            float idleOffset = shift * 2f;
            int idx = Mathf.Clamp(pose.Index - 1, 0, total - 1);
            float low = idx * segment + idleOffset;
            float high = (idx + 1) * segment + idleOffset;
            if (high > 1f) high = 1f + shift;
            return (low, high, shift);
        }
        private AnimatorCondition[] VariationEntryConditions(PoseEntry pose, string guid)
        {
            var (low, high, shift) = GetVariationInterval(pose, guid);
            string vp = VariationParam(guid);
            return new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = vp, threshold = low - shift },
                new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = vp, threshold = high + shift }
            };
        }
        private void AddVariationExitTransitions(AnimatorState poseState, AnimatorState target, PoseEntry pose, string guid)
        {
            var (low, high, shift) = GetVariationInterval(pose, guid);
            string vp = VariationParam(guid);
            var t1 = poseState.MakeTransition(target, false);
            t1.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = vp, threshold = low - shift } };
            var t2 = poseState.MakeTransition(target, false);
            t2.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = vp, threshold = high + shift } };
        }

        public void AddParamLayer(
            AnimatorControllerLayer layer, PoseEntry pose,
            List<string> parameters, string guid,
            AnimatorState defaultState, AnimatorState resetState, AnimatorState preResetState)
        {
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => (PreReset =>) Reset => Default
             * のループ。
             * 
             * Reserveでパラメーターの同期、Resetでパラメーターの初期化を行う
             * Loopアニメーションの場合は、再生終了後にPreResetを経由する
             */
            bool isMove = MotionBuilder.IsMoveAnimation(pose.animationClip);
            var anim = MotionBuilder.SafeAnimation(pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // 準備ステートの作成
            var reserveState = layer.stateMachine.AddState("Reserve_" + pose.Value.ToString());
            reserveState.motion = MotionBuilder.FrameAnimation;
            reserveState.writeDefaultValues = writeDefault_;
            {
                var trackingOnParam = reserveState.AddSafeParameterDriver();
                trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.SpeedParamPrefix}_{guid}",
                    value = pose.tracking.motionSpeed == 0f ? 0f : 0.5f,
                });
                trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.ActionParamPrefix}_{guid}",
                    value = 1f,
                });
                trackingOnParam.parameters.AddRange(
                    pose.GetAnimatorFlag().Select((flag, index) => new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = $"{ConstVariables.FlagParamPrefix}_{guid}_{index}",
                        value = flag
                    })
                );
                trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    name = $"{ConstVariables.OnPlayParamPrefix}_{guid}",
                    value = 1f,
                });
                if (IsVariationMode)
                {
                    // Mirror current variation state into legacy Int/Flag for Fx/Locomotion layers driven by variation — driver already flag-set, keep add but also ensure OnPlay flag
                }
                if (poseLibrary_.suppressAdditiveAnimator)
                {
                    var action = reserveState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                    action.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                    action.goalWeight = 1f;
                }
            }

            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_" + pose.Value.ToString());
            poseState.writeDefaultValues = writeDefault_;
            poseState.motion = MotionBuilder.PartAnimation(anim, MotionBuilder.AnimationPart.None);
            if (isMove)
            {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            }
            if (pose.audioClip)
            {
                var vapa = poseState.AddSafeStateMachineBehaviour<VRCAnimatorPlayAudio>();
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
            var joinTransition = defaultState.MakeTransition(reserveState, false);
            if (IsVariationMode)
            {
                joinTransition.conditions = VariationEntryConditions(pose, guid);
            }
            else
            {
                joinTransition.conditions = new AnimatorCondition[] {
                    new AnimatorCondition () {
                    mode = AnimatorConditionMode.Equals,
                    parameter = pose.Parameter,
                    threshold = pose.Value
                    }
                };
            }

            // 侵入経路
            if (pose.beforeAnimationClip)
            {
                // メインステートの作成
                var beforeState = layer.stateMachine.AddState("Before_" + pose.Value.ToString());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = MotionBuilder.PartAnimation(pose.beforeAnimationClip, MotionBuilder.AnimationPart.None);

                // バイパスの作成
                var bypassTransition = reserveState.MakeTransition(beforeState, true);
                bypassTransition.duration = 0.1f;

                // メインへの移行
                beforeState.MakeTransition(poseState, true);
            }
            else
            {
                // メインへの移行
                reserveState.MakeTransition(poseState, true);
            }

            // 脱出経路
            if (IsVariationMode)
            {
                AddVariationExitTransitions(poseState, resetState, pose, guid);
            }
            else
            {
                var leftTransition = poseState.MakeTransition(resetState, false);
                leftTransition.conditions = new[]
                {
                    new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.NotEqual,
                        parameter = pose.Parameter,
                        threshold = pose.Value
                    }
                };
            }

            // パラメーターリストを作成 - skip in variation mode (variation slider exclusively selects, so non-zero other Ints are not used)
            if (!IsVariationMode)
            {
                parameters.Remove(pose.Parameter);
                foreach (var parameter in parameters)
                {
                    var preResetTransition = poseState.MakeTransition(preResetState, false);
                    preResetTransition.conditions = new[]
                    {
                        new AnimatorCondition
                        {
                            mode = AnimatorConditionMode.NotEqual,
                            parameter = parameter,
                            threshold = 0
                        }
                    };
                }
            }

            if (!pose.tracking.loop)
            {
                poseState.MakeTransition(preResetState, true);
            }
        }
        public AnimatorControllerLayer AudioVolumeLayer(string param, string obj, float volume)
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
            var defaultState = layer.stateMachine.AddState("Default");
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

                var vapa = poseState.AddSafeStateMachineBehaviour<VRCAnimatorPlayAudio>();
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

                var joinTransition = defaultState.MakeTransition(poseState, false);
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

                var leftTransition_1 = poseState.MakeTransition(defaultState, false);
                leftTransition_1.conditions = new AnimatorCondition[]
                {
                    new AnimatorCondition()
                    {
                        mode = AnimatorConditionMode.Less,
                        parameter = param,
                        threshold = v - shift,
                    }
                };

                var leftTransition_2 = poseState.MakeTransition(defaultState, false);
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

        public void AddFxLayer(
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState, AnimatorState resetState,
            string guid)
        {
            /*
             * 基本的な処理の流れは
             * Default => Reserve => Pose => Reset => Default
             * のループ。
             * 
             * ReserveでActionの有効化、ResetでActionの無効化を行う
             */
            bool isMove = MotionBuilder.IsMoveAnimation(pose.animationClip);
            var flags = pose.GetAnimatorFlag();
            var anim = MotionBuilder.SafeAnimation(pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // トラッキング設定用のオブジェクト
            var trackingMap = new (bool enabled, string prefix)[] {
                (pose.tracking.head, ConstVariables.HeadParamPrefix),
                (pose.tracking.arm, ConstVariables.ArmParamPrefix),
                (pose.tracking.foot, ConstVariables.FootParamPrefix),
                (pose.tracking.finger, ConstVariables.FingerParamPrefix),
                (pose.tracking.locomotion, ConstVariables.BaseParamPrefix),
                (pose.tracking.fx, ConstVariables.FaceParamPrefix)
            };

            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_" + pose.Value.ToString());
            poseState.writeDefaultValues = writeDefault_;
            poseState.mirrorParameterActive = true;
            poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            poseState.motion = anim.MakeFxAnim(pose.tracking.loop);
            var trackingOnParam = poseState.AddSafeParameterDriver();
            foreach (var (enabled, prefix) in trackingMap)
            {
                trackingOnParam.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
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
                ApplyLoopingMotionCycleOffsetWorkaround(
                    poseState, pose.tracking.loop, guid);
            }

            AnimatorCondition[] inTransition;
            if (IsVariationMode)
            {
                inTransition = VariationEntryConditions(pose, guid);
            }
            else
            {
                inTransition = flags.Select((flag, i) => new AnimatorCondition
                {
                    mode = AnimatorConditionMode.Equals,
                    parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                    threshold = flag,
                }).ToArray();
            }

            // 侵入経路
            if (pose.beforeAnimationClip)
            {
                // メインステートの作成
                var beforeState = layer.stateMachine.AddState("Before_" + pose.Value.ToString());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = pose.beforeAnimationClip.MakeFxAnim(pose.tracking.loop);

                // 遷移を作成
                var joinTransition = defaultState.MakeTransition(beforeState, false);
                joinTransition.conditions = inTransition;

                // バイパスの作成
                var bypassTransition = beforeState.MakeTransition(poseState, true);
                bypassTransition.duration = 0.1f;

                // レイヤーの設定
                var actionOn = beforeState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                actionOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                actionOn.goalWeight = 1f;
            }
            else
            {
                // 遷移を作成
                var joinTransition = defaultState.MakeTransition(poseState, false);
                joinTransition.conditions = inTransition;

                // レイヤーの設定
                var actionOn = poseState.AddSafeStateMachineBehaviour<VRCPlayableLayerControl>();
                actionOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                actionOn.goalWeight = 1f;
            }

            // 脱出経路
            if (pose.afterAnimationClip)
            {
                // メインステートの作成
                var afterState = layer.stateMachine.AddState("After_" + pose.Value.ToString());
                afterState.writeDefaultValues = writeDefault_;
                afterState.mirrorParameterActive = true;
                afterState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                afterState.motion = pose.afterAnimationClip.MakeFxAnim(pose.tracking.loop);

                // 遷移を作成
                if (IsVariationMode)
                {
                    AddVariationExitTransitions(poseState, afterState, pose, guid);
                }
                else
                {
                    for (int i = 0; i < flags.Length; i++)
                    {
                        var leftTransition = poseState.MakeTransition(afterState, false);
                        leftTransition.conditions = new AnimatorCondition[] {
                            new AnimatorCondition {
                            mode = AnimatorConditionMode.NotEqual,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flags[i],
                            }
                        };
                    }
                }

                // バイパスを作成
                var bypassTransition = afterState.MakeTransition(resetState, true);
                bypassTransition.duration = 0.1f;
            }
            else
            {
                // 遷移を作成
                if (IsVariationMode)
                {
                    AddVariationExitTransitions(poseState, resetState, pose, guid);
                }
                else
                {
                    for (int i = 0; i < flags.Length; i++)
                    {
                        var leftTransition = poseState.MakeTransition(resetState, false);
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

        private static void ApplyLoopingMotionCycleOffsetWorkaround(
            AnimatorState state,
            bool loop,
            string guid)
        {
            if (!loop)
            {
                return;
            }

            // TODO: Remove this workaround when supported Unity versions no longer require a
            // cycle-offset parameter to keep mirrored looping motion synchronized. Keep this
            // Unity-specific behavior isolated here instead of duplicating it across builders.
            state.cycleOffsetParameterActive = true;
            state.cycleOffsetParameter =
                $"{ConstVariables.MirrorCycleOffsetParamPrefix}_{guid}";
        }

        public void AddLocomotionLayer(
            PoseEntry pose,
            AnimatorControllerLayer layer,
            AnimatorState defaultState,
            bool height, bool speed, bool mirror,
            bool rotation,
            string guid)
        {
            bool isMove = MotionBuilder.IsMoveAnimation(pose.animationClip);
            var flags = pose.GetAnimatorFlag();
            var anim = MotionBuilder.SafeAnimation(
                pose.animationClip, pose.beforeAnimationClip, pose.afterAnimationClip);

            // メインステートの作成
            var poseState = layer.stateMachine.AddState("Pose_" + pose.Value.ToString());
            poseState.writeDefaultValues = writeDefault_;
            poseState.mirrorParameterActive = true;
            poseState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
            poseState.motion = anim.MakeLocomotionAnim(
                pose.tracking.loop, height, speed, guid, rotation, Vector3.zero);
            if (isMove)
            {
                // スピードを制御可能にする
                poseState.speed = pose.tracking.motionSpeed * 2f;
                poseState.speedParameterActive = true;
                poseState.speedParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
                ApplyLoopingMotionCycleOffsetWorkaround(
                    poseState, pose.tracking.loop, guid);
            }

            // 侵入経路
            if (pose.beforeAnimationClip)
            {
                var beforeState = layer.stateMachine.AddState("Before_" + pose.Value.ToString());
                beforeState.writeDefaultValues = writeDefault_;
                beforeState.mirrorParameterActive = true;
                beforeState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                beforeState.motion = pose.beforeAnimationClip.MakeLocomotionAnim(
                    pose.tracking.loop, height, speed, guid, rotation, Vector3.zero);

                // 遷移を作成
                var joinTransition = defaultState.MakeTransition(beforeState, false);
                if (IsVariationMode)
                    joinTransition.conditions = VariationEntryConditions(pose, guid);
                else
                    joinTransition.conditions = flags.Select((flag, i) => new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.Equals,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flag
                    }).ToArray();

                // バイパスの作成
                var bypassTransition = beforeState.MakeTransition(poseState, true);
                bypassTransition.duration = 0.1f;
            }
            else
            {
                // 遷移を作成
                var joinTransition = defaultState.MakeTransition(poseState, false);
                if (IsVariationMode)
                    joinTransition.conditions = VariationEntryConditions(pose, guid);
                else
                    joinTransition.conditions = flags.Select((flag, i) => new AnimatorCondition
                    {
                        mode = AnimatorConditionMode.Equals,
                        parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                        threshold = flag
                    }).ToArray();
            }

            // 脱出経路
            if (pose.afterAnimationClip)
            {
                // メインステートの作成
                var afterState = layer.stateMachine.AddState("After_" + pose.Value.ToString());
                afterState.writeDefaultValues = writeDefault_;
                afterState.mirrorParameterActive = true;
                afterState.mirrorParameter = $"{ConstVariables.MirrorParamPrefix}_{guid}";
                afterState.motion = pose.afterAnimationClip.MakeLocomotionAnim(
                    pose.tracking.loop, height, speed, guid, rotation, Vector3.zero);

                // 遷移を作成
                if (IsVariationMode)
                    AddVariationExitTransitions(poseState, afterState, pose, guid);
                else
                {
                    for (int i = 0; i < flags.Length; i++)
                    {
                        var leftTransition = poseState.MakeTransition(afterState, false);
                        leftTransition.conditions = new AnimatorCondition[] {
                            new AnimatorCondition {
                            mode = AnimatorConditionMode.NotEqual,
                            parameter = $"{ConstVariables.FlagParamPrefix}_{guid}_{i}",
                            threshold = flags[i],
                            }
                        };
                    }
                }

                // バイパスを作成
                var bypassTransition = afterState.MakeTransition(defaultState, true);
                bypassTransition.duration = 0.1f;
            }
            else
            {
                // 遷移を作成
                if (IsVariationMode)
                    AddVariationExitTransitions(poseState, defaultState, pose, guid);
                else
                {
                    for (int i = 0; i < flags.Length; i++)
                    {
                        var leftTransition = poseState.MakeTransition(defaultState, false);
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
}
