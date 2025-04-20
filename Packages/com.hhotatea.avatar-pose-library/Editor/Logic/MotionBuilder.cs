using System;
using System.Linq;
using com.hhotatea.avatar_pose_library.editor;
using UnityEngine;
using UnityEditor;
using AnimationClip = UnityEngine.AnimationClip;
using Object = UnityEngine.Object;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class MotionBuilder
    {
        public static AnimationClip BuildMotionLevel(AnimationClip anim,float level)
        {
            if (!anim) return null;
            
            var settings = AnimationUtility.GetAnimationClipSettings(anim);
            var result = Object.Instantiate(anim);
            result.name = $"{anim.name}_level{level}";
            //result.wrapMode = anim.wrapMode;
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
            if (!anim) return false;
            
            // 既存のアニメーションの検証
            var curves = AnimationUtility.GetCurveBindings(anim);
            foreach (var binding in curves)
            {
                var c = AnimationUtility.GetEditorCurve(anim, binding);
                if(c.keys.Length == 0) continue;
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
            if (!anim) return false;
            
            var result = Object.Instantiate(anim);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            return settings.loopTime;
        }
        
        public static AnimationClip SetAnimationLoop(AnimationClip anim, bool loop)
        {
            if (!anim) return null;
            
            var result = Object.Instantiate(anim);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopTime = loop;
            result.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;
            AnimationUtility.SetAnimationClipSettings(result, settings);
            return result;
        }

        public static AnimationClip IdleAnimation(AnimationClip anim,float noiseScale)
        {
            if (!anim) return null;
            
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
                                           FBMNoise(t * 0.1f,seed,5)
                                           * noiseScale);
                        t += DynamicVariables.Settings.motionDuration;
                    }
                }
                result.SetCurve(binding.path, binding.type, binding.propertyName, newCurve);
            }
            
            return result;
        }

        private static float FBMNoise(float time,float seed,int loop)
        {
            float sum = 0f;
            float count = 0f;
            int i = 1;
            int j = loop;
            while (j > 0)
            {
                sum += PerlinNoise(time * i*i, seed) * j*j;
                count += j*j;
                i++;
                j--;
            }

            return sum / count;
        }
        
        private static float PerlinNoise(float time,float seed)
        {
            return Mathf.PerlinNoise(time, seed * 10f) - 0.5f;
        }

        public static AnimationClip NoneAnimation()
        {
            var result = new AnimationClip();
            result.name = "None";

            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(1f/60f, 0f);
            // あり得ないアニメーションを1フレームだけ入れておく。
            result.SetCurve(Guid.NewGuid().ToString("N").Substring(0, 8), 
                typeof(Transform), "localPosition.x", curve);
            
            return result;
        }
    }
}