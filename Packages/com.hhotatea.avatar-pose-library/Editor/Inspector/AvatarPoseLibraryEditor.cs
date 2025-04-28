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

        private const float TextBoxWidth  = 350f;
        private readonly float _lineHeight = EditorGUIUtility.singleLineHeight;
        private const float Spacing       = 4f;

        private ReorderableList _categoryList;
        private readonly List<ReorderableList> _poseLists = new();

        // キャッシュ (foldout なし)
        private readonly List<List<Texture2D>>    _thumbnails = new();
        private readonly List<List<AnimationClip>> _lastClips  = new();

        // GUIContent 定義（省略無し）
        private GUIContent _libraryLabel, _categoryListLabel, _categoryIconLabel, _categoryTextLabel,
                           _openAllLabel, _closeAllLabel, _poseListLabel, _openLabel, _closeLabel,
                           _thumbnailAutoLabel, _animationClipLabel, _trackingLabel, _isLoopLabel,
                           _motionSpeedLabel, _dropBoxLabel, _enableHeightLabel, _enableSpeedLabel, _enableMirrorLabel;

        private string[] _libraryTagList;
        private int _libraryTagIndex;
        private string _instanceIdPathBuffer = string.Empty;
        private string[] _trackingOptions;
        #endregion

        #region ===== Serialized helpers =====
        private SerializedProperty FindData(string rel) => serializedObject.FindProperty($"data.{rel}");
        private void Apply(string undoLabel, Action body)
        {
            serializedObject.Update();
            Undo.RegisterCompleteObjectUndo(target, undoLabel);
            body();
            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #region ===== init =====
        private void OnEnable()
        {
            _library = (AvatarPoseLibrary)target;
            BuildGuiContent();
            EnsureInitialData();
            SyncLibraryTags();
            SyncCacheSize();
        }

        // BuildGuiContent() 省略なし
        private void BuildGuiContent()
        {
            var i = DynamicVariables.Settings.Inspector;
            _libraryLabel       = new(i.libraryMenuLabel, i.libraryMenuTooltip);
            _categoryListLabel  = new(i.categoriesLabel, i.categoriesTooltip);
            _categoryIconLabel  = new(i.categoryIconLabel, i.categoryIconTooltip);
            _categoryTextLabel  = new(i.categoryTextLabel, i.categoryTextTooltip);
            _openAllLabel       = new(i.openAllLabel, i.openAllTooltip);
            _closeAllLabel      = new(i.closeAllLabel, i.closeAllTooltip);
            _poseListLabel      = new(i.poseListLabel, i.poseListTooltip);
            _openLabel          = new(i.openLabel, i.openTooltip);
            _closeLabel         = new(i.closeLabel, i.closeTooltip);
            _thumbnailAutoLabel = new(i.thumbnailAutoLabel, i.thumbnailAutoTooltip);
            _animationClipLabel = new(i.animationClipLabel, i.animationClipTooltip);
            _trackingLabel      = new(i.trackingSettingsLabel, i.trackingSettingsTooltip);
            _isLoopLabel        = new(i.isLoopLabel, i.isLoopTooltip);
            _motionSpeedLabel   = new(i.motionSpeedLabel, i.motionSpeedTooltip);
            _dropBoxLabel       = new(i.dropboxLabel, i.dropboxTooltip);
            _enableHeightLabel  = new(i.enableHeightLabel, i.enableHeightTooltip);
            _enableSpeedLabel   = new(i.enableSpeedLabel, i.enableSpeedTooltip);
            _enableMirrorLabel  = new(i.enableMirrorLabel, i.enableMirrorTooltip);

            _trackingOptions = new[]
            {
                i.headTrackingOption,
                i.armTrackingOption,
                i.fingerTrackingOption,
                i.footTrackingOption,
                i.locomotionTrackingOption
            };
        }

        private void EnsureInitialData()
        {
            foreach (var cat in Data.categories)
            {
                foreach (var pose in cat.poses)
                {
                    if(pose.parameter == "Foldout Cash") continue;
                    pose.parameter = "Foldout Cash";
                    pose.value = 0;
                }
            }
            
            if (_library.isInitialized) return;
            Apply("Init PoseLibrary", () =>
            {
                var d = serializedObject.FindProperty("data");
                d.FindPropertyRelative("name").stringValue             = DynamicVariables.Settings.Menu.main.title;
                d.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.main.thumbnail;
                d.FindPropertyRelative("categories").arraySize           = 0;
                d.FindPropertyRelative("guid").stringValue              = string.Empty;
                _library.isInitialized = true;
            });
        }
        #endregion

        #region ===== inspector GUI =====
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
                    FindData("name").stringValue = (newIdx != _libraryTagIndex) ? _libraryTagList[newIdx] : newName;
                });
                SyncLibraryTags();
            }
        }

        private void ApplyGlobalToggles()
        {
            bool height = EditorGUILayout.Toggle(_enableHeightLabel, Data.enableHeightParam);
            bool speed  = EditorGUILayout.Toggle(_enableSpeedLabel,  Data.enableSpeedParam);
            bool mirror = EditorGUILayout.Toggle(_enableMirrorLabel, Data.enableMirrorParam);

            if (height == Data.enableHeightParam && speed == Data.enableSpeedParam && mirror == Data.enableMirrorParam) return;

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

        
        public override void OnInspectorGUI()
        {
            EnsureGuiLists();
            SyncCacheSize();
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
            if (_categoryList != null) return;

            _categoryList = new ReorderableList(serializedObject, catProp, true, true, true, true)
            {
                drawHeaderCallback    = r => EditorGUI.LabelField(r, _categoryListLabel),
                elementHeightCallback = GetCategoryHeight,
                drawElementCallback   = DrawCategory,

                onReorderCallback = l =>
                {
                    _thumbnails.Clear();
                    _lastClips.Clear();
                    // _thumbnails.Insert(newIdx, _thumbnails[oldIdx]);     _thumbnails.RemoveAt(oldIdx < newIdx ? oldIdx : oldIdx+1);
                    // _lastClips.Insert(newIdx, _lastClips[oldIdx]);       _lastClips.RemoveAt(oldIdx < newIdx ? oldIdx : oldIdx+1);
                },

                onAddCallback = l => Apply("Add Category", () =>
                {
                    int i = catProp.arraySize;
                    catProp.InsertArrayElementAtIndex(i);
                    var c = catProp.GetArrayElementAtIndex(i);
                    c.FindPropertyRelative("name").stringValue             = DynamicVariables.Settings.Menu.category.title;
                    c.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.category.thumbnail;
                    c.FindPropertyRelative("poses").arraySize              = 0;
                    // 同期して空行を追加
                    _poseLists.Insert(i, null);
                    _thumbnails.Insert(i, new List<Texture2D>());
                    _lastClips.Insert(i, new List<AnimationClip>());
                }),

                onRemoveCallback = l => Apply("Remove Category", () =>
                {
                    catProp.DeleteArrayElementAtIndex(l.index);
                    _poseLists.RemoveAt(l.index);
                    _thumbnails.RemoveAt(l.index);
                    _lastClips.RemoveAt(l.index);
                }),

                onChangedCallback = _ => serializedObject.ApplyModifiedProperties()
            };
        }
        #endregion

        #region ===== Pose list =====
        private ReorderableList EnsurePoseList(int catIdx, SerializedProperty posesProp)
        {
            if (catIdx < _poseLists.Count && _poseLists[catIdx] != null) return _poseLists[catIdx];

            var list = new ReorderableList(serializedObject, posesProp, true, true, true, true)
            {
                drawHeaderCallback    = r => EditorGUI.LabelField(r, _poseListLabel),
                elementHeightCallback = i => GetPoseHeight(catIdx, i),
                drawElementCallback   = (r, i, a, f) => DrawPose(r, catIdx, i, posesProp.GetArrayElementAtIndex(i)),

                onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
                {
                    Swap(_thumbnails[catIdx], oldIndex, newIndex);
                    Swap(_lastClips[catIdx], oldIndex, newIndex);
                },

                onAddCallback = l => Apply("Add Pose", () =>
                {
                    int i = posesProp.arraySize;
                    posesProp.InsertArrayElementAtIndex(i);
                    var p = posesProp.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("name").stringValue             = DynamicVariables.Settings.Menu.pose.title;
                    p.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.pose.thumbnail;
                    p.FindPropertyRelative("animationClip").objectReferenceValue = null;
                    p.FindPropertyRelative("autoThumbnail").boolValue       = true;
                    var tr = p.FindPropertyRelative("tracking");
                    tr.FindPropertyRelative("head").boolValue       = true;
                    tr.FindPropertyRelative("arm").boolValue        = true;
                    tr.FindPropertyRelative("finger").boolValue     = true;
                    tr.FindPropertyRelative("foot").boolValue       = true;
                    tr.FindPropertyRelative("locomotion").boolValue = true;
                    tr.FindPropertyRelative("loop").boolValue       = true;
                    tr.FindPropertyRelative("motionSpeed").floatValue = 0f;
                    // cache 行追加
                    _thumbnails[catIdx].Add(null);
                    _lastClips[catIdx].Add(null);
                }),

                onRemoveCallback = l => Apply("Remove Pose", () =>
                {
                    posesProp.DeleteArrayElementAtIndex(l.index);
                    _thumbnails[catIdx].RemoveAt(l.index);
                    _lastClips[catIdx].RemoveAt(l.index);
                }),

                onChangedCallback = _ => serializedObject.ApplyModifiedProperties()
            };

            while (_poseLists.Count <= catIdx) _poseLists.Add(null);
            _poseLists[catIdx] = list;
            return list;
        }
        #endregion

        #region ===== Foldout/Cache helpers =====
        private static void Swap<T>(List<T> list, int a, int b)
        {
            var tmp = list[a]; list[a] = list[b]; list[b] = tmp;
        }

        private void SyncCacheSize()
        {
            int catCount = Data.categories.Count;

            Resize2DList(_thumbnails, catCount);
            Resize2DList(_lastClips,  catCount);
            for (int c=0; c<catCount; ++c)
            {
                int poseCount = Data.categories[c].poses.Count;
                Resize1D(_thumbnails[c],  poseCount);
                Resize1D(_lastClips[c],   poseCount);
            }
            
            // poseLists 同期
            while (_poseLists.Count > catCount) _poseLists.RemoveAt(_poseLists.Count-1);
            while (_poseLists.Count < catCount) _poseLists.Add(null);
        }

        private static void Resize2DList<T>(List<List<T>> list, int size)
        {
            while (list.Count < size) list.Add(new List<T>());
            while (list.Count > size) list.RemoveAt(list.Count-1);
        }
        private static void Resize1D<T>(List<T> list, int size, T defaultVal=default)
        {
            while (list.Count < size) list.Add(defaultVal);
            while (list.Count > size) list.RemoveAt(list.Count-1);
        }
        #endregion

        #region ===== Drawers =====
        private float GetCategoryHeight(int i)
        {
            var list = EnsurePoseList(i, FindData($"categories.Array.data[{i}].poses"));
            return _lineHeight + 8f + Mathf.Max(_lineHeight*5, _lineHeight) + list.GetHeight() + 60f;
        }

        private void DrawCategory(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty cat   = FindData("categories").GetArrayElementAtIndex(index);
            SerializedProperty name  = cat.FindPropertyRelative("name");
            SerializedProperty icon  = cat.FindPropertyRelative("thumbnail");
            SerializedProperty poses = cat.FindPropertyRelative("poses");

            float y       = rect.y + Spacing;
            float thumbSz = _lineHeight*5f;
            float nameArea= rect.width - Spacing;

            var thumbRect = new Rect(rect.x+Spacing, y, thumbSz, thumbSz);
            var newThumb  = (Texture2D)EditorGUI.ObjectField(thumbRect, _categoryIconLabel, icon.objectReferenceValue, typeof(Texture2D), false);
            if (newThumb != icon.objectReferenceValue) Apply("Edit Category Thumbnail", () => icon.objectReferenceValue = newThumb);
            GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);

            GUI.Label(new Rect(rect.x+Spacing*2+thumbSz, y+_lineHeight, 100, _lineHeight), _categoryTextLabel);
            string catName = EditorGUI.TextField(new Rect(rect.x+Spacing*2+thumbSz, y+_lineHeight*3,
                                                           Mathf.Min(TextBoxWidth, nameArea-thumbSz-15f), _lineHeight),
                                                           name.stringValue);
            if (catName != name.stringValue) Apply("Rename Category", () => name.stringValue = catName);

            // 一括の開閉処理
            y += Mathf.Max(thumbSz, _lineHeight) + Spacing;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_openAllLabel).x, GUI.skin.button.CalcSize(_closeAllLabel).x)+5f;
            if (GUI.Button(new Rect(rect.x+rect.width-btnW*2-10, y, btnW, _lineHeight), _openAllLabel))
                foreach (var t in Data.categories[index].poses)
                    t.value = 1;
            if (GUI.Button(new Rect(rect.x+rect.width-btnW-5, y, btnW, _lineHeight), _closeAllLabel))
                foreach (var t in Data.categories[index].poses)
                    t.value = 0;


            y += _lineHeight + Spacing;
            var list = EnsurePoseList(index, poses);
            list.DoList(new Rect(rect.x, y, rect.width, list.GetHeight()));
            y += list.GetHeight() + Spacing;
            DrawPoseDropArea(new Rect(rect.x, y, rect.width, 40f), poses, index);
        }

        private float GetPoseHeight(int catIdx, int poseIdx)
        {
            // 既知のバグ。Undoで要素を消したときに、ここでエラー発生
            if (Data.categories.Count-1 < catIdx) return _lineHeight;
            if (Data.categories[catIdx].poses.Count-1 < poseIdx) return _lineHeight;
            
            return Data.categories[catIdx].poses[poseIdx].value == 1 ? _lineHeight*7f : _lineHeight*1.5f;
        }

        private void DrawPose(Rect rect, int catIdx, int poseIdx, SerializedProperty poseProp)
        {
            // 既知のバグ。Undoで要素を消したときに、ここでエラー発生
            if (Data.categories.Count-1 < catIdx) return;
            if (Data.categories[catIdx].poses.Count-1 < poseIdx) return;
            
            float y = rect.y + 2f;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_closeLabel).x, GUI.skin.button.CalcSize(_openLabel).x) + 2;
            if (Data.categories[catIdx].poses[poseIdx].value == 1)
            {
                string newName = GUI.TextField(new Rect(rect.x+10, y, Mathf.Min(TextBoxWidth, rect.width-60), _lineHeight),
                                               poseProp.FindPropertyRelative("name").stringValue);
                if (newName != poseProp.FindPropertyRelative("name").stringValue)
                    Apply("Rename Pose", () => poseProp.FindPropertyRelative("name").stringValue = newName);
                if (GUI.Button(new Rect(rect.x+rect.width-btnW, y, btnW, 20), _closeLabel)) 
                    Data.categories[catIdx].poses[poseIdx].value = 0;
            }
            else
            {
                GUI.Label(new Rect(rect.x+10, y, rect.width-60, _lineHeight), poseProp.FindPropertyRelative("name").stringValue);
                if (GUI.Button(new Rect(rect.x+rect.width-btnW, y, btnW, 20), _openLabel))
                    Data.categories[catIdx].poses[poseIdx].value = 1;
                return;
            }
            y += _lineHeight + Spacing + 4;

            float thumbnailSize = _lineHeight*4f;
            float leftWidth = thumbnailSize + Spacing;
            float rightWidth = rect.width - leftWidth - Spacing*3;
            float rightX = rect.x + leftWidth + Spacing*2;

            var thumbRect = new Rect(rect.x, y, thumbnailSize, thumbnailSize);
            SerializedProperty autoTnProp = poseProp.FindPropertyRelative("autoThumbnail");
            SerializedProperty thumbProp  = poseProp.FindPropertyRelative("thumbnail");
            SerializedProperty clipProp   = poseProp.FindPropertyRelative("animationClip");

            if (autoTnProp.boolValue && clipProp.objectReferenceValue)
            {
                if (_lastClips[catIdx][poseIdx] != clipProp.objectReferenceValue)
                {
                    _lastClips[catIdx][poseIdx]=(AnimationClip)clipProp.objectReferenceValue;
                    _thumbnails[catIdx][poseIdx]=GenerateThumbnail(_library.gameObject,(AnimationClip)clipProp.objectReferenceValue);
                }
                if (_thumbnails[catIdx][poseIdx])
                {
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Inspector.thumbnailBg, ScaleMode.StretchToFill,false);
                    GUI.DrawTexture(Rect.MinMaxRect(thumbRect.xMin+1,thumbRect.yMin+1,thumbRect.xMax-1,thumbRect.yMax-1), _thumbnails[catIdx][poseIdx], ScaleMode.StretchToFill,true);
                    GUI.Button(thumbRect, GUIContent.none, GUIStyle.none);
                }
            }
            else
            {
                var newTex=(Texture2D)EditorGUI.ObjectField(thumbRect, thumbProp.objectReferenceValue, typeof(Texture2D), false);
                if(newTex!=thumbProp.objectReferenceValue) Apply("Edit Pose Thumbnail", ()=> thumbProp.objectReferenceValue=newTex);
                GUI.Button(thumbRect,GUIContent.none,GUIStyle.none);
            }

            bool auto = EditorGUI.ToggleLeft(new Rect(rect.x, y+thumbnailSize+Spacing, leftWidth, _lineHeight), _thumbnailAutoLabel, autoTnProp.boolValue);
            if(auto!=autoTnProp.boolValue) Apply("Toggle AutoThumbnail", ()=> autoTnProp.boolValue=auto);
            GUI.Box(new Rect(rightX-Spacing, y,1, thumbnailSize+_lineHeight), GUIContent.none);

            float infoY = rect.y + _lineHeight + Spacing + 4;
            var trProp = poseProp.FindPropertyRelative("tracking");
            var newClip=(AnimationClip)EditorGUI.ObjectField(new Rect(rightX, infoY, rightWidth-Spacing, _lineHeight), _animationClipLabel, clipProp.objectReferenceValue, typeof(AnimationClip), false);
            if(newClip!=clipProp.objectReferenceValue) ApplyClipChange(poseProp,newClip);
            infoY+=_lineHeight+Spacing;
            int flagsOld=FlagsFromTracking(trProp);
            int flagsNew=EditorGUI.MaskField(new Rect(rightX, infoY, rightWidth, _lineHeight), _trackingLabel, flagsOld, _trackingOptions);
            if(flagsNew!=flagsOld) Apply("Edit Tracking Mask", ()=> FlagsToTracking(flagsNew, trProp));
            infoY+=_lineHeight+_lineHeight/2+Spacing; GUI.Box(new Rect(rightX, infoY, rightWidth,1),GUIContent.none); infoY+=_lineHeight/2;
            bool loop=EditorGUI.Toggle(new Rect(rightX, infoY, rightWidth, _lineHeight), _isLoopLabel, trProp.FindPropertyRelative("loop").boolValue);
            if(loop!=trProp.FindPropertyRelative("loop").boolValue) Apply("Toggle Loop", ()=> trProp.FindPropertyRelative("loop").boolValue=loop);
            infoY+=_lineHeight;
            float spd=EditorGUI.FloatField(new Rect(rightX, infoY, rightWidth, _lineHeight), _motionSpeedLabel, trProp.FindPropertyRelative("motionSpeed").floatValue);
            if(!Mathf.Approximately(spd, trProp.FindPropertyRelative("motionSpeed").floatValue)) Apply("Change Motion Speed", ()=> trProp.FindPropertyRelative("motionSpeed").floatValue=spd);
        }

        private void DrawPoseDropArea(Rect area, SerializedProperty posesProp, int catIdx)
        {
            GUI.Box(area, _dropBoxLabel, EditorStyles.helpBox);
            Event evt = Event.current;
            if(!area.Contains(evt.mousePosition)|| evt.type is not (EventType.DragUpdated or EventType.DragPerform)) return;
            DragAndDrop.visualMode=DragAndDropVisualMode.Copy;
            if(evt.type!=EventType.DragPerform) return;
            DragAndDrop.AcceptDrag();
            Apply("Add Pose by Drag&Drop", ()=>
            {
                foreach(var clip in DragAndDrop.objectReferences.OfType<AnimationClip>())
                {
                    int i=posesProp.arraySize;
                    posesProp.InsertArrayElementAtIndex(i);
                    var p=posesProp.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("name").stringValue=clip.name;
                    p.FindPropertyRelative("thumbnail").objectReferenceValue=DynamicVariables.Settings.Menu.pose.thumbnail;
                    p.FindPropertyRelative("animationClip").objectReferenceValue=clip;
                    p.FindPropertyRelative("autoThumbnail").boolValue=true;
                    var tr=p.FindPropertyRelative("tracking");
                    bool moving=MotionBuilder.IsMoveAnimation(clip);
                    tr.FindPropertyRelative("motionSpeed").floatValue=moving?1f:0f;
                    tr.FindPropertyRelative("loop").boolValue=!moving || MotionBuilder.IsLoopAnimation(clip);
                    _thumbnails[catIdx].Add(null);
                    _lastClips[catIdx].Add(null);
                }
            });
            evt.Use();
        }
        #endregion

        #region ===== helpers =====
        private void DetectHierarchyChange()
        {
            string path=GetInstancePath(_library.transform);
            if(path==_instanceIdPathBuffer) return;
            SyncLibraryTags();
            _instanceIdPathBuffer=path;
        }

        private void SyncLibraryTags()
        {
            string[] duplicates=GetLibraryComponents().Select(e=>e.data.name).ToArray();
            _libraryTagList=duplicates.Distinct().ToArray();
            _libraryTagIndex=Array.FindIndex(_libraryTagList, n=>n==Data.name);
        }

        private AvatarPoseLibrary[] GetLibraryComponents()
        {
            var avatar=_library.transform.GetComponentInParent<VRCAvatarDescriptor>();
            return avatar? avatar.GetComponentsInChildren<AvatarPoseLibrary>() : new[]{ _library };
        }

        private void ApplyClipChange(SerializedProperty poseProp, AnimationClip clip)
        {
            Apply("Change Animation Clip", ()=>
            {
                poseProp.FindPropertyRelative("animationClip").objectReferenceValue=clip;
                if(!clip) return;
                poseProp.FindPropertyRelative("name").stringValue=clip.name;
                var tr=poseProp.FindPropertyRelative("tracking");
                bool moving=MotionBuilder.IsMoveAnimation(clip);
                tr.FindPropertyRelative("motionSpeed").floatValue=moving?1f:0f;
                tr.FindPropertyRelative("loop").boolValue=moving? MotionBuilder.IsLoopAnimation(clip):true;
            });
        }

        private Texture2D GenerateThumbnail(GameObject obj, AnimationClip clip)
        {
            var avatar=obj.GetComponentInParent<VRCAvatarDescriptor>(); if(!avatar) return null;
            var clone=Object.Instantiate(avatar.gameObject); Texture2D tex;
            using(var cap=new ThumbnailGenerator(clone)) tex=cap.Capture(clip);
            Object.DestroyImmediate(clone); return tex;
        }

        private static int FlagsFromTracking(SerializedProperty t)
        {
            int f=0; if(t.FindPropertyRelative("head").boolValue) f|=1<<0; if(t.FindPropertyRelative("arm").boolValue) f|=1<<1;
            if(t.FindPropertyRelative("finger").boolValue) f|=1<<2; if(t.FindPropertyRelative("foot").boolValue) f|=1<<3;
            if(t.FindPropertyRelative("locomotion").boolValue) f|=1<<4; return f;
        }
        private static void FlagsToTracking(int f, SerializedProperty t)
        {
            t.FindPropertyRelative("head").boolValue      =(f&(1<<0))!=0;
            t.FindPropertyRelative("arm").boolValue       =(f&(1<<1))!=0;
            t.FindPropertyRelative("finger").boolValue    =(f&(1<<2))!=0;
            t.FindPropertyRelative("foot").boolValue      =(f&(1<<3))!=0;
            t.FindPropertyRelative("locomotion").boolValue=(f&(1<<4))!=0;
        }
        private static string GetInstancePath(Transform t)=> t.parent? GetInstancePath(t.parent)+"/"+t.gameObject.GetInstanceID(): t.gameObject.GetInstanceID().ToString();
        #endregion
    }
}
