using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
    /// <summary>
    /// Play モード専用のプレビュー補助コンポーネント。
    ///
    /// Unity の Play モードでは VRChat の制約（VRCRotationConstraint /
    /// VRCPositionConstraint）が評価されません。そのため体の回転リグの鎖を
    /// アニメーションで回しても、アーマチュアが追従せず、傾きも高さも
    /// 見た目に反映されません。
    ///
    /// このコンポーネントは、制約がやるはずの計算を LateUpdate で手で適用し、
    /// エディター上でもリグの動きを確認できるようにするだけのものです。
    /// VRChat SDK のビルド（アップロード）では追加されないので、
    /// 配布されるアバターには一切含まれません。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class APLBodyRotationPreviewDriver : MonoBehaviour
    {
        /// <summary>回される側（アーマチュア）。アバタールートからの相対パス。</summary>
        public string armaturePath;

        /// <summary>回転のソース（鎖の末端 Z）。アバタールートからの相対パス。</summary>
        public string sourcePath;

        /// <summary>位置のソース（鎖の末端 Offset）。アバタールートからの相対パス。</summary>
        public string offsetPath;

        /// <summary>
        /// 制約に渡している回転オフセット（オイラー角）。
        /// 無回転のときに元の姿勢へ戻るよう、リグを作った時点で測った値です。
        /// </summary>
        public Vector3 rotationOffsetEuler;

        private Transform _armature;
        private Transform _source;
        private Transform _offset;

        private void Resolve()
        {
            _armature = string.IsNullOrEmpty(armaturePath) ? null : transform.Find(armaturePath);
            _source = string.IsNullOrEmpty(sourcePath) ? null : transform.Find(sourcePath);
            _offset = string.IsNullOrEmpty(offsetPath) ? null : transform.Find(offsetPath);
        }

        private void LateUpdate()
        {
            // 初回はトランスフォームを解決してから適用します。
            if (_armature == null || _source == null || _offset == null)
            {
                Resolve();
            }
            if (_armature == null || _source == null || _offset == null)
            {
                return;
            }

            // VRCRotationConstraint / VRCPositionConstraint と同じ計算です。
            // 位置側のオフセットは鎖の内側の Offset が既に持っているため足しません。
            _armature.rotation = _source.rotation * Quaternion.Euler(rotationOffsetEuler);
            _armature.position = _offset.position;
        }
    }
}
