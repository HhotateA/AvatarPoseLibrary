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
        public VRCExpressionsMenu target;

        // このポーズの再生中に適用するトラッキング設定。
        public TrackingSetting tracking = new TrackingSetting();

        // AnimatorとExpression Menuの生成処理で使用する値。
        public string Parameter { get; set; }
        public int Value { get; set; }
        public int Index { get; set; }

        /// <summary>ポーズのインデックスをAnimatorで扱える8ビット単位のフラグに分割します。</summary>
        public int[] GetAnimatorFlag()
        {
            return new[]
            {
                Index & 0xFF,
                (Index >> 8) & 0xFF,
            };
        }
    }

    [Serializable]
    public class PoseCategory
    {
        public string name;
        public Texture2D thumbnail;
        public List<PoseEntry> poses = new List<PoseEntry>();
        public VRCExpressionsMenu target;

        // このカテゴリ内のポーズで共有する生成済みパラメーター名。
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
        public bool enableTrackingParam = true;
        public bool enableDeepSync = true;
        public bool enablePoseSpace = true;
        public bool enableUseCache;
        public bool enableAutoResetAnim = true;
        public bool enableLocomotionAnimator = true;
        public bool enableFxAnimator = true;
        public bool suppressAdditiveAnimator = true;

        [Tooltip("Throws a test exception during the APL build pipeline.")]
        public bool debugForceBuildError;

        public VRCExpressionsMenu target;
        public VRCExpressionsMenu settings;
        public WriteDefaultType writeDefaultType = WriteDefaultType.MatchAvatar;

        // ビルド時に生成する値のため、Unityのシリアライズ対象には含めません。
        public string Guid { get; set; }

        public List<string> Parameters => Categories
            .SelectMany(category => category.poses ?? Enumerable.Empty<PoseEntry>())
            .Select(pose => pose?.Parameter)
            .Where(parameter => !string.IsNullOrEmpty(parameter))
            .Distinct()
            .ToList();

        public int PoseCount => Categories.Sum(category => category.poses?.Count ?? 0);

        public bool EnableAudioMode => Categories
            .Any(category => category.poses?.Any(pose => pose?.audioClip != null) == true);

        private IEnumerable<PoseCategory> Categories =>
            categories?.Where(category => category != null) ?? Enumerable.Empty<PoseCategory>();

        /// <summary>安定したIDと各ポーズのAnimator用の値を再生成します。</summary>
        public AvatarPoseData UpdateParameter()
        {
            Guid = ToHash();
            var value = ConstVariables.MaxAnimationState + 1;
            var poseIndex = 1;
            var parameterName = "";

            foreach (var category in Categories)
            {
                foreach (var pose in category.poses ?? Enumerable.Empty<PoseEntry>())
                {
                    if (pose == null)
                    {
                        continue;
                    }

                    if (value > ConstVariables.MaxAnimationState)
                    {
                        parameterName = $"AnimPose_{Guid}_{poseIndex}";
                        value = 1;
                    }

                    pose.Parameter = parameterName;
                    pose.Value = value++;
                    pose.Index = poseIndex++;
                }
            }

            return this;
        }

        /// <summary>
        /// 明示的なExpression Menuが指定されていない同名のデータを統合します。
        /// </summary>
        public static List<AvatarPoseData> Combine(AvatarPoseData[] data)
        {
            if (data == null)
            {
                return new List<AvatarPoseData>();
            }

            var sources = data.Where(item => item != null).ToArray();
            var result = new List<AvatarPoseData>();

            foreach (var libraryName in sources.Select(item => item.name).Distinct())
            {
                var combined = new AvatarPoseData { name = libraryName };
                var hasSettings = false;

                foreach (var source in sources)
                {
                    if (source.name != libraryName || source.target != null)
                    {
                        continue;
                    }

                    if (!hasSettings)
                    {
                        combined.CopySettingsFrom(source);
                        hasSettings = true;
                    }

                    combined.categories.AddRange(source.categories ?? Enumerable.Empty<PoseCategory>());
                }

                if (combined.categories.Count > 0)
                {
                    result.Add(combined.UpdateParameter());
                }
            }

            foreach (var source in sources.Where(item => item.target != null))
            {
                result.Add(source.UpdateParameter());
            }

            return result;
        }

        private void CopySettingsFrom(AvatarPoseData source)
        {
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

        /// <summary>生成アセット名とキャッシュキーに使用する短いコンテンツハッシュを返します。</summary>
        public string ToHash()
        {
#if UNITY_EDITOR
            var json = UnityEditor.EditorJsonUtility.ToJson(this, false);
#else
            var json = JsonUtility.ToJson(this, false);
#endif
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json ?? ""));
            var hash = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                hash.Append(value.ToString("x2"));
            }

            return hash.ToString(0, ConstVariables.HashLong);
        }
    }

    public enum WriteDefaultType
    {
        MatchAvatar,
        OverrideTrue,
        OverrideFalse
    }
}
