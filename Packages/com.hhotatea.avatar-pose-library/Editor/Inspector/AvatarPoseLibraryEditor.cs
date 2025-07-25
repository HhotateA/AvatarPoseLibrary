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

        // GUIContent 定義（省略無し）
        private GUIContent _libraryLabel, _categoryListLabel, _categoryIconLabel, _categoryTextLabel,
                           _openAllLabel, _closeAllLabel, _poseListLabel, _openLabel, _closeLabel,
                           _thumbnailAutoLabel, _animationClipLabel, _trackingLabel, _isLoopLabel,
                           _motionSpeedLabel, _dropBoxLabel, _poseThumbnailLabel, _posePreviewLabel,
                           _enableHeightLabel, _enableSpeedLabel, _enableMirrorLabel, _enableFxLabel,
                           _createCategoryMenu, _cutCategoryMenu, _deleteCategoryMenu, 
                           _createPoseMenu, _cutPoseMenu, _deletePoseMenu, _clearPosesMenu,
                           _copyCategoryMenu, _pasteCategoryMenu, _pasteNewCategoryMenu,
                           _copyPoseMenu, _pastePoseMenu, _pasteNewPoseMenu;

        private string[] _libraryTagList;
        private int _libraryTagIndex;
        private string _instanceIdPathBuffer = string.Empty;
        private string[] _trackingOptions;
        #endregion
        
        #region ===== Foldout/Cache helpers =====

        private readonly Dictionary<string,Texture2D> _thumbnails = new();
        private readonly Dictionary<string,AnimationClip> _lastClips  = new();
        private readonly Dictionary<string,bool> _foldout  = new();

        Texture2D GetThumbnailBuffer(PoseEntry pose)
        {
            var p = GetParameter(pose);
            _thumbnails.TryAdd(p, null);
            return _thumbnails[p];
        }

        void SetThumbnailBuffer(PoseEntry pose,Texture2D value)
        {
            var p = GetParameter(pose);
            _thumbnails.TryAdd(p, null);
            _thumbnails[p] = value;
        }

        AnimationClip GetClipBuffer(PoseEntry pose)
        {
            var p = GetParameter(pose);
            _lastClips.TryAdd(p, null);
            return _lastClips[p];
        }

        void SetClipBuffer(PoseEntry pose,AnimationClip value)
        {
            var p = GetParameter(pose);
            _lastClips.TryAdd(p, null);
            _lastClips[p] = value;
        }

        bool GetFoldoutBuffer(PoseEntry pose)
        {
            var p = GetParameter(pose);
            _foldout.TryAdd(p, false);
            return _foldout[p];
        }

        void SetFoldoutBuffer(PoseEntry pose,bool value)
        {
            var p = GetParameter(pose);
            _foldout.TryAdd(p, false);
            _foldout[p] = value;
        }

        string GetParameter(PoseEntry pose)
        {
            if (String.IsNullOrWhiteSpace(pose.Parameter))
            {
                pose.Parameter = Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            return pose.Parameter;
        }

        void SyncBuffer()
        {
            var ps = _thumbnails.Keys.ToList();
            foreach (var category in Data.categories)
            {
                foreach (var pose in category.poses)
                {
                    var p = GetParameter(pose);
                    ps.Remove(p);
                    _thumbnails.TryAdd(p, null);
                    _lastClips.TryAdd(p, null);
                    _foldout.TryAdd(p, false);
                }
            }

            foreach (var p in ps)
            {
                if(_thumbnails.ContainsKey(p))
                    _thumbnails.Remove(p);
                if(_lastClips.ContainsKey(p))
                    _lastClips.Remove(p);
                if(_foldout.ContainsKey(p))
                    _foldout.Remove(p);
            }
        }
        
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

            foreach (var category in Data.categories)
            {
                foreach (var pose in category.poses)
                {
                    // パラメーターの初期化。
                    pose.Parameter = "";
                }
            }
            
            // 初期化時に、タグを合わせる
            SyncGlobalToggles(Data.name);
        }

        // BuildGuiContent() 省略なし
        private void BuildGuiContent()
        {
            var i= DynamicVariables.Settings.Inspector;
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
            _enableFxLabel      = new(i.enableFxLabel, i.enableFxTooltip);
            _poseThumbnailLabel = new("",i.poseThumbnailTooltip);
            _posePreviewLabel   = new("",i.posePreviewTooltip);
            _createCategoryMenu = new(i.createCategoryLabel, i.createCategoryTooltip);
            _deleteCategoryMenu = new(i.deleteCategoryLabel, i.deleteCategoryTooltip);
            _copyCategoryMenu   = new(i.copyCategoryLabel, i.copyCategoryTooltip); 
            _cutCategoryMenu    = new(i.cutCategoryLabel, i.cutCategoryTooltip); 
            _pasteCategoryMenu  = new(i.pasteCategoryLabel, i.pasteCategoryTooltip); 
            _pasteNewCategoryMenu = new(i.pasteNewCategoryLabel, i.pasteNewCategoryTooltip);
            _createPoseMenu     = new(i.createPoseLabel, i.createPoseTooltip);
            _deletePoseMenu     = new(i.deletePoseLabel, i.deletePoseTooltip);
            _clearPosesMenu     = new(i.clearPoseLabel, i.clearPoseTooltip);
            _copyPoseMenu       = new(i.copyPoseLabel, i.copyPoseTooltip); 
            _cutPoseMenu        = new(i.cutPoseLabel, i.cutPoseTooltip); 
            _pastePoseMenu      = new(i.pastePoseLabel, i.pastePoseTooltip); 
            _pasteNewPoseMenu   = new(i.pasteNewPoseLabel, i.pasteNewPoseTooltip);

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
            // データの初期化
            if (_library.isInitialized) return;
            _library.data = new AvatarPoseData();
            _library.data.name = DynamicVariables.Settings.Menu.main.title;
            _library.data.thumbnail = DynamicVariables.Settings.Menu.main.thumbnail;
            _library.isInitialized = true;
        }
        #endregion

        #region ===== inspector GUI =====
        private void DrawMainHeader()
        {
            string newName = Data.name;
            int    newIdx  = _libraryTagIndex;

            float texSize = _lineHeight * 8f;
            if (DynamicVariables.CurrentVersion < DynamicVariables.LatestVersion)
            {
                GUIStyle updateStyle = new GUIStyle(EditorStyles.boldLabel);
                updateStyle.normal.textColor = Color.red;

                EditorGUILayout.LabelField(
                    DynamicVariables.Settings.Inspector.updateMessage + $" (v{DynamicVariables.CurrentVersion} => v{DynamicVariables.LatestVersion})", updateStyle);
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(Data.thumbnail, DynamicVariables.Settings.Inspector.mainThumbnailTooltip),
                                GUILayout.Width(texSize), GUILayout.Height(texSize));

                using (new GUILayout.VerticalScope())
                {
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
                SyncGlobalToggles((newIdx != _libraryTagIndex) ? _libraryTagList[newIdx] : newName);
                SyncLibraryTags();
            }
        }

        private void ApplyGlobalToggles()
        {
            bool height   = EditorGUILayout.Toggle(_enableHeightLabel, Data.enableHeightParam);
            bool speed    = EditorGUILayout.Toggle(_enableSpeedLabel,  Data.enableSpeedParam);
            bool mirror   = EditorGUILayout.Toggle(_enableMirrorLabel, Data.enableMirrorParam);
            bool fxLayer  = EditorGUILayout.Toggle(_enableFxLabel, Data.enableFxParam);

            if (height == Data.enableHeightParam && 
                speed == Data.enableSpeedParam && 
                mirror == Data.enableMirrorParam && 
                fxLayer == Data.enableFxParam) return;

            Apply("Toggle Global Flags", () =>
            {
                foreach (var lib in GetLibraryComponents().Where(l => l.data.name == Data.name))
                {
                    var so = new SerializedObject(lib);
                    so.FindProperty("data.enableHeightParam").boolValue = height;
                    so.FindProperty("data.enableSpeedParam").boolValue  = speed;
                    so.FindProperty("data.enableMirrorParam").boolValue = mirror;
                    so.FindProperty("data.enableFxParam").boolValue     = fxLayer;
                    so.ApplyModifiedProperties();
                }
            });
        }

        private void SyncGlobalToggles(string tag)
        {
            // 名前の整合性を取る
            if (Data.name != tag)
            {
                Apply("Sync APL Name", () =>
                {
                    FindData("name").stringValue = tag;
                });
            }
            
            // フラグの整合性を取る。
            var comp = GetLibraryComponents().FirstOrDefault(
                e => e.data.name == tag && e.data != Data);
            if (!comp) return;
            if (comp.data.enableHeightParam   != Data.enableHeightParam ||
                comp.data.enableSpeedParam    != Data.enableSpeedParam ||
                comp.data.enableMirrorParam   != Data.enableMirrorParam ||
                comp.data.enableFxParam       != Data.enableFxParam)
            {
                Apply("Sync APL Param", () =>
                {
                    FindData("enableHeightParam").boolValue = comp.data.enableHeightParam;
                    FindData("enableSpeedParam").boolValue  = comp.data.enableSpeedParam;
                    FindData("enableMirrorParam").boolValue = comp.data.enableMirrorParam;
                    FindData("enableFxParam").boolValue     = comp.data.enableFxParam;
                });
            }
        }

        
        public override void OnInspectorGUI()
        {
            EnsureGuiLists();
            DetectHierarchyChange();
            SyncBuffer();

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

                onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
                {
                    // fold outの同期を入れるべきだけど、この時点でデータは失われている。
                },

                onAddCallback = l => Apply("Add Category", () =>
                {
                    int i = catProp.arraySize;
                    catProp.InsertArrayElementAtIndex(i);
                    var c = catProp.GetArrayElementAtIndex(i);
                    c.FindPropertyRelative("name").stringValue               = DynamicVariables.Settings.Menu.category.title;
                    c.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.category.thumbnail;
                    c.FindPropertyRelative("poses").arraySize                = 0;
                    _poseLists.Insert(i, null);
                }),

                onRemoveCallback = l => Apply("Remove Category", () =>
                {
                    catProp.DeleteArrayElementAtIndex(l.index);
                    _poseLists.Clear();
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
                    var a = Data.categories[catIdx].poses[oldIndex].Parameter;
                    var b = Data.categories[catIdx].poses[newIndex].Parameter;
                    Data.categories[catIdx].poses[oldIndex].Parameter = b;
                    Data.categories[catIdx].poses[newIndex].Parameter = a;
                },


                onAddCallback = l => Apply("Add Pose", () =>
                {
                    AddPose(posesProp);
                }),

                onRemoveCallback = l => Apply("Remove Pose", () =>
                {
                    posesProp.DeleteArrayElementAtIndex(l.index);
                }),

                onChangedCallback = _ => serializedObject.ApplyModifiedProperties()
            };

            while (_poseLists.Count <= catIdx) _poseLists.Add(null);
            _poseLists[catIdx] = list;
            return list;
        }
        #endregion

        #region ===== Drawers =====
        private float GetCategoryHeight(int i)
        {
            var list = EnsurePoseList(i, FindData($"categories.Array.data[{i}].poses"));
            return _lineHeight + 8f + Mathf.Max(_lineHeight*5, _lineHeight) + list.GetHeight() + 60f;
        }

        void CategorySubmenu(Rect rect, int catIdx)
        {
            // 右クリックメニュー
            Event evt = Event.current;
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(_copyCategoryMenu, false, () => CopyCategory(catIdx));
                menu.AddItem(_cutCategoryMenu, false, () =>
                {
                    if (CopyCategory(catIdx))
                    {
                        Data.categories.RemoveAt(catIdx);
                    }
                });
                if (IsValidJson(EditorGUIUtility.systemCopyBuffer) == JsonType.Category)
                {
                    menu.AddItem(_pasteCategoryMenu, false, () => PasteCategory(catIdx, false));
                    menu.AddItem(_pasteNewCategoryMenu, false, () => PasteCategory(catIdx, true));
                }
                else
                {
                    menu.AddDisabledItem(_pasteCategoryMenu);
                    menu.AddDisabledItem(_pasteNewCategoryMenu);
                }
                
                menu.AddSeparator("");

                menu.AddItem(_clearPosesMenu, false, () => Apply("Clear Pose", () =>
                {
                    Data.categories[catIdx].poses = new List<PoseEntry>();
                }));
                
                // トラッキング設定定義
                var trackingSettings = new[]
                {
                    new {
                        Label = DynamicVariables.Settings.Inspector.autoThumbnailMenu,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.autoThumbnail = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.autoThumbnail == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.headTrackingOption,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.head = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.head == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.armTrackingOption,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.arm = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.arm == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.fingerTrackingOption,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.finger = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.finger == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.footTrackingOption,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.foot = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.foot == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.locomotionTrackingOption,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.locomotion = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.locomotion == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.isLoopLabel,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.loop = enabled),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=>p.tracking.loop == enabled))
                    },
                    new {
                        Label = DynamicVariables.Settings.Inspector.motionSpeedLabel,
                        Setter = new Action<PoseEntry, bool>((pose, enabled) =>
                            pose.tracking.motionSpeed = enabled ? 1f : 0f),
                        Selector = new Func<List<PoseEntry>,bool,bool>((poses, enabled) =>
                            poses.All(p=> 
                                Math.Abs(p.tracking.motionSpeed - (enabled ? 1f : 0f)) < 0.001f))
                    },
                };

                // 共通メニュー生成ループ
                foreach (var setting in trackingSettings)
                {
                    AddTrackingMenu(menu,Data.categories[catIdx].poses,
                        setting.Label, DynamicVariables.Settings.Inspector.enableMenuLabel,
                        pose => setting.Setter(pose, true),
                        poses => setting.Selector(poses,true));

                    AddTrackingMenu(menu,Data.categories[catIdx].poses,
                        setting.Label, DynamicVariables.Settings.Inspector.disableMenuLabel,
                        pose => setting.Setter(pose, false),
                    poses => setting.Selector(poses,false));
                }
                
                menu.AddSeparator("");
                
                menu.AddItem(_createCategoryMenu, false, () =>
                {
                    Apply("Add Category", () =>
                    {
                        Data.categories.Insert(catIdx+1,new PoseCategory
                        {
                            name = DynamicVariables.Settings.Menu.category.title,
                            thumbnail = DynamicVariables.Settings.Menu.category.thumbnail,
                            poses = new List<PoseEntry>()
                        });
                    });
                });
                
                menu.AddItem(_deleteCategoryMenu, false, () =>
                {
                    Apply("Remove Category", () =>
                    {
                        Data.categories.RemoveAt(catIdx);
                    });
                });

                menu.ShowAsContext();
                evt.Use();
            }
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

            CategorySubmenu(new Rect(rect.x,rect.y,rect.width,_lineHeight*3f),index);

            var thumbRect = new Rect(rect.x+Spacing, y, thumbSz, thumbSz);
            var newThumb  = (Texture2D)EditorGUI.ObjectField(thumbRect, _categoryIconLabel, icon.objectReferenceValue, typeof(Texture2D), false);
            if (newThumb != icon.objectReferenceValue) Apply("Edit Category Thumbnail", () => icon.objectReferenceValue = newThumb);
            GUI.Button(thumbRect, _categoryIconLabel, GUIStyle.none);

            GUI.Label(new Rect(rect.x+Spacing*2+thumbSz, y+_lineHeight, 200, _lineHeight), _categoryTextLabel);
            string catName = EditorGUI.TextField(new Rect(rect.x+Spacing*2+thumbSz, y+_lineHeight*3,
                                                           Mathf.Min(TextBoxWidth, nameArea-thumbSz-15f), _lineHeight),
                                                           name.stringValue);
            if (catName != name.stringValue) Apply("Rename Category", () => name.stringValue = catName);

            // 一括の開閉処理
            y += Mathf.Max(thumbSz, _lineHeight) + Spacing;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_openAllLabel).x, GUI.skin.button.CalcSize(_closeAllLabel).x)+5f;
            if (GUI.Button(new Rect(rect.x+rect.width-btnW*2-10, y, btnW, _lineHeight), _openAllLabel))
                foreach (var t in Data.categories[index].poses)
                    SetFoldoutBuffer(t,true);
            if (GUI.Button(new Rect(rect.x+rect.width-btnW-5, y, btnW, _lineHeight), _closeAllLabel))
                foreach (var t in Data.categories[index].poses)
                    SetFoldoutBuffer(t,false);


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
            
            return GetFoldoutBuffer(Data.categories[catIdx].poses[poseIdx]) ? _lineHeight*7f : _lineHeight*1.5f;
        }

        void PoseSubmenu(Rect rect, int catIdx, int poseIdx, SerializedProperty poseProp)
        {
            // 右クリックメニュー
            Event evt = Event.current;
            if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(_copyPoseMenu, false, () => CopyPose(catIdx, poseIdx));
                menu.AddItem(_cutPoseMenu, false, () =>
                {
                    if (CopyPose(catIdx, poseIdx))
                    {
                        Data.categories[catIdx].poses.RemoveAt(poseIdx);
                    }
                });
                if (IsValidJson(EditorGUIUtility.systemCopyBuffer) == JsonType.Pose)
                {
                    menu.AddItem(_pastePoseMenu, false, () => PastePose(catIdx, poseIdx, false));
                    menu.AddItem(_pasteNewPoseMenu, false, () => PastePose(catIdx, poseIdx, true));
                }
                else
                {
                    menu.AddDisabledItem(_pastePoseMenu);
                    menu.AddDisabledItem(_pasteNewPoseMenu);
                }
                
                menu.AddSeparator("");
                
                menu.AddItem(_createPoseMenu, false, () =>
                {
                    var newPose = new PoseEntry
                    {
                        name = DynamicVariables.Settings.Menu.pose.title,
                        thumbnail = DynamicVariables.Settings.Menu.pose.thumbnail,
                        autoThumbnail = true,
                    };
                    Apply("Add Pose", () =>
                    {
                        Data.categories[catIdx].poses.Insert(poseIdx+1,newPose);
                    });
                    
                    // 開いた状態で張り付ける
                    SetFoldoutBuffer(newPose,true);
                });
                
                menu.AddItem(_deletePoseMenu, false, () =>
                {
                    Apply("Remove Pose", () =>
                    {
                        Data.categories[catIdx].poses.RemoveAt(poseIdx);
                    });
                });

                menu.ShowAsContext();
                evt.Use();
            }
        }

        private void DrawPose(Rect rect, int catIdx, int poseIdx, SerializedProperty poseProp)
        {
            // 既知のバグ。Undoで要素を消したときに、ここでエラー発生
            if (Data.categories.Count-1 < catIdx) return;
            if (Data.categories[catIdx].poses.Count-1 < poseIdx) return;

            PoseSubmenu(rect, catIdx, poseIdx, poseProp);
            Rect fullRect = new Rect(rect.x, rect.y, rect.width, _lineHeight);
            
            float y = rect.y + 2f;
            float btnW = Mathf.Max(GUI.skin.button.CalcSize(_closeLabel).x, GUI.skin.button.CalcSize(_openLabel).x) + 2;
            if (GetFoldoutBuffer(Data.categories[catIdx].poses[poseIdx]))
            {
                string newName = GUI.TextField(new Rect(rect.x+10, y, Mathf.Min(TextBoxWidth, rect.width-60), _lineHeight),
                                               poseProp.FindPropertyRelative("name").stringValue);
                if (newName != poseProp.FindPropertyRelative("name").stringValue)
                    Apply("Rename Pose", () => poseProp.FindPropertyRelative("name").stringValue = newName);
                if (GUI.Button(new Rect(rect.x+rect.width-btnW, y, btnW, 20), _closeLabel))
                    SetFoldoutBuffer(Data.categories[catIdx].poses[poseIdx],false);
            }
            else
            {
                GUI.Label(new Rect(rect.x+10, y, rect.width-60, _lineHeight), poseProp.FindPropertyRelative("name").stringValue);
                if (GUI.Button(new Rect(rect.x+rect.width-btnW, y, btnW, 20), _openLabel))
                    SetFoldoutBuffer(Data.categories[catIdx].poses[poseIdx],true);
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
                var p = Data.categories[catIdx].poses[poseIdx];
                if (GetClipBuffer(p) != clipProp.objectReferenceValue)
                {
                    SetClipBuffer(p,(AnimationClip)clipProp.objectReferenceValue);
                    SetThumbnailBuffer(p,GenerateThumbnail(_library.gameObject,(AnimationClip)clipProp.objectReferenceValue));
                }
                if (GetThumbnailBuffer(p))
                {
                    GUI.DrawTexture(thumbRect, DynamicVariables.Settings.Inspector.thumbnailBg, ScaleMode.StretchToFill,false);
                    GUI.DrawTexture(Rect.MinMaxRect(thumbRect.xMin+1,thumbRect.yMin+1,thumbRect.xMax-1,thumbRect.yMax-1), GetThumbnailBuffer(p), ScaleMode.StretchToFill,true);
                    GUI.Button(thumbRect, _posePreviewLabel, GUIStyle.none);
                }
            }
            else
            {
                var newTex=(Texture2D)EditorGUI.ObjectField(thumbRect, thumbProp.objectReferenceValue, typeof(Texture2D), false);
                if(newTex!=thumbProp.objectReferenceValue) Apply("Edit Pose Thumbnail", ()=> thumbProp.objectReferenceValue=newTex);
                GUI.Button(thumbRect, _poseThumbnailLabel, GUIStyle.none);
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
                    AddPose(posesProp,clip);
                }
            });
            evt.Use();
        }
        #endregion

        #region ===== helpers =====

        private void AddPose(SerializedProperty posesProp, AnimationClip clip = null)
        {
            int i = posesProp.arraySize;
            posesProp.InsertArrayElementAtIndex(i);
            var p= posesProp.GetArrayElementAtIndex(i);

            // 初期値の設定
            p.FindPropertyRelative("thumbnail").objectReferenceValue = DynamicVariables.Settings.Menu.pose.thumbnail;
            p.FindPropertyRelative("animationClip").objectReferenceValue = clip;
            p.FindPropertyRelative("name").stringValue = DynamicVariables.Settings.Menu.pose.title;

            p.FindPropertyRelative("autoThumbnail").boolValue=true;
            
            var tr=p.FindPropertyRelative("tracking");
            tr.FindPropertyRelative("motionSpeed").floatValue = 1f;
            tr.FindPropertyRelative("loop").boolValue = true;
            tr.FindPropertyRelative("head").boolValue = true;
            tr.FindPropertyRelative("arm").boolValue = true;
            tr.FindPropertyRelative("foot").boolValue = true;
            tr.FindPropertyRelative("finger").boolValue = true;
            tr.FindPropertyRelative("locomotion").boolValue = false;
            
            if (i > 0)
            {
                // カテゴリ内の値を参照
                var pm= posesProp.GetArrayElementAtIndex(i-1);
                var trm=pm.FindPropertyRelative("tracking");
                p.FindPropertyRelative("autoThumbnail").boolValue = pm.FindPropertyRelative("autoThumbnail").boolValue;
                tr.FindPropertyRelative("head").boolValue = trm.FindPropertyRelative("head").boolValue;
                tr.FindPropertyRelative("arm").boolValue = trm.FindPropertyRelative("arm").boolValue;
                tr.FindPropertyRelative("foot").boolValue = trm.FindPropertyRelative("foot").boolValue;
                tr.FindPropertyRelative("finger").boolValue = trm.FindPropertyRelative("finger").boolValue;
                tr.FindPropertyRelative("locomotion").boolValue = trm.FindPropertyRelative("locomotion").boolValue;
            }

            if (clip)
            {
                p.FindPropertyRelative("name").stringValue = clip.name;
                
                // アニメーション種別による初期値
                bool moving = MotionBuilder.IsMoveAnimation(clip);
                tr.FindPropertyRelative("motionSpeed").floatValue = moving?1f:0f;
                tr.FindPropertyRelative("loop").boolValue =! moving || MotionBuilder.IsLoopAnimation(clip);
            }
        }
        
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
        
        private bool CopyCategory(int catIdx)
        {
            try
            {
                var pose = Data.categories[catIdx];
                string json = JsonUtility.ToJson(pose);
                EditorGUIUtility.systemCopyBuffer = json;
            }
            catch (Exception e)
            {
                Debug.LogError("Copy category failed: " + e.Message);
                return false;
            }

            return true;
        }

        private void PasteCategory(int catIdx, bool asNew = false)
        {
            try
            {
                string json = EditorGUIUtility.systemCopyBuffer;

                PoseCategory newCat = JsonUtility.FromJson<PoseCategory>(json);
                if (newCat == null) return;

                Apply("Paste category", () =>
                {
                    if (asNew)
                    {
                        Data.categories.Insert(catIdx + 1, newCat);
                        
                    }
                    else
                    {
                        Data.categories[catIdx] = newCat;
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError("Paste pose failed: " + e.Message);
            }
        }
        private bool CopyPose(int catIdx, int poseIdx)
        {
            try
            {
                var pose = Data.categories[catIdx].poses[poseIdx];
                string json = JsonUtility.ToJson(pose);
                EditorGUIUtility.systemCopyBuffer = json;
            }
            catch (Exception e)
            {
                Debug.LogError("Copy pose failed: " + e.Message);
                return false;
            }
            return true;
        }

        private void PastePose(int catIdx, int poseIdx, bool asNew = false)
        {
            try
            {
                string json = EditorGUIUtility.systemCopyBuffer;

                PoseEntry newPose = JsonUtility.FromJson<PoseEntry>(json);
                if (newPose == null) return;

                Apply("Paste pose", () =>
                {
                    if (asNew)
                    {
                        Data.categories[catIdx].poses.Insert(poseIdx + 1, newPose);
                
                        // 開いた状態で張り付ける
                        SetFoldoutBuffer(newPose,true);
                    }
                    else
                    {
                        var foldout = GetFoldoutBuffer(Data.categories[catIdx].poses[poseIdx]);
                        Data.categories[catIdx].poses[poseIdx] = newPose;
                
                        // 開いた状態で張り付ける
                        SetFoldoutBuffer(newPose,foldout);
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogError("Paste pose failed: " + e.Message);
            }
        }

        enum JsonType
        {
            Pose,
            Category,
            None
        }
        private JsonType IsValidJson(string json)
        {
            if (string.IsNullOrEmpty(json)) 
                return JsonType.None;
            if (!json.StartsWith("{") || !json.EndsWith("}")) 
                return JsonType.None;
            if (json.Contains("poses"))
            {
                return JsonType.Category;
            }
            if (json.Contains("animationClip"))
            {
                return JsonType.Pose;
            }
            return JsonType.None;
        }
                
        void MenuHelper(GenericMenu menu, string parent, string label, Action onSelect, bool selected)
        {
            menu.AddItem(new GUIContent($"{parent}/{label}"), selected, () =>
            {
                onSelect?.Invoke();
            });
        }

        void AddTrackingMenu(GenericMenu menu, List<PoseEntry> poses,
            string optionName, string label, 
            Action<PoseEntry> trackingSetter, Func<List<PoseEntry>,bool> trackingSelected)
        {
            if (poses.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent($"{optionName}"));
            }
            else
            {
                MenuHelper(menu, optionName, label, () =>
                {
                    Apply($"{optionName} {label}", () =>
                    {
                        foreach (var pose in poses)
                        {
                            trackingSetter(pose);
                        }
                    });
                }, trackingSelected.Invoke(poses));
            }
        }
        
        #endregion
    }
}
