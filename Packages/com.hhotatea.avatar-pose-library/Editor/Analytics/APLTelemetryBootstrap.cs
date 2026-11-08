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

            var configuration = DynamicVariables.TelemetryConfiguration;
            if (configuration == null
                || (!configuration.CanSendLogs && !configuration.CanSendErrors))
            {
                FinishInitialization(false);
                return;
            }

            // Version acquisition, including its session-start event, is
            // independent of the telemetry consent choice.
            _ = DynamicVariables.LatestVersion;

            // Batch mode cannot present an interactive consent dialog. The
            // version request above still records the editor session.
            if (Application.isBatchMode)
            {
                FinishInitialization(TelemetryPreferences.HasSelection);
                return;
            }

            if (TelemetryPreferences.RequiresChoice(configuration))
            {
                ShowPrivacyChoice();
                return;
            }

            FinishInitialization(true);
        }

        public static void ShowPrivacyChoice()
        {
            var configuration = DynamicVariables.TelemetryConfiguration;
            if (configuration == null)
            {
                return;
            }

            var inspector = DynamicVariables.Settings.Inspector;
            TelemetryConsentDialog.Show(
                inspector.telemetryPrivacyDialogTitle,
                inspector.telemetryPrivacyDialogMessage,
                inspector.telemetryDetailedConsentButton,
                inspector.telemetryMinimalConsentButton,
                inspector.telemetryPrivacyPolicyButton,
                () => OpenPrivacyPolicy(configuration),
                result => CompletePrivacyChoice(result, configuration));
        }

        private static void CompletePrivacyChoice(
            TelemetryConsentDialogResult result,
            APLTelemetryConfiguration configuration)
        {
            if (result == TelemetryConsentDialogResult.Closed)
            {
                if (!SessionState.GetBool(SessionInitializedKey, false))
                {
                    FinishInitialization(false);
                }

                return;
            }

            TelemetryPreferences.SetMode(
                result == TelemetryConsentDialogResult.Primary
                    ? TelemetryMode.Detailed
                    : TelemetryMode.Minimal,
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
            TelemetryConsentDialog.Show(
                inspector.telemetryErrorDialogTitle,
                inspector.telemetryErrorDialogMessage,
                inspector.telemetryYesButton,
                inspector.telemetryNoButton,
                inspector.telemetryPrivacyPolicyButton,
                () => OpenPrivacyPolicy(configuration),
                result => CompleteDetailedErrorConsent(
                    result,
                    completed,
                    configuration));
        }

        private static void CompleteDetailedErrorConsent(
            TelemetryConsentDialogResult result,
            Action<bool> completed,
            APLTelemetryConfiguration configuration)
        {
            var accepted = result == TelemetryConsentDialogResult.Primary;
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
            if (TelemetryPreferences.ConsumeFirstSessionPending())
            {
                APLTelemetry.SendFirstSession();
            }

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
