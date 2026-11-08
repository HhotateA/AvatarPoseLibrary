using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using com.hhotatea.avatar_pose_library.component;

namespace com.hhotatea.avatar_pose_library.editor
{
    /// <summary>
    /// VRCFury の Fix Write Defaults が回転リグのレイヤーを書き換えるのを防ぐ補助。
    ///
    /// 【何が起きるか】
    /// BodyRotationRigBuilder は回転用の 4 ステート
    /// （AnimPoseBodyRotateRig_{Y,X,Z,Height}）を writeDefaultValues = false で作ります。
    /// これはリグのレイヤーが、ポーズ側のレイヤーが出した値を消さないために
    /// 必ず必要な設定です。
    ///
    /// ところが VRCFury の Fix Write Defaults は mode = Auto のとき
    /// applyToUnmanagedLayers = true で動き、VRCFury 自身のレイヤーだけでなく
    /// コントローラー内の全レイヤーを走査して Write Defaults を書き換えます。
    /// そのため APL が false で作った 4 ステートも true にされ、
    /// リグのレイヤーがポーズ側の出力を既定値へ戻してしまいます。
    /// 結果としてリグもクリップも正しいのに、体だけが回らなくなります。
    ///
    /// 【なぜリグ側ではなくここで直すのか】
    /// リグ側を Write Defaults に依存しない形へ作り替えることはできません。
    /// true にされたレイヤーはポーズ側の値を消してしまうため、
    /// 「どちらでも動く」書き方が存在しないからです。書き換え自体を止めます。
    ///
    /// 【なぜ Disabled なのか】
    ///   Auto     … 全レイヤーを書き換える（この問題の原因）
    ///   ForceOff … 全レイヤーを false にする。ベースが true 前提のとき壊れる
    ///   ForceOn  … 全レイヤーを true にする。この問題そのもの
    ///   Disabled … VRCFury は何もしない。APL の false がそのまま残る
    ///
    /// ベースの Write Defaults が混在している場合、Disabled はその混在を
    /// 直さずに残す点に注意してください。
    /// </summary>
    public static class VRCFuryWriteDefaultsGuard
    {
        // VRCFury の型は internal なので、コンパイル時に参照できません。
        // 型名で照合し、SerializedObject 経由でのみ読み書きします。
        const string VRCFuryTypeName = "VF.Model.VRCFury";
        const string FixWriteDefaultsTypeName = "FixWriteDefaults";

        const string AutoModeName = "Auto";
        const string DisabledModeName = "Disabled";

        const string FixMenuPath =
            "Tools/Avatar Pose Library/Fix VRCFury Write Defaults";
        const string CheckMenuPath =
            "Tools/Avatar Pose Library/Check VRCFury Write Defaults";

        const string DialogTitle = "AvatarPoseLibrary";

        /// <summary>
        /// 開いているシーンの中で、APL を使っていて Auto のままの
        /// Fix Write Defaults をまとめて Disabled にします。
        /// </summary>
        [MenuItem(FixMenuPath, false, 210)]
        static void FixAll()
        {
            var targets = new List<Entry>();
            foreach (var entry in CollectEntries())
            {
                // APL を使っていないアバターの設定は、勝手に変えません。
                if (!HasAplComponent(entry.Component))
                {
                    continue;
                }

                if (ModeName(entry.Mode) == AutoModeName)
                {
                    targets.Add(entry);
                }
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "No AvatarPoseLibrary avatar has a VRCFury 'Fix Write Defaults' "
                    + "set to Auto.\n\nNothing to fix.",
                    "OK");
                return;
            }

            var message = new StringBuilder();
            message.AppendLine(
                $"{targets.Count} VRCFury 'Fix Write Defaults' component(s) on "
                + "AvatarPoseLibrary avatars are set to Auto.");
            message.AppendLine();
            message.AppendLine(
                "Auto rewrites the Write Defaults flag of every layer, including "
                + "AvatarPoseLibrary's body rotation rig. That makes the body stop rotating.");
            message.AppendLine();
            message.AppendLine("Switch them to 'Disabled'?");

            if (!EditorUtility.DisplayDialog(
                    DialogTitle, message.ToString(), "Fix", "Cancel"))
            {
                return;
            }

            var disabledIndex = Array.IndexOf(
                targets[0].Mode.enumNames, DisabledModeName);
            if (disabledIndex < 0)
            {
                Debug.LogError(
                    "AvatarPoseLibrary: VRCFury's Fix Write Defaults has no "
                    + "'Disabled' mode. The VRCFury version may have changed.");
                return;
            }

            var fixedCount = 0;
            foreach (var entry in targets)
            {
                Undo.RecordObject(
                    entry.Component, "Disable VRCFury Fix Write Defaults");
                entry.Mode.enumValueIndex = disabledIndex;
                if (!entry.Serialized.ApplyModifiedProperties())
                {
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(entry.Component.gameObject.scene);
                Debug.Log(
                    "AvatarPoseLibrary: Set VRCFury 'Fix Write Defaults' to Disabled on '"
                    + GetPath(entry.Component.gameObject) + "'.",
                    entry.Component);
                fixedCount++;
            }

            Debug.Log(
                $"AvatarPoseLibrary: Disabled VRCFury 'Fix Write Defaults' on "
                + $"{fixedCount} avatar(s). Save the scene(s) to keep the change.");
            EditorUtility.DisplayDialog(
                DialogTitle,
                $"Fixed {fixedCount} avatar(s).\n\nSave the scene(s) to keep the change.",
                "OK");
        }

        /// <summary>
        /// 読み取り専用の点検。シーンには一切書き込みません。
        /// どのアバターが Auto のまま残っているかを確認するために使います。
        /// </summary>
        [MenuItem(CheckMenuPath, false, 211)]
        static void CheckAll()
        {
            var report = new StringBuilder();
            var avatarCount = 0;
            var problemCount = 0;

            foreach (var entry in CollectEntries())
            {
                if (!HasAplComponent(entry.Component))
                {
                    continue;
                }

                avatarCount++;
                var modeName = ModeName(entry.Mode);
                var line = GetPath(entry.Component.gameObject)
                    + ": Fix Write Defaults = " + modeName;
                if (modeName == AutoModeName)
                {
                    problemCount++;
                    line += "   <-- body rotation will not work";
                }

                report.AppendLine(line);
            }

            if (avatarCount == 0)
            {
                EditorUtility.DisplayDialog(
                    DialogTitle,
                    "No AvatarPoseLibrary avatar with a VRCFury 'Fix Write Defaults' "
                    + "was found in the open scenes.",
                    "OK");
                return;
            }

            Debug.Log(
                "AvatarPoseLibrary: VRCFury Fix Write Defaults report\n"
                + report.ToString().TrimEnd());

            var summary = problemCount == 0
                ? $"{avatarCount} avatar(s) checked. All are safe."
                : $"{avatarCount} avatar(s) checked. {problemCount} will not rotate.\n\n"
                  + "Run 'Tools/Avatar Pose Library/Fix VRCFury Write Defaults' to repair them.\n\n"
                  + "Details are in the Console.";
            EditorUtility.DisplayDialog(DialogTitle, summary, "OK");
        }

        /// <summary>
        /// ビルド時に、まだ Auto のままの Fix Write Defaults があれば警告します。
        ///
        /// VRCFury は APL より後に走るため、書き換えそのものをここで観測することは
        /// できません。見られるのは「Auto のまま残っているか」だけですが、
        /// Auto は必ず全レイヤーを書き換えるので、それで十分に予測できます。
        ///
        /// ビルドは止めません。回転しないだけで、アバター自体は壊れていないためです。
        /// </summary>
        public static void WarnIfConflict(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return;
            }

            foreach (var component in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null
                    || component.GetType().FullName != VRCFuryTypeName)
                {
                    continue;
                }

                if (!TryReadMode(component, out _, out var mode)
                    || ModeName(mode) != AutoModeName)
                {
                    continue;
                }

                Debug.LogWarning(
                    "AvatarPoseLibrary: '" + GetPath(component.gameObject)
                    + "' has VRCFury 'Fix Write Defaults' set to Auto. It rewrites the "
                    + "Write Defaults flag of AvatarPoseLibrary's body rotation rig, so the "
                    + "body will not rotate. Set it to Disabled with "
                    + "'Tools/Avatar Pose Library/Fix VRCFury Write Defaults'.",
                    component);
            }
        }

        sealed class Entry
        {
            public Component Component;
            public SerializedObject Serialized;
            public SerializedProperty Mode;
        }

        /// <summary>
        /// 開いているシーンから、Fix Write Defaults を持つ VRCFury を集めます。
        /// アセットのプレハブは対象外です（シーン上の実体だけを見ます）。
        /// </summary>
        static List<Entry> CollectEntries()
        {
            var entries = new List<Entry>();

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null
                            || component.GetType().FullName != VRCFuryTypeName)
                        {
                            continue;
                        }

                        if (!TryReadMode(component, out var serialized, out var mode))
                        {
                            continue;
                        }

                        entries.Add(new Entry
                        {
                            Component = component,
                            Serialized = serialized,
                            Mode = mode,
                        });
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// VRCFury コンポーネントから mode プロパティを取り出します。
        ///
        /// content は [SerializeReference] なので、機能の型名を確認してから
        /// 中へ潜ります。Fix Write Defaults 以外の機能には触りません。
        /// </summary>
        static bool TryReadMode(
            Component component,
            out SerializedObject serialized,
            out SerializedProperty mode)
        {
            serialized = null;
            mode = null;
            if (component == null)
            {
                return false;
            }

            var target = new SerializedObject(component);
            var content = target.FindProperty("content");

            if (content != null
                && content.propertyType == SerializedPropertyType.ManagedReference)
            {
                var typeName = content.managedReferenceFullTypename ?? string.Empty;
                if (!typeName.Contains(FixWriteDefaultsTypeName))
                {
                    // 別の機能が入っているコンポーネントです。
                    return false;
                }

                mode = content.FindPropertyRelative("mode")
                    ?? target.FindProperty("content.mode");
            }
            else if (content != null)
            {
                // SerializeReference として解決できなかった場合の保険。
                // 列挙名で Fix Write Defaults かどうかを判定します。
                mode = target.FindProperty("content.mode");
            }

            if (mode == null || mode.propertyType != SerializedPropertyType.Enum)
            {
                return false;
            }

            // 列挙名に Disabled が無いものは Fix Write Defaults ではありません。
            if (Array.IndexOf(mode.enumNames, DisabledModeName) < 0)
            {
                return false;
            }

            serialized = target;
            return true;
        }

        static string ModeName(SerializedProperty mode)
        {
            var names = mode.enumNames;
            var index = mode.enumValueIndex;
            if (names == null || index < 0 || index >= names.Length)
            {
                return "Unknown(" + mode.intValue + ")";
            }

            return names[index];
        }

        /// <summary>
        /// この VRCFury が、APL を使っているアバターのものかを調べます。
        /// 無関係なアバターの設定を変えないための歯止めです。
        /// </summary>
        static bool HasAplComponent(Component vrcFury)
        {
            if (vrcFury == null)
            {
                return false;
            }

            var go = vrcFury.gameObject;
            if (go.GetComponentInChildren<AvatarPoseLibrary>(true) != null)
            {
                return true;
            }

            return go.GetComponentInParent<AvatarPoseLibrary>(true) != null;
        }

        static string GetPath(GameObject go)
        {
            var parts = new List<string>();
            for (var current = go.transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
