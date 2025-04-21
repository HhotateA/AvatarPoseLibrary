// Unity のエディター拡張に関する名前空間をインポート

using System;
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
using Object = UnityEngine.Object;

namespace com.hhotatea.avatar_pose_library.editor
{
    // AvatarPoseLibrarySettings に対するカスタムインスペクター
    [CustomEditor(typeof(AvatarPoseLibrarySettings))]
    public class AvatarPoseLibraryEditor : UnityEditor.Editor
    {
        // 対象の設定データ
        private AvatarPoseLibrarySettings poseLibrary;
        private AvatarPoseData data => poseLibrary.Data;

        // カテゴリごとの ReorderableList
        private ReorderableList categoryReorderableList;
        private Dictionary<PoseCategory,ReorderableList> poseReorderableLists = new Dictionary<PoseCategory, ReorderableList>();

        // Pose に対応するサムネイルなどのキャッシュ
        private readonly Dictionary<PoseEntry, Texture2D> generatedThumbnails = new();
        private readonly Dictionary<PoseEntry, AnimationClip> lastClips = new();
        private readonly Dictionary<PoseEntry, bool> poseFoldouts = new();
        
        // 固定値
        private const float textboxWidth = 350f;
        private float lineHeight = EditorGUIUtility.singleLineHeight;
        private float spacing = 4f;
        
        GUIContent libraryLabelContext = new GUIContent(DynamicVariables.Settings.Context.libraryMenuLabel, DynamicVariables.Settings.Context.libraryMenuTooltip);
        GUIContent categoryListContext = new GUIContent(DynamicVariables.Settings.Context.categoriesLabel, DynamicVariables.Settings.Context.categoriesTooltip);
        GUIContent categoryIconContext = new GUIContent(DynamicVariables.Settings.Context.categoryIconLabel, DynamicVariables.Settings.Context.categoryIconTooltip);
        GUIContent categoryTextContext = new GUIContent(DynamicVariables.Settings.Context.categoryTextLabel, DynamicVariables.Settings.Context.categoryTextTooltip);
        GUIContent openAllContext = new GUIContent(DynamicVariables.Settings.Context.openAllLabel, DynamicVariables.Settings.Context.openAllTooltip);
        GUIContent closeAllContext = new GUIContent(DynamicVariables.Settings.Context.closeAllLabel, DynamicVariables.Settings.Context.closeAllTooltip);
        GUIContent poseListContext = new GUIContent(DynamicVariables.Settings.Context.poseListLabel, DynamicVariables.Settings.Context.poseListTooltip);
        GUIContent openButtonContext = new GUIContent(DynamicVariables.Settings.Context.openLabel, DynamicVariables.Settings.Context.openTooltip);
        GUIContent closeButtonContext = new GUIContent(DynamicVariables.Settings.Context.closeLabel, DynamicVariables.Settings.Context.closeTooltip);
        GUIContent thumbnailAutoContext = new GUIContent(DynamicVariables.Settings.Context.thumbnailAutoLabel, DynamicVariables.Settings.Context.thumbnailAutoTooltip);
        GUIContent animationClipContext = new GUIContent(DynamicVariables.Settings.Context.animationClipLabel, DynamicVariables.Settings.Context.animationClipTooltip);
        GUIContent trackingSettingsContext = new GUIContent(DynamicVariables.Settings.Context.trackingSettingsLabel, DynamicVariables.Settings.Context.trackingSettingsTooltip);
        GUIContent isLoopContext = new GUIContent(DynamicVariables.Settings.Context.isLoopLabel, DynamicVariables.Settings.Context.isLoopTooltip);
        GUIContent motionSpeedContext = new GUIContent(DynamicVariables.Settings.Context.motionSpeedLabel, DynamicVariables.Settings.Context.motionSpeedTooltip);
        GUIContent dropboxContext = new GUIContent(DynamicVariables.Settings.Context.dropboxLabel, DynamicVariables.Settings.Context.dropboxTooltip);
        
        string[] trackingOptions = new string[]
        {
            DynamicVariables.Settings.Context.headTrackingOption, 
            DynamicVariables.Settings.Context.armTrackingOption,
            DynamicVariables.Settings.Context.fingerTrackingOption,
            DynamicVariables.Settings.Context.footTrackingOption,
            DynamicVariables.Settings.Context.locomotionTrackingOption,
        };
        
        // インスペクターが有効化されたときの初期化処理
        private void OnEnable()
        {
            poseLibrary = (AvatarPoseLibrarySettings)target;
            
            InitializeData();
            SetupCategoryList();
        }

        // インスペクターGUIの描画処理
        public override void OnInspectorGUI()
        {
            InitializeData();
            
            // ここから描画開始
            float texSize = lineHeight * 6f;
            EditorGUILayout.LabelField("Avatar Pose Library Settings", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    new GUIContent(data.thumbnail,DynamicVariables.Settings.Context.mainThumbnailTooltip), 
                    GUILayout.Width(texSize), GUILayout.Height(texSize));
                // EditorGUILayout.Space();
                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(libraryLabelContext, EditorStyles.label);
                    EditorGUILayout.Space();
                    data.name = EditorGUILayout.TextField(data.name,GUILayout.MaxWidth(textboxWidth));
                    EditorGUILayout.Space();
                }
            }
            EditorGUILayout.Space(15f);
            categoryReorderableList.DoLayoutList();
        }

        void InitializeData()
        {
            if (poseLibrary.Data != null) return;
            
            // 初期化処理
            var avatarPoseData = new AvatarPoseData();
            avatarPoseData.name = DynamicVariables.Settings.Menu.main.title;
            avatarPoseData.thumbnail = DynamicVariables.Settings.Menu.main.thumbnail;
            poseLibrary.Data = avatarPoseData;
            SetupCategoryList();
        }

        // カテゴリ一覧の ReorderableList 設定
        private void SetupCategoryList()
        {
            categoryReorderableList = new ReorderableList(data.categories, typeof(PoseCategory), true, true, true, true)
            {
                drawHeaderCallback = r => EditorGUI.LabelField(r, categoryListContext),
                elementHeightCallback = i => GetCategoryElementHeight(i),
                drawElementCallback = (r, i, isActive, isFocused) => DrawCategoryElement(r, i),
                onAddCallback = l => data.categories.Add(CreateCategory()),
                onRemoveCallback = l => data.categories.RemoveAt(l.index)
            };
        }

        // カテゴリエレメントの高さを取得
        private float GetCategoryElementHeight(int index)
        {
            var list = EnsurePoseList(data.categories[index]);
            float poseListHeight = list.GetHeight();
            return EditorGUIUtility.singleLineHeight + 8f + Mathf.Max(EditorGUIUtility.singleLineHeight * 5f, EditorGUIUtility.singleLineHeight) + poseListHeight + 60f;
        }

        // カテゴリエレメントの描画処理
        private void DrawCategoryElement(Rect rect, int index)
        {
            var category = data.categories[index];
            float y = rect.y + spacing;
            float thumbnailSize = lineHeight * 5f;
            float nameWidth = rect.width - spacing;

            // カテゴリ名とサムネイル
            var thumbRect = new Rect(rect.x + spacing, y, thumbnailSize, thumbnailSize);
            category.thumbnail = (Texture2D)EditorGUI.ObjectField( thumbRect, categoryIconContext, category.thumbnail, typeof(Texture2D), false);
            GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Context.categoryThumbnailTooltip), GUIStyle.none);
            EditorGUI.LabelField(new Rect(rect.x + spacing * 2f + thumbnailSize, y + lineHeight, 100, lineHeight), categoryTextContext);
            category.name = EditorGUI.TextField(new Rect(rect.x + spacing * 2f + thumbnailSize, y + lineHeight*3f, 
                Mathf.Min(textboxWidth, nameWidth - thumbnailSize - 15f), lineHeight), category.name);
            y += Mathf.Max(thumbnailSize, lineHeight) + spacing;

            // 一括開閉ボタン
            Rect btnRect = new Rect(rect.x, y, 200, lineHeight);
            var buttonWidth = Mathf.Max(GUI.skin.button.CalcSize(openAllContext).x,GUI.skin.button.CalcSize(closeAllContext).x) + 5f;
            if (GUI.Button(new Rect( btnRect.x + rect.width - buttonWidth*2f - 10f, btnRect.y, buttonWidth, lineHeight), openAllContext))
            {
                foreach (var pose in category.poses) poseFoldouts[pose] = true;
            }
            if (GUI.Button(new Rect( btnRect.x + rect.width - buttonWidth - 5f, btnRect.y, buttonWidth, lineHeight), closeAllContext))
            {
                foreach (var pose in category.poses) poseFoldouts[pose] = false;
            }

            y += lineHeight + spacing;

            var list = EnsurePoseList(data.categories[index]);
            list.DoList(new Rect(rect.x, y, rect.width, list.GetHeight()));
            y += list.GetHeight() + spacing;

            // ドロップエリアの描画
            DrawPoseDropArea(new Rect(rect.x, y, rect.width, 40f), category);
        }

        // Pose リストの初期化
        private ReorderableList EnsurePoseList(PoseCategory category)
        {
            poseReorderableLists.TryGetValue(category,out var list);
            if (list != null) return poseReorderableLists[category];

            list = new ReorderableList(category.poses, typeof(PoseEntry), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, poseListContext),
                elementHeightCallback = i => GetPoseElementHeight(category.poses[i]),
                drawElementCallback = (rect, i, isActive, isFocused) => DrawPoseElement(rect, i, category),
                onAddCallback = l => category.poses.Add(CreatePose(null)),
                onRemoveCallback = l => category.poses.RemoveAt(l.index)
            };

            poseReorderableLists[category] = list;
            return list;
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
            float y = rect.y + 2f;
            
            // Labelの表示（Open時にはInputFieldにする）
            poseFoldouts.TryAdd(pose, false);
            var buttonWidth = Mathf.Max(GUI.skin.button.CalcSize(closeButtonContext).x,GUI.skin.button.CalcSize(openButtonContext).x) + 2f;
            if (poseFoldouts[pose])
            {
                pose.name = GUI.TextField(new Rect(rect.x + 10f, y, Mathf.Min(textboxWidth,rect.width - 60f), lineHeight),pose.name);
                if (GUI.Button(new Rect(rect.x + rect.width - buttonWidth, y, buttonWidth, 20f), closeButtonContext))
                {
                    poseFoldouts[pose] = false;
                }
            }
            else
            {
                GUI.Label(new Rect(rect.x + 10f, y, rect.width - 60f, lineHeight),pose.name);
                if (GUI.Button(new Rect(rect.x + rect.width - buttonWidth, y, buttonWidth, 20f), openButtonContext))
                {
                    poseFoldouts[pose] = true;
                }
                return;
            }
            
            y += lineHeight + spacing + 4f;

            float thumbnailSize = lineHeight * 4f;
            float leftWidth = thumbnailSize + spacing;
            float rightWidth = rect.width - leftWidth - spacing * 3f;;
            float rightX = rect.x + leftWidth + spacing * 2f;

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
                {
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Context.thumbnailBg, ScaleMode.StretchToFill, false);
                    thumbRect = Rect.MinMaxRect(
                        thumbRect.xMin + 1f,
                        thumbRect.yMin + 1f,
                        thumbRect.xMax - 1f,
                        thumbRect.yMax - 1f
                    );
                    GUI.DrawTexture(thumbRect, thumb, ScaleMode.StretchToFill, true);
                    GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Context.posePreviewTooltip), GUIStyle.none);
                }
            }
            else
            {
                pose.thumbnail = (Texture2D)EditorGUI.ObjectField(thumbRect, pose.thumbnail, typeof(Texture2D), false);
                GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Context.poseThumbnailTooltip), GUIStyle.none);
            }
            pose.autoThumbnail = EditorGUI.ToggleLeft(new Rect(rect.x, y + thumbnailSize + spacing, leftWidth, lineHeight), thumbnailAutoContext, pose.autoThumbnail);
            
            // 縦線を引く
            GUI.Box(new Rect(rightX - spacing, y, 1f, thumbnailSize + lineHeight),"");

            // 基本情報と設定の入力欄
            float infoY = rect.y + lineHeight + spacing + 4f;
            float fieldWidth = (rightWidth - spacing);

            var animationClip = (AnimationClip)EditorGUI.ObjectField(
                new Rect(rightX, infoY, fieldWidth, lineHeight), 
                animationClipContext, pose.animationClip, typeof(AnimationClip), false);
            if (pose.animationClip != animationClip)
            {
                ChangePoseAnimation(pose, animationClip);
            }
            infoY += lineHeight + spacing;

            // トラッキングタイプ選択
            var flags = GetAnimationType(pose.tracking);
            flags = EditorGUI.MaskField(new Rect(rightX, infoY, rightWidth, lineHeight), trackingSettingsContext, flags, trackingOptions);
            ApplyTrackingFromType(flags,pose.tracking);
            
            // 横線を引く
            infoY += lineHeight + lineHeight/2f + spacing;
            GUI.Box(new Rect(rightX, infoY, rightWidth, 1f),"");
            infoY += lineHeight/2f;
            
            pose.tracking.loop = EditorGUI.Toggle(new Rect(rightX, infoY, rightWidth, lineHeight), isLoopContext, pose.tracking.loop);
            infoY += lineHeight;
            pose.tracking.motionSpeed = EditorGUI.FloatField(new Rect(rightX, infoY, rightWidth, lineHeight), motionSpeedContext, pose.tracking.motionSpeed);
        }

        // Pose ドロップ用エリア
        private void DrawPoseDropArea(Rect rect, PoseCategory category)
        {
            float dropHeight = 30f;
            Rect dropArea = new Rect(rect.x, rect.y + 4f, rect.width, dropHeight);
            GUI.Box(dropArea, dropboxContext, EditorStyles.helpBox);

            Event evt = Event.current;
            if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.objectReferences.OfType<AnimationClip>())
                    {
                        category.poses.Add(CreatePose(dragged));
                    }
                    GUI.changed = true;
                    evt.Use();
                }
            }
        }

        private PoseCategory CreateCategory()
        {
            var result = new PoseCategory
            {
                name = DynamicVariables.Settings.Menu.category.title,
                thumbnail = DynamicVariables.Settings.Menu.category.thumbnail,
                poses = new List<PoseEntry>()
            };
            Repaint();

            return result;
        }

        private PoseEntry CreatePose(AnimationClip clip)
        {
            var result = new PoseEntry
            {
                name = DynamicVariables.Settings.Menu.pose.title,
                thumbnail = DynamicVariables.Settings.Menu.pose.thumbnail,
                autoThumbnail = true,
                tracking = new TrackingSetting()
            };

            ChangePoseAnimation(result, clip);

            return result;
        }

        void ChangePoseAnimation(PoseEntry pose, AnimationClip anim)
        {
            pose.animationClip = anim;
            if(anim==null) return;
            pose.name = anim.name;
            if (MotionBuilder.IsMoveAnimation(anim))
            {
                pose.tracking.motionSpeed = 1f;
                pose.tracking.loop = MotionBuilder.IsLoopAnimation(anim) ? true : false;
            }
            else
            {
                pose.tracking.motionSpeed = 0f;
                pose.tracking.loop = true;
            }
        }

        // トラッキング設定をプリセットに基づき適用
        private void ApplyTrackingFromType(int flag, TrackingSetting tracking)
        {
            tracking.head = Convert.ToBoolean(flag & (1 << 0));
            tracking.arm = Convert.ToBoolean(flag & (1 << 1));
            tracking.finger = Convert.ToBoolean(flag & (1 << 2));
            tracking.foot = Convert.ToBoolean(flag & (1 << 3));
            tracking.locomotion = Convert.ToBoolean(flag & (1 << 4));
        }

        // トラッキング設定からプリセットを判別
        private int GetAnimationType(TrackingSetting t)
        {
            int flag = 0;
            if (t.head) flag |= (1 << 0);
            if (t.arm) flag |= (1 << 1);
            if (t.finger) flag |= (1 << 2);
            if (t.foot) flag |= (1 << 3);
            if (t.locomotion) flag |= (1 << 4);
            return flag;
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
