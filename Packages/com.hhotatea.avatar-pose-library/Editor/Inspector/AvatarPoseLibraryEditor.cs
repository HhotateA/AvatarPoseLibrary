using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.editor
{
    [CustomEditor(typeof(AvatarPoseLibrarySettings))]
    public class AvatarPoseLibraryEditor : UnityEditor.Editor
    {
        private AvatarPoseLibrarySettings poseLibrary;
        public AvatarPoseData data => poseLibrary.data;

        private void OnEnable()
        {
            poseLibrary = (AvatarPoseLibrarySettings)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }
}