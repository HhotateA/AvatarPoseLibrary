using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using UnityEngine;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.logic
{
    static class AnimatorUtility
    {
        public static AnimatorStateTransition DuplicateTransition(this AnimatorState state, AnimatorStateTransition source)
        {
            var destination = state.AddTransition(source.destinationState);
            destination.hasExitTime = source.hasExitTime;
            destination.exitTime = source.exitTime;
            destination.hasFixedDuration = source.hasFixedDuration;
            destination.duration = source.duration;
            destination.offset = source.offset;
            destination.interruptionSource = source.interruptionSource;
            destination.orderedInterruption = source.orderedInterruption;
            destination.canTransitionToSelf = source.canTransitionToSelf;

            // 条件のコピー
            foreach (var condition in source.conditions)
            {
                destination.AddCondition(condition.mode, condition.threshold, condition.parameter);
            }

            return destination;
        }

        public static VRCAvatarParameterDriver AddSafeParameterDriver(this AnimatorState state)
        {
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            if (driver.parameters == null)
            {
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();
            }

            return driver;
        }

        public static Motion MakeFxAnim(this AnimationClip anim, bool loop)
        {
            // blendTree
            anim = MotionBuilder.SetAnimationLoop(anim, loop);
            // Transform以外のAnimationを抽出
            anim = MotionBuilder.PartAnimation(anim, MotionBuilder.AnimationPart.Fx);

            return anim;
        }

        public static Motion MakeLocomotionAnim(this AnimationClip anim, bool loop, bool height, bool speed, string guid)
        {
            var blendTree = new BlendTree();
            anim = MotionBuilder.SetAnimationLoop(anim, loop);
            anim = MotionBuilder.PartAnimation(anim, MotionBuilder.AnimationPart.Locomotion);

            if (MotionBuilder.IsMoveAnimation(anim))
            {
                // アニメーションの生成
                AnimationClip motionClip0 = height ? MotionBuilder.BuildMotionLevel(anim, DynamicVariables.Settings.minMaxHeight) : anim;
                AnimationClip motionClip1 = height ? MotionBuilder.BuildMotionLevel(anim, -DynamicVariables.Settings.minMaxHeight) : anim;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.AddChild(motionClip0, 0);
                blendTree.AddChild(motionClip1, 1);
            }
            else
            {
                // アニメーションの生成
                var motionClip0 = speed ? MotionBuilder.IdleAnimation(anim, 0f) : anim;
                var motionClip1 = speed ? MotionBuilder.IdleAnimation(anim, DynamicVariables.Settings.motionNoiseScale) : anim;
                var motionClip00 = height ? MotionBuilder.BuildMotionLevel(motionClip0, DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip01 = height ? MotionBuilder.BuildMotionLevel(motionClip0, -DynamicVariables.Settings.minMaxHeight) : motionClip0;
                var motionClip10 = height ? MotionBuilder.BuildMotionLevel(motionClip1, DynamicVariables.Settings.minMaxHeight) : motionClip1;
                var motionClip11 = height ? MotionBuilder.BuildMotionLevel(motionClip1, -DynamicVariables.Settings.minMaxHeight) : motionClip1;
                blendTree.blendType = BlendTreeType.FreeformCartesian2D;
                blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                blendTree.blendParameterY = $"{ConstVariables.SpeedParamPrefix}_{guid}";
                blendTree.AddChild(motionClip00, new Vector2(0f, 0f));
                blendTree.AddChild(motionClip01, new Vector2(1f, 0f));
                blendTree.AddChild(motionClip10, new Vector2(0f, 1f));
                blendTree.AddChild(motionClip11, new Vector2(1f, 1f));
            }

            return blendTree;
        }

        public static AnimatorStateTransition CreateLoopTransition(AnimatorState state, string param, bool invert)
        {
            var loopTransition = CreateLoopTransition(state, param);
            loopTransition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = invert ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If,
                    parameter = param,
                }
            };
            return loopTransition;
        }

        public static AnimatorStateTransition CreateLoopTransition(AnimatorState state, string param)
        {
            var loopTransition = state.AddTransition(state);
            loopTransition.canTransitionToSelf = true;
            loopTransition.hasExitTime = false;
            loopTransition.exitTime = 0f;
            loopTransition.hasFixedDuration = true;
            loopTransition.duration = 0f;
            return loopTransition;
        }

        public static void CreateActiveTransition(
            AnimatorState onState,
            AnimatorStateTransition onTransition,
            AnimatorStateTransition offTransition,
            string param1, string param2)
        {
            var firstOffTransition = offTransition;
            var secondOffTransition = onState.DuplicateTransition(offTransition);
            onTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                    mode = AnimatorConditionMode.If,
                    parameter = param1
                },
                new AnimatorCondition {
                    mode = AnimatorConditionMode.If,
                    parameter = param2
                }
            };
            firstOffTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = param1
                }
            };
            secondOffTransition.conditions = new AnimatorCondition[] {
                new AnimatorCondition {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = param2
                }
            };
        }

        public static AnimatorStateTransition MakeTransition(this AnimatorState from, AnimatorState to, bool exit)
        {
            var transition = from.AddTransition(to);
            transition.canTransitionToSelf = false;
            transition.hasExitTime = exit;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.exitTime = 0f;
            return transition;
        }

        public static AnimatorController ReplaceResetAnimation(AnimatorController animator, AvatarPoseData data, GameObject root)
        {
            if (!data.enableAutoResetAnim)
            {
                return animator;
            }
            var resetClip = CreateResetAnim(data, root);

            foreach (var layer in animator.layers)
            {
                if (layer.name != $"{ConstVariables.FxAnimatorPrefix}_{data.Guid}")
                {
                    continue;
                }
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state == null)
                    {
                        continue;
                    }
                    if (state.state.name == ConstVariables.StateNameReset)
                    {
                        state.state.motion = resetClip;
                    }
                }
            }
            return animator;
        }

        static AnimationClip CreateResetAnim(AvatarPoseData data, GameObject root)
        {
            // リセットアニメーションの生成
            var anims = data.categories
                .SelectMany(category => category.poses)
                .Select(pose => pose.animationClip)
                .ToArray();
            return MotionBuilder.ResetAnimation(root, anims);
        }
        public static bool IsIncludeVRCShapeKey(AnimationClip anim)
        {
            if (anim == null)
            {
                return false;
            }

            return AnimationUtility.GetCurveBindings(anim)
                .Any(binding => IsVRCShapeKey(binding.type, binding.path, binding.propertyName));
        }
        public static bool IsVRCShapeKey(Type type, string path, string propertyName)
        {
            return type == typeof(SkinnedMeshRenderer)
                && path == "Body"
                && propertyName.StartsWith("blendShape.vrc.", StringComparison.Ordinal);
        }
    }
}
