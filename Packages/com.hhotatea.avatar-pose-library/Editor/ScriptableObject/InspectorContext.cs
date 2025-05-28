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
        public string enableFxLabel = "Enable Fx";
        [Multiline] public string enableFxTooltip = "表情用のレイヤーを有効にする。";
        
        public string createCategoryLabel = "Create Category";
        [Multiline] public string createCategoryTooltip = "カテゴリーを新規作成。";
        public string deleteCategoryLabel = "Delete Category";
        [Multiline] public string deleteCategoryTooltip = "選択中のカテゴリーを削除。";
        public string copyCategoryLabel = "Copy Category";
        [Multiline] public string copyCategoryTooltip = "選択中のカテゴリーをコピー。";
        public string cutCategoryLabel = "Cut Category";
        [Multiline] public string cutCategoryTooltip = "選択中のカテゴリーを切り取り。";
        public string pasteCategoryLabel = "Paste Category";
        [Multiline] public string pasteCategoryTooltip = "選択中のカテゴリーを上書き。";
        public string pasteNewCategoryLabel = "Paste Category As New";
        [Multiline] public string pasteNewCategoryTooltip = "ここにコピーを作成。";
        public string createPoseLabel = "Create Pose";
        [Multiline] public string createPoseTooltip = "ポーズを新規作成。";
        public string deletePoseLabel = "Delete Pose";
        [Multiline] public string deletePoseTooltip = "選択中のポーズを削除。";
        public string copyPoseLabel = "Copy Pose";
        [Multiline] public string copyPoseTooltip = "選択中のポーズをコピー。";
        public string cutPoseLabel = "Cut Pose";
        [Multiline] public string cutPoseTooltip = "選択中のポーズを切り取り。";
        public string pastePoseLabel = "Paste Pose";
        [Multiline] public string pastePoseTooltip = "選択中のポーズを上書き。";
        public string pasteNewPoseLabel = "Paste Pose As New";
        [Multiline] public string pasteNewPoseTooltip = "ここにコピーを作成。";
        
        public string autoThumbnailMenu = "Auto Thumbnail";
        
        public string disableMenuLabel = "Disable All Pose";
        [Multiline] public string disableMenuTooltip = "一括で有効化。";
        public string enableMenuLabel = "Enable All Pose";
        [Multiline] public string enableMenuTooltip = "一括で無効化。";
        
        
        [Multiline] public string updateMessage = "最新版アップデートがあります。VCCを確認してください。";
        
        public Texture2D thumbnailBg;
        
        public string headTrackingOption = "Head Lock";
        public string armTrackingOption = "Arm Lock";
        public string fingerTrackingOption = "Finger Lock";
        public string footTrackingOption = "Foot Lock";
        public string locomotionTrackingOption = "Move Lock";

    }
}