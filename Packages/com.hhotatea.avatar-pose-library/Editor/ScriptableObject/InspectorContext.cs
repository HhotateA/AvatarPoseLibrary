using System;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{ 
    // [CreateAssetMenu(menuName = "HhotateA/InspectorContext")]
    public class InspectorContext : ScriptableObject
    {
        public string libraryMenuLabel = "Library Name";
        [Multiline] public string libraryMenuTooltip = "ここの文字列が同じメニューは統合されます。";
        public string categoriesLabel = "Pose Categories";
        [Multiline] public string categoriesTooltip = "カテゴリーのリストです。";
        public string categoryIconLabel = "";
        [Multiline] public string categoryIconTooltip = "メニューに表示される、カテゴリーアイコン。";
        public string categoryTextLabel = "Category Name";
        [Multiline] public string categoryTextTooltip = "メニューに表示される、カテゴリー名。";
        public string openAllLabel = "Open All";
        [Multiline] public string openAllTooltip = "Pose項目を全て開きます。";
        public string closeAllLabel = "Close All";
        [Multiline] public string closeAllTooltip = "Pose項目を全て閉じます。";
        public string openLabel = "Open";
        [Multiline] public string openTooltip = "Pose項目を展開して編集します。";
        public string closeLabel = "Close";
        [Multiline] public string closeTooltip = "Pose項目を閉じます。";
        public string poseListLabel = "Pose List";
        [Multiline] public string poseListTooltip = "登録されたポーズリストです。";
        public string thumbnailAutoLabel = "Auto Thumbnail";
        [Multiline] public string thumbnailAutoTooltip = "サムネイルの自動撮影機能を有効にします。";
        public string animationClipLabel = "Animation Clip";
        [Multiline] public string animationClipTooltip = "再生するアニメーションクリック。";
        public string trackingSettingsLabel = "Tracking Settings";
        [Multiline] public string trackingSettingsTooltip = "トラッキングの設定。";
        public string isLoopLabel = "Is Loop";
        [Multiline] public string isLoopTooltip = "アニメーションをループさせる。";
        public string motionSpeedLabel = "Motion Speed";
        [Multiline] public string motionSpeedTooltip = "アニメーションの再生速度。";
        public string dropboxLabel = "Drop AnimationClips here to add Poses";
        [Multiline] public string dropboxTooltip = "ここにアニメーションを一括ドロップすることでPoseを生成できます。";
        [Multiline] public string mainThumbnailTooltip = "アバターポーズライブラリー by.HhotateA_xR";
        [Multiline] public string categoryThumbnailTooltip = "メニューに表示されるサムネイル画像のプレビューです。";
        [Multiline] public string poseThumbnailTooltip = "メニューに表示されるサムネイル画像のプレビューです。";
        [Multiline] public string posePreviewTooltip = "メニューに表示されるサムネイル画像のプレビューです。\nプレビューでは服などの表示がずれるケースが有りますが、アップロード時に再撮影されます。";

        public string enableHeightLabel = "Enable Height";
        [Multiline] public string enableHeightTooltip = "高さ調整機能を有効にする。";
        public string enableSpeedLabel = "Enable Speed";
        [Multiline] public string enableSpeedTooltip = "スピード調整機能を有効にする。";
        public string enableMirrorLabel = "Enable Mirror";
        [Multiline] public string enableMirrorTooltip = "ミラー機能を有効にする。";
        public string enableTrackingLabel = "Enable Tracking";
        [Multiline] public string enableTrackingTooltip = "トラッキングメニューを有効にする。";
        
        
        public Texture2D thumbnailBg;
        
        public string headTrackingOption = "Head Lock";
        public string armTrackingOption = "Arm Lock";
        public string fingerTrackingOption = "Finger Lock";
        public string footTrackingOption = "Foot Lock";
        public string locomotionTrackingOption = "Move Lock";

    }
}