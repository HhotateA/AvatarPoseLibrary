using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using com.hhotatea.avatar_pose_library.editor;
using Unity.Collections;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.logic {
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
            // パラメーターの最適化
            _component.data.UpdateParameter();
            // AvatarPoseLibrary のデータからパラメータ用の GameObject を構築
            var parameterGameObj = ParameterBuilder.BuildPoseParameter(_component.data);
            // ModularAvatarParameters コンポーネントを取得
            var mParams = parameterGameObj.GetComponent<ModularAvatarParameters>();

            foreach (var cfg in mParams.parameters)
            {
                var type = cfg.syncType switch {
                    // syncType に応じて AnimatorControllerParameterType をマッピング
                    ParameterSyncType.Bool  => AnimatorControllerParameterType.Bool,
                    ParameterSyncType.Int   => AnimatorControllerParameterType.Int,
                    ParameterSyncType.Float => AnimatorControllerParameterType.Float,
                    _ => AnimatorControllerParameterType.Float
                };

                // ProvidedParameter を生成して返却
                yield return new ProvidedParameter(
                    name: cfg.nameOrPrefix,
                    namespace_: ParameterNamespace.Animator,
                    source: mParams,
                    plugin: DataConvertPlugin.Instance,
                    parameterType: type
                ) {
                    // localOnly が false の場合は同期を希望
                    WantSynced = !cfg.localOnly
                };
            }

            // 使用後は GameObject を破棄
            Object.DestroyImmediate(parameterGameObj);
        }
    }
}