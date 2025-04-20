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
        public bool customSetting = false; 
    }
    
    
    [Serializable]
    public class PoseEntry
    {
        public string name;
        public bool autoThumbnail;
        public Texture2D thumbnail;
        public AnimationClip animationClip;
        public float motionSpeed;
        
        // 固定するパラメーターの選択
        public TrackingSetting tracking;
        
        public string parameter { get; set; }//プロパティ
        
        public int value { get; set; }//プロパティ
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
        public string guid { get; set; }//プロパティ
        public List<string> Parameters => 
            categories.SelectMany(c =>
            {
                return c.poses.Select(p => p.parameter);
            }).Distinct().ToList();
        
        /// <summary>
        /// パラメーターの最適化
        /// </summary>
        void UpdateParameter()
        {
            int paramCount = 999;
            string paramName = "";
            foreach (var category in categories)
            {
                foreach (var pose in category.poses)
                {
                    var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
                    if (paramCount > ConstVariables.MaxAnimationState)
                    {
                        paramName = $"AnimPose_{guid}";
                        paramCount = 1;
                    }

                    paramCount++;
                    pose.parameter = paramName;
                    pose.value = paramCount;
                }
            }
            guid = Guid.NewGuid().ToString("N").Substring(0, 8);
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
                }
                apd.UpdateParameter();
                result[i] = apd;
            }
            return result;
        }
    }
}