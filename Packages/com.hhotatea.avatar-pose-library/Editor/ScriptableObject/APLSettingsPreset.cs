using System;
using UnityEngine;
using com.hhotatea.avatar_pose_library.model;

namespace com.hhotatea.avatar_pose_library.component
{
    [CreateAssetMenu(menuName = "HhotateA/APLSettingsPreset")]
    public class APLSettingsPreset : ScriptableObject
    {
        public string name{ get; set; }
        public string defaultName;
        public bool heightParam = true;
        public bool speedParam = true;
        public bool mirrorParam = true;
        public bool trackingParam = true;
        public bool deepSync = true;
        public bool poseSpace = true;
        public bool locomotionAnimator = true;
        public bool fxAnimator = true;

        public bool Is(AvatarPoseData data)
        {
            if (data.enableHeightParam != heightParam) return false;
            if (data.enableSpeedParam != speedParam) return false;
            if (data.enableMirrorParam != mirrorParam) return false;
            if (data.enableTrackingParam != trackingParam) return false;
            if (data.enableDeepSync != deepSync) return false;
            if (data.enablePoseSpace != poseSpace) return false;
            if (data.enableLocomotionAnimator != locomotionAnimator) return false;
            if (data.enableFxAnimator != fxAnimator) return false;
            return true;
        }

        public void Apply(AvatarPoseData data)
        {
            data.enableHeightParam = heightParam;
            data.enableSpeedParam = speedParam;
            data.enableMirrorParam = mirrorParam;
            data.enableTrackingParam = trackingParam;
            data.enableDeepSync = deepSync;
            data.enablePoseSpace = poseSpace;
            data.enableLocomotionAnimator = locomotionAnimator;
            data.enableFxAnimator = fxAnimator;
        }
    }
}