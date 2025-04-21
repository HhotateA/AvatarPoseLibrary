using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
// [CreateAssetMenu(menuName = "HhotateA/AvatarPoseSettings")]
    public class AvatarPoseSettings : ScriptableObject
    {
        [SerializeField] MenuContext menuBuff;
        public MenuContext Menu
        {
            get
            {
                if (menuBuff != null) return menuBuff;
                throw new NullReferenceException("MenuContextファイルが見つかりません。再インポートしてください。");
            }
        }
        
        [SerializeField] InspectorContext contextBuff;
        public InspectorContext Context
        {
            get
            {
                if (contextBuff != null) return contextBuff;
                throw new NullReferenceException("InspectorContextファイルが見つかりません。再インポートしてください。");
            }
        }
    
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
    }
}