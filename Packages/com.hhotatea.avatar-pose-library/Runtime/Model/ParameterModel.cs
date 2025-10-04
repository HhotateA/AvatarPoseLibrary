using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Security.Cryptography;
using System.Text;

namespace com.hhotatea.avatar_pose_library.model {
    [Serializable]
    public class TrackingSetting {
        public bool head = true;
        public bool arm = true;
        public bool foot = true;
        public bool finger = true;
        public bool locomotion = true;
        public bool fx = true;
        public float motionSpeed = 1f;
        public bool loop = true;
    }

    [Serializable]
    public class PoseEntry {
        public string name;
        public bool autoThumbnail;
        public Texture2D thumbnail;
        public AnimationClip beforeAnimationClip;
        public AnimationClip afterAnimationClip;
        public AnimationClip animationClip;
        public VRCExpressionsMenu target = null;

        // 固定するパラメーターの選択
        public TrackingSetting tracking;

        // システムが使用
        public string Parameter { get; set; }
        public int Value { get; set; }
        public int Index { get; set; }

        public int[] GetAnimatorFlag () {
            return new [] {
                Index & 0xFF, // 0‒7 ビット目
                    (Index >> 8) & 0xFF, // 8‒15 ビット目
                    /*(Index >> 16) & 0xFF,  // 16‒23 ビット目
                    (Index >> 24) & 0xFF   // 24‒31 ビット目*/
            };
        }
    }

    [Serializable]
    public class PoseCategory {
        public string name;
        public Texture2D thumbnail;
        public List<PoseEntry> poses = new List<PoseEntry> ();
        public VRCExpressionsMenu target = null;
    }

    [Serializable]
    public class AvatarPoseData {
        public string name = "";
        public Texture2D thumbnail;
        public List<PoseCategory> categories = new List<PoseCategory> ();
        public bool enableHeightParam = true;
        public bool enableSpeedParam = true;
        public bool enableMirrorParam = true;
        public bool enableTrackingParam = true;
        public bool enableFxParam = true;
        public bool enableDeepSync = true;
        public bool enablePoseSpace = false;
        public bool enableUseCache = false;
        public VRCExpressionsMenu target = null;
        public VRCExpressionsMenu settings = null;
        public WriteDefaultType writeDefaultType = WriteDefaultType.MatchAvatar;

        // システムが使用
        public string Guid { get; set; }
        public List<string> Parameters =>
            categories.SelectMany (c => {
                return c.poses.Select (p => p.Parameter);
            }).Distinct ().ToList ();

        public int PoseCount => categories.Sum (cat => cat.poses.Count);

        /// <summary>
        /// パラメーターの最適化
        /// 0906コミットにより、決定論的に動作
        /// </summary>
        public AvatarPoseData UpdateParameter (){
            Guid = ToHash();
            int paramCount = 999;
            int paramIndex = 1;
            string paramName = "";
            foreach (var category in categories) {
                foreach (var pose in category.poses) {
                    if (paramCount > ConstVariables.MaxAnimationState) {
                        paramName = $"AnimPose_{Guid}_from{paramIndex}";
                        paramCount = 1;
                    }

                    pose.Parameter = paramName;
                    pose.Value = paramCount;
                    pose.Index = paramIndex;
                    paramCount++;
                    paramIndex++;
                }
            }
            return this;
        }

        /// <summary>
        /// 複数のデータを統合するスタティックメソッド
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<AvatarPoseData> Combine (AvatarPoseData[] data) {
            var result = new List<AvatarPoseData> ();
            var ps = data.Select (d => d.name).Distinct ().ToArray ();
            foreach (var t in ps) {
                var apd = new AvatarPoseData ();
                apd.name = t;
                foreach (var d in data) {
                    if (d.name != apd.name) continue;
                    if (d.target != null) continue;
                    apd.categories.AddRange (d.categories);
                    apd.thumbnail = d.thumbnail;

                    // とりあえず、値を代入し続ける。（Index最下位の者が採用）
                    apd.enableHeightParam = d.enableHeightParam;
                    apd.enableSpeedParam = d.enableSpeedParam;
                    apd.enableMirrorParam = d.enableMirrorParam;
                    apd.enableTrackingParam = d.enableTrackingParam;
                    apd.enableFxParam = d.enableFxParam;
                    apd.enableDeepSync = d.enableDeepSync;
                    apd.enablePoseSpace = d.enablePoseSpace;
                    apd.writeDefaultType = d.writeDefaultType;
                    apd.enableUseCache = d.enableUseCache;
                }

                if (apd.categories.Count > 0) {
                    apd.UpdateParameter ();
                    result.Add (apd);
                }
            }

            foreach (var apd in data) {
                if (apd.target == null) continue;
                apd.UpdateParameter ();
                result.Add (apd);
            }

            return result;
        }

        public string ToHash()
        {
            string json;
#if UNITY_EDITOR
            json = UnityEditor.EditorJsonUtility.ToJson(this, false);
#else
            json = JsonUtility.ToJson(this, false);
#endif
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json ?? ""));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.AppendFormat("{0:x2}", b);
            return sb.ToString ().Substring (0, ConstVariables.HashLong);
        }
    }

    public enum WriteDefaultType {
        MatchAvatar,
        OverrideTrue,
        OverrideFalse
    }
}