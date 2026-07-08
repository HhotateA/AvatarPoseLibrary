using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        public bool fx = true;
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
        public AudioClip audioClip;
        public VRCExpressionsMenu target = null;

        // 固定するパラメーターの選択
        public TrackingSetting tracking;

        // システムが使用
        public string Parameter { get; set; }
        public int Value { get; set; }
        public int Index { get; set; }

        public int[] GetAnimatorFlag()
        {
            return new[] {
                Index & 0xFF, // 0‒7 ビット目
                    (Index >> 8) & 0xFF, // 8‒15 ビット目
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

        // システムが使用
        public string Parameter { get; set; }
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
        public bool enableTrackingParam = true; // メニューにトラッキング項目を表示する
        public bool enableDeepSync = true; // 同期を安定させる（ON推奨）
        public bool enablePoseSpace = true; // 視線追従の機能を有効にする
        public bool enableUseCache = false; // キャッシュを使用してビルド時間を短縮する
        public bool enableAutoResetAnim = true; // リセット用アニメーションを自動生成する
        public bool enableLocomotionAnimator = true;
        public bool enableFxAnimator = true;
        public bool suppressAdditiveAnimator = true; // additiveレイヤーの抑制設定

        public VRCExpressionsMenu target = null; // メニューの登録先を上書きする
        public VRCExpressionsMenu settings = null; // 設定メニューのみ分離する
        public WriteDefaultType writeDefaultType = WriteDefaultType.MatchAvatar;

        // システムが使用
        public string Guid { get; set; }
        public List<string> Parameters => categories
            .SelectMany(category => category.poses)
            .Select(pose => pose.Parameter)
            .Distinct()
            .ToList();

        public int PoseCount => categories.Sum(cat => cat.poses.Count);
        public bool EnableAudioMode => categories
            .Any(category => category.poses.Any(pose => pose.audioClip != null));

        /// <summary>
        /// パラメーターの最適化
        /// 0906コミットにより、決定論的に動作
        /// </summary>
        public AvatarPoseData UpdateParameter()
        {
            Guid = ToHash();
            var paramCount = 999;
            var paramIndex = 1;
            var paramName = "";
            foreach (var category in categories)
            {
                foreach (var pose in category.poses)
                {
                    if (paramCount > ConstVariables.MaxAnimationState)
                    {
                        paramName = $"AnimPose_{Guid}_{paramIndex}";
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
        public static List<AvatarPoseData> Combine(AvatarPoseData[] data)
        {
            var result = new List<AvatarPoseData>();
            var names = data.Select(item => item.name).Distinct();
            foreach (var name in names)
            {
                var combined = new AvatarPoseData { name = name };
                foreach (var source in data)
                {
                    if (source.name != combined.name || source.target != null)
                    {
                        continue;
                    }

                    if (!combined.thumbnail)
                    {
                        combined.CopySettingsFrom(source);
                    }
                    combined.categories.AddRange(source.categories);
                }

                if (combined.categories.Count == 0)
                {
                    continue;
                }

                combined.UpdateParameter();
                result.Add(combined);
            }

            foreach (var item in data)
            {
                if (item.target == null)
                {
                    continue;
                }

                item.UpdateParameter();
                result.Add(item);
            }

            return result;
        }

        private void CopySettingsFrom(AvatarPoseData source)
        {
            // 変数の同期
            thumbnail = source.thumbnail;
            enableHeightParam = source.enableHeightParam;
            enableSpeedParam = source.enableSpeedParam;
            enableMirrorParam = source.enableMirrorParam;
            enableTrackingParam = source.enableTrackingParam;
            enableDeepSync = source.enableDeepSync;
            enablePoseSpace = source.enablePoseSpace;
            enableUseCache = source.enableUseCache;
            enableAutoResetAnim = source.enableAutoResetAnim;
            writeDefaultType = source.writeDefaultType;
            enableLocomotionAnimator = source.enableLocomotionAnimator;
            enableFxAnimator = source.enableFxAnimator;
            suppressAdditiveAnimator = source.suppressAdditiveAnimator;
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
            return sb.ToString().Substring(0, ConstVariables.HashLong);
        }
    }

    public enum WriteDefaultType
    {
        MatchAvatar,
        OverrideTrue,
        OverrideFalse
    }
}