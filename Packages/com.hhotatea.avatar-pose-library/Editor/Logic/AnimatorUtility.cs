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
            var driver = state.AddSafeStateMachineBehaviour<VRCAvatarParameterDriver>();

            if (driver.parameters == null)
            {
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();
            }

            return driver;
        }

        public static T AddSafeStateMachineBehaviour<T>(this AnimatorState state)
            where T : StateMachineBehaviour
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var behaviour = state.AddStateMachineBehaviour<T>();
            return behaviour != null
                ? behaviour
                : CreateAndAttachStateMachineBehaviourFallback<T>(state);
        }

        private static T CreateAndAttachStateMachineBehaviourFallback<T>(AnimatorState state)
            where T : StateMachineBehaviour
        {
            var behaviourType = typeof(T);
            // TODO: Remove this fallback after the root cause of Unity returning null from
            // AnimatorState.AddStateMachineBehaviour is identified and fixed. Keep the manual
            // ScriptableObject creation isolated here because it is not the normal Unity path.
            var behaviour = ScriptableObject.CreateInstance<T>();
            if (behaviour == null)
            {
                throw new InvalidOperationException(
                    $"Failed to create {behaviourType.FullName} for animator state '{state.name}'.");
            }

            try
            {
                var behaviours = state.behaviours ?? Array.Empty<StateMachineBehaviour>();
                Array.Resize(ref behaviours, behaviours.Length + 1);
                behaviours[behaviours.Length - 1] = behaviour;
                state.behaviours = behaviours;

                if (state.behaviours == null
                    || !state.behaviours.Any(candidate => ReferenceEquals(candidate, behaviour)))
                {
                    throw new InvalidOperationException(
                        $"Unity did not attach {behaviourType.FullName} to animator state '{state.name}'.");
                }

                return behaviour;
            }
            catch (Exception exception)
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
                throw new InvalidOperationException(
                    $"Failed to attach {behaviourType.FullName} to animator state '{state.name}'.",
                    exception);
            }
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
            return anim.MakeLocomotionAnim(loop, height, speed, guid, false, Vector3.zero);
        }

        /// <summary>
        /// ポーズをブレンドツリー化します。rotation が true の場合は X/Y/Z の3軸を
        /// それぞれ1Dブレンドツリーで合成するため、クリップ数が BodyRotationSteps^3 倍になります。
        /// </summary>
        public static Motion MakeLocomotionAnim(
            this AnimationClip anim,
            bool loop,
            bool height,
            bool speed,
            string guid,
            bool rotation,
            Vector3 baseRotation)
        {
            var isMoveAnimation = MotionBuilder.IsMoveAnimation(anim);
            MotionBuilder.WarnIfNoRootCurves(anim);
            var clip = MotionBuilder.SetAnimationLoop(anim, loop);
            clip = MotionBuilder.PartAnimation(clip, MotionBuilder.AnimationPart.Locomotion);

            if (!rotation)
            {
                return BuildLocomotionBlendTree(
                    MotionBuilder.BuildBodyRotation(clip, baseRotation),
                    isMoveAnimation, height, speed, guid);
            }

            // 回転は軸ごとに独立して補間できないため、全組み合わせのクリップが必要になる。
            var steps = Mathf.Max(2, ConstVariables.BodyRotationSteps);
            var xAxis = ConstVariables.BodyRotationAxisX;
            var yAxis = ConstVariables.BodyRotationAxisY;
            var zAxis = ConstVariables.BodyRotationAxisZ;
            // 警告は無回転の組み合わせでだけ出し、steps^3 回ぶん重複しないようにします。
            var neutralX = NeutralStepIndex(xAxis, steps);
            var neutralY = NeutralStepIndex(yAxis, steps);
            var neutralZ = NeutralStepIndex(zAxis, steps);
            var xTree = CreateRotationBlendTree($"{ConstVariables.BodyRotationXParamPrefix}_{guid}");
            for (var xi = 0; xi < steps; xi++)
            {
                var xAngle = RotationStepAngle(xi, steps, xAxis);
                var yTree = CreateRotationBlendTree($"{ConstVariables.BodyRotationYParamPrefix}_{guid}");
                for (var yi = 0; yi < steps; yi++)
                {
                    var yAngle = RotationStepAngle(yi, steps, yAxis);
                    var zTree = CreateRotationBlendTree($"{ConstVariables.BodyRotationZParamPrefix}_{guid}");
                    for (var zi = 0; zi < steps; zi++)
                    {
                        var zAngle = RotationStepAngle(zi, steps, zAxis);
                        var rotated = MotionBuilder.BuildBodyRotation(
                            clip,
                            baseRotation + new Vector3(xAngle, yAngle, zAngle),
                            xi == neutralX && yi == neutralY && zi == neutralZ);
                        zTree.AddChild(
                            BuildLocomotionBlendTree(rotated, isMoveAnimation, height, speed, guid),
                            RotationStepThreshold(zi, steps));
                    }

                    yTree.AddChild(zTree, RotationStepThreshold(yi, steps));
                }

                xTree.AddChild(yTree, RotationStepThreshold(xi, steps));
            }

            return xTree;
        }

        static BlendTree CreateRotationBlendTree(string parameter)
        {
            return new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
            };
        }

        static float RotationStepThreshold(int index, int steps)
        {
            return steps <= 1 ? 0.5f : index / (float)(steps - 1);
        }

        static float RotationStepAngle(int index, int steps, ConstVariables.BodyRotationAxis axis)
        {
            // ラジアルの 0..1 を MinAngle..MaxAngle に割り当てます。
            // 無回転の位置は軸ごとに違うため、角度は軸の定義から引きます
            // （X/Z は左端が無回転、Y は中央が無回転）。
            return axis.AngleAt(RotationStepThreshold(index, steps));
        }

        /// <summary>無回転に対応するステップ番号。</summary>
        static int NeutralStepIndex(ConstVariables.BodyRotationAxis axis, int steps)
        {
            return Mathf.RoundToInt(axis.NeutralValue * (steps - 1));
        }

        static Motion BuildLocomotionBlendTree(
            AnimationClip anim, bool isMoveAnimation, bool height, bool speed, string guid)
        {
            if (isMoveAnimation)
            {
                var moveTree = new BlendTree();
                moveTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
                moveTree.useAutomaticThresholds = false;
                moveTree.AddChild(height ? MotionBuilder.BuildMotionLevel(anim, DynamicVariables.Settings.minMaxHeight) : anim, 0);
                moveTree.AddChild(height ? MotionBuilder.BuildMotionLevel(anim, -DynamicVariables.Settings.minMaxHeight) : anim, 1);
                if (!height) return moveTree;
                // ポーズ未選択時（AnimPosePlay=0）は高さをニュートラルに固定し、選択時だけ効かせます。
                return BuildPlayGate(guid, anim, moveTree);
            }

            var motionClipIdle0 = speed ? MotionBuilder.IdleAnimation(anim, 0f) : anim;
            var motionClipIdle1 = speed ? MotionBuilder.IdleAnimation(anim, DynamicVariables.Settings.motionNoiseScale) : anim;
            var motionClip00 = height ? MotionBuilder.BuildMotionLevel(motionClipIdle0, DynamicVariables.Settings.minMaxHeight) : motionClipIdle0;
            var motionClip01 = height ? MotionBuilder.BuildMotionLevel(motionClipIdle0, -DynamicVariables.Settings.minMaxHeight) : motionClipIdle0;
            var motionClip10 = height ? MotionBuilder.BuildMotionLevel(motionClipIdle1, DynamicVariables.Settings.minMaxHeight) : motionClipIdle1;
            var motionClip11 = height ? MotionBuilder.BuildMotionLevel(motionClipIdle1, -DynamicVariables.Settings.minMaxHeight) : motionClipIdle1;

            var blendTree = new BlendTree();
            blendTree.blendType = BlendTreeType.FreeformCartesian2D;
            blendTree.blendParameter = $"{ConstVariables.HeightParamPrefix}_{guid}";
            blendTree.blendParameterY = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            blendTree.AddChild(motionClip00, new Vector2(0f, 0f));
            blendTree.AddChild(motionClip01, new Vector2(1f, 0f));
            blendTree.AddChild(motionClip10, new Vector2(0f, 1f));
            blendTree.AddChild(motionClip11, new Vector2(1f, 1f));
            if (!height) return blendTree;

            // 未選択時は高さオフセットを 0 にし、速度だけを残したニュートラル側を用意します。
            var neutralIdle = new BlendTree();
            neutralIdle.blendType = BlendTreeType.Simple1D;
            neutralIdle.blendParameter = $"{ConstVariables.SpeedParamPrefix}_{guid}";
            neutralIdle.useAutomaticThresholds = false;
            neutralIdle.AddChild(motionClipIdle0, 0f);
            neutralIdle.AddChild(motionClipIdle1, 1f);
            return BuildPlayGate(guid, neutralIdle, blendTree);
        }

        // ポーズ選択中だけ onMotion を反映し、未選択時は offMotion（ニュートラル）に戻すゲート。
        static BlendTree BuildPlayGate(string guid, Motion offMotion, Motion onMotion)
        {
            var gate = new BlendTree();
            gate.blendType = BlendTreeType.Simple1D;
            gate.blendParameter = $"{ConstVariables.OnPlayParamPrefix}_{guid}";
            gate.useAutomaticThresholds = false;
            gate.AddChild(offMotion, 0f);
            gate.AddChild(onMotion, 1f);
            return gate;
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
