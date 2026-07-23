using System;
using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.logic;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AnimatorUtility = com.hhotatea.avatar_pose_library.logic.AnimatorUtility;

[assembly: ExportsPlugin(typeof(DataConvertPlugin))]
namespace com.hhotatea.avatar_pose_library.editor
{
    public class DataConvertPlugin : Plugin<DataConvertPlugin>
    {
        public override string DisplayName => "AvatarPoseLibrary";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("AvatarPose: Data Converting...", ctx =>
                {
                    var settings = ctx.AvatarRootObject
                        .GetComponentsInChildren<AvatarPoseLibrary>()
                        .Where(setting => setting.data != null)
                        .ToArray();
                    APLTelemetry.BeginBuild(ctx.AvatarRootObject, settings);
                    try
                    {
                        // ターゲット未指定のデータを統合して処理
                        var combinedData = AvatarPoseData.Combine(
                            settings.Select(e => e.data)
                            .ToArray());

                        foreach (var d in combinedData)
                        {
                            var go = new GameObject(d.Guid);
                            var root = FindRootComponent(settings, d);
                            go.transform.SetParent(root?.transform ?? ctx.AvatarRootObject.transform);

                            var assets = GetAssetCache(d, d.enableUseCache);
                            if (d.EnableAudioMode)
                            {
                                BuildAudioSource(ctx.AvatarRootObject.transform, d);
                            }
                            BuildRuntimeAnimator(go, assets, d, ctx.AvatarRootObject);
                            BuildRuntimeMenu(go, assets, root?.transform, ctx.AvatarRootObject.transform);
                            BuildRuntimeParameter(go, assets);
                        }
                    }
                    catch (Exception exception)
                    {
                        APLTelemetry.FailBuild(
                            ctx.AvatarRootObject,
                            settings,
                            exception,
                            "data_convert");
                        throw;
                    }
                });
        }

        CacheModel GetAssetCache(AvatarPoseData data, bool useCache)
        {
            if (!useCache)
            {
                return CreateAssets(data);
            }

            var cache = new CacheSave(data.ToHash());
            var asset = cache.LoadAsset();
            if (asset)
            {
                return asset;
            }

            asset = CreateAssets(data);

            if (cache.SaveAsset(asset))
            {
                var savedAsset = cache.LoadAsset();
                if (savedAsset)
                {
                    return savedAsset;
                }
            }

            // SaveAsset persists and imports the generated objects, so some of the references
            // in asset may already have been replaced or destroyed. Generate a fully transient
            // set instead of returning that potentially invalid instance.
            return CreateAssets(data);
        }

        CacheModel CreateAssets(AvatarPoseData data)
        {
            var overrideWriteDefault = data.writeDefaultType == WriteDefaultType.OverrideTrue;

            var cache = ScriptableObject.CreateInstance<CacheModel>();
            cache.locomotionLayer = AnimatorBuilder.BuildLocomotionAnimator(data, overrideWriteDefault);
            cache.paramLayer = AnimatorBuilder.BuildFxAnimator(data, overrideWriteDefault);
            cache.trackingLayer = AnimatorBuilder.BuildTrackingAnimator(data, overrideWriteDefault);
            cache.menuObject = MenuBuilder.BuildPoseMenu(data);
            cache.paramObject = ParameterBuilder.BuildPoseParameter(data);

            return cache;
        }

        /// <summary>
        /// アニメーターの設定
        /// </summary>
        void BuildRuntimeAnimator(GameObject obj, CacheModel assets, AvatarPoseData data, GameObject root)
        {
            var result = new GameObject();
            var matchAvatarWriteDefaults = data.writeDefaultType == WriteDefaultType.MatchAvatar;

            var ma_base = result.AddComponent<ModularAvatarMergeAnimator>();
            ConfigureMergeAnimator(
                ma_base,
                assets.locomotionLayer,
                VRCAvatarDescriptor.AnimLayerType.Base,
                matchAvatarWriteDefaults);

            var ma_action = result.AddComponent<ModularAvatarMergeAnimator>();
            ConfigureMergeAnimator(
                ma_action,
                assets.locomotionLayer,
                VRCAvatarDescriptor.AnimLayerType.Action,
                matchAvatarWriteDefaults);

            var ma_fx = result.AddComponent<ModularAvatarMergeAnimator>();
            var fxAnimator = GetFxAnimatorForBuild(assets.paramLayer, data, root);
            ConfigureMergeAnimator(
                ma_fx,
                fxAnimator,
                VRCAvatarDescriptor.AnimLayerType.FX,
                matchAvatarWriteDefaults);

            var ma_tracking = result.AddComponent<ModularAvatarMergeAnimator>();
            ConfigureMergeAnimator(
                ma_tracking,
                assets.trackingLayer,
                VRCAvatarDescriptor.AnimLayerType.Base,
                matchAvatarWriteDefaults);

            result.transform.SetParent(obj.transform);
        }

        /// <summary>
        /// メニューの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildRuntimeMenu(GameObject obj, CacheModel assets, Transform main, Transform root)
        {
            var result = assets.menuObject;
            foreach (var installer in result.GetComponentsInChildren<ModularAvatarMenuInstaller>())
            {
                installer.transform.SetParent(root);
            }
            if (result.transform.parent != null)
            {
                // 既に親オブジェクト処理済みのためスキップ
                // Targetの機能自体を削除予定
                return;
            }

            if (IsItemRoot(main))
            {
                // 親オブジェクトがMAMenuGroup等の場合
                var parent = main.parent;
                result.transform.SetParent(parent);
                int targetIndex = main.GetSiblingIndex();
                result.transform.SetSiblingIndex(targetIndex + 1);
            }
            else
            {
                // Rootにそのまま入れる場合（既存挙動）
                result.AddComponent<ModularAvatarMenuInstaller>();
                result.transform.SetParent(obj.transform);
            }
        }

        /// <summary>
        /// AudioSourceを作成
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildAudioSource(Transform root, AvatarPoseData data)
        {
            var result = new GameObject($"{ConstVariables.AudioParamPrefix}_{data.Guid}");
            result.transform.SetParent(root);
            var audio = result.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.mute = false;
            audio.rolloffMode = DynamicVariables.Settings.audioRolloffMode;
            audio.minDistance = DynamicVariables.Settings.audioMinDistance;
            audio.maxDistance = DynamicVariables.Settings.audioMaxDistance;
            audio.volume = DynamicVariables.Settings.audioVolume;
            audio.pitch = DynamicVariables.Settings.audioPitch;
        }

        private static bool IsItemRoot(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            var parent = root.parent;
            if (parent == null)
            {
                return false;
            }

            var menuRoot = parent.gameObject;

            var group = menuRoot.GetComponent<ModularAvatarMenuGroup>();
            if (group)
            {
                return true;
            }

            var item = menuRoot.GetComponent<ModularAvatarMenuItem>();
            if (item)
            {
                if (item.Control?.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
                    item.MenuSource == SubmenuSource.Children)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// パラメーターの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildRuntimeParameter(GameObject obj, CacheModel assets)
        {
            var result = assets.paramObject;
            result.transform.SetParent(obj.transform);
        }

        private static void ConfigureMergeAnimator(
            ModularAvatarMergeAnimator mergeAnimator,
            RuntimeAnimatorController animator,
            VRCAvatarDescriptor.AnimLayerType layerType,
            bool matchAvatarWriteDefaults)
        {
            mergeAnimator.layerPriority = 1;
            mergeAnimator.animator = animator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            mergeAnimator.layerType = layerType;
        }

        private static RuntimeAnimatorController GetFxAnimatorForBuild(
            UnityEditor.Animations.AnimatorController cachedAnimator,
            AvatarPoseData data,
            GameObject root)
        {
            var overrideWriteDefault = data.writeDefaultType == WriteDefaultType.OverrideTrue;
            if (!cachedAnimator)
            {
                Debug.LogWarning(
                    "AvatarPoseLibrary: The cached FX animator is missing. Rebuilding it for this build.");
                cachedAnimator = AnimatorBuilder.BuildFxAnimator(data, overrideWriteDefault);
            }

            if (!data.enableAutoResetAnim)
            {
                return cachedAnimator;
            }

            var animator = cachedAnimator;
            if (animator != null && AssetDatabase.Contains(animator))
            {
                // The reset clip depends on the current avatar's default values. Never write it
                // into the persistent cache asset, which may be reused by another avatar/build.
                animator = AnimatorBuilder.BuildFxAnimator(data, overrideWriteDefault);
            }

            return AnimatorUtility.ReplaceResetAnimation(animator, data, root);
        }

        private static AvatarPoseLibrary FindRootComponent(
            AvatarPoseLibrary[] settings,
            AvatarPoseData data)
        {
            // 明示的なターゲットを持つデータは参照一致を優先し、統合データだけ名前で検索する。
            return settings.FirstOrDefault(setting => ReferenceEquals(setting.data, data))
                ?? settings.FirstOrDefault(setting =>
                    setting.data.target == null &&
                    string.Equals(setting.data.name, data.name, StringComparison.Ordinal));
        }
    }
}
