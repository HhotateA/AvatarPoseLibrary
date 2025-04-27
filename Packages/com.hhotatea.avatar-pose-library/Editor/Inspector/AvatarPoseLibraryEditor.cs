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
    [CustomEditor(typeof(AvatarPoseLibrary))]
    public class AvatarPoseLibraryEditor : Editor
    {
        #region Variant
        
        // 対象の設定データ
        private AvatarPoseLibrary poseLibrary;
        private AvatarPoseData data => poseLibrary.data;

        // カテゴリごとの ReorderableList
        private ReorderableList categoryReorderableList;
        private Dictionary<PoseCategory,ReorderableList> poseReorderableLists = new Dictionary<PoseCategory, ReorderableList>();
        private string[] libraryTagList;
        private int libraryTagIndex;

        // Pose に対応するサムネイルなどのキャッシュ
        private readonly Dictionary<PoseEntry, Texture2D> generatedThumbnails = new();
        private readonly Dictionary<PoseEntry, AnimationClip> lastClips = new();
        private readonly Dictionary<PoseEntry, bool> poseFoldouts = new();
        
        // 固定値
        private const float textboxWidth = 350f;
        private float lineHeight = EditorGUIUtility.singleLineHeight;
        private float spacing = 4f;
        private string instanceIdPathBuffer = "";

        private GUIContent 
            libraryLabelContext,
            categoryListContext,
            categoryIconContext,
            categoryTextContext,
            openAllContext,
            closeAllContext,
            poseListContext,
            openButtonContext,
            closeButtonContext,
            thumbnailAutoContext,
            animationClipContext,
            trackingSettingsContext,
            isLoopContext,
            motionSpeedContext,
            dropboxContext,
            enableHeightContext,
            enableSpeedContext,
            enableMirrorContext;
            
        string[] trackingOptions;
        
        #endregion

        #region Initialize
        
        // インスペクターが有効化されたときの初期化処理
        private void OnEnable()
        {
            poseLibrary = (AvatarPoseLibrary)target;
            InitializeVariant();
            InitializeData();
            FindLibraryObject();
        }

        private Void InitializeVariant()
        {
            var i = DynamicVariables.Settings.Inspector;
            libraryLabelContext = new GUIContent(i.libraryMenuLabel, i.libraryMenuTooltip);
            categoryListContext = new GUIContent(i.categoriesLabel, i.categoriesTooltip);
            categoryIconContext = new GUIContent(i.categoryIconLabel, i.categoryIconTooltip);
            categoryTextContext = new GUIContent(i.categoryTextLabel, i.categoryTextTooltip);
            openAllContext = new GUIContent(i.openAllLabel, i.openAllTooltip);
            closeAllContext = new GUIContent(i.closeAllLabel, i.closeAllTooltip);
            poseListContext = new GUIContent(i.poseListLabel, i.poseListTooltip);
            openButtonContext = new GUIContent(i.openLabel, i.openTooltip);
            closeButtonContext = new GUIContent(i.closeLabel, i.closeTooltip);
            thumbnailAutoContext = new GUIContent(i.thumbnailAutoLabel, i.thumbnailAutoTooltip);
            animationClipContext = new GUIContent(i.animationClipLabel, i.animationClipTooltip);
            trackingSettingsContext = new GUIContent(i.trackingSettingsLabel, i.trackingSettingsTooltip);
            isLoopContext = new GUIContent(i.isLoopLabel, i.isLoopTooltip);
            motionSpeedContext = new GUIContent(i.motionSpeedLabel, i.motionSpeedTooltip);
            dropboxContext = new GUIContent(i.dropboxLabel, i.dropboxTooltip);
            enableHeightContext = new GUIContent(i.enableHeightLabel, i.enableHeightTooltip);
            enableSpeedContext = new GUIContent(i.enableSpeedLabel, i.enableSpeedTooltip);
            enableMirrorContext = new GUIContent(i.enableMirrorLabel, i.enableMirrorTooltip);
            trackingOptions = new string[]
            {
                i.headTrackingOption, 
                i.armTrackingOption,
                i.fingerTrackingOption,
                i.footTrackingOption,
                i.locomotionTrackingOption,
            };
        }
        
        #endregion

        #region DataUtility
        
        void UpdateData()
        {
            // 階層構造の変更を検知
            var pathBuff = GetInstanceIdPath(poseLibrary.transform);
            if (instanceIdPathBuffer != pathBuff)
            {
                FindLibraryObject();
                instanceIdPathBuffer = pathBuff;
            }
        }
        
        // コンポーネントの取得
        private AvatarPoseLibrary[] GetAvatarComponents()
        {
            var parent = poseLibrary.transform.GetComponentInParent<VRCAvatarDescriptor>();
            if (!parent)
            {
                Debug.LogWarning("コンポーネントがアバター直下に含まれていません。");
                return new AvatarPoseLibrary[1] {poseLibrary};
            }
            return parent.GetComponentsInChildren<AvatarPoseLibrary>();
        }

        // カテゴリーリストを収集
        private bool FindLibraryObject()
        {
            // 値の整合性を取る
            var comp = GetAvatarComponents().FirstOrDefault(
                e => e.data.name == data.name && e != poseLibrary);
            if (comp)
            {
                data.enableHeightParam = comp.data.enableHeightParam;
                data.enableSpeedParam = comp.data.enableSpeedParam;
                data.enableMirrorParam = comp.data.enableMirrorParam;
            }
            EditorUtility.SetDirty(target);
            
            // リストの生成
            var list = GetAvatarComponents().Select(e => e.data.name).ToArray();
            libraryTagList = list.Distinct().ToArray();
            libraryTagIndex = libraryTagList
                .Select((e, i) => new { e, i })
                .FirstOrDefault(x => x.e == data.name)?.i ?? -1;
            return list.Count(e => e == data.name) > 1;
        }

        // 値を同期する。
        private void ApplyLibrarySetting(bool height,bool speed,bool mirror)
        {
            if (data.enableHeightParam == height &&
                data.enableSpeedParam == speed &&
                data.enableMirrorParam == mirror)
            {
                return;
            }
            
            // 値の書き換え
            foreach (var apl in GetAvatarComponents())
            {
                if(data.name != apl.data.name) continue;
                apl.data.enableHeightParam = height;
                apl.data.enableSpeedParam = speed;
                apl.data.enableMirrorParam = mirror;
                EditorUtility.SetDirty(apl);
            }
        }

        // 値を同期する。
        private void ApplyLibraryName(int index,string name)
        {
            // 名前の更新
            if (index != libraryTagIndex)
            {
                name = libraryTagList[index];
            }
            if (name != data.name)
            {
                data.name = name;
                FindLibraryObject();
                EditorUtility.SetDirty(target);
            }
        }

        #endregion

        #region DrawUtility
        
        // コンポーネントの初期化処理を行う。
        void InitializeData()
        {
            if (poseLibrary.isInitialized == false)
            {
                // 初期化処理
                poseLibrary.data = new AvatarPoseData
                {
                    name = DynamicVariables.Settings.Menu.main.title,
                    thumbnail = DynamicVariables.Settings.Menu.main.thumbnail,
                    categories = new List<PoseCategory>(),
                    guid = ""
                };
                poseLibrary.isInitialized = true;
                categoryReorderableList = null;
            }

            if (categoryReorderableList == null)
            {
                categoryReorderableList = new ReorderableList(data.categories, typeof(PoseCategory), true, true, true, true)
                {
                    drawHeaderCallback = r => EditorGUI.LabelField(r, categoryListContext),
                    elementHeightCallback = i => GetCategoryElementHeight(i),
                    drawElementCallback = (r, i, isActive, isFocused) => DrawCategoryElement(r, i),
                    onAddCallback = l => data.categories.Add(CreateCategory()),
                    onRemoveCallback = l => data.categories.RemoveAt(l.index),
                    onChangedCallback = l => EditorUtility.SetDirty(target)
                };
            }
        }

        // カテゴリエレメントの高さを取得
        private float GetCategoryElementHeight(int index)
        {
            var list = EnsurePoseList(data.categories[index]);
            float poseListHeight = list.GetHeight();
            return EditorGUIUtility.singleLineHeight + 8f + Mathf.Max(EditorGUIUtility.singleLineHeight * 5f, EditorGUIUtility.singleLineHeight) + poseListHeight + 60f;
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
                onRemoveCallback = l => category.poses.RemoveAt(l.index),
                onChangedCallback = l => EditorUtility.SetDirty(target)
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
        
        #endregion
        
        // インスペクターGUIの描画処理
        public override void OnInspectorGUI()
        {
            InitializeData();
            UpdateData();
            
            // 変更する変数
            string name = data.name;
            int index = libraryTagIndex;

            // ここから描画開始
            float texSize = lineHeight * 8f;
            EditorGUILayout.LabelField("Avatar Pose Library Settings", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    new GUIContent(data.thumbnail,DynamicVariables.Settings.Inspector.mainThumbnailTooltip), 
                    GUILayout.Width(texSize), GUILayout.Height(texSize));
                // EditorGUILayout.Space();
                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(libraryLabelContext, EditorStyles.label);
                    EditorGUILayout.Space();
                    using (new GUILayout.HorizontalScope())
                    {
                        name = EditorGUILayout.TextField(data.name, GUILayout.MaxWidth(textboxWidth));
                        index = EditorGUILayout.Popup("", libraryTagIndex, libraryTagList, GUILayout.Width(20));
                    }

                    EditorGUILayout.Space();

                    var height = EditorGUILayout.Toggle(enableHeightContext,data.enableHeightParam);
                    var speed = EditorGUILayout.Toggle(enableSpeedContext,data.enableSpeedParam);
                    var mirror = EditorGUILayout.Toggle(enableMirrorContext,data.enableMirrorParam);
                    ApplyLibrarySetting(height, speed, mirror);
                }
            }
            EditorGUILayout.Space(15f);
            categoryReorderableList.DoLayoutList();

            ApplyLibraryName(index, name);
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
            var thumbnail = (Texture2D)EditorGUI.ObjectField( thumbRect, categoryIconContext, category.thumbnail, typeof(Texture2D), false);
            if (category.thumbnail != thumbnail)
            {
                // 更新処理
                category.thumbnail = thumbnail;
                EditorUtility.SetDirty(target);
            }
            GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Inspector.categoryThumbnailTooltip), GUIStyle.none);
            EditorGUI.LabelField(new Rect(rect.x + spacing * 2f + thumbnailSize, y + lineHeight, 100, lineHeight), categoryTextContext);
            var catName = EditorGUI.TextField(new Rect(rect.x + spacing * 2f + thumbnailSize, y + lineHeight*3f, 
                Mathf.Min(textboxWidth, nameWidth - thumbnailSize - 15f), lineHeight), category.name);
            if (category.name != catName)
            {
                // 更新処理
                category.name = catName;
                EditorUtility.SetDirty(target);
            }
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
                var poseName = GUI.TextField(new Rect(rect.x + 10f, y, Mathf.Min(textboxWidth,rect.width - 60f), lineHeight),pose.name);
                if (pose.name != poseName)
                {
                    // 更新処理
                    pose.name = poseName;
                    EditorUtility.SetDirty(target);
                }
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
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Inspector.thumbnailBg, ScaleMode.StretchToFill, false);
                    thumbRect = Rect.MinMaxRect(
                        thumbRect.xMin + 1f,
                        thumbRect.yMin + 1f,
                        thumbRect.xMax - 1f,
                        thumbRect.yMax - 1f
                    );
                    GUI.DrawTexture(thumbRect, thumb, ScaleMode.StretchToFill, true);
                    GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Inspector.posePreviewTooltip), GUIStyle.none);
                }
            }
            else
            {
                var newThumbnail = (Texture2D)EditorGUI.ObjectField(thumbRect, pose.thumbnail, typeof(Texture2D), false);
                if (pose.thumbnail != newThumbnail)
                {
                    // 更新処理
                    pose.thumbnail = newThumbnail;
                    EditorUtility.SetDirty(target);
                }
                GUI.Button(thumbRect, new GUIContent("", DynamicVariables.Settings.Inspector.poseThumbnailTooltip), GUIStyle.none);
            }
            var auto = EditorGUI.ToggleLeft(new Rect(rect.x, y + thumbnailSize + spacing, leftWidth, lineHeight), thumbnailAutoContext, pose.autoThumbnail);
            if (pose.autoThumbnail != auto)
            {
                // 更新処理
                pose.autoThumbnail = auto;
                EditorUtility.SetDirty(target);
            }
            
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
                // 更新処理
                ChangePoseAnimation(pose, animationClip);
                EditorUtility.SetDirty(target);
            }
            infoY += lineHeight + spacing;

            // トラッキングタイプ選択
            var flags = GetAnimationType(pose.tracking);
            var newFlags = EditorGUI.MaskField(new Rect(rightX, infoY, rightWidth, lineHeight), trackingSettingsContext, flags, trackingOptions);
            if (flags != newFlags)
            {
                // 更新処理
                ApplyTrackingFromType(newFlags,pose.tracking);
                EditorUtility.SetDirty(target);
            }
            
            // 横線を引く
            infoY += lineHeight + lineHeight/2f + spacing;
            GUI.Box(new Rect(rightX, infoY, rightWidth, 1f),"");
            infoY += lineHeight/2f;
            
            var loop = EditorGUI.Toggle(new Rect(rightX, infoY, rightWidth, lineHeight), isLoopContext, pose.tracking.loop);
            if (loop != pose.tracking.loop)
            {
                // 更新処理
                pose.tracking.loop = loop;
                EditorUtility.SetDirty(target);
            }
            infoY += lineHeight;
            var speed = EditorGUI.FloatField(new Rect(rightX, infoY, rightWidth, lineHeight), motionSpeedContext, pose.tracking.motionSpeed);
            if (speed != pose.tracking.motionSpeed)
            {
                // 更新処理
                pose.tracking.motionSpeed = speed;
                EditorUtility.SetDirty(target);
            }
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
                        EditorUtility.SetDirty(target);
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
        
        // オブジェクト構造に固有の文字列
        public static string GetInstanceIdPath(Transform transform)
        {
            if (transform.parent == null)
            {
                return transform.gameObject.GetInstanceID().ToString();
            }
            else
            {
                return GetInstanceIdPath(transform.parent) + "/" + transform.gameObject.GetInstanceID();
            }
        }
    }
}
