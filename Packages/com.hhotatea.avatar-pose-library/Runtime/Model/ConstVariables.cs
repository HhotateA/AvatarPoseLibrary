using UnityEngine;

namespace com.hhotatea.avatar_pose_library.model
{
    public static class ConstVariables
    {
        public const string BaseName = "AvatarPoseLibrary";
        public const string OnPlayParamPrefix = "AnimPosePlay";
        public const string DummyParamPrefix = "AnimPoseDummy";

        /// <summary>
        /// パラメーター名
        /// </summary>
        public const string HeightParamPrefix = "AnimPoseHeight";
        public const string HeightUpdateParamPrefix = "AnimHeightUpdate";
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
        public const string MirrorCycleOffsetParamPrefix = "AnimPoseMirrorCycleOffset";
        public const string FlagParamPrefix = "AnimPoseFlag";
        public const string AudioParamPrefix = "AnimPoseAudio";
        public const string PoseSpaceParamPrefix = "AnimPoseSpace";
        public const string VariationParamPrefix = "AnimPoseVariation";
        public const string BodyRotationXParamPrefix = "AnimPoseBodyRotateX";
        public const string BodyRotationYParamPrefix = "AnimPoseBodyRotateY";
        public const string BodyRotationZParamPrefix = "AnimPoseBodyRotateZ";
        public const string BodyRotationResetParamPrefix = "AnimPoseBodyRotateReset";
        // 回転中だけ IK を切るレイヤーの名前。パラメーターではなくレイヤー名としてのみ使います。
        public const string BodyRotationTrackingLayerPrefix = "AnimPoseBodyRotateIK";
        // 体全体を回すための Transform リグ。アバタールート直下に作成し、
        // アーマチュアをこの配下へ付け替えてから Animator で回します。
        public const string BodyRotationRigRootName = "AnimPoseBodyRotateRig";
        public const string BodyRotationRigLayerPrefix = "AnimPoseBodyRotateRig";

        /// <summary>
        /// State名
        /// </summary>
        public const string StateNameReset = "Reset";

        /// <summary>
        /// アニメーター名
        /// </summary>
        public const string MotionAnimatorPrefix = "AnimPoseMotion";
        public const string FxAnimatorPrefix = "AnimPoseFx";
        public const string ParamAnimatorPrefix = "AnimPoseParam";

        // 1つのIntパラメーターで管理するAnimationの最大数。
        public const int MaxAnimationState = 255;
        public const int PoseFlagCount = 2;
        public const int HashLong = 16;

        // 生成アセットのキャッシュ形式のバージョン。
        // アニメーターのパラメーター構成など生成内容を変更した場合はこの値を上げてください。
        // 古いキャッシュが再利用され、修正が反映されないことを防ぎます。
        // Guid（パラメーター名）には影響しないため、既存のアバターの設定は維持されます。
        public const string AssetCacheVersion = "10";

        // ランタイム回転ラジアルの分解能。0..1 をこの段数で分割します。
        //
        // 回転はポーズへ焼き込まず、アーマチュアの親に挟んだ Transform を
        // 実行時に回して合成します（BodyRotationRigBuilder 参照）。
        // そのため生成コストはポーズ数にも組み合わせ数にも依存しません。
        //
        //   1ライブラリあたりのクリップ数 = 3 軸 * Steps 本
        //
        //   Steps = 3 :   9 クリップ（全ポーズ共通）
        //   Steps = 5 :  15 クリップ（全ポーズ共通）
        //
        // 以前は Steps^3 本をポーズごとに生成していたため、
        // Steps=3 で 1 ポーズあたり 108 クリップ必要でした。
        // 段数を増やしてもコストはほぼ増えないため、滑らかさを優先して 5 にしています。
        // 5 段だと全軸とも 90 度刻みになり、
        // クォータニオンの補間が最も安定する間隔になります。
        public const int BodyRotationSteps = 5;

        // ラジアルの両端に対応する角度（度）。軸ごとに個別に指定します。
        // 左端（0%）= MinAngle、右端（100%）= MaxAngle を等分します。
        // 無回転（0 度）に対応するパラメーター値は、この 2 つの値から自動で決まります
        // （BodyRotationAxis.NeutralValue 参照）。そのため全軸が「中央 = 無回転」に
        // 固定されることはありません。
        //
        //   X / Z :   0 〜 +360 度 … 左端が無回転。右端で一周して左端と同じ向きに戻ります。
        //   Y     : -180 〜 +180 度 … 中央（50%）が無回転。左右対称に振れます。
        //
        // 段数は BodyRotationSteps のままなので、クリップ数は増えません。
        //
        // 注意 1: 回転の支点は Hips です。X/Z を 360 度まで回すと、途中で頭や足が
        //         床を突き抜けます。MaxAngle をいくつにしても支点は変わりません。
        // 注意 2: X/Z は 0 度始まりの一方向なので、逆方向へは 360 度回り込んで到達します。
        //         左右対称に振りたい場合は MinAngle を -360f にしてください。
        public const float BodyRotationMinAngleX = 0f;
        public const float BodyRotationMaxAngleX = 360f;
        public const float BodyRotationMinAngleY = -180f;
        public const float BodyRotationMaxAngleY = 180f;
        public const float BodyRotationMinAngleZ = 0f;
        public const float BodyRotationMaxAngleZ = 360f;

        /// <summary>
        /// 回転ラジアル 1 軸ぶんの定義。
        ///
        /// パラメーター名・回転軸・角度範囲を 1 か所にまとめ、無回転値は角度範囲から
        /// 導出します。式パラメーターの既定値・アニメーターの既定値・リセット値・
        /// IK を切るしきい値はすべてここから引くため、軸ごとに値がずれません。
        /// </summary>
        public struct BodyRotationAxis
        {
            public readonly string AxisName;
            public readonly string ParameterPrefix;
            public readonly Vector3 Axis;
            public readonly float MinAngle;
            public readonly float MaxAngle;

            public BodyRotationAxis(
                string axisName, string parameterPrefix, Vector3 axis, float minAngle, float maxAngle)
            {
                AxisName = axisName;
                ParameterPrefix = parameterPrefix;
                Axis = axis;
                MinAngle = minAngle;
                MaxAngle = maxAngle;
            }

            /// <summary>
            /// 無回転（0 度）に対応するパラメーター値。
            /// 左端が 0 度の軸では 0、中央が 0 度の軸では 0.5 になります。
            /// </summary>
            public float NeutralValue => (0f - MinAngle) / (MaxAngle - MinAngle);

            /// <summary>パラメーター値（0..1）に対応する角度（度）。</summary>
            public float AngleAt(float value) => MinAngle + (MaxAngle - MinAngle) * value;
        }

        public static readonly BodyRotationAxis BodyRotationAxisY =
            new BodyRotationAxis("Y", BodyRotationYParamPrefix, Vector3.up,
                BodyRotationMinAngleY, BodyRotationMaxAngleY);

        public static readonly BodyRotationAxis BodyRotationAxisX =
            new BodyRotationAxis("X", BodyRotationXParamPrefix, Vector3.right,
                BodyRotationMinAngleX, BodyRotationMaxAngleX);

        public static readonly BodyRotationAxis BodyRotationAxisZ =
            new BodyRotationAxis("Z", BodyRotationZParamPrefix, Vector3.forward,
                BodyRotationMinAngleZ, BodyRotationMaxAngleZ);

        /// <summary>
        /// 回転ラジアルの全軸。レイヤー生成順（Y → X → Z）に並べています。
        /// 既定値の設定やリセットなど、全軸へ同じ処理をする箇所はここを回してください。
        /// </summary>
        public static readonly BodyRotationAxis[] BodyRotationAxes =
        {
            BodyRotationAxisY,
            BodyRotationAxisX,
            BodyRotationAxisZ,
        };

        // 設定で使用するアイコン
        public const string warmIcon = "console.warnicon.sml";
    }
}
