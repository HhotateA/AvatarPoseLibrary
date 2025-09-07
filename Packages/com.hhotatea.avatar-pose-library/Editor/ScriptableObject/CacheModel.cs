using System;
using UnityEditor.Animations;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
    public class CacheModel : ScriptableObject
    {
        public string libraryName;
        public AnimatorController locomotionLayer;
        public AnimatorController paramLayer;
        public AnimatorController trackingLayer;
        public GameObject menuObject;
        public GameObject paramObject;
    }
}