using System.Collections.Generic;
using System.Linq;
using com.hhotatea.avatar_pose_library.model;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.hhotatea.avatar_pose_library.tests
{
    public class AvatarPoseDataTests
    {
        [Test]
        public void UpdateParameter_SplitsParametersAtTheByteBoundary()
        {
            var poses = Enumerable.Range(1, 256)
                .Select(index => new PoseEntry { name = $"Pose {index}" })
                .ToList();
            var data = CreateData("Boundary", poses.ToArray());

            var result = data.UpdateParameter();

            Assert.That(result, Is.SameAs(data));
            Assert.That(data.Guid, Has.Length.EqualTo(ConstVariables.HashLong));
            AssertPose(poses[0], $"AnimPose_{data.Guid}_1", 1, 1, 1, 0);
            AssertPose(poses[254], $"AnimPose_{data.Guid}_1", 255, 255, 255, 0);
            AssertPose(poses[255], $"AnimPose_{data.Guid}_256", 1, 256, 0, 1);
            Assert.That(data.Parameters, Is.EquivalentTo(new[]
            {
                $"AnimPose_{data.Guid}_1",
                $"AnimPose_{data.Guid}_256",
            }));
        }

        [Test]
        public void DerivedValues_HandleNullEntriesAndDeduplicateParameters()
        {
            var audio = AudioClip.Create("Audio", 16, 1, 44100, false);
            try
            {
                var first = new PoseEntry { Parameter = "Shared" };
                var second = new PoseEntry { Parameter = "Shared", audioClip = audio };
                var data = new AvatarPoseData
                {
                    categories = new List<PoseCategory>
                    {
                        null,
                        new PoseCategory { poses = null },
                        new PoseCategory { poses = new List<PoseEntry> { null, first, second } },
                    },
                };

                Assert.That(data.PoseCount, Is.EqualTo(3));
                Assert.That(data.Parameters, Is.EqualTo(new[] { "Shared" }));
                Assert.That(data.EnableAudioMode, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(audio);
            }
        }

        [Test]
        public void Combine_MergesUntargetedLibrariesAndKeepsTargetedLibrariesSeparate()
        {
            var first = CreateData("Shared", new PoseEntry { name = "First" });
            first.enableHeightParam = false;
            var second = CreateData("Shared", new PoseEntry { name = "Second" });
            second.enableHeightParam = true;
            var targetMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var targeted = CreateData("Shared", new PoseEntry { name = "Targeted" });
            targeted.target = targetMenu;

            try
            {
                var result = AvatarPoseData.Combine(new[] { first, null, second, targeted });

                Assert.That(result, Has.Count.EqualTo(2));
                var merged = result.Single(item => item.target == null);
                Assert.That(merged.categories.SelectMany(category => category.poses).Select(pose => pose.name),
                    Is.EqualTo(new[] { "First", "Second" }));
                Assert.That(merged.enableHeightParam, Is.False,
                    "Settings should be copied from the first matching library.");
                Assert.That(result.Single(item => item.target != null), Is.SameAs(targeted));
                Assert.That(result.All(item => !string.IsNullOrEmpty(item.Guid)), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(targetMenu);
            }
        }

        [Test]
        public void ToHash_IsStableAndChangesWhenSerializedDataChanges()
        {
            var data = CreateData("Hash", new PoseEntry { name = "Pose" });

            var initial = data.ToHash();
            var repeated = data.ToHash();
            data.categories[0].poses[0].name = "Changed";
            var changed = data.ToHash();

            Assert.That(repeated, Is.EqualTo(initial));
            Assert.That(changed, Is.Not.EqualTo(initial));
            Assert.That(initial, Has.Length.EqualTo(ConstVariables.HashLong));
        }

        private static AvatarPoseData CreateData(string name, params PoseEntry[] poses)
        {
            return new AvatarPoseData
            {
                name = name,
                categories = new List<PoseCategory>
                {
                    new PoseCategory { name = "Category", poses = poses.ToList() },
                },
            };
        }

        private static void AssertPose(
            PoseEntry pose, string parameter, int value, int index, int lowFlag, int highFlag)
        {
            Assert.That(pose.Parameter, Is.EqualTo(parameter));
            Assert.That(pose.Value, Is.EqualTo(value));
            Assert.That(pose.Index, Is.EqualTo(index));
            Assert.That(pose.GetAnimatorFlag(), Is.EqualTo(new[] { lowFlag, highFlag }));
        }
    }
}
