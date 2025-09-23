using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
    // [CreateAssetMenu(menuName = "HhotateA/AvatarPoseSettings")]
    public class AvatarPoseSettings : ScriptableObject
    {
        [SerializeField] MenuContext menuContext;
        public MenuContext Menu
        {
            get
            {
                foreach (var localContext in localContexts)
                {
                    if (Application.systemLanguage == localContext.language)
                    {
                        // 言語ファイルが存在するなら使う！
                        if (localContext.menuContext)
                            return localContext.menuContext;
                    }
                }

                if (menuContext != null)
                    return menuContext;

                throw new NullReferenceException("MenuContextファイルが見つかりません。再インポートしてください。");
            }
        }

        [SerializeField] InspectorContext inspectorContext;
        public InspectorContext Inspector
        {
            get
            {
                foreach (var localContext in localContexts)
                {
                    if (Application.systemLanguage == localContext.language)
                    {
                        // 言語ファイルが存在するなら使う！
                        if (localContext.inspectorContext)
                            return localContext.inspectorContext;
                    }
                }

                if (inspectorContext != null)
                    return inspectorContext;

                throw new NullReferenceException("InspectorContextファイルが見つかりません。再インポートしてください。");
            }
        }

        [SerializeField]
        LocalFile[] localContexts;

        /// <summary>
        /// サムネイル撮影用のレイヤー
        /// </summary>
        public int thumbnailLayer = 4;

        /// <summary>
        /// サムネイルテクスチャのサイズ
        /// </summary>
        public int texSize = 128;

        /// <summary>
        /// モーションにノイズを混ぜる
        /// </summary>
        public float motionLong = 15f;
        public float motionDuration = 0.5f;
        // public float motionNoiseTime = 0.1f;
        public float motionNoiseScale = 0.6f;

        public float minMaxHeight = 1f;

        /// <summary>
        /// サムネ撮影の設定
        /// </summary>
        public float lookAtFace = 0f;
        public float fieldOfView = 30f;
        public float cameraDistance = 1f;
        public Vector3 cameraOffset = Vector3.zero;

        /// <summary>
        /// AnimatorLayerに使うアセット群
        /// </summary>
        public AnimationClip defaultAnimation;

        /// <summary>
        /// Menuの項目
        /// </summary>
        public bool poseSpaceMenu = true;
        public bool fxAnimationMenu = true;

        /// <summary>
        /// キャッシュ用のパス
        /// </summary>
        public string cachePath = "Assets/_APLCache";
    }

    [Serializable]
    public class LocalFile
    {
        public SystemLanguage language;
        public MenuContext menuContext;
        public InspectorContext inspectorContext;
    }
}