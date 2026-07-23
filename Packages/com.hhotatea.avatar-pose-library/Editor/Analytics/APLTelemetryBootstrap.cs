using System;
using com.hhotatea.avatar_pose_library.component;
using UnityEditor;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.editor
{
    [InitializeOnLoad]
    public static class APLTelemetryBootstrap
    {
        private const string SessionInitializedKey =
            "com.hhotatea.avatar-pose-library.telemetry.session-initialized";

        static APLTelemetryBootstrap()
        {
            EditorApplication.delayCall += TryInitialize;
        }

        private static void TryInitialize()
        {
            if (SessionState.GetBool(SessionInitializedKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryInitialize;
                return;
            }

            if (Application.isBatchMode)
            {
                FinishInitialization(TelemetryPreferences.HasSelection);
                return;
            }

            var configuration = DynamicVariables.TelemetryConfiguration;
            if (configuration == null
                || (!configuration.CanSendLogs && !configuration.CanSendErrors))
            {
                FinishInitialization(false);
                return;
            }

            if (TelemetryPreferences.RequiresChoice(configuration))
            {
                ShowPrivacyChoice(false);
                return;
            }

            FinishInitialization(true);
        }

        public static void ShowPrivacyChoice(bool allowCancel)
        {
            var configuration = DynamicVariables.TelemetryConfiguration;
            if (configuration == null)
            {
                return;
            }

            var inspector = DynamicVariables.Settings.Inspector;
            var result = EditorUtility.DisplayDialogComplex(
                inspector.telemetryPrivacyDialogTitle,
                inspector.telemetryPrivacyDialogMessage,
                inspector.telemetryDetailedConsentButton,
                inspector.telemetryPrivacyPolicyButton,
                inspector.telemetryMinimalConsentButton);

            if (result == 1)
            {
                OpenPrivacyPolicy(configuration);
                if (!allowCancel || TelemetryPreferences.RequiresChoice(configuration))
                {
                    EditorApplication.delayCall += () => ShowPrivacyChoice(allowCancel);
                }

                return;
            }

            TelemetryPreferences.SetMode(
                result == 0 ? TelemetryMode.Detailed : TelemetryMode.Minimal,
                configuration);

            if (!SessionState.GetBool(SessionInitializedKey, false))
            {
                FinishInitialization(true);
            }
        }

        public static void RequestDetailedErrorConsent(Action<bool> completed)
        {
            if (TelemetryPreferences.IsDetailed)
            {
                completed?.Invoke(true);
                return;
            }

            if (Application.isBatchMode)
            {
                completed?.Invoke(false);
                return;
            }

            var configuration = DynamicVariables.TelemetryConfiguration;
            var inspector = DynamicVariables.Settings.Inspector;
            var result = EditorUtility.DisplayDialogComplex(
                inspector.telemetryErrorDialogTitle,
                inspector.telemetryErrorDialogMessage,
                inspector.telemetryYesButton,
                inspector.telemetryPrivacyPolicyButton,
                inspector.telemetryNoButton);

            if (result == 1)
            {
                OpenPrivacyPolicy(configuration);
                EditorApplication.delayCall += () => RequestDetailedErrorConsent(completed);
                return;
            }

            var accepted = result == 0;
            if (accepted)
            {
                TelemetryPreferences.SetMode(TelemetryMode.Detailed, configuration);
            }

            completed?.Invoke(accepted);
        }

        private static void OpenPrivacyPolicy(
            APLTelemetryConfiguration configuration)
        {
            if (configuration != null
                && !string.IsNullOrWhiteSpace(configuration.PrivacyPolicyUrl))
            {
                Application.OpenURL(configuration.PrivacyPolicyUrl);
            }
        }

        private static void FinishInitialization(bool startSession)
        {
            SessionState.SetBool(SessionInitializedKey, true);
            if (startSession)
            {
                StartSession();
            }
        }

        private static void StartSession()
        {
            _ = DynamicVariables.LatestVersion;

            var current = DynamicVariables.CurrentVersion.ToString();
            var previous = TelemetryPreferences.LastAplVersion;
            if (!string.IsNullOrWhiteSpace(previous)
                && !string.Equals(previous, current, StringComparison.Ordinal))
            {
                APLTelemetry.SendVersionChanged(previous);
            }

            TelemetryPreferences.LastAplVersion = current;
        }
    }
}
