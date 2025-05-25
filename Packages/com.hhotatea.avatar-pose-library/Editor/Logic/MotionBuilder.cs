using System;
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
            
            return DynamicVariables.Settings.defaultAnimation;
        }

        static AnimationClip SplitAnimation(AnimationClip anim,float time)
        {
            if (anim == null)
            {
                return DynamicVariables.Settings.defaultAnimation;
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
                return DynamicVariables.Settings.defaultAnimation;
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
    }
}