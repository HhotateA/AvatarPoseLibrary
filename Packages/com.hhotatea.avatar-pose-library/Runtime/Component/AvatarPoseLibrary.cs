using com.hhotatea.avatar_pose_library.model;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using System.Linq;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// アバターにアタッチすることでポーズカテゴリを設定可能にするコンポーネント
    /// ポーズごとのオーディオ再生と、ポーズ速度に合わせたオーディオ速度調整に対応。
    /// </summary>
    [HelpURL("https://github.com/HhotateA/AvatarPoseLibrary/wiki")]
    public class AvatarPoseLibrary : MonoBehaviour, IEditorOnly
    {
        // データの本体。
        public AvatarPoseData data;

        // Editorで初期化処理を行いたいので、フラグを持っておく。
        public bool isInitialized;

        // 追加：ポーズ再生時に使用する AudioSource
        public AudioSource audioSource;

        private void Reset()
        {
            isInitialized = false;

        /// <summary>
        /// ポーズに紐づいた AudioClip を再生する。
        /// speed に合わせて AudioSource の pitch も変更する。
        /// </summary>
        /// <param name="clip">再生する AudioClip</param>
        /// <param name="speed">ポーズの再生速度</param>
        public void PlayPoseAudio(AudioClip clip, float speed)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.clip = clip;
            audioSource.pitch = speed;
            audioSource.Play();
        }

        /// <summary>
        /// 現在再生中のポーズ音声を停止する。
        /// </summary>
        public void StopPoseAudio()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.Stop();
        }

        /* MenuSourceComponent参照でバグりそうなので将来的実装
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
                icon = data.thumbnail,
            });
        }

        public override void ResolveReferences()
        {
            // no-op
        }
        */

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
