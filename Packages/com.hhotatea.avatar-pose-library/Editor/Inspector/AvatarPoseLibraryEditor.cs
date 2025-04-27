using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.logic;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.editor
{
    /// <summary>
    /// Custom inspector for <see cref="AvatarPoseLibrary"/>.
    /// すべての書き換えを SerializedProperty 経由で行い、Undo/Redo・Dirty を
    /// Unity 標準ワークフローに完全準拠させた改訂版。
    /// </summary>
    [CustomEditor(typeof(AvatarPoseLibrary))]
    public class AvatarPoseLibraryEditor : Editor
    {
        #region ===== model / const =====
        private AvatarPoseLibrary _library;
        private AvatarPoseData Data => _library.data;

        private const float TextBoxWidth = 350f;
        private readonly float _lineHeight = EditorGUIUtility.singleLineHeight;
        private const float Spacing = 4f;

        private ReorderableList _categoryList;
        private readonly Dictionary<PoseCategory, ReorderableList> _poseLists = new();

        // GUIContent
        private GUIContent _libraryLabel,
                           _categoryListLabel,
                           _categoryIconLabel,
                           _categoryTextLabel,
                           _openAllLabel,
                           _closeAllLabel,
                           _poseListLabel,
                           _openLabel,
                           _closeLabel,
                           _thumbnailAutoLabel,
                           _animationClipLabel,
                           _trackingLabel,
                           _isLoopLabel,
                           _motionSpeedLabel,
                           _dropBoxLabel,
                           _enableHeightLabel,
                           _enableSpeedLabel,
                           _enableMirrorLabel;

        // misc
        private string[] _libraryTagList;
        private int _libraryTagIndex;
        private readonly Dictionary<PoseEntry, Texture2D> _thumbnails = new();
        private readonly Dictionary<PoseEntry, AnimationClip> _lastClips = new();
        private readonly Dictionary<PoseEntry, bool> _poseFoldouts = new();

        private string _instanceIdPathBuffer = string.Empty;
        private string[] _trackingOptions;
        #endregion

        #region ===== Serialized helpers =====
        private SerializedProperty FindData(string rel) => serializedObject.FindProperty($"data.{rel}");

        /// <summary>
        /// Undo + Dirty をワンショットで行うヘルパ
        /// </summary>
        private void Apply(string undoLabel, Action body)
        {
            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(target, undoLabel);
            body();
            serializedObject.ApplyModifiedProperties();   // Dirty も立つ
        }
        #endregion

        #region ===== init =====
        private void OnEnable()
        {
            _library = (AvatarPoseLibrary)target;
            BuildGuiContent();
            EnsureInitialData();
            SyncLibraryTags();
        }

        private void BuildGuiContent()
        {
            var i = DynamicVariables.Settings.Inspector;
            _libraryLabel      = new(i.libraryMenuLabel, i.libraryMenuTooltip);
            _categoryListLabel = new(i.categoriesLabel, i.categoriesTooltip);
            _categoryIconLabel = new(i.categoryIconLabel, i.categoryIconTooltip);
            _categoryTextLabel = new(i.categoryTextLabel, i.categoryTextTooltip);
            _openAllLabel      = new(i.openAllLabel, i.openAllTooltip);
            _closeAllLabel     = new(i.closeAllLabel, i.closeAllTooltip);
            _poseListLabel     = new(i.poseListLabel, i.poseListTooltip);
            _openLabel         = new(i.openLabel, i.openTooltip);
            _closeLabel        = new(i.closeLabel, i.closeTooltip);
            _thumbnailAutoLabel= new(i.thumbnailAutoLabel, i.thumbnailAutoTooltip);
            _animationClipLabel= new(i.animationClipLabel, i.animationClipTooltip);
            _trackingLabel     = new(i.trackingSettingsLabel, i.trackingSettingsTooltip);
            _isLoopLabel       = new(i.isLoopLabel, i.isLoopTooltip);
            _motionSpeedLabel  = new(i.motionSpeedLabel, i.motionSpeedTooltip);
            _dropBoxLabel      = new(i.dropboxLabel, i.dropboxTooltip);
            _enableHeightLabel = new(i.enableHeightLabel, i.enableHeightTooltip);
            _enableSpeedLabel  = new(i.enableSpeedLabel, i.enableSpeedTooltip);
            _enableMirrorLabel = new(i.enableMirrorLabel, i.enableMirrorTooltip);

            _trackingOptions = new[]
            {
                i.headTrackingOption,
                i.armTrackingOption,
                i.fingerTrackingOption,
                i.footTrackingOption,
                i.locomotionTrackingOption,
            };
        }

        private void EnsureInitialData()
        {
            if (_library.isInitialized) return;

            Apply("Init PoseLibrary", () =>
            {
                var d = serializedObject.FindProperty("data");
                d.FindPropertyRelative("name").stringValue =
                    DynamicVariables.Settings.Menu.main.title;
                d.FindPropertyRelative("thumbnail").objectReferenceValue =
                    DynamicVariables.Settings.Menu.main.thumbnail;
                d.FindPropertyRelative("categories").arraySize = 0;
                d.FindPropertyRelative("guid").stringValue = string.Empty;
                _library.isInitialized = true;           // managed
            });
        }
        #endregion

        #region ===== inspector GUI =====
        public override void OnInspectorGUI()
        {
            EnsureGuiLists();
            DetectHierarchyChange();

            DrawMainHeader();

            EditorGUILayout.Space(15);
            _categoryList.DoLayoutList();
        }
        #endregion

        #region ===== Category list =====
        private void EnsureGuiLists()
        {
            var catProp = FindData("categories");
            _categoryList ??= new ReorderableList(serializedObject, catProp, true, true, true, true)
            {
                drawHeaderCallback   = r => EditorGUI.LabelField(r, _categoryListLabel),
                elementHeightCallback= GetCategoryHeight,
                drawElementCallback  = DrawCategory,

                onAddCallback = l => Apply("Add Category", () =>
                {
                    int i = catProp.arraySize;
                    catProp.InsertArrayElementAtIndex(i);
                    var c = catProp.GetArrayElementAtIndex(i);
                    c.FindPropertyRelative("name").stringValue = DynamicVariables.Settings.Menu.category.title;
                    c.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.category.thumbnail;
                    c.FindPropertyRelative("poses").arraySize = 0;
                }),

                onRemoveCallback = l => Apply("Remove Category", () =>
                {
                    catProp.DeleteArrayElementAtIndex(l.index);
                }),

                onChangedCallback   = l => serializedObject.ApplyModifiedProperties()
            };
        }
        #endregion

        #region ===== Pose list =====
        private ReorderableList EnsurePoseList(int catIdx, SerializedProperty posesProp)
        {
            if (_poseLists.TryGetValue(Data.categories[catIdx], out var list)) return list;

            list = new ReorderableList(serializedObject, posesProp, true, true, true, true)
            {
                drawHeaderCallback   = r => EditorGUI.LabelField(r, _poseListLabel),
                elementHeightCallback= i => GetPoseHeight(Data.categories[catIdx].poses[i]),
                drawElementCallback  = (r,i,a,f)=>DrawPose(r,i,catIdx,posesProp.GetArrayElementAtIndex(i)),

                onAddCallback = l => Apply("Add Pose", () =>
                {
                    int i = posesProp.arraySize;
                    posesProp.InsertArrayElementAtIndex(i);
                    var p = posesProp.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("name").stringValue = DynamicVariables.Settings.Menu.pose.title;
                    p.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.pose.thumbnail;
                    p.FindPropertyRelative("autoThumbnail").boolValue = true;

                    var tr = p.FindPropertyRelative("tracking");
                    tr.FindPropertyRelative("head").boolValue = false;
                    tr.FindPropertyRelative("arm").boolValue = false;
                    tr.FindPropertyRelative("finger").boolValue = false;
                    tr.FindPropertyRelative("foot").boolValue = false;
                    tr.FindPropertyRelative("locomotion").boolValue = false;
                    tr.FindPropertyRelative("loop").boolValue = true;
                    tr.FindPropertyRelative("motionSpeed").floatValue = 0f;
                }),

                onRemoveCallback = l => Apply("Remove Pose", () =>
                {
                    posesProp.DeleteArrayElementAtIndex(l.index);
                }),

                onChangedCallback   = l => serializedObject.ApplyModifiedProperties()
            };
            return _poseLists[Data.categories[catIdx]] = list;
        }
        #endregion

        #region ===== Drawers =====
        private void DrawMainHeader()
        {
            string newName = Data.name;
            int    newIdx  = _libraryTagIndex;

            float texSize = _lineHeight * 8f;
            EditorGUILayout.LabelField("Avatar Pose Library Settings", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(Data.thumbnail, DynamicVariables.Settings.Inspector.mainThumbnailTooltip),
                                GUILayout.Width(texSize), GUILayout.Height(texSize));

                using (new GUILayout.VerticalScope())
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(_libraryLabel, EditorStyles.label);
                    EditorGUILayout.Space();

                    using (new GUILayout.HorizontalScope())
                    {
                        newName = EditorGUILayout.TextField(Data.name, GUILayout.MaxWidth(TextBoxWidth));
                        newIdx  = EditorGUILayout.Popup(string.Empty, _libraryTagIndex, _libraryTagList, GUILayout.Width(20));
                    }

                    EditorGUILayout.Space();
                    ApplyGlobalToggles();
                }
            }

            if (newName != Data.name || newIdx != _libraryTagIndex)
            {
                Apply("Rename PoseLibrary", () =>
                {
                    FindData("name").stringValue =
                        (newIdx != _libraryTagIndex) ? _libraryTagList[newIdx] : newName;
                });
                SyncLibraryTags();
            }
        }

        private void ApplyGlobalToggles()
        {
            bool height = EditorGUILayout.Toggle(_enableHeightLabel, Data.enableHeightParam);
            bool speed  = EditorGUILayout.Toggle(_enableSpeedLabel,  Data.enableSpeedParam);
            bool mirror = EditorGUILayout.Toggle(_enableMirrorLabel, Data.enableMirrorParam);

            if (height == Data.enableHeightParam &&
                speed  == Data.enableSpeedParam  &&
                mirror == Data.enableMirrorParam) return;

            Apply("Toggle Global Flags", () =>
            {
                foreach (var lib in GetLibraryComponents().Where(l => l.data.name == Data.name))
                {
                    var so = new SerializedObject(lib);
                    so.FindProperty("data.enableHeightParam").boolValue = height;
                    so.FindProperty("data.enableSpeedParam").boolValue  = speed;
                    so.FindProperty("data.enableMirrorParam").boolValue = mirror;
                    so.ApplyModifiedProperties();
                }
            });
        }

        private float GetCategoryHeight(int i)
        {
            var list = EnsurePoseList(i, FindData($"categories.Array.data[{i}].poses"));
            return _lineHeight + 8f + Mathf.Max(_lineHeight * 5, _lineHeight) + list.GetHeight() + 60f;
        }

        private void DrawCategory(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty cat  = FindData("categories").GetArrayElementAtIndex(index);
            SerializedProperty name = cat.FindPropertyRelative("name");
            SerializedProperty icon = cat.FindPropertyRelative("thumbnail");
            SerializedProperty poses= cat.FindPropertyRelative("poses");

            float y        = rect.y + Spacing;
            float thumbSz  = _lineHeight * 5f;
            float nameArea = rect.width - Spacing;

            var thumbRect = new Rect(rect.x + Spacing, y, thumbSz, thumbSz);
            var newThumb  = (Texture2D)EditorGUI.ObjectField(thumbRect, _categoryIconLabel, icon.objectReferenceValue, typeof(Texture2D), false);
            if (newThumb != icon.objectReferenceValue)
                Apply("Edit Category Thumbnail", () => icon.objectReferenceValue = newThumb);

            GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);

            GUI.Label(new Rect(rect.x + Spacing * 2f + thumbSz, y + _lineHeight, 100, _lineHeight), _categoryTextLabel);
            string catName = EditorGUI.TextField(new Rect(rect.x + Spacing * 2f + thumbSz, y + _lineHeight * 3f,
                                                           Mathf.Min(TextBoxWidth, nameArea - thumbSz - 15f), _lineHeight),
                                                           name.stringValue);
            if (catName != name.stringValue)
                Apply("Rename Category", () => name.stringValue = catName);

            y += Mathf.Max(thumbSz, _lineHeight) + Spacing;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_openAllLabel).x, GUI.skin.button.CalcSize(_closeAllLabel).x) + 5f;

            if (GUI.Button(new Rect(rect.x + rect.width - btnW * 2f - 10, y, btnW, _lineHeight), _openAllLabel))
                foreach (var p in Data.categories[index].poses) _poseFoldouts[p] = true;
            if (GUI.Button(new Rect(rect.x + rect.width - btnW - 5, y, btnW, _lineHeight), _closeAllLabel))
                foreach (var p in Data.categories[index].poses) _poseFoldouts[p] = false;

            y += _lineHeight + Spacing;
            var list = EnsurePoseList(index, poses);
            list.DoList(new Rect(rect.x, y, rect.width, list.GetHeight()));
            y += list.GetHeight() + Spacing;

            DrawPoseDropArea(new Rect(rect.x, y, rect.width, 40f), poses);
        }

        private float GetPoseHeight(PoseEntry pose) =>
            _poseFoldouts.TryGetValue(pose, out var exp) && exp ? _lineHeight * 7f : _lineHeight * 1.5f;

        private void DrawPose(Rect rect, int idx, int catIdx, SerializedProperty poseProp)
        {
            PoseEntry pose = Data.categories[catIdx].poses[idx];
            float y = rect.y + 2f;

            // fold / name
            _poseFoldouts.TryAdd(pose, false);
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_closeLabel).x, GUI.skin.button.CalcSize(_openLabel).x) + 2;

            if (_poseFoldouts[pose])
            {
                string newName = GUI.TextField(new Rect(rect.x + 10, y, Mathf.Min(TextBoxWidth, rect.width - 60), _lineHeight),
                                               poseProp.FindPropertyRelative("name").stringValue);
                if (newName != poseProp.FindPropertyRelative("name").stringValue)
                    Apply("Rename Pose", () => poseProp.FindPropertyRelative("name").stringValue = newName);

                if (GUI.Button(new Rect(rect.x + rect.width - btnW, y, btnW, 20), _closeLabel))
                    _poseFoldouts[pose] = false;
            }
            else
            {
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 60, _lineHeight), pose.name);
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, y, btnW, 20), _openLabel))
                    _poseFoldouts[pose] = true;
                return;
            }
            y += _lineHeight + Spacing + 4;

            float thumbnailSize = _lineHeight * 4f;
            float leftWidth     = thumbnailSize + Spacing;
            float rightWidth    = rect.width - leftWidth - Spacing * 3;
            float rightX        = rect.x + leftWidth + Spacing * 2;

            var thumbRect = new Rect(rect.x, y, thumbnailSize, thumbnailSize);
            SerializedProperty autoTnProp = poseProp.FindPropertyRelative("autoThumbnail");
            SerializedProperty thumbProp  = poseProp.FindPropertyRelative("thumbnail");
            SerializedProperty clipProp   = poseProp.FindPropertyRelative("animationClip");

            if (autoTnProp.boolValue && clipProp.objectReferenceValue)
            {
                if (!_lastClips.TryGetValue(pose, out var last) || last != clipProp.objectReferenceValue)
                {
                    _lastClips[pose] = (AnimationClip)clipProp.objectReferenceValue;
                    _thumbnails[pose] = GenerateThumbnail(_library.gameObject, (AnimationClip)clipProp.objectReferenceValue);
                }
                if (_thumbnails.TryGetValue(pose, out var tex) && tex)
                {
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Inspector.thumbnailBg, ScaleMode.StretchToFill, false);
                    GUI.DrawTexture(Rect.MinMaxRect(thumbRect.xMin+1,thumbRect.yMin+1,thumbRect.xMax-1,thumbRect.yMax-1),
                                    tex, ScaleMode.StretchToFill,true);
                    GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);
                }
            }
            else
            {
                var newTex = (Texture2D)EditorGUI.ObjectField(thumbRect, thumbProp.objectReferenceValue,
                                                              typeof(Texture2D), false);
                if (newTex != thumbProp.objectReferenceValue)
                    Apply("Edit Pose Thumbnail", () => thumbProp.objectReferenceValue = newTex);

                GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);
            }

            bool auto = EditorGUI.ToggleLeft(new Rect(rect.x, y + thumbnailSize + Spacing, leftWidth, _lineHeight),
                                             _thumbnailAutoLabel, autoTnProp.boolValue);
            if (auto != autoTnProp.boolValue)
                Apply("Toggle AutoThumbnail", () => autoTnProp.boolValue = auto);

            GUI.Box(new Rect(rightX - Spacing, y, 1, thumbnailSize + _lineHeight), GUIContent.none);

            float infoY = rect.y + _lineHeight + Spacing + 4;
            float fldW  = rightWidth - Spacing;

            var newClip = (AnimationClip)EditorGUI.ObjectField(new Rect(rightX, infoY, fldW, _lineHeight),
                                                               _animationClipLabel, clipProp.objectReferenceValue,
                                                               typeof(AnimationClip), false);
            if (newClip != clipProp.objectReferenceValue) ApplyClipChange(poseProp, newClip);
            infoY += _lineHeight + Spacing;

            var trProp = poseProp.FindPropertyRelative("tracking");
            int flagsOld = FlagsFromTracking(trProp);
            int flagsNew = EditorGUI.MaskField(new Rect(rightX, infoY, rightWidth, _lineHeight),
                                               _trackingLabel, flagsOld, _trackingOptions);
            if (flagsNew != flagsOld)
                Apply("Edit Tracking Mask", () => FlagsToTracking(flagsNew, trProp));
            infoY += _lineHeight + _lineHeight/2 + Spacing;
            GUI.Box(new Rect(rightX, infoY, rightWidth, 1), GUIContent.none);
            infoY += _lineHeight/2;

            bool loop = EditorGUI.Toggle(new Rect(rightX, infoY, rightWidth, _lineHeight),
                                         _isLoopLabel, trProp.FindPropertyRelative("loop").boolValue);
            if (loop != trProp.FindPropertyRelative("loop").boolValue)
                Apply("Toggle Loop", () => trProp.FindPropertyRelative("loop").boolValue = loop);
            infoY += _lineHeight;

            float spd = EditorGUI.FloatField(new Rect(rightX, infoY, rightWidth, _lineHeight),
                                             _motionSpeedLabel, trProp.FindPropertyRelative("motionSpeed").floatValue);
            if (!Mathf.Approximately(spd, trProp.FindPropertyRelative("motionSpeed").floatValue))
                Apply("Change Motion Speed", () => trProp.FindPropertyRelative("motionSpeed").floatValue = spd);
        }

        private void DrawPoseDropArea(Rect area, SerializedProperty posesProp)
        {
            GUI.Box(area, _dropBoxLabel, EditorStyles.helpBox);
            Event evt = Event.current;
            if (!area.Contains(evt.mousePosition) ||
                evt.type is not (EventType.DragUpdated or EventType.DragPerform)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            Apply("Add Pose by Drag&Drop", () =>
            {
                foreach (var clip in DragAndDrop.objectReferences.OfType<AnimationClip>())
                {
                    int i = posesProp.arraySize;
                    posesProp.InsertArrayElementAtIndex(i);
                    var p = posesProp.GetArrayElementAtIndex(i);

                    p.FindPropertyRelative("name").stringValue = clip.name;
                    p.FindPropertyRelative("thumbnail").objectReferenceValue =
                        DynamicVariables.Settings.Menu.pose.thumbnail;
                    p.FindPropertyRelative("animationClip").objectReferenceValue = clip;
                    p.FindPropertyRelative("autoThumbnail").boolValue = true;

                    var tr = p.FindPropertyRelative("tracking");
                    bool moving = MotionBuilder.IsMoveAnimation(clip);
                    tr.FindPropertyRelative("motionSpeed").floatValue = moving ? 1f : 0f;
                    tr.FindPropertyRelative("loop").boolValue = moving ? MotionBuilder.IsLoopAnimation(clip) : true;
                }
            });
            evt.Use();
        }
        #endregion

        #region ===== helpers =====
        private void DetectHierarchyChange()
        {
            string path = GetInstancePath(_library.transform);
            if (path == _instanceIdPathBuffer) return;

            SyncLibraryTags();
            _instanceIdPathBuffer = path;
        }

        private void SyncLibraryTags()
        {
            string[] duplicates = GetLibraryComponents().Select(e => e.data.name).ToArray();
            _libraryTagList  = duplicates.Distinct().ToArray();
            _libraryTagIndex = Array.FindIndex(_libraryTagList, n => n == Data.name);
        }

        private AvatarPoseLibrary[] GetLibraryComponents()
        {
            var avatar = _library.transform.GetComponentInParent<VRCAvatarDescriptor>();
            return avatar ? avatar.GetComponentsInChildren<AvatarPoseLibrary>() : new[] { _library };
        }

        private void ApplyClipChange(SerializedProperty poseProp, AnimationClip clip)
        {
            Apply("Change Animation Clip", () =>
            {
                poseProp.FindPropertyRelative("animationClip").objectReferenceValue = clip;
                if (!clip) return;

                poseProp.FindPropertyRelative("name").stringValue = clip.name;
                var tr = poseProp.FindPropertyRelative("tracking");
                bool moving = MotionBuilder.IsMoveAnimation(clip);
                tr.FindPropertyRelative("motionSpeed").floatValue = moving ? 1f : 0f;
                tr.FindPropertyRelative("loop").boolValue = moving ? MotionBuilder.IsLoopAnimation(clip) : true;
            });
        }

        private Texture2D GenerateThumbnail(GameObject obj, AnimationClip clip)
        {
            var avatar = obj.GetComponentInParent<VRCAvatarDescriptor>();
            if (!avatar) return null;

            var clone = Object.Instantiate(avatar.gameObject);
            Texture2D tex;
            using (var cap = new ThumbnailGenerator(clone))
                tex = cap.Capture(clip);
            Object.DestroyImmediate(clone);
            return tex;
        }

        private static int FlagsFromTracking(SerializedProperty t)
        {
            int f = 0;
            if (t.FindPropertyRelative("head").boolValue)       f |= 1 << 0;
            if (t.FindPropertyRelative("arm").boolValue)        f |= 1 << 1;
            if (t.FindPropertyRelative("finger").boolValue)     f |= 1 << 2;
            if (t.FindPropertyRelative("foot").boolValue)       f |= 1 << 3;
            if (t.FindPropertyRelative("locomotion").boolValue) f |= 1 << 4;
            return f;
        }

        private static void FlagsToTracking(int f, SerializedProperty t)
        {
            t.FindPropertyRelative("head").boolValue       = (f & 1 << 0) != 0;
            t.FindPropertyRelative("arm").boolValue        = (f & 1 << 1) != 0;
            t.FindPropertyRelative("finger").boolValue     = (f & 1 << 2) != 0;
            t.FindPropertyRelative("foot").boolValue       = (f & 1 << 3) != 0;
            t.FindPropertyRelative("locomotion").boolValue = (f & 1 << 4) != 0;
        }

        private static string GetInstancePath(Transform t) =>
            t.parent ? GetInstancePath(t.parent) + "/" + t.gameObject.GetInstanceID()
                     : t.gameObject.GetInstanceID().ToString();
        #endregion
    }
}
