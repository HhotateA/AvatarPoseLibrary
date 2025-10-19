namespace com.hhotatea.avatar_pose_library.model
{
    public static class ConstVariables
    {
        public const string OnPlayParamPrefix = "AnimPosePlay";
        
        /// <summary>
        /// パラメーター名
        /// </summary>
        public const string HeightParamPrefix = "AnimPoseHeight";
        public const string BaseParamPrefix = "AnimPoseBase";
        public const string HeadParamPrefix = "AnimPoseHead";
        public const string ArmParamPrefix = "AnimPoseArm";
        public const string FootParamPrefix = "AnimPoseFoot";
        public const string FingerParamPrefix = "AnimPoseFinger";
        public const string FaceParamPrefix = "AnimPoseFace";
        public const string ActionParamPrefix = "AnimPoseAction";
        public const string SpeedParamPrefix = "AnimPoseSpeed";
        public const string ResetParamPrefix = "AnimPoseReset";
        public const string MirrorParamPrefix = "AnimPoseMirror";
        public const string FlagParamPrefix = "AnimPoseFlag";
        public const string PoseSpaceParamPrefix = "AnimPoseSpace";
        public const string AudioParamPrefix = "AnimPoseAudio";
        public const string VolumeParamPrefix = "AnimPoseVolume";
        public const string DummyParamPrefix = "AnimPoseDummy";
        // public const string BlockIdleParamPrefix = "AnimPoseBlock"; // 動的アニメーションかどうかのフラグ
        
        public const string MotionAnimatorPrefix = "AnimPoseMotion";
        public const string FxAnimatorPrefix = "AnimPoseFx";
        public const string ParamAnimatorPrefix = "AnimPoseParam";

        // 1つのIntパラメーターで管理するAnimationの最大数。
        public const int MaxAnimationState = 255;
        public const int PoseFlagCount = 2;
        public const int HashLong = 16;
    }
}
