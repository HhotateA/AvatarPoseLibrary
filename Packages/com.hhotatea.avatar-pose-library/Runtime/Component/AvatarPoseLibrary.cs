using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using nadena.dev.modular_avatar.core.menu;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Linq;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// アバターにアタッチすることでポーズカテゴリを設定可能にするコンポーネント
    /// </summary>
    [HelpURL("https://github.com/HhotateA/AvatarPoseLibrary/wiki")]
    public class AvatarPoseLibrary : MenuSourceComponent, IEditorOnly
    {
        // データの本体。
        public AvatarPoseData data;

        // Editorで初期化処理を行いたいので、フラグを持っておく。
        public bool isInitialized;

        private void Reset()
        {
            isInitialized = false;
        }

        public override void Visit(NodeContext context)
        {
            if (Application.isPlaying)
            {
                // 再生モードでは早期リターン
                return;
            }
            context.PushControl(new VRCExpressionsMenu.Control()
            {
                name = data.name,
                icon = data.thumbnail
            });
        }

        public override void ResolveReferences()
        {
            // no-op
        }

        public AvatarPoseLibrary[] GetLibraries()
        {
            var avatar = transform.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar)
            {
                return avatar.GetComponentsInChildren<AvatarPoseLibrary>();
            }
            return new[] { this };
        }

        public AvatarPoseLibrary[] GetComponentMember()
        {
            return GetLibraries()
                    .Where(e => e.data.name == this.data.name)
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