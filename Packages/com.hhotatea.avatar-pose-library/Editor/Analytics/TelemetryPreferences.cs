using System;
using com.hhotatea.avatar_pose_library.component;
using UnityEditor;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.editor
{
    public enum TelemetryMode
    {
        Unknown = 0,
        Detailed = 1,
        Minimal = 2
    }

    public static class TelemetryPreferences
    {
        private const string Prefix = "com.hhotatea.avatar-pose-library.telemetry.";
        private const string ModeKey = Prefix + "mode";
        private const string PolicyVersionKey = Prefix + "policy-version";
        private const string ClientIdKey = Prefix + "client-id";
        private const string FirstSessionPendingKey =
            Prefix + "first-session-pending";
        private const string LastAplVersionKey = Prefix + "last-apl-version";

        public static TelemetryMode Mode
        {
            get
            {
                var value = EditorPrefs.GetInt(ModeKey, (int)TelemetryMode.Unknown);
                return Enum.IsDefined(typeof(TelemetryMode), value)
                    ? (TelemetryMode)value
                    : TelemetryMode.Unknown;
            }
        }

        public static bool HasSelection => Mode != TelemetryMode.Unknown;
        public static bool IsDetailed => Mode == TelemetryMode.Detailed;

        public static bool RequiresChoice(APLTelemetryConfiguration configuration)
        {
            return configuration != null
                   && (!HasSelection
                       || EditorPrefs.GetInt(PolicyVersionKey, 0)
                       != configuration.PrivacyPolicyVersion);
        }

        public static void SetMode(
            TelemetryMode mode,
            APLTelemetryConfiguration configuration)
        {
            if (mode == TelemetryMode.Unknown)
            {
                EditorPrefs.DeleteKey(ModeKey);
                EditorPrefs.DeleteKey(PolicyVersionKey);
                return;
            }

            EditorPrefs.SetInt(ModeKey, (int)mode);
            EditorPrefs.SetInt(
                PolicyVersionKey,
                configuration != null ? configuration.PrivacyPolicyVersion : 0);
            GetOrCreateClientId();
        }

        public static string GetOrCreateClientId()
        {
            if (!HasSelection)
            {
                return string.Empty;
            }

            var value = EditorPrefs.GetString(ClientIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = Guid.NewGuid().ToString("N");
            EditorPrefs.SetString(ClientIdKey, value);
            EditorPrefs.SetBool(FirstSessionPendingKey, true);
            return value;
        }

        public static bool ConsumeFirstSessionPending()
        {
            var pending = EditorPrefs.GetBool(FirstSessionPendingKey, false);
            EditorPrefs.DeleteKey(FirstSessionPendingKey);
            return pending;
        }

        public static string LastAplVersion
        {
            get => EditorPrefs.GetString(LastAplVersionKey, string.Empty);
            set => EditorPrefs.SetString(LastAplVersionKey, value ?? string.Empty);
        }

        [MenuItem("Tools/Avatar Pose Library/Privacy Settings")]
        private static void OpenPrivacySettings()
        {
            APLTelemetryBootstrap.ShowPrivacyChoice(true);
        }

        [MenuItem("Tools/Avatar Pose Library/Reset analytics and consent")]
        private static void ResetClientId()
        {
            var inspector = DynamicVariables.Settings.Inspector;
            if (!EditorUtility.DisplayDialog(
                    inspector.telemetryResetDialogTitle,
                    inspector.telemetryResetDialogMessage,
                    inspector.telemetryResetButton,
                    inspector.telemetryCancelButton))
            {
                return;
            }

            SetMode(TelemetryMode.Unknown, null);
            EditorPrefs.DeleteKey(ClientIdKey);
            EditorPrefs.DeleteKey(FirstSessionPendingKey);

            Debug.Log(
                "AvatarPoseLibrary: Analytics identifier and telemetry consent were reset. "
                + "The privacy choice will be shown on the next editor launch.");
        }
    }
}
