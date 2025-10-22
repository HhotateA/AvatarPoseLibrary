using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.logic;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using UnityEditor.VersionControl;

[assembly : ExportsPlugin (typeof (DataConvertPlugin))]
namespace com.hhotatea.avatar_pose_library.editor {
    public class DataConvertPlugin : Plugin<DataConvertPlugin> {
        public override string DisplayName => "AvatarPoseLibrary";

        protected override void Configure () {
            InPhase (BuildPhase.Generating)
                .BeforePlugin ("nadena.dev.modular-avatar")
                .Run ("AvatarPose: Data Converting...", ctx => {
                    var settings = ctx.AvatarRootObject.GetComponentsInChildren<AvatarPoseLibrary> ();

                    // ターゲット未指定のデータを統合して処理
                    var combinedData = AvatarPoseData.Combine (
                        settings.Select (e => e.data)
                        .ToArray ());

                    foreach (var d in combinedData) {
                        var go = new GameObject (d.Guid);
                        var root = settings.FirstOrDefault (e => e.data.name == d.name);
                        go.transform.SetParent (root?.transform ?? ctx.AvatarRootObject.transform);

                        var assets = GetAssetCache(d, d.enableUseCache);
                        if(d.EnableAudioMode) BuildAudioSource(ctx.AvatarRootObject.transform,d);
                        BuildRuntimeAnimator (go, assets, d);
                        BuildRuntimeMenu (go, assets, root?.transform);
                        BuildRuntimeParameter(go, assets);
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
                return cache.LoadAsset();
            }
            else
            {
                return asset;
            }
        }

        CacheModel CreateAssets(AvatarPoseData data)
        {
            bool overrideWriteDefault = (data.writeDefaultType == WriteDefaultType.OverrideTrue);
            return new CacheModel()
            {
                locomotionLayer = AnimatorBuilder.BuildLocomotionAnimator(data, overrideWriteDefault),
                paramLayer = AnimatorBuilder.BuildFxAnimator(data, overrideWriteDefault),
                trackingLayer = AnimatorBuilder.BuildTrackingAnimator(data, overrideWriteDefault),
                menuObject = MenuBuilder.BuildPoseMenu(data),
                paramObject = ParameterBuilder.BuildPoseParameter(data)
            };
        }

        /// <summary>
        /// アニメーターの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="data"></param>
        void BuildRuntimeAnimator (GameObject obj, CacheModel assets, AvatarPoseData data) {
            var result = new GameObject ();
            bool matchAvatarWriteDefaults = (data.writeDefaultType == WriteDefaultType.MatchAvatar);

            var ma_base = result.AddComponent<ModularAvatarMergeAnimator> ();
            ma_base.layerPriority = 1;
            ma_base.animator = assets.locomotionLayer;
            ma_base.pathMode = MergeAnimatorPathMode.Absolute;
            ma_base.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            ma_base.layerType = VRCAvatarDescriptor.AnimLayerType.Base;

            var ma_action = result.AddComponent<ModularAvatarMergeAnimator> ();
            ma_action.layerPriority = 1;
            ma_action.animator = assets.locomotionLayer;
            ma_action.pathMode = MergeAnimatorPathMode.Absolute;
            ma_action.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            ma_action.layerType = VRCAvatarDescriptor.AnimLayerType.Action;

            var ma_fx = result.AddComponent<ModularAvatarMergeAnimator> ();
            ma_fx.layerPriority = 1;
            ma_fx.animator = assets.paramLayer;
            ma_fx.pathMode = MergeAnimatorPathMode.Absolute;
            ma_fx.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            ma_fx.layerType = VRCAvatarDescriptor.AnimLayerType.FX;

            var ma_tracking = result.AddComponent<ModularAvatarMergeAnimator> ();
            ma_tracking.layerPriority = 1;
            ma_tracking.animator = assets.trackingLayer;
            ma_tracking.pathMode = MergeAnimatorPathMode.Absolute;
            ma_tracking.matchAvatarWriteDefaults = matchAvatarWriteDefaults;
            ma_tracking.layerType = VRCAvatarDescriptor.AnimLayerType.Base;

            result.transform.SetParent (obj.transform);
        }

        /// <summary>
        /// メニューの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildRuntimeMenu(GameObject obj, CacheModel assets, Transform root)
        {
            var result = assets.menuObject;
            foreach (var installer in result.GetComponentsInChildren<ModularAvatarMenuInstaller>())
            {
                installer.transform.SetParent(obj.transform);
            }

            if (IsItenRoot(root))
            {
                var parent = root.parent;
                result.transform.SetParent(parent);
                int targetIndex = root.GetSiblingIndex();
                result.transform.SetSiblingIndex(targetIndex + 1);
            }
            else
            {
                result.AddComponent<ModularAvatarMenuInstaller>();
                result.transform.SetParent(obj.transform);
            }
        }
        
        /// <summary>
        /// AudioSourceを作成
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildAudioSource (Transform root, AvatarPoseData data) 
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

        bool IsItenRoot(Transform root)
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
                if (item.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu &&
                    item.MenuSource == SubmenuSource.Children)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// パラメーターの設定
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="assets"></param>
        void BuildRuntimeParameter (GameObject obj, CacheModel assets) {
            var result = assets.paramObject;
            result.transform.SetParent (obj.transform);
        }
    }
}