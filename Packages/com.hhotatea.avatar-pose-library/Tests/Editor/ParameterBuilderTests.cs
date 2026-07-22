using System.Collections.Generic;
using System.Linq;
using com.hhotatea.avatar_pose_library.logic;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace com.hhotatea.avatar_pose_library.tests
{
    public class ParameterBuilderTests
    {
        [Test]
        public void BuildPoseParameter_CreatesExpectedRequiredAndOptionalParameters()
        {
            var audio = AudioClip.Create("Audio", 16, 1, 44100, false);
            var data = new AvatarPoseData
            {
                name = "Parameters",
                enableHeightParam = true,
                enableSpeedParam = false,
                enableMirrorParam = true,
                enablePoseSpace = true,
                categories = new List<PoseCategory>
                {
                    new PoseCategory
                    {
                        poses = new List<PoseEntry>
                        {
                            new PoseEntry { name = "Pose", audioClip = audio },
                        },
                    },
                },
            }.UpdateParameter();

            GameObject result = null;
            try
            {
                result = ParameterBuilder.BuildPoseParameter(data);
                var parameters = result.GetComponent<ModularAvatarParameters>().parameters;

                Assert.That(parameters.Count(item => item.nameOrPrefix == data.Parameters.Single()), Is.EqualTo(1));
                Assert.That(parameters.Any(item => item.nameOrPrefix == Name(ConstVariables.HeightParamPrefix, data)), Is.True);
                Assert.That(parameters.Any(item => item.nameOrPrefix == Name(ConstVariables.SpeedParamPrefix, data)), Is.False);
                Assert.That(parameters.Any(item => item.nameOrPrefix == Name(ConstVariables.MirrorParamPrefix, data)), Is.True);
                Assert.That(parameters.Any(item => item.nameOrPrefix == Name(ConstVariables.PoseSpaceParamPrefix, data)), Is.True);
                Assert.That(parameters.Any(item => item.nameOrPrefix == Name(ConstVariables.AudioParamPrefix, data)), Is.True);

                var height = parameters.Single(item => item.nameOrPrefix == Name(ConstVariables.HeightParamPrefix, data));
                Assert.That(height.syncType, Is.EqualTo(ParameterSyncType.Float));
                Assert.That(height.localOnly, Is.False);
                Assert.That(height.defaultValue, Is.EqualTo(0.5f));
                Assert.That(height.saved, Is.True);

                var mirror = parameters.Single(item => item.nameOrPrefix == Name(ConstVariables.MirrorParamPrefix, data));
                Assert.That(mirror.saved, Is.True);
            }
            finally
            {
                if (result != null)
                {
                    Object.DestroyImmediate(result);
                }

                Object.DestroyImmediate(audio);
            }
        }

        [Test]
        public void BuildPoseParameter_MarksBothFlagBytesAsSyncedFor256Poses()
        {
            var poses = Enumerable.Range(0, 256).Select(_ => new PoseEntry()).ToList();
            var data = new AvatarPoseData
            {
                categories = new List<PoseCategory> { new PoseCategory { poses = poses } },
            }.UpdateParameter();

            GameObject result = null;
            try
            {
                result = ParameterBuilder.BuildPoseParameter(data);
                var parameters = result.GetComponent<ModularAvatarParameters>().parameters;
                var lowByte = parameters.Single(item =>
                    item.nameOrPrefix == $"{ConstVariables.FlagParamPrefix}_{data.Guid}_0");
                var highByte = parameters.Single(item =>
                    item.nameOrPrefix == $"{ConstVariables.FlagParamPrefix}_{data.Guid}_1");

                Assert.That(lowByte.localOnly, Is.False);
                Assert.That(highByte.localOnly, Is.False);
            }
            finally
            {
                if (result != null)
                {
                    Object.DestroyImmediate(result);
                }
            }
        }

        [Test]
        public void BuildLocomotionAnimator_ClassifiesMovementBeforeFilteringFxCurves()
        {
            var clip = new AnimationClip { name = "FxOnlyMotion" };
            clip.SetCurve(
                "Body",
                typeof(SkinnedMeshRenderer),
                "blendShape.Probe",
                AnimationCurve.Linear(0f, 0f, 1f, 100f));
            var data = new AvatarPoseData
            {
                enableHeightParam = false,
                enableSpeedParam = false,
                enableMirrorParam = false,
                categories = new List<PoseCategory>
                {
                    new PoseCategory
                    {
                        poses = new List<PoseEntry>
                        {
                            new PoseEntry { animationClip = clip },
                        },
                    },
                },
            }.UpdateParameter();

            AnimatorController controller = null;
            BlendTree blendTree = null;
            try
            {
                Assert.That(MotionBuilder.IsMoveAnimation(clip), Is.True);

                controller = AnimatorBuilder.BuildLocomotionAnimator(data, false);
                var poseState = controller.layers
                    .SelectMany(layer => layer.stateMachine.states)
                    .Select(child => child.state)
                    .Single(state => state.name == "Pose_1");
                blendTree = (BlendTree)poseState.motion;

                Assert.That(blendTree.blendType, Is.EqualTo(BlendTreeType.Simple1D));
                Assert.That(blendTree.blendParameter,
                    Is.EqualTo($"{ConstVariables.HeightParamPrefix}_{data.Guid}"));
                Assert.That(blendTree.children, Has.Length.EqualTo(2));
            }
            finally
            {
                if (blendTree != null)
                {
                    var generatedMotions = blendTree.children
                        .Select(child => child.motion)
                        .Where(motion => motion != null)
                        .Distinct()
                        .ToArray();
                    foreach (var motion in generatedMotions)
                    {
                        Object.DestroyImmediate(motion);
                    }

                    Object.DestroyImmediate(blendTree);
                }

                if (controller != null)
                {
                    Object.DestroyImmediate(controller);
                }

                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void BuildFxAnimator_AttachesInitializedParameterDrivers()
        {
            var data = new AvatarPoseData
            {
                categories = new List<PoseCategory>
                {
                    new PoseCategory
                    {
                        poses = new List<PoseEntry> { new PoseEntry() },
                    },
                },
            }.UpdateParameter();

            AnimatorController controller = null;
            try
            {
                controller = AnimatorBuilder.BuildFxAnimator(data, false);
                var drivers = controller.layers
                    .SelectMany(layer => layer.stateMachine.states)
                    .SelectMany(child => child.state.behaviours)
                    .OfType<VRCAvatarParameterDriver>()
                    .ToArray();

                Assert.That(drivers, Is.Not.Empty);
                Assert.That(drivers.All(driver => driver.parameters != null), Is.True);
            }
            finally
            {
                if (controller != null)
                {
                    Object.DestroyImmediate(controller);
                }
            }
        }

        private static string Name(string prefix, AvatarPoseData data)
        {
            return $"{prefix}_{data.Guid}";
        }
    }
}
