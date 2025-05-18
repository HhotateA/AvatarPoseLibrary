using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// アバターにアタッチすることでポーズカテゴリを設定可能にするコンポーネント
    /// </summary>
    [HelpURL("https://github.com/HhotateA/AvatarPoseLibrary/wiki")]
    public class AvatarPoseLibrary : MonoBehaviour, IEditorOnly
    {
        // インストール先のメニュー（通常は空）
        public VRCExpressionsMenu target = null;
        
        // データの本体。
        public AvatarPoseData data;

        // Editorで初期化処理を行いたいので、フラグを持っておく。
        public bool isInitialized;

        private void Reset()
        {
            isInitialized = false;
        }
    }
}