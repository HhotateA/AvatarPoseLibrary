using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using com.hhotatea.avatar_pose_library.editor;
using UnityEngine;
using UnityEditor;
using AnimationClip = UnityEngine.AnimationClip;
using Object = UnityEngine.Object;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class MotionBuilder
    {
        readonly struct CurveKey : IEquatable<CurveKey>
        {
            public readonly string Path;
            public readonly Type Type;
            public readonly string PropertyName;

            public CurveKey(string path, Type type, string propertyName)
            {
                Path = path ?? string.Empty;
                Type = type;
                PropertyName = propertyName ?? string.Empty;
            }

            public bool Equals(CurveKey other)
            {
                return Path == other.Path && Type == other.Type && PropertyName == other.PropertyName;
            }

            public override bool Equals(object obj)
            {
                return obj is CurveKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + (Path != null ? Path.GetHashCode() : 0);
                    hash = hash * 31 + (Type != null ? Type.GetHashCode() : 0);
                    hash = hash * 31 + (PropertyName != null ? PropertyName.GetHashCode() : 0);
                    return hash;
                }
            }
        }

        /// <summary>
        /// アニメーションの脚の高さを変える
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static AnimationClip BuildMotionLevel(AnimationClip anim,float level)
        {
            if (anim == null) return null;
            
            var curves = AnimationUtility.GetCurveBindings(anim);
            foreach (var binding in curves)
            {
                var c = AnimationUtility.GetEditorCurve(anim, binding);
                if (binding.propertyName == "MotionT.y")
                {
                    return BuildMotionLevel_Motion(anim, level);
                }
            }
            return BuildMotionLevel_Root(anim, level);
        }

        /// <summary>
        /// MotionTを制御されている場合はLevelの修正が効かないので、こちらで上書き
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        static AnimationClip BuildMotionLevel_Motion(AnimationClip anim, float level)
        {
            var result = new AnimationClip();
            result.name = $"{anim.name}_levelM{level}";
            result.wrapMode = anim.wrapMode;
            
            var curves = AnimationUtility.GetCurveBindings(anim);
            foreach (var binding in curves)
            {
                var curve = AnimationUtility.GetEditorCurve(anim, binding);
                if (binding.propertyName == "MotionT.y")
                {
                    if(curve.keys.Length == 0) continue;
                    var c = new AnimationCurve();
                    foreach (var key in curve.keys)
                    {
                        c.AddKey(key.time, key.value + level);
                    }
                    result.SetCurve(binding.path, binding.type, binding.propertyName, c);
                    continue;
                }
                result.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
            
            var settings = AnimationUtility.GetAnimationClipSettings(anim);
            AnimationUtility.SetAnimationClipSettings(result, settings);
            
            return result;
        }
        
        /// <summary>
        /// AnimationのLevel設定を変更
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        static AnimationClip BuildMotionLevel_Root(AnimationClip anim, float level)
        {
            var result = Object.Instantiate(anim);
            result.name = $"{anim.name}_levelR{level}";
            result.wrapMode = anim.wrapMode;
            
            var settings = AnimationUtility.GetAnimationClipSettings(anim);
            settings.level += level;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            
            return result;
        }
        
        /// <summary>
        /// 動きのあるアニメーションか返す
        /// </summary>
        /// <param name="anim"></param>
        /// <returns></returns>
        public static bool IsMoveAnimation(AnimationClip anim)
        {
            if (anim == null) return false;
            
            // 既存のアニメーションの検証
            var curves = AnimationUtility.GetCurveBindings(anim);
            foreach (var binding in curves)
            {
                var c = AnimationUtility.GetEditorCurve(anim, binding);
                if (c.keys.Length == 0) continue;
                if (c.keys.Any(
                        k => Math.Abs(k.value - c.keys[0].value) > 0.01f))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLoopAnimation(AnimationClip anim)
        {
            if (anim == null) return false;
            
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(anim);
            return settings.loopTime;
        }
        
        public static AnimationClip SetAnimationLoop(AnimationClip anim, bool loop)
        {
            if (anim == null) return null;
            
            var result = Object.Instantiate(anim);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = loop;
            result.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            return result;
        }

        public static AnimationClip IdleAnimation(AnimationClip anim,float noiseScale)
        {
            if (anim == null) return null;
            
            // 既存のアニメーションの検証
            var curves = AnimationUtility.GetCurveBindings(anim);

            var result = new AnimationClip();
            result.name = $"{anim.name}_move";
            
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(anim);
            settings.loopTime = true;
            result.wrapMode = WrapMode.Loop;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            
            float seed = 0f;
            foreach (var binding in curves)
            {
                seed++;
                var oldCurve = AnimationUtility.GetEditorCurve(anim, binding);
                var newCurve = new AnimationCurve();
                if(oldCurve.keys.Length == 0) continue;
                newCurve.AddKey(0f, oldCurve[0].value);
                newCurve.AddKey(DynamicVariables.Settings.motionLong, oldCurve[0].value);
                if (HumanTrait.MuscleName.Contains(binding.propertyName))
                {
                    float t = DynamicVariables.Settings.motionDuration;
                    while (t < DynamicVariables.Settings.motionLong)
                    {
                        newCurve.AddKey(t, oldCurve[0].value +
                                           (Mathf.PerlinNoise(t * 0.1f, seed * 10f) - 0.5f)
                                           * noiseScale);
                        t += DynamicVariables.Settings.motionDuration;
                    }
                }
                result.SetCurve(binding.path, binding.type, binding.propertyName, newCurve);
            }
            
            return result;
        }

        public static AnimationClip SafeAnimation(AnimationClip anim, AnimationClip before, AnimationClip after)
        {
            if (anim != null)
            {
                return anim;
            }

            if (before != null)
            {
                return SplitAnimation(before, 1f);
            }

            if (after != null)
            {
                return SplitAnimation(after, 0f);
            }
            
            return NoneAnimation ;
        }

        static AnimationClip SplitAnimation(AnimationClip anim,float time)
        {
            if (anim == null)
            {
                return NoneAnimation ;
            }
            
            // 既存のアニメーションの検証
            var curves = AnimationUtility.GetCurveBindings(anim);

            var result = new AnimationClip();
            result.name = $"{anim.name}_{time.ToString()}";
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(anim);
            AnimationUtility.SetAnimationClipSettings(result, settings);
            
            foreach (var binding in curves)
            {
                var curve = AnimationUtility.GetEditorCurve(anim, binding);
                float minValue = curve.keys.OrderByDescending(k => k.time).Last().value;
                float maxValue = curve.keys.OrderByDescending(k => k.time).First().value;
                
                
                var c = new AnimationCurve();
                c.AddKey(0f, Mathf.Lerp(minValue,maxValue,time));
                result.SetCurve(binding.path, binding.type, binding.propertyName, c);
            }

            return result;
        }


        public enum AnimationPart
        {
            Locomotion,
            Fx,
            None
        }
        public static AnimationClip PartAnimation(AnimationClip anim, AnimationPart part)
        {
            if (anim == null)
            {
                return NoneAnimation;
            }
            
            // 既存のアニメーションの検証
            var curves = AnimationUtility.GetCurveBindings(anim);

            var result = new AnimationClip();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(anim);
            result.name = $"{anim.name}_{part.ToString()}";
            AnimationUtility.SetAnimationClipSettings(result, settings);

            float animTime = 0f;
            
            foreach (var binding in curves)
            {
                var curve = AnimationUtility.GetEditorCurve(anim, binding);
                bool isLocomotionAnimation = binding.type == typeof(Animator);
                if (isLocomotionAnimation && part == AnimationPart.Locomotion)
                {
                    result.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                }
                else if(!isLocomotionAnimation && part == AnimationPart.Fx)
                {
                    result.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                }

                foreach (var c in curve.keys)
                {
                    animTime = Mathf.Max(animTime, c.time);
                }
            }
            
            // アニメーションの長さを一定にするために、空プロパティを入れておく
            {
                var curve = new AnimationCurve();
                curve.AddKey(0f, 0f);
                curve.AddKey(animTime, 0f);
                // あり得ないアニメーションを1フレームだけ入れておく。
                result.SetCurve("FakeAnimationKey", 
                    typeof(Transform), "localPosition.x", curve);
            }
            
            return result;
        }

        static AnimationClip _frameAnimation;

        public static AnimationClip FrameAnimation
        {
            get
            {
                if (_frameAnimation == null)
                {
                    _frameAnimation = CreateFrameAnimation(1f / 60f);
                }

                return _frameAnimation;
            }
        }

        static AnimationClip _noneAnimation;

        public static AnimationClip NoneAnimation
        {
            get
            {
                if (_noneAnimation == null)
                {
                    _noneAnimation = new AnimationClip();
                }

                return _noneAnimation;
            }
        }

        public static AnimationClip CreateFrameAnimation(float interval)
        {
            var result = new AnimationClip();
            result.name = ($"None_{interval:C}Sec");

            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(interval, 0f);
            // あり得ないアニメーションを1フレームだけ入れておく。
            result.SetCurve("FakeAnimationKey", 
                typeof(Transform), "localPosition.x", curve);
            
            return result;
        }

        static float GetDefaultValue(GameObject root, string path, Type type, string propertyName)
        {
            if (root == null) return 0f;
            if (type == null) return 0f;
            if (string.IsNullOrEmpty(propertyName)) return 0f;

            var binding = EditorCurveBinding.FloatCurve(path, type, propertyName);
            if (UnityEditor.AnimationUtility.GetFloatValue(root, binding, out var v))
            {
                return v;
            }

            // Fallback (covers a few common cases when GetFloatValue fails)
            var targetTransform = string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);
            if (targetTransform == null) return 0f;

            if (type == typeof(Transform))
            {
                switch (propertyName)
                {
                    case "m_LocalPosition.x": return targetTransform.localPosition.x;
                    case "m_LocalPosition.y": return targetTransform.localPosition.y;
                    case "m_LocalPosition.z": return targetTransform.localPosition.z;

                    case "m_LocalRotation.x": return targetTransform.localRotation.x;
                    case "m_LocalRotation.y": return targetTransform.localRotation.y;
                    case "m_LocalRotation.z": return targetTransform.localRotation.z;
                    case "m_LocalRotation.w": return targetTransform.localRotation.w;

                    case "m_LocalScale.x": return targetTransform.localScale.x;
                    case "m_LocalScale.y": return targetTransform.localScale.y;
                    case "m_LocalScale.z": return targetTransform.localScale.z;
                }
            }

            // BlendShape curve name pattern: "blendShape.<BlendShapeName>"
            if (type == typeof(SkinnedMeshRenderer) && propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
            {
                var renderer = targetTransform.GetComponent<SkinnedMeshRenderer>();
                var mesh = renderer != null ? renderer.sharedMesh : null;
                if (renderer != null && mesh != null)
                {
                    var blendShapeName = propertyName.Substring("blendShape.".Length);
                    var index = mesh.GetBlendShapeIndex(blendShapeName);
                    if (index >= 0) return renderer.GetBlendShapeWeight(index);
                }
            }

            return 0f;
        }

        public static AnimationClip ResetAnimation(GameObject root, AnimationClip[] anims)
        {
            var result = CreateFrameAnimation(1f / 60f);
            result.name = "ResetAnimation";
            if (root == null || anims == null) return result;

            var defaultValueCache = new Dictionary<CurveKey, float>();

            foreach (var anim in anims)
            {
                if (anim == null) continue;
                var curves = UnityEditor.AnimationUtility.GetCurveBindings(anim);
                foreach (var binding in curves)
                {
                    bool isLocomotionAnimation = binding.type == typeof(Animator);
                    if(isLocomotionAnimation)
                    {
                        continue;
                    }

                    // rootにおける現在のValueを取得
                    var key = new CurveKey(binding.path, binding.type, binding.propertyName);
                    if (!defaultValueCache.TryGetValue(key, out var v))
                    {
                        v = GetDefaultValue(root, binding.path, binding.type, binding.propertyName);
                        defaultValueCache[key] = v;
                    }

                    var c = new AnimationCurve();
                    c.AddKey(0f, v);
                    c.AddKey(1f/60f, v);
                    result.SetCurve(binding.path, binding.type, binding.propertyName, c);
                }
            }
            return result;
        }
    }
}
