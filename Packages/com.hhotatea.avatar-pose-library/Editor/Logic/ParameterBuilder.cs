using System.Linq;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.logic
{
    public static class ParameterBuilder
    {
        public static GameObject BuildPoseParameter(AvatarPoseData poseLibrary)
        {
            var result = new GameObject("");
            var mResult = result.AddComponent<ModularAvatarParameters>();
            foreach (var parameter in poseLibrary.Parameters)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = parameter,
                    syncType = ParameterSyncType.Int,
                    localOnly = true,
                    defaultValue = 0,
                    saved = false,
                });
            }

            for (int i = 0; i < ConstVariables.BoolFlagCount; i++)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}",
                    syncType = ParameterSyncType.Bool,
                    localOnly = poseLibrary.PoseCount < (1 << i),
                    defaultValue = 0,
                    saved = false,
                });
            }

            if (poseLibrary.enableHeightParam)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}",
                    syncType = ParameterSyncType.Float,
                    localOnly = false,
                    defaultValue = 0.5f,
                    saved = false,
                });
            }

            if (poseLibrary.enableSpeedParam)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.Guid}",
                    syncType = ParameterSyncType.Float,
                    localOnly = false,
                    defaultValue = 0.5f,
                    saved = false,
                });
            }

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.BaseParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.HeadParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.ArmParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.FootParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.FingerParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            if (poseLibrary.enableMirrorParam)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.Guid}",
                    syncType = ParameterSyncType.Bool,
                    localOnly = false,
                    defaultValue = 0,
                    saved = false,
                });
            }

            return result;
        }
    }
}