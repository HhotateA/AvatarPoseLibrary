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
                    localOnly = false,
                    defaultValue = 0,
                    saved = false,
                });
            }

            if (poseLibrary.enableHeightParam)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.guid}",
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
                    nameOrPrefix = $"{ConstVariables.SpeedParamPrefix}_{poseLibrary.guid}",
                    syncType = ParameterSyncType.Float,
                    localOnly = false,
                    defaultValue = 0.5f,
                    saved = false,
                });
            }

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.BaseParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.HeadParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.ArmParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.FootParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.FingerParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            mResult.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = $"{ConstVariables.ResetParamPrefix}_{poseLibrary.guid}",
                syncType = ParameterSyncType.Bool,
                localOnly = true,
                defaultValue = 0,
                saved = false,
            });

            if (poseLibrary.enableMirrorParam)
            {
                mResult.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = $"{ConstVariables.MirrorParamPrefix}_{poseLibrary.guid}",
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