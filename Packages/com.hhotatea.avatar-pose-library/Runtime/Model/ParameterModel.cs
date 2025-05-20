using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        public AnimationClip animationClip;
        
        // 固定するパラメーターの選択
        public TrackingSetting tracking;
        
        // UI用のキャッシュ
        public bool foldout = false;

        // システムが使用
        public string Parameter { get; set; }
        public int Value { get; set; }
        public int Index { get; set; }

        public bool[] GetAnimatorFlag()
        {
            bool[] bits = new bool[32];
            for (int i = 0; i < 32; i++)
            {
                bits[i] = (Index & (1 << i)) != 0;
            }
            return bits;
        }
    }

    [Serializable]
    public class PoseCategory
    {
        public string name;
        public Texture2D thumbnail;
        public List<PoseEntry> poses = new List<PoseEntry>();
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
        public static AvatarPoseData[] Combine(AvatarPoseData[] data)
        {
            var ps = data.Select(d => d.name).Distinct().ToArray();
            var result = new AvatarPoseData[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var apd = new AvatarPoseData();
                apd.name = ps[i];
                foreach (var d in data)
                {
                    if(d.name != apd.name) continue;
                    apd.categories.AddRange(d.categories);
                    apd.thumbnail = d.thumbnail;

                    apd.enableHeightParam = d.enableHeightParam;
                    apd.enableSpeedParam = d.enableSpeedParam;
                    apd.enableMirrorParam = d.enableMirrorParam;
                    apd.enableTrackingParam = d.enableTrackingParam;
                }
                apd.UpdateParameter();
                result[i] = apd;
            }
            return result;
        }
    }
}