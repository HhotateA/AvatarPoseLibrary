using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{ 
    // [CreateAssetMenu(menuName = "HhotateA/InspectorContext")]
    public class InspectorContext : ScriptableObject
    {
        public string libraryMenuLabel = "Library Name";
        public string libraryMenuTooltip = "ここの文字列が同じメニューは統合されます。";
        public string categoriesLabel = "Pose Categories";
        public string categoriesTooltip = "カテゴリーのリストです。";
        public string categoryIconLabel = "";
        public string categoryIconTooltip = "メニューに表示される、カテゴリーアイコン。";
        public string categoryTextLabel = "Category Name";
        public string categoryTextTooltip = "メニューに表示される、カテゴリー名。";
        public string openAllLabel = "Open All";
        public string openAllTooltip = "Pose項目を全て開きます。";
        public string closeAllLabel = "Close All";
        public string closeAllTooltip = "Pose項目を全て閉じます。";
        public string openLabel = "Open";
        public string openTooltip = "Pose項目を展開して編集します。";
        public string closeLabel = "Close";
        public string closeTooltip = "Pose項目を閉じます。";
        public string poseListLabel = "Pose List";
        public string poseListTooltip = "登録されたポーズリストです。";
        public string thumbnailAutoLabel = "Auto Thumbnail";
        public string thumbnailAutoTooltip = "サムネイルの自動撮影機能を有効にします。";
        public string animationClipLabel = "Animation Clip";
        public string animationClipTooltip = "再生するアニメーションクリック。";
        public string trackingSettingsLabel = "Tracking Settings";
        public string trackingSettingsTooltip = "トラッキングの設定。";
        public string isLoopLabel = "Is Loop";
        public string isLoopTooltip = "アニメーションをループさせる。";
        public string motionSpeedLabel = "Motion Speed";
        public string motionSpeedTooltip = "アニメーションの再生速度。";
        public string dropboxLabel = "Drop AnimationClips here to add Poses";
        public string dropboxTooltip = "ここにアニメーションを一括ドロップすることでPoseを生成できます。";

        public Texture2D thumbnailBg;
        
        public string headTrackingOption = "Head Lock";
        public string armTrackingOption = "Arm Lock";
        public string fingerTrackingOption = "Finger Lock";
        public string footTrackingOption = "Foot Lock";
        public string locomotionTrackingOption = "Move Lock";

    }
}