using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class ParameterBuilder
    {
        public static GameObject BuildPoseParameter(AvatarPoseData library)
        {
            var result = new GameObject("");
            var parameters = result.AddComponent<ModularAvatarParameters>();

            foreach (var parameter in library.Parameters)
            {
                AddParameter(parameters, parameter, ParameterSyncType.Int, true);
            }

            for (var index = 0; index < ConstVariables.PoseFlagCount; index++)
            {
                AddParameter(
                    parameters,
                    $"{ConstVariables.FlagParamPrefix}_{library.Guid}_{index}",
                    ParameterSyncType.Int,
                    library.PoseCount < 1 << index * 8);
            }

            if (library.enableHeightParam)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.HeightParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    false,
                    0.5f,
                    true);
            }

            if (library.enableSpeedParam)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.SpeedParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    false,
                    0.5f);
            }

            AddParameter(parameters, Name(ConstVariables.BaseParamPrefix, library.Guid), ParameterSyncType.Bool, true);
            AddParameter(parameters, Name(ConstVariables.HeadParamPrefix, library.Guid), ParameterSyncType.Bool, true);
            AddParameter(parameters, Name(ConstVariables.ArmParamPrefix, library.Guid), ParameterSyncType.Bool, true);
            AddParameter(parameters, Name(ConstVariables.FootParamPrefix, library.Guid), ParameterSyncType.Bool, true);
            AddParameter(
                parameters,
                Name(ConstVariables.FingerParamPrefix, library.Guid),
                ParameterSyncType.Bool,
                !library.enableDeepSync);
            AddParameter(
                parameters,
                Name(ConstVariables.FaceParamPrefix, library.Guid),
                ParameterSyncType.Bool,
                !library.enableFxAnimator || !library.enableDeepSync);
            AddParameter(
                parameters,
                Name(ConstVariables.ActionParamPrefix, library.Guid),
                ParameterSyncType.Bool,
                !library.enableDeepSync);
            AddParameter(parameters, Name(ConstVariables.ResetParamPrefix, library.Guid), ParameterSyncType.Bool, true);

            if (library.enableMirrorParam)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.MirrorParamPrefix, library.Guid),
                    ParameterSyncType.Bool,
                    false,
                    saved: true);
                AddParameter(
                    parameters,
                    Name(ConstVariables.MirrorCycleOffsetParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    true);
            }

            if (library.enablePoseSpace)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.PoseSpaceParamPrefix, library.Guid),
                    ParameterSyncType.Bool,
                    true,
                    1f,
                    true);
            }

            if (library.EnableAudioMode)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.AudioParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    false,
                    1f,
                    true);
            }

            // 生成したAnimatorレイヤー間の連携に使用する実行時専用パラメーター。
            AddParameter(
                parameters,
                Name(ConstVariables.OnPlayParamPrefix, library.Guid),
                ParameterSyncType.Bool,
                !library.enableDeepSync);
            AddParameter(
                parameters,
                Name(ConstVariables.HeightUpdateParamPrefix, library.Guid),
                ParameterSyncType.Bool,
                true);

            return result;
        }

        private static void AddParameter(
            ModularAvatarParameters parameters,
            string name,
            ParameterSyncType syncType,
            bool localOnly,
            float defaultValue = 0f,
            bool saved = false)
        {
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = name,
                syncType = syncType,
                localOnly = localOnly,
                defaultValue = defaultValue,
                saved = saved,
            });
        }

        private static string Name(string prefix, string guid)
        {
            return $"{prefix}_{guid}";
        }
    }
}
