using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// アバターにアタッチすることでポーズカテゴリを設定可能にするコンポーネント
    /// 今後、ModularAvatarの拡張機能として動作するように拡張予定
    /// </summary>
    public class AvatarPoseLibrarySettings : MonoBehaviour, IEditorOnly
    {
        public AvatarPoseData data = new AvatarPoseData();
    }
}