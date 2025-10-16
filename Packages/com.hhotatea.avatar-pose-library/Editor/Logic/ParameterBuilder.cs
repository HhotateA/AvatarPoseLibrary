using com.hhotatea.avatar_pose_library.model;
using com.hhotatea.avatar_pose_library.editor;
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

            for (int i = 0; i < ConstVariables.PoseFlagCount; i++)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.FlagParamPrefix}_{poseLibrary.Guid}_{i}",
                    syncType = ParameterSyncType.Int,
                    localOnly = poseLibrary.PoseCount < (1 << i*8),
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
                    saved = true,
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
                localOnly = !poseLibrary.enableDeepSync,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.FaceParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = !poseLibrary.enableFxAnimator,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.ActionParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = !poseLibrary.enableDeepSync,
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
                    saved = true,
                });
            }

            if (poseLibrary.enablePoseSpace)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.PoseSpaceParamPrefix}_{poseLibrary.Guid}",
                    syncType = ParameterSyncType.Bool,
                    localOnly = true,
                    defaultValue =  1,
                    saved = true,
                });
            }

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = !poseLibrary.enableDeepSync,
                defaultValue = 0,
                saved = false,
            });

            return result;
        }
    }
}