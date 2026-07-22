using System.Linq;
using com.hhotatea.avatar_pose_library.logic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace com.hhotatea.avatar_pose_library.tests
{
    public class MotionBuilderTests
    {
        [Test]
        public void ResetAnimation_UsesCurrentTransformValue()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            var source = new AnimationClip { name = "Source" };

            try
            {
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(1.25f, 2.5f, 3.75f);
                source.SetCurve(
                    "Child",
                    typeof(Transform),
                    "m_LocalPosition.x",
                    AnimationCurve.Constant(0f, 1f, 10f));

                var reset = MotionBuilder.ResetAnimation(root, new[] { source });
                var binding = EditorCurveBinding.FloatCurve(
                    "Child",
                    typeof(Transform),
                    "m_LocalPosition.x");
                var curve = AnimationUtility.GetEditorCurve(reset, binding);

                Assert.That(curve, Is.Not.Null);
                Assert.That(curve.Evaluate(0f), Is.EqualTo(1.25f));

                Object.DestroyImmediate(reset);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateFrameAnimation_PreservesDurationWithSyntheticBinding()
        {
            const float duration = 0.25f;
            var clip = MotionBuilder.CreateFrameAnimation(duration);

            try
            {
                var binding = AnimationUtility.GetCurveBindings(clip)
                    .First(item => item.path == "FakeAnimationKey");
                var curve = AnimationUtility.GetEditorCurve(clip, binding);

                Assert.That(binding.path, Is.EqualTo("FakeAnimationKey"));
                Assert.That(binding.type, Is.EqualTo(typeof(Transform)));
                Assert.That(curve.keys.Last().time, Is.EqualTo(duration));
                Assert.That(clip.length, Is.EqualTo(duration));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
        [Test]
        public void ResetAnimation_SkipsUnresolvableBindingType()
        {
            var root = new GameObject("Root");
            var source = new AnimationClip { name = "InvalidBinding" };

            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    "AvatarPoseLibrary: Skipping reset animation binding " +
                    "'InvalidBinding::UnityEngine.Behaviour:m_Enabled'. " +
                    "The current value could not be resolved.");
                source.SetCurve(
                    string.Empty,
                    typeof(Behaviour),
                    "m_Enabled",
                    AnimationCurve.Constant(0f, 1f, 1f));

                var reset = MotionBuilder.ResetAnimation(root, new[] { source });
                var hasInvalidBinding = AnimationUtility.GetCurveBindings(reset)
                    .Any(binding => binding.type == typeof(Behaviour) && binding.propertyName == "m_Enabled");

                Assert.That(hasInvalidBinding, Is.False);
                LogAssert.NoUnexpectedReceived();

                Object.DestroyImmediate(reset);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

    }
}
