using System;
using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// アバターにアタッチすることでポーズカテゴリを設定可能にするコンポーネント
    /// </summary>
    public class AvatarPoseLibrarySettings : MonoBehaviour, IEditorOnly
    {
        private AvatarPoseData data;

        public AvatarPoseData Data
        {
            get => data;
            set => data = value;
        }

        private void Reset()
        {
            // エディターでセットするので、Nullにしておく。
            data = null;
        }
    }
}