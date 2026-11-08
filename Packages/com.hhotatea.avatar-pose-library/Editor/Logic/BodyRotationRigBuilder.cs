using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using com.hhotatea.avatar_pose_library.editor;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.logic
{
    /// <summary>
    /// ポーズを焼き込まずに体全体を回転させる仕組み。
    ///
    /// 【従来の方式】
    /// MotionBuilder.BuildBodyRotation がクリップの RootQ（Hips の回転）へ
    /// 「回転 * RootQ」を焼き込んだ複製を作っていました。
    /// 軸ごとの段数の組み合わせ分だけクリップが増えるため、
    /// 1ポーズあたり Steps^3 * 4 本（Steps=3 で 108 本）必要でした。
    ///
    /// 【この方式】
    /// アバタールート直下に「回すだけの」Transform の鎖を作り、
    /// アーマチュア側は RotationConstraint / PositionConstraint で追従させます。
    ///
    /// 【なぜ Unity の制約なのか】
    /// Unity の RotationConstraint / PositionConstraint は Unity 自身が評価するため、
    /// エディターおよび Play モードで傾きをそのまま確認できます。
    /// アップロード時は VRChat SDK（AvatarDynamicsSetup の SubstituteConstraint）が
    /// Unity の制約を VRChat の制約へ自動変換し、対象コンポーネントを指す
    /// アニメーションのバインディングも合わせて付け替えます。
    /// このリグが動かしているのは AnimPoseBodyRotateRig 以下の Transform なので、
    /// そのパスは変換の影響を受けません。
    ///
    ///   AvatarRoot
    ///   ├─ Armature                ... 位置も親子関係も変えない（制約で追従するだけ）
    ///   │  └─ Hips
    ///   └─ AnimPoseBodyRotateRig   ... 回転の支点（Hips の位置）に置く
    ///      └─ Height (localPosition.y)  ... 高さスライダー。回転の外側に置く
    ///         └─ Y   (localRotation.y)
    ///            └─ X   (localRotation.x)
    ///               └─ Z   (localRotation.z)
    ///
    /// 【なぜアーマチュアを鎖の配下へ付け替えないのか】
    /// 付け替えると Hips 以下のボーンパスが
    /// AnimPoseBodyRotateRig/Y/X/Z/Armature/Hips/... に変わります。
    /// アニメーションクリップのバインディングはパス文字列で解決されるため、
    /// 既存のポーズクリップが全滅します（MotionBuilder は binding.path をそのまま複製します）。
    /// さらに VRCFury の ArmatureLink は Avatar アセットの m_TOS
    /// （インポート時に焼かれたパス表）から avatarObject.Find("Armature/Hips/...") で
    /// ボーンを引くため、パスが変わるとビルド自体が失敗します。
    /// 制約なら Transform の親子関係は一切変わらないので、この問題が起きません。
    ///
    /// 親から Y → X → Z の順に並べているため、合成結果は
    /// Quaternion.Euler(x, y, z)（= Ry * Rx * Rz）と一致します。
    /// 焼き込み方式と同じ角度順になるので、ラジアルの挙動は変わりません。
    ///
    /// 必要なクリップは 3 軸 * Steps 本だけで、ポーズ数に依存しません。
    /// 軸ごとに独立した Transform なので、組み合わせ爆発も起きません。
    /// </summary>
    public static class BodyRotationRigBuilder
    {
        public const string AxisXName = "X";
        public const string AxisYName = "Y";
        public const string AxisZName = "Z";

        /// <summary>
        /// リグの親子順。Quaternion.Euler の適用順（Y → X → Z）に合わせています。
        /// </summary>
        static readonly string[] AxisOrder = { AxisYName, AxisXName, AxisZName };

        /// <summary>
        /// 支点から見たアーマチュアの位置を持つ Transform の名前。
        ///
        /// PositionConstraint の translationOffset はソースの回転で回されないため、
        /// オフセット側を鎖の子にして一緒に回す必要があります。
        /// </summary>
        const string OffsetName = "Offset";

        /// <summary>
        /// 高さスライダーで上下させる Transform の名前。
        ///
        /// 回転チェーン（Y → X → Z）の外側、つまり回らない位置に置きます。
        /// ポーズクリップの Root Transform Position (Y) はアーマチュアの
        /// ローカル空間で足されるため、アーマチュアを回すと高さの向きも一緒に傾きます。
        /// 体を横倒しにしたときに高さスライダーが横方向の移動になってしまうのはそのためです。
        /// ここで動かせば、回転の有無に関わらず常にアバターの真上へ動きます。
        ///
        /// 支点（Hips）より上に置いているため、上下させると回転の支点も一緒に動きます。
        /// 支点は本来 Hips の位置なので、これは正しい挙動です。
        /// </summary>
        const string HeightName = "Height";

        /// <summary>
        /// アバターに回転リグを作成し、アーマチュアを制約で追従させます。
        /// 既に作成済みの場合は何もせず、既存のリグを返します。
        /// </summary>
        /// <param name="avatarRoot">アバターのルート</param>
        /// <returns>作成したリグのルート Transform。アーマチュアが無い場合は null</returns>
        public static Transform EnsureRig(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                throw new ArgumentNullException(nameof(avatarRoot));
            }

            var existing = avatarRoot.transform.Find(ConstVariables.BodyRotationRigRootName);
            if (existing != null)
            {
                // 既存のリグはそのまま使いますが、形が崩れている場合は作り直します。
                //
                // 壊れたリグを無条件に返すと、鎖は回るのに体が付いてこない状態になり、
                // 原因の分からない「体が回らない」という症状だけが残ります。
                // 古い方式（アーマチュアを鎖の配下へ付け替えていた頃）のリグが
                // シーンに残っている場合も、ここで検出して作り直します。
                string invalidReason;
                if (IsRigValid(existing, FindArmatureRoot(avatarRoot), out invalidReason))
                {
                    return existing;
                }

                Debug.LogWarning(
                    "AvatarPoseLibrary: Rebuilding the body rotation rig. " + invalidReason);
                // DestroyRig は旧方式のリグならアーマチュアを元の位置へ戻すため、
                // 作り直す前に一度片付けます。以降の探索はその結果を見ます。
                DestroyRig(avatarRoot);
            }

            var armature = FindArmatureRoot(avatarRoot);
            if (armature == null)
            {
                // ビルドを止めない。回転できないだけなので警告に留めます。
                Debug.LogWarning(
                    "AvatarPoseLibrary: Could not find the armature root for the body rotation rig. " +
                    "Body rotation is disabled for this avatar.");
                return null;
            }

            // 回転の支点を Hips に合わせます。
            //
            // 旧実装はクリップの RootQ（Hips の回転）を書き換えていたため、
            // 体は Hips を支点に回っていました。鎖をアバタールートの原点に置くと
            // 支点が足元になり、90 度傾けたときに体全体が弧を描いて大きく移動します。
            var hips = FindHips(avatarRoot);
            if (hips == null)
            {
                Debug.LogWarning(
                    "AvatarPoseLibrary: Could not find the Hips bone. " +
                    "The body rotation rig will pivot around the armature root instead.");
            }

            var pivotWorld = hips != null ? hips.position : armature.position;

            var rigRoot = new GameObject(ConstVariables.BodyRotationRigRootName);
            rigRoot.transform.SetParent(avatarRoot.transform, false);
            // 位置だけを支点へ移動します。回転は付けないため、
            // 子の Y → X → Z はアバターのローカル軸のまま回ります。
            rigRoot.transform.position = pivotWorld;

            // 高さスライダーは回転チェーンの外側に置きます。
            // 内側（Y の下）に置くと、回したときに高さの向きまで一緒に回り、
            // 体を横倒しにした状態でスライダーが横方向の移動になってしまいます。
            var leaf = CreateChild(rigRoot.transform, HeightName);
            foreach (var axisName in AxisOrder)
            {
                leaf = CreateChild(leaf, axisName);
            }

            // 支点から見たアーマチュアの位置を、鎖の内側に置いておきます。
            // 鎖が回るとこの点も一緒に回るので、位置制約はこれをコピーするだけで
            // 「支点を中心に回した位置」になります。
            var offset = CreateChild(leaf, OffsetName);
            offset.position = armature.position;

            AttachConstraints(armature, leaf, offset);

            return rigRoot.transform;
        }

        /// <summary>
        /// 既存のリグが、この方式で作った通りの形になっているかを調べます。
        ///
        /// 見るのは「鎖が揃っているか」と「アーマチュアがその鎖に追従しているか」の 2 点です。
        /// どちらが欠けても体は回らないため、作り直して復帰させます。
        /// </summary>
        /// <param name="rig">調べるリグのルート</param>
        /// <param name="armature">アバタールート直下のアーマチュア。解決できなければ null</param>
        /// <param name="reason">壊れていると判断した理由。警告にそのまま出します</param>
        static bool IsRigValid(Transform rig, Transform armature, out string reason)
        {
            // 鎖の途中が欠けていると、Animator のバインディングパスが解決できません。
            // 高さ用の Transform も鎖の一部なので、無ければ作り直します。
            var leaf = rig.Find(HeightName);
            if (leaf == null)
            {
                reason = $"The rig '{rig.name}' has no '{HeightName}' transform.";
                return false;
            }

            foreach (var axisName in AxisOrder)
            {
                leaf = leaf.Find(axisName);
                if (leaf == null)
                {
                    reason = $"The rig '{rig.name}' has no '{axisName}' transform.";
                    return false;
                }
            }

            var offset = leaf.Find(OffsetName);
            if (offset == null)
            {
                reason = $"The rig '{rig.name}' has no '{OffsetName}' transform.";
                return false;
            }

            if (armature == null)
            {
                reason = "The armature root could not be resolved from the rig.";
                return false;
            }

            // 旧方式のリグはアーマチュアを鎖の配下へ付け替えていました。
            // そのまま使うとボーンパスの先頭に鎖が挟まり、ポーズのクリップが解決できません。
            if (armature == rig || armature.IsChildOf(rig))
            {
                reason = $"The armature '{armature.name}' is parented under the rig.";
                return false;
            }

            // 制約が外れていると、鎖だけが回って体が付いてきません。
            var hasRotation = false;
            foreach (var constraint in armature.GetComponents<UnityEngine.Animations.RotationConstraint>())
            {
                if (!UsesLegacySource(constraint, leaf))
                {
                    continue;
                }

                // Locked が外れていると、アーマチュアが動くたびに
                // RotationOffset が今の姿勢から計算し直され、支点がずれます。
                // 見た目が静かに壊れるため、作り直しの対象にします。
                if (!constraint.locked)
                {
                    reason = $"'{armature.name}' has a RotationConstraint driven by the rig, but it is not locked.";
                    return false;
                }

                hasRotation = true;
                break;
            }

            if (!hasRotation)
            {
                reason = $"'{armature.name}' has no RotationConstraint driven by the rig.";
                return false;
            }

            var hasPosition = false;
            foreach (var constraint in armature.GetComponents<UnityEngine.Animations.PositionConstraint>())
            {
                if (!UsesLegacySource(constraint, offset))
                {
                    continue;
                }

                if (!constraint.locked)
                {
                    reason = $"'{armature.name}' has a PositionConstraint driven by the rig, but it is not locked.";
                    return false;
                }

                hasPosition = true;
                break;
            }

            if (!hasPosition)
            {
                reason = $"'{armature.name}' has no PositionConstraint driven by the rig.";
                return false;
            }

            reason = null;
            return true;
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

        /// <summary>
        /// アーマチュアを鎖へ追従させます。
        ///
        /// 鎖の末端は「支点を中心に回したときのアーマチュアの姿勢」を表すので、
        /// 回転をそのまま写せば Hips を支点にした剛体回転になります。
        /// 支点が原点と一致しないため、回転だけでは体が弧を描いてずれます。
        /// そのぶんは鎖の内側に置いた Offset（支点から見たアーマチュアの位置）で
        /// 打ち消します。Offset は鎖と一緒に回るので、位置制約はこれを写すだけです。
        ///
        /// 元から付いている制約を壊さないよう、既存コンポーネントは再利用せず
        /// 必ず新しいものを追加します（DestroyRig 側で自分のものだけ外します）。
        /// </summary>
        static void AttachConstraints(Transform armature, Transform source, Transform offset)
        {
            // 鎖はまだ無回転なので、この時点の姿勢がそのまま「無回転時の基準」になります。
            var restRotation = armature.rotation;
            var inverseSource = Quaternion.Inverse(source.rotation);

            // 同じ種類の制約は 1 オブジェクトに 1 つしか付きません
            // （DisallowMultipleComponent）。既に付いている場合は AddComponent が null を返し、
            // そのまま設定すると NullReferenceException でビルドが止まります。
            // VRCFury の ArmatureLink などが先に付けている場合があるため、再利用します。
            var rotation = GetOrAddRotationConstraint(armature);
            // Unity の制約は constraintActive が既定で false なので必ず立てます。
            rotation.constraintActive = true;
            rotation.weight = 1f;
            // 再利用した制約が無効（enabled = false）のまま残っている場合があります。
            // 無効なコンポーネントは制約を解かないため、constraintActive も weight も
            // 立っているのに鎖だけが回って体が付いてこない、という状態になります。
            // 見た目からは原因が分からないので、ここで必ず有効にします。
            rotation.enabled = true;
            // 軸の除外が残っていると、その軸だけ回り切らないため解除します。
            rotation.rotationAxis =
                UnityEngine.Animations.Axis.X |
                UnityEngine.Animations.Axis.Y |
                UnityEngine.Animations.Axis.Z;
            // target.rotation = source.rotation * offset になるので、
            // 無回転時に元の姿勢へ戻るよう offset を決めます。
            rotation.rotationOffset = (inverseSource * restRotation).eulerAngles;
            // Locked を立てないと、アーマチュアが動くたびに
            // 上のオフセットを今の姿勢から計算し直し、支点がずれます。
            // 値は必ずオフセットを書き終えてから固めます。
            rotation.locked = true;
            EnsureSingleSource(rotation, source);

            var position = GetOrAddPositionConstraint(armature);
            position.constraintActive = true;
            position.weight = 1f;
            // 回転側と同じ理由で、無効なままの制約を有効に戻します。
            // 位置が効かないと、体を傾けたときに支点から弧を描いてずれます。
            position.enabled = true;
            position.translationAxis =
                UnityEngine.Animations.Axis.X |
                UnityEngine.Animations.Axis.Y |
                UnityEngine.Animations.Axis.Z;
            // オフセットは鎖の内側で既に回っているので、ここでは何も足しません。
            position.translationOffset = Vector3.zero;
            position.locked = true;
            EnsureSingleSource(position, offset);
        }

        /// <summary>
        /// アーマチュアに付ける回転制約を取得します。
        ///
        /// RotationConstraint は 1 オブジェクトに 1 つしか付かないため、
        /// 既に付いている場合は AddComponent が null を返します。
        /// VRCFury の ArmatureLink などが先に付けている場合があるので、
        /// その場合は再利用し、設定をこのリグ向けに上書きします。
        /// </summary>
        static UnityEngine.Animations.RotationConstraint GetOrAddRotationConstraint(Transform armature)
        {
            var existing = armature.GetComponent<UnityEngine.Animations.RotationConstraint>();
            if (existing != null)
            {
                Debug.LogWarning(
                    "AvatarPoseLibrary: The armature root '" + armature.name + "' already has a " +
                    "RotationConstraint. It is reused for body rotation, so its previous sources, " +
                    "offset and excluded axes are replaced.");
                return existing;
            }

            return armature.gameObject.AddComponent<UnityEngine.Animations.RotationConstraint>();
        }

        /// <summary>
        /// アーマチュアに付ける位置制約を取得します。
        ///
        /// 体の位置は支点を中心に回るため、この制約で固定します。
        /// 既存のものを再利用する場合、ルートモーションや
        /// 「足元に合わせる」設定と競合する可能性があります。
        /// </summary>
        static UnityEngine.Animations.PositionConstraint GetOrAddPositionConstraint(Transform armature)
        {
            var existing = armature.GetComponent<UnityEngine.Animations.PositionConstraint>();
            if (existing != null)
            {
                Debug.LogWarning(
                    "AvatarPoseLibrary: The armature root '" + armature.name + "' already has a " +
                    "PositionConstraint. It is reused for body rotation, so its previous sources " +
                    "and offset are replaced. Root motion or a move-to-feet setup may be overridden.");
                return existing;
            }

            return armature.gameObject.AddComponent<UnityEngine.Animations.PositionConstraint>();
        }

        /// <summary>
        /// 制約のソースを 1 本だけにします。
        ///
        /// Sources は構造体なので、一度変数へ受けると書き換えが制約へ反映されません。
        /// 制約そのものを受け取り、フィールドを直接触ります。
        /// </summary>
        static void EnsureSingleSource(UnityEngine.Animations.RotationConstraint constraint, Transform source)
        {
            constraint.SetSources(new List<UnityEngine.Animations.ConstraintSource>
            {
                new UnityEngine.Animations.ConstraintSource
                {
                    sourceTransform = source,
                    weight = 1f,
                },
            });
        }

        static void EnsureSingleSource(UnityEngine.Animations.PositionConstraint constraint, Transform source)
        {
            constraint.SetSources(new List<UnityEngine.Animations.ConstraintSource>
            {
                new UnityEngine.Animations.ConstraintSource
                {
                    sourceTransform = source,
                    weight = 1f,
                },
            });
        }

        /// <summary>
        /// 軸名から、アバタールートを基準としたバインディングパスを返します。
        /// </summary>
        public static string BindingPath(string axisName)
        {
            var index = Array.IndexOf(AxisOrder, axisName);
            if (index < 0)
            {
                throw new ArgumentException($"Unknown body rotation axis: {axisName}", nameof(axisName));
            }

            var parts = new List<string>
            {
                ConstVariables.BodyRotationRigRootName,
                HeightName,
            };
            for (var i = 0; i <= index; i++)
            {
                parts.Add(AxisOrder[i]);
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// 高さ用 Transform のバインディングパス。
        /// 回転軸ではないため <see cref="BindingPath"/> では扱えません。
        /// </summary>
        static string HeightBindingPath =>
            $"{ConstVariables.BodyRotationRigRootName}/{HeightName}";

        /// <summary>
        /// Hips を含む、アバタールート直下の Transform を返します。
        /// 通常は Armature オブジェクトです。
        /// </summary>
        static Transform FindArmatureRoot(GameObject avatarRoot)
        {
            var hips = FindHips(avatarRoot);
            if (hips == null)
            {
                return null;
            }

            // Hips から親をたどり、アバタールート直下の Transform を探します。
            var current = hips;
            while (current.parent != null && current.parent != avatarRoot.transform)
            {
                current = current.parent;
            }

            if (current.parent != avatarRoot.transform)
            {
                return null;
            }

            return current;
        }

        /// <summary>
        /// Hips ボーンを探します。Animator から取得できない場合は名前で探索します。
        /// </summary>
        static Transform FindHips(GameObject avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (bone != null)
                {
                    return bone;
                }
            }

            foreach (var child in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == avatarRoot.transform)
                {
                    continue;
                }

                if (string.Equals(child.name, "Hips", StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// 回転専用の AnimatorController を組み立てます。
        ///
        /// ポーズ側とは別コントローラーとしてマージします。
        /// Write Defaults を必ず false で扱う必要があるため、
        /// Modular Avatar の Write Defaults 自動一致（matchAvatarWriteDefaults）を
        /// 無効にしてマージできるよう、独立させています。
        /// </summary>
        public static AnimatorController BuildAnimator(AvatarPoseData poseLibrary)
        {
            return BuildAnimator(poseLibrary, null);
        }

        /// <summary>
        /// 回転専用の AnimatorController を組み立てます。
        ///
        /// <paramref name="avatarRoot"/> を渡すと、アーマチュアの制約を常時有効に保つ
        /// カーブも一緒に焼き込みます（<see cref="AddConstraintEnableCurves"/> を参照）。
        /// </summary>
        public static AnimatorController BuildAnimator(AvatarPoseData poseLibrary, GameObject avatarRoot)
        {
            var controller = new AnimatorController();

            foreach (var axis in ConstVariables.BodyRotationAxes)
            {
                // 既定値は式パラメーター・BaseAnimator 側と必ず揃えます。
                // ずれるとレイヤー起動直後に体が片側へ傾いた状態で始まります。
                // X/Z の無回転は 0、Y の無回転は 0.5 です。
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = $"{axis.ParameterPrefix}_{poseLibrary.Guid}",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = axis.NeutralValue,
                });
            }

            // ポーズ選択中だけ回転・高さを反映するためのゲートに使います。
            // 軸レイヤーは常にこのパラメーターを参照するため、高さの有無に関係なく追加します。
            controller.AddParameter(
                $"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}",
                AnimatorControllerParameterType.Float);

            if (UsesRigHeight(poseLibrary))
            {
                // 高さスライダーもこのコントローラーで扱います。
                // ポーズ側のクリップはアーマチュアのローカル空間で高さを足すため、
                // 体を回すと高さの向きも一緒に傾いてしまいます。
                // 回らない Transform を動かせば、傾きに関係なく上下します。
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}",
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0.5f,
                });
            }

            AddLayers(controller, poseLibrary, ResolveConstraintBindingPath(avatarRoot));
            return controller;
        }

        /// <summary>
        /// FX アニメーターへ回転用レイヤーを 3 本追加します。
        /// 軸ごとに別レイヤー・別 Transform を動かすため、
        /// クリップ数は 3 * Steps 本で済みます。
        /// 高さスライダーが有効な場合は、これに高さ用のレイヤーを 1 本足します。
        /// </summary>
        public static void AddLayers(AnimatorController controller, AvatarPoseData poseLibrary)
        {
            AddLayers(controller, poseLibrary, null);
        }

        static void AddLayers(AnimatorController controller, AvatarPoseData poseLibrary, string constraintPath)
        {
            // 回転軸・角度範囲は ConstVariables.BodyRotationAxes が唯一の定義元です。
            foreach (var axis in ConstVariables.BodyRotationAxes)
            {
                AddAxisLayer(controller, poseLibrary, axis, constraintPath);
            }

            if (UsesRigHeight(poseLibrary))
            {
                AddHeightLayer(controller, poseLibrary, constraintPath);
            }
        }

        /// <summary>
        /// 高さスライダーをリグ側（回らない Transform）で動かすかどうかを返します。
        ///
        /// リグは FX アニメーターのレイヤーとして動くため、FX アニメーターが
        /// 無効なときは従来どおりポーズのクリップ側で高さを扱います。
        /// 呼び出し側は、この値とリグを作れたかどうかの両方を見てください。
        /// </summary>
        public static bool UsesRigHeight(AvatarPoseData poseLibrary)
        {
            return poseLibrary.enableHeightParam && poseLibrary.enableFxAnimator;
        }

        static void AddAxisLayer(
            AnimatorController controller,
            AvatarPoseData poseLibrary,
            ConstVariables.BodyRotationAxis axis,
            string constraintPath)
        {
            var parameter = $"{axis.ParameterPrefix}_{poseLibrary.Guid}";
            var layerName = $"{ConstVariables.BodyRotationRigLayerPrefix}_{axis.AxisName}_{poseLibrary.Guid}";

            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };
            controller.AddLayer(layer);

            // AddLayer 後の実体は controller 側にあるため、そこから引き直します。
            var layerIndex = controller.layers.Length - 1;
            var actualLayer = controller.layers[layerIndex];

            // ポーズ選択中だけ回転を反映します。未選択時は鎖を無回転（中立）に保ちます。
            var playParam = $"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}";

            var idleState = actualLayer.stateMachine.AddState($"Idle_{axis.AxisName}");
            idleState.writeDefaultValues = false;
            idleState.motion = BuildAxisClip(axis.AxisName, axis.Axis, 0f, constraintPath);
            actualLayer.stateMachine.defaultState = idleState;

            var state = actualLayer.stateMachine.AddState($"Rotate_{axis.AxisName}");
            // ポーズ側のクリップを消さないよう、Write Defaults は必ず切ります。
            state.writeDefaultValues = false;

            var blendTree = new BlendTree
            {
                name = layerName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
            };
            state.motion = blendTree;

            var enter = idleState.MakeTransition(state, false);
            enter.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = playParam } };
            var exit = state.MakeTransition(idleState, false);
            exit.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = playParam } };

            var steps = Mathf.Max(2, ConstVariables.BodyRotationSteps);
            for (var index = 0; index < steps; index++)
            {
                var rate = (float)index / (steps - 1);
                // 無回転の位置は軸ごとに違います（X/Z は左端 0%、Y は中央 50%）。
                // 角度は ConstVariables の MinAngle〜MaxAngle をそのまま等分します。
                var angle = axis.AngleAt(rate);
                blendTree.AddChild(BuildAxisClip(axis.AxisName, axis.Axis, angle, constraintPath), rate);
            }
        }

        /// <summary>
        /// 1 軸ぶんの回転だけを書き込んだクリップを作ります。
        ///
        /// 書き込む先は鎖の Transform で、ボーンではありません。
        /// アーマチュアは制約経由で追従するため、ボーンパスは一切変わりません。
        /// </summary>
        static AnimationClip BuildAxisClip(string axisName, Vector3 axis, float angle, string constraintPath)
        {
            var clip = new AnimationClip
            {
                name = $"AnimPoseBodyRotate_{axisName}_{angle:0.##}",
                frameRate = 60f,
                legacy = false,
            };

            var rotation = Quaternion.AngleAxis(angle, axis);
            var path = BindingPath(axisName);

            SetConstant(clip, path, "m_LocalRotation.x", rotation.x);
            SetConstant(clip, path, "m_LocalRotation.y", rotation.y);
            SetConstant(clip, path, "m_LocalRotation.z", rotation.z);
            SetConstant(clip, path, "m_LocalRotation.w", rotation.w);

            AddConstraintEnableCurves(clip, constraintPath);

            return clip;
        }

        /// <summary>
        /// 高さスライダー用のレイヤーを 1 本追加します。
        ///
        /// 動かすのは回転チェーンの外側にある Height の localPosition.y だけです。
        /// チェーンの内側で動かすと、体を回したときに高さの向きまで一緒に回り、
        /// 横倒しの状態でスライダーが横方向の移動になってしまいます。
        ///
        /// 向きはポーズ側の高さスライダーと揃えます（0% が最も高く、100% が最も低い）。
        ///
        /// ポーズが選ばれているかどうか（AnimPosePlay==1）で切り替え、
        /// ポーズ未選択時は Height を 0（中立）に保ちます。
        /// 高さパラメーターは保存しないため、前回の値が別ワールドで
        /// アバターを浮かせたまま表示することはありません。
        /// </summary>
        static void AddHeightLayer(AnimatorController controller, AvatarPoseData poseLibrary, string constraintPath)
        {
            var parameter = $"{ConstVariables.HeightParamPrefix}_{poseLibrary.Guid}";
            var layerName =
                $"{ConstVariables.BodyRotationRigLayerPrefix}_{HeightName}_{poseLibrary.Guid}";

            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine(),
                blendingMode = AnimatorLayerBlendingMode.Override,
            };
            controller.AddLayer(layer);

            // AddLayer 後の実体は controller 側にあるため、そこから引き直します。
            var layerIndex = controller.layers.Length - 1;
            var actualLayer = controller.layers[layerIndex];

            // ポーズ選択中だけ高さを反映します。未選択時は Height を 0 に保ちます。
            var playParam = $"{ConstVariables.OnPlayParamPrefix}_{poseLibrary.Guid}";

            var idleState = actualLayer.stateMachine.AddState("Idle_Height");
            idleState.writeDefaultValues = false;
            idleState.motion = BuildHeightClip(0f, constraintPath);
            actualLayer.stateMachine.defaultState = idleState;

            var state = actualLayer.stateMachine.AddState("Height");
            // ポーズ側のクリップを消さないよう、Write Defaults は必ず切ります。
            state.writeDefaultValues = false;

            var blendTree = new BlendTree
            {
                name = layerName,
                blendType = BlendTreeType.Simple1D,
                blendParameter = parameter,
                useAutomaticThresholds = false,
            };
            state.motion = blendTree;

            var enter = idleState.MakeTransition(state, false);
            enter.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = playParam } };
            var exit = state.MakeTransition(idleState, false);
            exit.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = playParam } };

            // ポーズ側の BuildMotionLevel と同じ幅・同じ向きで動かします。
            var range = DynamicVariables.Settings.minMaxHeight;
            blendTree.AddChild(BuildHeightClip(range, constraintPath), 0f);
            blendTree.AddChild(BuildHeightClip(-range, constraintPath), 1f);
        }

        /// <summary>
        /// Height の localPosition.y だけを書き込んだクリップを作ります。
        /// </summary>
        static AnimationClip BuildHeightClip(float level, string constraintPath)
        {
            var clip = new AnimationClip
            {
                name = $"AnimPoseHeight_{level:0.##}",
                frameRate = 60f,
                legacy = false,
            };

            SetConstant(clip, HeightBindingPath, "m_LocalPosition.y", level);

            AddConstraintEnableCurves(clip, constraintPath);

            return clip;
        }

        /// <summary>
        /// 制約の enabled を常時 1 に保つカーブを、リグのクリップへ足します。
        ///
        /// Avatar Optimizer の Trace and Optimize は、
        /// 「enabled がアニメーションされている」コンポーネントの有効/無効を
        /// 書き換えません（FindUnusedObjectsProcessor の ContainsAnimationForFloat 判定）。
        /// カーブが無いと、リグからしか参照されていないと判断された制約が
        /// enabled = false で焼かれ、鎖だけが回って体が付いてこない状態になります。
        /// 見た目からは原因が分からないため、ここで明示的に固定します。
        ///
        /// 値は制約を付けた時点の状態（有効）と同じなので、
        /// 再生結果は変わりません。書き込み先はアーマチュアです。
        /// </summary>
        static void AddConstraintEnableCurves(AnimationClip clip, string constraintPath)
        {
            if (string.IsNullOrEmpty(constraintPath))
            {
                return;
            }

            SetConstant(clip, constraintPath, typeof(UnityEngine.Animations.RotationConstraint), "m_Enabled", 1f);
            SetConstant(clip, constraintPath, typeof(UnityEngine.Animations.PositionConstraint), "m_Enabled", 1f);
        }

        /// <summary>
        /// 制約が載っている Transform のバインディングパスを返します。
        /// アーマチュアはアバタールート直下にあるため、名前がそのままパスになります。
        /// 解決できない場合は null（カーブを足さない）を返します。
        /// </summary>
        /// <summary>
        /// Play モード専用のプレビュー補助をアバタールートへ付けます。
        ///
        /// エディターの Play モードでは VRChat の制約が評価されないため、
        /// 鎖を回してもアーマチュアが追従しません。制約と同じ計算を毎フレーム
        /// 手で適用するコンポーネントを足して、傾きと高さを確認できるようにします。
        /// アップロードビルドでは呼ばれないので、配布物には含まれません。
        /// </summary>
        public static void AttachPreviewDriver(GameObject avatarRoot)
        {
            if (avatarRoot == null || !Application.isPlaying)
            {
                return;
            }

            var armature = FindArmatureRoot(avatarRoot);
            var rig = avatarRoot.transform.Find(ConstVariables.BodyRotationRigRootName);
            if (armature == null || rig == null)
            {
                return;
            }

            var source = rig.Find($"{HeightName}/{AxisYName}/{AxisXName}/{AxisZName}");
            if (source == null)
            {
                return;
            }

            var offset = source.Find(OffsetName);
            if (offset == null)
            {
                return;
            }

            var root = avatarRoot.transform;
            var driver = avatarRoot.GetComponent<com.hhotatea.avatar_pose_library.component.APLBodyRotationPreviewDriver>();
            if (driver == null)
            {
                driver = avatarRoot.AddComponent<com.hhotatea.avatar_pose_library.component.APLBodyRotationPreviewDriver>();
            }

            driver.armaturePath = UnityEditor.AnimationUtility.CalculateTransformPath(armature, root);
            driver.sourcePath = UnityEditor.AnimationUtility.CalculateTransformPath(source, root);
            driver.offsetPath = UnityEditor.AnimationUtility.CalculateTransformPath(offset, root);
            driver.rotationOffsetEuler = (Quaternion.Inverse(source.rotation) * armature.rotation).eulerAngles;
        }

        /// <summary>
        /// 制約の m_Enabled を留めるカーブを書く先になる、アーマチュアのパスを返します。
        /// </summary>
        static string ResolveConstraintBindingPath(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            var armature = FindArmatureRoot(avatarRoot);
            if (armature == null)
            {
                // カーブが入らないと Avatar Optimizer に制約を無効化され得るため、
                // 黙って続けずに理由を残します。
                Debug.LogWarning(
                    "AvatarPoseLibrary: Could not resolve the armature for the body rotation rig. " +
                    "The constraints are not pinned enabled, so an avatar optimizer may disable them.");
                return null;
            }

            return armature.name;
        }

        static void SetConstant(AnimationClip clip, string path, string property, float value)
        {
            clip.SetCurve(path, typeof(Transform), property, AnimationCurve.Constant(0f, 1f, value));
        }

        /// <summary>
        /// Transform 以外（制約コンポーネントなど）へ定数を書き込みます。
        /// </summary>
        static void SetConstant(AnimationClip clip, string path, Type type, string property, float value)
        {
            clip.SetCurve(path, type, property, AnimationCurve.Constant(0f, 1f, value));
        }

        /// <summary>
        /// 生成済みのリグを破棄し、アーマチュアに付けた制約を外します。
        /// </summary>
        public static void DestroyRig(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return;
            }

            var rig = avatarRoot.transform.Find(ConstVariables.BodyRotationRigRootName);
            if (rig == null)
            {
                return;
            }

            // 高さ用の Transform が無い旧形式のリグも外せるよう、
            // 有るときだけ 1 段深く潜ります。
            var leaf = rig.Find(HeightName);
            if (leaf == null)
            {
                leaf = rig;
            }

            foreach (var axisName in AxisOrder)
            {
                leaf = leaf.Find(axisName);
                if (leaf == null)
                {
                    break;
                }
            }

            if (leaf != null)
            {
                var offset = leaf.Find(OffsetName);
                DetachConstraints(avatarRoot, leaf, offset);
                // 旧バージョンがアーマチュアに残した Unity の制約も、ここで一緒に片付けます。
                DetachLegacyUnityConstraints(avatarRoot, leaf, offset);

                // 旧方式（アーマチュアを鎖の配下へ付け替えていた頃）のリグが
                // 残っている場合に備えて、子を元の位置へ戻します。
                // 新しい方式の Offset は鎖の一部なので対象外です。
                for (var i = leaf.childCount - 1; i >= 0; i--)
                {
                    var child = leaf.GetChild(i);
                    if (child.name == OffsetName)
                    {
                        continue;
                    }

                    child.SetParent(avatarRoot.transform, true);
                }
            }
            else
            {
                // 鎖が途中で欠けていると、上の走査では制約にたどり着けません。
                // 参照先を失った制約はアーマチュア側に残り続けるため、ここで外します。
                // 作り直すたびに増えていくのを防ぐのが目的です。
                var armature = FindArmatureRoot(avatarRoot);
                if (armature != null)
                {
                    RemoveDanglingConstraints(armature.gameObject);
                }
            }

            UnityEngine.Object.DestroyImmediate(rig.gameObject);
        }

        /// <summary>
        /// 参照先が破棄された制約を外します。
        ///
        /// ソースが 1 本も残っていない制約は何も動かせないため、外しても挙動は変わりません。
        /// ソースが生きている制約には触れません。
        /// </summary>
        static void RemoveDanglingConstraints(GameObject target)
        {
            foreach (var constraint in target.GetComponents<VRCRotationConstraint>())
            {
                if (IsDangling(constraint.Sources.Count, i => constraint.Sources[i].SourceTransform))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }

            foreach (var constraint in target.GetComponents<VRCPositionConstraint>())
            {
                if (IsDangling(constraint.Sources.Count, i => constraint.Sources[i].SourceTransform))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }

            // 旧バージョンの Unity の制約も、参照先を失っていれば同じように片付けます。
            foreach (var constraint in target.GetComponents<UnityEngine.Animations.RotationConstraint>())
            {
                if (IsDangling(constraint.sourceCount, i => constraint.GetSource(i).sourceTransform))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }

            foreach (var constraint in target.GetComponents<UnityEngine.Animations.PositionConstraint>())
            {
                if (IsDangling(constraint.sourceCount, i => constraint.GetSource(i).sourceTransform))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }
        }

        static bool IsDangling(int sourceCount, Func<int, Transform> getSource)
        {
            if (sourceCount == 0)
            {
                return false;
            }

            for (var i = 0; i < sourceCount; i++)
            {
                if (getSource(i) != null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// このリグを参照している制約だけを外します。
        /// アバターが元から持っている制約には触れません。
        /// </summary>
        static void DetachConstraints(GameObject avatarRoot, Transform source, Transform offset)
        {
            foreach (var constraint in avatarRoot.GetComponentsInChildren<VRCRotationConstraint>(true))
            {
                if (UsesSource(constraint, source) || (offset != null && UsesSource(constraint, offset)))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }

            foreach (var constraint in avatarRoot.GetComponentsInChildren<VRCPositionConstraint>(true))
            {
                if (UsesSource(constraint, source) || (offset != null && UsesSource(constraint, offset)))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }
        }

        /// <summary>
        /// このリグが使う Unity の制約（RotationConstraint / PositionConstraint）を外します。
        ///
        /// 以前の実装は UnityEngine.Animations.RotationConstraint / PositionConstraint を
        /// アーマチュアに付けていました。これを残したままアップロードすると
        /// PC 以外のプラットフォームで失敗するうえ、参照先の鎖を消すと
        /// 何も動かせない置物がアーマチュアに残り続けます。
        /// アップデート後に一度だけ通る掃除経路として、ここで外します。
        ///
        /// 型を完全修飾しているのは、これが「もう作らない種類」であることを
        /// 読んでいて分かるようにするためです。
        /// </summary>
        static void DetachLegacyUnityConstraints(GameObject avatarRoot, Transform source, Transform offset)
        {
            foreach (var constraint in avatarRoot.GetComponentsInChildren<UnityEngine.Animations.RotationConstraint>(true))
            {
                if (UsesLegacySource(constraint, source) || (offset != null && UsesLegacySource(constraint, offset)))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }

            foreach (var constraint in avatarRoot.GetComponentsInChildren<UnityEngine.Animations.PositionConstraint>(true))
            {
                if (UsesLegacySource(constraint, source) || (offset != null && UsesLegacySource(constraint, offset)))
                {
                    UnityEngine.Object.DestroyImmediate(constraint);
                }
            }
        }

        static bool UsesLegacySource(UnityEngine.Animations.RotationConstraint constraint, Transform source)
        {
            for (var i = 0; i < constraint.sourceCount; i++)
            {
                if (constraint.GetSource(i).sourceTransform == source)
                {
                    return true;
                }
            }

            return false;
        }

        static bool UsesLegacySource(UnityEngine.Animations.PositionConstraint constraint, Transform source)
        {
            for (var i = 0; i < constraint.sourceCount; i++)
            {
                if (constraint.GetSource(i).sourceTransform == source)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 制約が指定した Transform をソースに使っているかを調べます。
        ///
        /// 回転制約と位置制約は共通の基底クラスを持つため、1 本で両方を扱えます。
        /// </summary>
        static bool UsesSource(VRCConstraintBase constraint, Transform source)
        {
            for (var i = 0; i < constraint.Sources.Count; i++)
            {
                if (constraint.Sources[i].SourceTransform == source)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
