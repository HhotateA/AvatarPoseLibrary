// Unity のエディター拡張に関する名前空間をインポート
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// カスタムコンポーネントとロジック
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using com.hhotatea.avatar_pose_library.logic;
using VRC.SDK3.Avatars.Components;

namespace com.hhotatea.avatar_pose_library.editor
{
    // AvatarPoseLibrarySettings に対するカスタムインスペクター
    [CustomEditor(typeof(AvatarPoseLibrarySettings))]
    public class AvatarPoseLibraryEditor : UnityEditor.Editor
    {
        // 対象の設定データ
        private AvatarPoseLibrarySettings poseLibrary;

        // カテゴリごとの ReorderableList
        private List<ReorderableList> categoryLists = new();
        private ReorderableList categoryReorderableList;

        // Pose に対応するサムネイルなどのキャッシュ
        private readonly Dictionary<PoseEntry, Texture2D> generatedThumbnails = new();
        private readonly Dictionary<PoseEntry, AnimationClip> lastClips = new();
        private readonly Dictionary<PoseEntry, bool> poseFoldouts = new();

        // アニメーションの種類の列挙体
        private enum AnimationType { FullLock, Movable, FaceOnly, FingerOnly, LockFeet, Custom }

        // インスペクターが有効化されたときの初期化処理
        private void OnEnable()
        {
            poseLibrary = (AvatarPoseLibrarySettings)target;
            SetupCategoryList();
        }

        // インスペクターGUIの描画処理
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Avatar Pose Library Settings", EditorStyles.boldLabel);
            poseLibrary.data.name = EditorGUILayout.TextField("Library Name", poseLibrary.data.name);
            categoryReorderableList.DoLayoutList();
        }

        // カテゴリ一覧の ReorderableList 設定
        private void SetupCategoryList()
        {
            categoryReorderableList = new ReorderableList(poseLibrary.data.categories, typeof(PoseCategory), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Pose Categories"),
                elementHeightCallback = index => GetCategoryElementHeight(index),
                drawElementCallback = (rect, index, isActive, isFocused) => DrawCategoryElement(rect, index),
                onReorderCallback = list => ResetCategoryLists()
            };
        }

        // カテゴリエレメントの高さを取得
        private float GetCategoryElementHeight(int index)
        {
            EnsurePoseList(index);
            var category = poseLibrary.data.categories[index];
            float poseListHeight = categoryLists[index].GetHeight();
            return EditorGUIUtility.singleLineHeight + 8f + Mathf.Max(EditorGUIUtility.singleLineHeight * 5f, EditorGUIUtility.singleLineHeight) + poseListHeight + 60f + 40f;
        }

        // ReorderableListのリストを再構築
        private void ResetCategoryLists()
        {
            categoryLists = Enumerable.Repeat<ReorderableList>(null, poseLibrary.data.categories.Count).ToList();
        }

        // カテゴリエレメントの描画処理
        private void DrawCategoryElement(Rect rect, int index)
        {
            var category = poseLibrary.data.categories[index];
            float spacing = 4f, lineHeight = EditorGUIUtility.singleLineHeight, y = rect.y + spacing;
            float thumbnailSize = lineHeight * 5f;
            float totalWidth = rect.width;
            float nameWidth = totalWidth - thumbnailSize - spacing;

            // カテゴリ名とサムネイル
            EditorGUI.LabelField(new Rect(rect.x, y, 100, lineHeight), "Category Name");
            category.name = EditorGUI.TextField(new Rect(rect.x + 100, y, nameWidth - 100, lineHeight), category.name);
            category.thumbnail = (Texture2D)EditorGUI.ObjectField(new Rect(rect.x + nameWidth + spacing, y, thumbnailSize, thumbnailSize), category.thumbnail, typeof(Texture2D), false);
            y += Mathf.Max(thumbnailSize, lineHeight) + spacing;

            // 一括開閉ボタン
            Rect btnRect = new Rect(rect.x, y, 200, lineHeight);
            if (GUI.Button(new Rect(btnRect.x, btnRect.y, 90f, lineHeight), "すべて展開"))
            {
                foreach (var pose in category.poses) poseFoldouts[pose] = true;
            }
            if (GUI.Button(new Rect(btnRect.x + 100f, btnRect.y, 90f, lineHeight), "すべて折りたたむ"))
            {
                foreach (var pose in category.poses) poseFoldouts[pose] = false;
            }

            y += lineHeight + spacing;

            EnsurePoseList(index);
            categoryLists[index].DoList(new Rect(rect.x, y, rect.width, categoryLists[index].GetHeight()));
            y += categoryLists[index].GetHeight() + spacing;

            // ドロップエリアの描画
            DrawPoseDropArea(new Rect(rect.x, y, rect.width, 40f), category);
        }

        // Pose リストの初期化
        private void EnsurePoseList(int index)
        {
            while (categoryLists.Count <= index) categoryLists.Add(null);
            if (categoryLists[index] != null) return;

            var category = poseLibrary.data.categories[index];
            var list = new ReorderableList(category.poses, typeof(PoseEntry), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Pose List"),
                elementHeightCallback = i => GetPoseElementHeight(category.poses[i]),
                drawElementCallback = (rect, i, isActive, isFocused) => DrawPoseElement(rect, i, category)
            };

            categoryLists[index] = list;
        }

        // Pose 要素の高さ取得
        private float GetPoseElementHeight(PoseEntry pose)
        {
            if (!poseFoldouts.TryGetValue(pose, out var expanded) || !expanded)
                return EditorGUIUtility.singleLineHeight * 1.5f;

            return EditorGUIUtility.singleLineHeight * 7f;
        }

        // Pose 要素の描画処理
        private void DrawPoseElement(Rect rect, int i, PoseCategory category)
        {
            var pose = category.poses[i];
            float lineHeight = EditorGUIUtility.singleLineHeight, spacing = 4f, y = rect.y + spacing;

            if (!poseFoldouts.ContainsKey(pose)) poseFoldouts[pose] = true;

            // 削除ボタン
            if (GUI.Button(new Rect(rect.x + rect.width - 20f, rect.y, 20f, 20f), "✖"))
            {
                category.poses.RemoveAt(i);
                generatedThumbnails.Remove(pose);
                lastClips.Remove(pose);
                poseFoldouts.Remove(pose);
                return;
            }

            // フォールドアウトで詳細表示
            poseFoldouts[pose] = EditorGUI.Foldout(new Rect(rect.x + 14f, y, rect.width - 34f, lineHeight), poseFoldouts[pose], pose.name, true);
            if (!poseFoldouts[pose]) return;
            y += lineHeight + spacing;

            float thumbnailSize = lineHeight * 4f;
            float leftWidth = thumbnailSize + spacing, rightWidth = rect.width - leftWidth - 20f, rightX = rect.x + leftWidth;

            // 自動サムネイル
            pose.autoThumbnail = EditorGUI.ToggleLeft(new Rect(rect.x, y, leftWidth, lineHeight), "Auto", pose.autoThumbnail);
            y += lineHeight + spacing;

            // サムネイル表示・編集
            var thumbRect = new Rect(rect.x, y, thumbnailSize, thumbnailSize);
            if (pose.autoThumbnail && pose.animationClip != null)
            {
                if (!lastClips.TryGetValue(pose, out var lastClip) || lastClip != pose.animationClip)
                {
                    lastClips[pose] = pose.animationClip;
                    generatedThumbnails[pose] = UpdateThumbnail(poseLibrary.gameObject, pose.animationClip);
                }
                if (generatedThumbnails.TryGetValue(pose, out var thumb) && thumb != null)
                    GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
            }
            else
            {
                pose.thumbnail = (Texture2D)EditorGUI.ObjectField(thumbRect, pose.thumbnail, typeof(Texture2D), false);
            }

            // 基本情報と設定の入力欄
            float infoY = rect.y + lineHeight + spacing;
            float fieldWidth = (rightWidth - spacing) / 2;

            pose.name = EditorGUI.TextField(new Rect(rightX, infoY, fieldWidth, lineHeight), pose.name);
            pose.animationClip = (AnimationClip)EditorGUI.ObjectField(new Rect(rightX + fieldWidth + spacing, infoY, fieldWidth, lineHeight), pose.animationClip, typeof(AnimationClip), false);
            infoY += lineHeight + spacing;

            // トラッキングタイプ選択
            var type = GetAnimationType(pose.tracking);
            var selectedType = (AnimationType)EditorGUI.EnumPopup(new Rect(rightX, infoY, rightWidth, lineHeight), "Animation Type", type);
            ApplyTrackingFromType(selectedType, pose.tracking);
            infoY += lineHeight;

            // カスタム設定トグル
            EditorGUI.BeginDisabledGroup(selectedType != AnimationType.Custom);
            DrawTrackingToggles(pose, rightX, rightWidth, infoY);
            EditorGUI.EndDisabledGroup();
        }

        // 各トラッキング項目のチェックボックス表示
        private void DrawTrackingToggles(PoseEntry pose, float rightX, float rightWidth, float infoY)
        {
            float spacing = 4f, lineHeight = EditorGUIUtility.singleLineHeight;
            float columnWidth = (rightWidth - spacing) / 2, colX1 = rightX, colX2 = rightX + columnWidth + spacing;

            pose.tracking.head = EditorGUI.ToggleLeft(new Rect(colX1, infoY, columnWidth, lineHeight), "Head", pose.tracking.head);
            pose.tracking.arm = EditorGUI.ToggleLeft(new Rect(colX2, infoY, columnWidth, lineHeight), "Arm", pose.tracking.arm);
            infoY += lineHeight;

            pose.tracking.foot = EditorGUI.ToggleLeft(new Rect(colX1, infoY, columnWidth, lineHeight), "Foot", pose.tracking.foot);
            pose.tracking.finger = EditorGUI.ToggleLeft(new Rect(colX2, infoY, columnWidth, lineHeight), "Finger", pose.tracking.finger);
            infoY += lineHeight;

            pose.tracking.locomotion = EditorGUI.ToggleLeft(new Rect(colX1, infoY, columnWidth, lineHeight), "Locomotion", pose.tracking.locomotion);
            EditorGUI.LabelField(new Rect(colX2, infoY, 90f, lineHeight), "Motion Speed:");
            pose.motionSpeed = EditorGUI.FloatField(new Rect(colX2 + 90f + spacing, infoY, 50f, lineHeight), pose.motionSpeed);
        }

        // Pose ドロップ用エリア
        private void DrawPoseDropArea(Rect rect, PoseCategory category)
        {
            float dropHeight = 30f;
            Rect dropArea = new Rect(rect.x, rect.y + 4f, rect.width, dropHeight);
            GUI.Box(dropArea, "Drop AnimationClips here to add Poses", EditorStyles.helpBox);

            Event evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.objectReferences.OfType<AnimationClip>())
                    {
                        category.poses.Add(new PoseEntry
                        {
                            name = dragged.name,
                            animationClip = dragged,
                            autoThumbnail = true,
                            tracking = new TrackingSetting()
                        });
                    }
                    GUI.changed = true;
                    evt.Use();
                }
            }
        }

        // トラッキング設定をプリセットに基づき適用
        private void ApplyTrackingFromType(AnimationType type, TrackingSetting tracking)
        {
            tracking.customSetting = (type == AnimationType.Custom);
            switch (type)
            {
                case AnimationType.FullLock:
                    tracking.head = tracking.arm = tracking.foot = tracking.finger = tracking.locomotion = true;
                    break;
                case AnimationType.Movable:
                    tracking.head = tracking.arm = tracking.foot = tracking.finger = true;
                    tracking.locomotion = false;
                    break;
                case AnimationType.FaceOnly:
                    tracking.head = false;
                    tracking.arm = tracking.foot = tracking.finger = true;
                    tracking.locomotion = false;
                    break;
                case AnimationType.FingerOnly:
                    tracking.head = tracking.arm = tracking.foot = false;
                    tracking.finger = true;
                    tracking.locomotion = false;
                    break;
                case AnimationType.LockFeet:
                    tracking.head = tracking.arm = tracking.finger = tracking.locomotion = false;
                    tracking.foot = true;
                    break;
            }
        }

        // トラッキング設定からプリセットを判別
        private AnimationType GetAnimationType(TrackingSetting t)
        {
            if (t.customSetting) return AnimationType.Custom;
            if (t.head && t.arm && t.foot && t.finger && t.locomotion) return AnimationType.FullLock;
            if (t.head && t.arm && t.foot && t.finger && !t.locomotion) return AnimationType.Movable;
            if (!t.head && t.arm && t.foot && t.finger && !t.locomotion) return AnimationType.FaceOnly;
            if (!t.head && !t.arm && !t.foot && t.finger && !t.locomotion) return AnimationType.FingerOnly;
            if (!t.head && !t.arm && t.foot && !t.finger && !t.locomotion) return AnimationType.LockFeet;
            return AnimationType.Custom;
        }

        // アニメーションのサムネイル生成
        private Texture2D UpdateThumbnail(GameObject obj, AnimationClip pose)
        {
            var avatar = obj.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar == null) return null;

            var clone = Object.Instantiate(avatar.gameObject);
            Texture2D result;
            using (var capture = new ThumbnailGenerator(clone))
            {
                result = capture.Capture(pose);
            }
            Object.DestroyImmediate(clone);
            return result;
        }
    }
}
