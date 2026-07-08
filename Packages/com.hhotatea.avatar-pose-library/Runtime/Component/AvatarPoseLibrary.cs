using System;
using System.Linq;
using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>アバターにアタッチされたポーズライブラリの設定を保持します。</summary>
    [HelpURL("https://github.com/HhotateA/AvatarPoseLibrary/wiki")]
    public class AvatarPoseLibrary : MonoBehaviour, IEditorOnly
    {
        public AvatarPoseData data;

        // カスタムインスペクターで初期化を一度だけ行うためのフラグ。
        public bool isInitialized;

        private void Reset()
        {
            isInitialized = false;
        }

        /// <summary>同じアバターに属するポーズライブラリコンポーネントを返します。</summary>
        public AvatarPoseLibrary[] GetLibraries()
        {
            var avatar = transform.GetComponentInParent<VRCAvatarDescriptor>();
            return avatar
                ? avatar.GetComponentsInChildren<AvatarPoseLibrary>()
                : new[] { this };
        }

        /// <summary>このコンポーネントと同じライブラリ名を持つコンポーネントを返します。</summary>
        public AvatarPoseLibrary[] GetComponentMember()
        {
            if (data == null)
            {
                return Array.Empty<AvatarPoseLibrary>();
            }

            return GetLibraries()
                .Where(library => library.data != null)
                .Where(library => string.Equals(library.data.name, data.name, StringComparison.Ordinal))
                .ToArray();
        }

        public AvatarPoseLibrary GetComponentLeader()
        {
            return GetComponentMember().FirstOrDefault();
        }

        public bool IsRootComponent()
        {
            return GetComponentLeader() == this;
        }
    }
}
