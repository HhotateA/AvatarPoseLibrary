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

            if (library.enableVariationSlider)
            {
                AddParameter(
                    parameters,
                    Name(ConstVariables.VariationParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    false,
                    0f,
                    true);
                // Flags still needed internally for Fx/Locomotion layer signalling but keep them localOnly when in variation mode
                for (var index = 0; index < ConstVariables.PoseFlagCount; index++)
                {
                    AddParameter(
                        parameters,
                        $"{ConstVariables.FlagParamPrefix}_{library.Guid}_{index}",
                        ParameterSyncType.Int,
                        true);
                }
                // Legacy Int pose selectors are driven internally by variation driver — keep localOnly to save synced memory
                foreach (var parameter in library.Parameters)
                {
                    AddParameter(parameters, parameter, ParameterSyncType.Int, true);
                }
            }
            else
            {
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
            }

            if (library.enableHeightParam)
            {
                // ポーズの選択状態によらず高さを適用するため、保存はしません。
                // 保存すると、ポーズを選んでいない状態で前回の値が復元され、
                // アバターが浮いたまま表示されてしまいます。
                AddParameter(
                    parameters,
                    Name(ConstVariables.HeightParamPrefix, library.Guid),
                    ParameterSyncType.Float,
                    false,
                    0.5f,
                    false);
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

            if (library.UseBodyRotationParam)
            {
                // 無回転の値は軸ごとに違うため、ConstVariables の定義から引きます
                // （X/Z は 0、Y は 0.5）。式パラメーターの並び順は従来どおり X → Y → Z です。
                foreach (var axis in new[]
                         {
                             ConstVariables.BodyRotationAxisX,
                             ConstVariables.BodyRotationAxisY,
                             ConstVariables.BodyRotationAxisZ,
                         })
                {
                    AddParameter(
                        parameters,
                        Name(axis.ParameterPrefix, library.Guid),
                        ParameterSyncType.Float,
                        false,
                        axis.NeutralValue,
                        true);
                }
                // ラジアルを無回転へ戻すためのトリガー。既存の Reset と同じくローカル専用ですが、
                // ドライバーが書き換える回転値そのものは同期されるため全員の表示が揃います。
                AddParameter(
                    parameters,
                    Name(ConstVariables.BodyRotationResetParamPrefix, library.Guid),
                    ParameterSyncType.Bool,
                    true);
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
                ParameterSyncType.Float,
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
