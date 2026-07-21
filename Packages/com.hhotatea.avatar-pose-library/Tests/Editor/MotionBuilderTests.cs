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
                    "Unity could not resolve the binding value type. " +
                    "No supported fallback value was found.");
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

        [Test]
        public void ResetAnimation_UsesCurrentSerializedComponentValues()
        {
            var root = new GameObject("Root");
            var probe = root.AddComponent<ResetAnimationProbe>();
            var source = new AnimationClip { name = "SerializedValues" };

            try
            {
                probe.floatValue = 1.5f;
                probe.intValue = 4;
                probe.boolValue = true;
                source.SetCurve(
                    string.Empty,
                    typeof(ResetAnimationProbe),
                    nameof(ResetAnimationProbe.floatValue),
                    AnimationCurve.Constant(0f, 1f, 10f));
                source.SetCurve(
                    string.Empty,
                    typeof(ResetAnimationProbe),
                    nameof(ResetAnimationProbe.intValue),
                    AnimationCurve.Constant(0f, 1f, 10f));
                source.SetCurve(
                    string.Empty,
                    typeof(ResetAnimationProbe),
                    nameof(ResetAnimationProbe.boolValue),
                    AnimationCurve.Constant(0f, 1f, 0f));

                var reset = MotionBuilder.ResetAnimation(root, new[] { source });

                Assert.That(GetResetValue(reset, nameof(ResetAnimationProbe.floatValue)), Is.EqualTo(1.5f));
                Assert.That(GetResetValue(reset, nameof(ResetAnimationProbe.intValue)), Is.EqualTo(4f));
                Assert.That(GetResetValue(reset, nameof(ResetAnimationProbe.boolValue)), Is.EqualTo(1f));

                Object.DestroyImmediate(reset);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(root);
            }
        }

        private static float GetResetValue(AnimationClip clip, string propertyName)
        {
            var binding = EditorCurveBinding.FloatCurve(
                string.Empty,
                typeof(ResetAnimationProbe),
                propertyName);
            return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(0f);
        }
    }

    public sealed class ResetAnimationProbe : MonoBehaviour
    {
        public float floatValue;
        public int intValue;
        public bool boolValue;
    }
}
