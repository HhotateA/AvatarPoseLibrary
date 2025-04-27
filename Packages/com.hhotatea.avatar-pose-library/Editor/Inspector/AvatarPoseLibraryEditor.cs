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
    /// Custom inspector for <see cref="AvatarPoseLibrary"/> organised with a simple MVVM flavour.
    /// Behaviour is unchanged – only the internal layout has been rearranged for clarity.
    /// </summary>
    [CustomEditor(typeof(AvatarPoseLibrary))]
    public class AvatarPoseLibraryEditor : Editor
    {
        #region =====================  Model  =====================
        // Target component and cached data
        private AvatarPoseLibrary _library;
        private AvatarPoseData Data => _library.data;

        // Inspector constants
        private const float TextBoxWidth = 350f;
        private readonly float _lineHeight = EditorGUIUtility.singleLineHeight;
        private const float Spacing = 4f;

        // ReorderableLists
        private ReorderableList _categoryList;
        private readonly Dictionary<PoseCategory, ReorderableList> _poseLists = new();

        // Re‑usable GUIContent cache
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

        // Misc caches
        private string[] _libraryTagList;
        private int _libraryTagIndex;
        private readonly Dictionary<PoseEntry, Texture2D> _thumbnails = new();
        private readonly Dictionary<PoseEntry, AnimationClip> _lastClips = new();
        private readonly Dictionary<PoseEntry, bool> _poseFoldouts = new();

        private string _instanceIdPathBuffer = string.Empty;
        private string[] _trackingOptions;
        #endregion

        #region =====================  View ‑ initialisation  =====================
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
        #endregion

        #region =====================  ViewModel  =====================
        /// <summary>
        /// Guarantee that the target component is initialised with default data.
        /// </summary>
        private void EnsureInitialData()
        {
            if (_library.isInitialized) return;

            _library.data = new AvatarPoseData
            {
                name       = DynamicVariables.Settings.Menu.main.title,
                thumbnail  = DynamicVariables.Settings.Menu.main.thumbnail,
                categories = new List<PoseCategory>(),
                guid       = string.Empty
            };
            _library.isInitialized = true;
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Create or refresh reorderable lists after data changes.
        /// </summary>
        private void EnsureGuiLists()
        {
            _categoryList ??= new ReorderableList(Data.categories, typeof(PoseCategory), true, true, true, true)
            {
                drawHeaderCallback   = r => EditorGUI.LabelField(r, _categoryListLabel),
                elementHeightCallback= GetCategoryHeight,
                drawElementCallback  = DrawCategory,
                onAddCallback        = l => Data.categories.Add(CreateCategory()),
                onRemoveCallback     = l => Data.categories.RemoveAt(l.index),
                onChangedCallback    = l => EditorUtility.SetDirty(target)
            };
        }

        /// <summary>
        /// Update caches when hierarchy changes.
        /// </summary>
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
        #endregion

        #region =====================  View – IMGUI =====================
        public override void OnInspectorGUI()
        {
            EnsureGuiLists();
            DetectHierarchyChange();

            DrawMainHeader();

            EditorGUILayout.Space(15);
            _categoryList.DoLayoutList();
        }

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

            ApplyLibraryRename(newIdx, newName);
        }

        private void ApplyGlobalToggles()
        {
            bool height  = EditorGUILayout.Toggle(_enableHeightLabel, Data.enableHeightParam);
            bool speed   = EditorGUILayout.Toggle(_enableSpeedLabel, Data.enableSpeedParam);
            bool mirror  = EditorGUILayout.Toggle(_enableMirrorLabel, Data.enableMirrorParam);
            if (height == Data.enableHeightParam && speed == Data.enableSpeedParam && mirror == Data.enableMirrorParam) return;

            foreach (var lib in GetLibraryComponents().Where(lib => lib.data.name == Data.name))
            {
                lib.data.enableHeightParam = height;
                lib.data.enableSpeedParam  = speed;
                lib.data.enableMirrorParam = mirror;
                EditorUtility.SetDirty(lib);
            }
        }
        #endregion

        #region =====================  View – Category =====================
        private float GetCategoryHeight(int index)
        {
            var list = EnsurePoseList(Data.categories[index]);
            return _lineHeight + 8f + Mathf.Max(_lineHeight * 5, _lineHeight) + list.GetHeight() + 60f;
        }

        private void DrawCategory(Rect rect, int index, bool isActive, bool isFocused)
        {
            var category   = Data.categories[index];
            float y        = rect.y + Spacing;
            float thumbSz  = _lineHeight * 5f;
            float nameArea = rect.width - Spacing;

            // Thumbnail
            var thumbRect = new Rect(rect.x + Spacing, y, thumbSz, thumbSz);
            var newThumb  = (Texture2D)EditorGUI.ObjectField(thumbRect, _categoryIconLabel, category.thumbnail, typeof(Texture2D), false);
            if (category.thumbnail != newThumb)
            {
                category.thumbnail = newThumb;
                EditorUtility.SetDirty(target);
            }
            GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);

            // Name
            GUI.Label(new Rect(rect.x + Spacing * 2f + thumbSz, y + _lineHeight, 100, _lineHeight), _categoryTextLabel);
            string catName = EditorGUI.TextField(new Rect(rect.x + Spacing * 2f + thumbSz, y + _lineHeight * 3f,
                                                           Mathf.Min(TextBoxWidth, nameArea - thumbSz - 15f), _lineHeight), category.name);
            if (category.name != catName)
            {
                category.name = catName;
                EditorUtility.SetDirty(target);
            }

            // Fold buttons
            y += Mathf.Max(thumbSz, _lineHeight) + Spacing;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_openAllLabel).x, GUI.skin.button.CalcSize(_closeAllLabel).x) + 5f;
            if (GUI.Button(new Rect(rect.x + rect.width - btnW * 2f - 10, y, btnW, _lineHeight), _openAllLabel))
                foreach (var p in category.poses) _poseFoldouts[p] = true;
            if (GUI.Button(new Rect(rect.x + rect.width - btnW - 5, y, btnW, _lineHeight), _closeAllLabel))
                foreach (var p in category.poses) _poseFoldouts[p] = false;

            // Pose list
            y += _lineHeight + Spacing;
            var poseList = EnsurePoseList(category);
            poseList.DoList(new Rect(rect.x, y, rect.width, poseList.GetHeight()));
            y += poseList.GetHeight() + Spacing;

            // Drag‑and‑drop
            DrawPoseDropArea(new Rect(rect.x, y, rect.width, 40f), category);
        }
        #endregion

        #region =====================  View – Pose =====================
        private ReorderableList EnsurePoseList(PoseCategory category)
        {
            if (_poseLists.TryGetValue(category, out var list)) return list;

            list = new ReorderableList(category.poses, typeof(PoseEntry), true, true, true, true)
            {
                drawHeaderCallback   = r => EditorGUI.LabelField(r, _poseListLabel),
                elementHeightCallback= i => GetPoseHeight(category.poses[i]),
                drawElementCallback  = (r, i, a, f) => DrawPose(r, i, category),
                onAddCallback        = l => category.poses.Add(CreatePose(null)),
                onRemoveCallback     = l => category.poses.RemoveAt(l.index),
                onChangedCallback    = l => EditorUtility.SetDirty(target)
            };
            return _poseLists[category] = list;
        }

        private float GetPoseHeight(PoseEntry pose)
        {
            return _poseFoldouts.TryGetValue(pose, out var expanded) && expanded ? _lineHeight * 7f : _lineHeight * 1.5f;
        }

        private void DrawPose(Rect rect, int index, PoseCategory category)
        {
            PoseEntry pose = category.poses[index];
            float y = rect.y + 2f;

            // Foldout / name
            _poseFoldouts.TryAdd(pose, false);
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_closeLabel).x, GUI.skin.button.CalcSize(_openLabel).x) + 2;
            if (_poseFoldouts[pose])
            {
                string newName = GUI.TextField(new Rect(rect.x + 10, y, Mathf.Min(TextBoxWidth, rect.width - 60), _lineHeight), pose.name);
                if (newName != pose.name) { pose.name = newName; EditorUtility.SetDirty(target); }
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, y, btnW, 20), _closeLabel)) _poseFoldouts[pose] = false;
            }
            else
            {
                GUI.Label(new Rect(rect.x + 10, y, rect.width - 60, _lineHeight), pose.name);
                if (GUI.Button(new Rect(rect.x + rect.width - btnW, y, btnW, 20), _openLabel)) _poseFoldouts[pose] = true;
                return;
            }
            y += _lineHeight + Spacing + 4;

            // Thumbnail and details when opened
            float thumbnailSize = _lineHeight * 4f;
            float leftWidth     = thumbnailSize + Spacing;
            float rightWidth    = rect.width - leftWidth - Spacing * 3;
            float rightX        = rect.x + leftWidth + Spacing * 2;

            var thumbRect = new Rect(rect.x, y, thumbnailSize, thumbnailSize);
            if (pose.autoThumbnail && pose.animationClip)
            {
                if (!_lastClips.TryGetValue(pose, out var last) || last != pose.animationClip)
                {
                    _lastClips[pose] = pose.animationClip;
                    _thumbnails[pose] = GenerateThumbnail(_library.gameObject, pose.animationClip);
                }
                if (_thumbnails.TryGetValue(pose, out var tex) && tex)
                {
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Inspector.thumbnailBg, ScaleMode.StretchToFill, false);
                    GUI.DrawTexture(Rect.MinMaxRect(thumbRect.xMin + 1, thumbRect.yMin + 1, thumbRect.xMax - 1, thumbRect.yMax - 1), tex, ScaleMode.StretchToFill, true);
                    GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);
                }
            }
            else
            {
                var newTex = (Texture2D)EditorGUI.ObjectField(thumbRect, pose.thumbnail, typeof(Texture2D), false);
                if (newTex != pose.thumbnail) { pose.thumbnail = newTex; EditorUtility.SetDirty(target); }
                GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);
            }
            bool autoTn = EditorGUI.ToggleLeft(new Rect(rect.x, y + thumbnailSize + Spacing, leftWidth, _lineHeight), _thumbnailAutoLabel, pose.autoThumbnail);
            if (autoTn != pose.autoThumbnail) { pose.autoThumbnail = autoTn; EditorUtility.SetDirty(target); }

            // separator
            GUI.Box(new Rect(rightX - Spacing, y, 1, thumbnailSize + _lineHeight), GUIContent.none);

            // Details
            float infoY = rect.y + _lineHeight + Spacing + 4;
            float fldW  = rightWidth - Spacing;

            var clip = (AnimationClip)EditorGUI.ObjectField(new Rect(rightX, infoY, fldW, _lineHeight), _animationClipLabel, pose.animationClip, typeof(AnimationClip), false);
            if (clip != pose.animationClip) { ApplyClipChange(pose, clip); }
            infoY += _lineHeight + Spacing;

            int flagsOld = FlagsFromTracking(pose.tracking);
            int flagsNew = EditorGUI.MaskField(new Rect(rightX, infoY, rightWidth, _lineHeight), _trackingLabel, flagsOld, _trackingOptions);
            if (flagsNew != flagsOld) { FlagsToTracking(flagsNew, pose.tracking); EditorUtility.SetDirty(target); }
            infoY += _lineHeight + _lineHeight / 2 + Spacing;
            GUI.Box(new Rect(rightX, infoY, rightWidth, 1), GUIContent.none);
            infoY += _lineHeight / 2;

            bool loop = EditorGUI.Toggle(new Rect(rightX, infoY, rightWidth, _lineHeight), _isLoopLabel, pose.tracking.loop);
            if (loop != pose.tracking.loop) { pose.tracking.loop = loop; EditorUtility.SetDirty(target); }
            infoY += _lineHeight;
            float speed = EditorGUI.FloatField(new Rect(rightX, infoY, rightWidth, _lineHeight), _motionSpeedLabel, pose.tracking.motionSpeed);
            if (!Mathf.Approximately(speed, pose.tracking.motionSpeed)) { pose.tracking.motionSpeed = speed; EditorUtility.SetDirty(target); }
        }
        #endregion

        #region =====================  View – Drag‑and‑drop =====================
        private void DrawPoseDropArea(Rect area, PoseCategory category)
        {
            GUI.Box(area, _dropBoxLabel, EditorStyles.helpBox);
            Event evt = Event.current;
            if (!area.Contains(evt.mousePosition) || evt.type is not (EventType.DragUpdated or EventType.DragPerform)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform) return;

            DragAndDrop.AcceptDrag();
            foreach (var clip in DragAndDrop.objectReferences.OfType<AnimationClip>())
            {
                category.poses.Add(CreatePose(clip));
            }
            EditorUtility.SetDirty(target);
            evt.Use();
        }
        #endregion

        #region =====================  ViewModel – helpers =====================
        private PoseCategory CreateCategory() => new()
        {
            name      = DynamicVariables.Settings.Menu.category.title,
            thumbnail = DynamicVariables.Settings.Menu.category.thumbnail,
            poses     = new List<PoseEntry>()
        };

        private PoseEntry CreatePose(AnimationClip clip)
        {
            var pose = new PoseEntry
            {
                name        = DynamicVariables.Settings.Menu.pose.title,
                thumbnail   = DynamicVariables.Settings.Menu.pose.thumbnail,
                autoThumbnail = true,
                tracking    = new TrackingSetting()
            };
            ApplyClipChange(pose, clip);
            return pose;
        }

        private void ApplyClipChange(PoseEntry pose, AnimationClip clip)
        {
            pose.animationClip = clip;
            if (!clip) return;

            pose.name                 = clip.name;
            bool moving               = MotionBuilder.IsMoveAnimation(clip);
            pose.tracking.motionSpeed = moving ? 1f : 0f;
            pose.tracking.loop        = moving ? MotionBuilder.IsLoopAnimation(clip) : true;
            EditorUtility.SetDirty(target);
        }

        private void ApplyLibraryRename(int newIndex, string newName)
        {
            if (newIndex != _libraryTagIndex) newName = _libraryTagList[newIndex];
            if (Data.name == newName) return;
            Data.name = newName;
            SyncLibraryTags();
            EditorUtility.SetDirty(target);
        }

        private Texture2D GenerateThumbnail(GameObject obj, AnimationClip clip)
        {
            var avatar = obj.GetComponentInParent<VRCAvatarDescriptor>();
            if (!avatar) return null;

            var clone = Object.Instantiate(avatar.gameObject);
            Texture2D tex;
            using (var cap = new ThumbnailGenerator(clone))
            {
                tex = cap.Capture(clip);
            }
            Object.DestroyImmediate(clone);
            return tex;
        }

        private int FlagsFromTracking(TrackingSetting t)
        {
            int f = 0;
            if (t.head)       f |= 1 << 0;
            if (t.arm)        f |= 1 << 1;
            if (t.finger)     f |= 1 << 2;
            if (t.foot)       f |= 1 << 3;
            if (t.locomotion) f |= 1 << 4;
            return f;
        }

        private void FlagsToTracking(int f, TrackingSetting t)
        {
            t.head       = (f & 1 << 0) != 0;
            t.arm        = (f & 1 << 1) != 0;
            t.finger     = (f & 1 << 2) != 0;
            t.foot       = (f & 1 << 3) != 0;
            t.locomotion = (f & 1 << 4) != 0;
        }

        private static string GetInstancePath(Transform t) => t.parent ? GetInstancePath(t.parent) + "/" + t.gameObject.GetInstanceID() : t.gameObject.GetInstanceID().ToString();
        #endregion
    }
}
