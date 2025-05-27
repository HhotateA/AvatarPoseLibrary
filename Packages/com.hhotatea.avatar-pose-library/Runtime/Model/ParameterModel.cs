using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.hhotatea.avatar_pose_library.model
{
    [Serializable]
    public class TrackingSetting
    {
        public bool head = true;
        public bool arm = true;
        public bool foot = true;
        public bool finger = true;
        public bool locomotion = true;
        public float motionSpeed = 1f;
        public bool loop = true;
    }
    
    [Serializable]
    public class PoseEntry
    {
        public string name;
        public bool autoThumbnail;
        public Texture2D thumbnail;
        public AnimationClip beforeAnimationClip;
        public AnimationClip afterAnimationClip;
        public AnimationClip animationClip;
        public VRCExpressionsMenu target = null;
        
        // 固定するパラメーターの選択
        public TrackingSetting tracking;
        
        // UI用のキャッシュ
        public bool foldout = false;

        // システムが使用
        public string Parameter { get; set; }
        public int Value { get; set; }
        public int Index { get; set; }

        public int[] GetAnimatorFlag()
        {
            return new[]
            {
                Index        & 0xFF,   // 0‒7 ビット目
                (Index >>  8) & 0xFF,  // 8‒15 ビット目
                /*(Index >> 16) & 0xFF,  // 16‒23 ビット目
                (Index >> 24) & 0xFF   // 24‒31 ビット目*/
            };
        }
    }

    [Serializable]
    public class PoseCategory
    {
        public string name;
        public Texture2D thumbnail;
        public List<PoseEntry> poses = new List<PoseEntry>();
        public VRCExpressionsMenu target = null;
    }

    [Serializable]
    public class AvatarPoseData
    {
        public string name = "";
        public Texture2D thumbnail;
        public List<PoseCategory> categories = new List<PoseCategory>();
        public bool enableHeightParam = true;
        public bool enableSpeedParam = true;
        public bool enableMirrorParam = true;
        public bool enableTrackingParam = true;
        public bool enableFxParam = false;
        public bool enableDeepSync = true;
        public VRCExpressionsMenu target = null;
        
        // システムが使用
        public string Guid { get; set; }
        public List<string> Parameters => 
            categories.SelectMany(c =>
            {
                return c.poses.Select(p => p.Parameter);
            }).Distinct().ToList();

        public int PoseCount => categories.Sum(cat => cat.poses.Count);
        
        /// <summary>
        /// パラメーターの最適化
        /// </summary>
        public AvatarPoseData UpdateParameter()
        {
            int paramCount = 999;
            int paramIndex = 1;
            string paramName = "";
            foreach (var category in categories)
            {
                foreach (var pose in category.poses)
                {
                    var guid = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                    if (paramCount > ConstVariables.MaxAnimationState)
                    {
                        paramName = $"AnimPose_{guid}";
                        paramCount = 1;
                    }

                    pose.Parameter = paramName;
                    pose.Value = paramCount;
                    pose.Index = paramIndex;
                    paramCount++;
                    paramIndex++;
                }
            }
            Guid = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            return this;
        }

        /// <summary>
        /// 複数のデータを統合するスタティックメソッド
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<AvatarPoseData> Combine(AvatarPoseData[] data)
        {
            var result = new List<AvatarPoseData>();
            var ps = data.Select(d => d.name).Distinct().ToArray();
            foreach (var t in ps)
            {
                var apd = new AvatarPoseData();
                apd.name = t;
                foreach (var d in data)
                {
                    if(d.name != apd.name) continue;
                    if(d.target != null) continue;
                    apd.categories.AddRange(d.categories);
                    apd.thumbnail = d.thumbnail;
                }

                apd.enableHeightParam = data.Any(e => e.enableHeightParam);
                apd.enableSpeedParam = data.Any(e => e.enableSpeedParam);
                apd.enableMirrorParam = data.Any(e => e.enableMirrorParam);
                apd.enableTrackingParam = data.Any(e => e.enableTrackingParam);
                apd.enableFxParam = data.Any(e => e.enableFxParam);
                apd.enableDeepSync = data.Any(e => e.enableDeepSync);

                if (apd.categories.Count > 0)
                {
                    apd.UpdateParameter();
                    result.Add(apd);
                }
            }

            foreach (var apd in data)
            {
                if(apd.target == null) continue;
                apd.UpdateParameter();
                result.Add(apd);
            }
            
            return result;
        }
    }
}