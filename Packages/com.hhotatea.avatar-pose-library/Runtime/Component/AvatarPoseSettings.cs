using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
// [CreateAssetMenu(menuName = "HhotateA/AvatarPoseSettings")]
    public class AvatarPoseSettings : ScriptableObject
    {
        public MenuItem mainMenu;
        public MenuItem categoryMenu;
        public MenuItem poseMenu;
        public MenuItem trackingMenu;
    
        public MenuItem resetMenu;
        public MenuItem settingMenu;
        public MenuItem heightMenu;
        public MenuItem headMenu;
        public MenuItem armMenu;
        public MenuItem footMenu;
        public MenuItem fingerMenu;
        public MenuItem speedMenu;
        public MenuItem locomotionMenu;
        public MenuItem mirrorMenu;
    
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
        // public float motionNoiseScale = 0.2f;
    
        [Serializable]
        public class MenuItem
        {
            public string title;
            public Texture2D thumbnail;
        }
    }
}