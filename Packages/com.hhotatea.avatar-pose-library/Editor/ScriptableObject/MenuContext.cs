using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
    // [CreateAssetMenu(menuName = "HhotateA/MenuContext")]
    public class MenuContext : ScriptableObject
    {
        public MenuItem main;
        public MenuItem category;
        public MenuItem pose;
        public MenuItem tracking;
    
        public MenuItem reset;
        public MenuItem setting;
        public MenuItem height;
        public MenuItem head;
        public MenuItem arm;
        public MenuItem foot;
        public MenuItem finger;
        public MenuItem face;
        public MenuItem speed;
        public MenuItem locomotion;
        public MenuItem mirror;
    
        [Serializable]
        public class MenuItem
        {
            public string title;
            public Texture2D thumbnail;
        }
    }
}