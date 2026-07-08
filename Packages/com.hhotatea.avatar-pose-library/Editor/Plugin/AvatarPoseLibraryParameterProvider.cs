using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using com.hhotatea.avatar_pose_library.editor;
using Unity.Collections;
using UnityEngine;
using System.Linq;

namespace com.hhotatea.avatar_pose_library.logic
{
    // AvatarPoseLibrary のパラメータを提供するクラス
    [ParameterProviderFor(typeof(AvatarPoseLibrary))]
    public sealed class AvatarPoseLibraryParametersProvider : IParameterProvider
    {
        private readonly AvatarPoseLibrary _component;

        public AvatarPoseLibraryParametersProvider(AvatarPoseLibrary comp)
        {
            _component = comp;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext ctx = null)
        {
            if (_component == null || _component.data == null)
            {
                yield break;
            }

            // 最上位オブジェクトのみで実行
            if (!_component.IsRootComponent())
            {
                yield break;
            }

            // データ抽出
            var combinedData = AvatarPoseData.Combine(
                _component.GetComponentMember()
                    .Select(e => e.data)
                    .ToArray());
            if (combinedData.Count == 0)
            {
                yield break;
            }

            foreach (var data in combinedData)
            {
                // AvatarPoseLibrary のデータからパラメータ設定を構築
                var parameterGameObj = ParameterBuilder.BuildPoseParameter(data);
                var mParams = parameterGameObj.GetComponent<ModularAvatarParameters>();

                try
                {
                    foreach (var cfg in mParams.parameters)
                    {
                        var type = cfg.syncType switch
                        {
                            ParameterSyncType.Bool => AnimatorControllerParameterType.Bool,
                            ParameterSyncType.Int => AnimatorControllerParameterType.Int,
                            ParameterSyncType.Float => AnimatorControllerParameterType.Float,
                            _ => AnimatorControllerParameterType.Float
                        };

                        yield return new ProvidedParameter(
                            name: cfg.nameOrPrefix,
                            namespace_: ParameterNamespace.Animator,
                            source: _component,
                            plugin: DataConvertPlugin.Instance,
                            parameterType: type
                        )
                        {
                            WantSynced = !cfg.localOnly
                        };
                    }
                }
                finally
                {
                    Object.DestroyImmediate(parameterGameObj);
                }
            }
        }
    }
}
