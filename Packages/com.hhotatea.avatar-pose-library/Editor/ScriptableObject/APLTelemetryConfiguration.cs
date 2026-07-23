using UnityEngine;

namespace com.hhotatea.avatar_pose_library.component
{
    public sealed class APLTelemetryConfiguration : ScriptableObject
    {
        [Header("Endpoints")]
        [SerializeField] string serviceEndpoint;

        [Header("Public links")]
        [SerializeField] string privacyPolicyUrl;

        [Header("Protocol")]
        [SerializeField] bool logEventsEnabled = true;
        [SerializeField] bool errorReportsEnabled = true;
        [SerializeField] int privacyPolicyVersion = 1;
        [SerializeField] int schemaVersion = 1;
        [SerializeField] int requestTimeoutSeconds = 10;
        [SerializeField] int maxLogRequestBytes = 8192;
        [SerializeField] int maxErrorReportBytes = 65536;
        [SerializeField] int maxErrorTextCharacters = 24000;
        [SerializeField] int maxStackFrames = 32;

        [Header("Event names")]
        [SerializeField] string editorSessionStartedEvent = "apl_editor_session_started";
        [SerializeField] string versionChangedEvent = "apl_version_changed";
        [SerializeField] string buildCompletedEvent = "apl_build_completed";
        [SerializeField] string buildFailedEvent = "apl_build_failed";

        public string ServiceEndpoint => serviceEndpoint;
        public string PrivacyPolicyUrl => privacyPolicyUrl;
        public int PrivacyPolicyVersion => Mathf.Max(1, privacyPolicyVersion);
        public int SchemaVersion => Mathf.Max(1, schemaVersion);
        public int RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 1, 30);
        public int MaxLogRequestBytes => Mathf.Clamp(maxLogRequestBytes, 1024, 65536);
        public int MaxErrorReportBytes => Mathf.Clamp(maxErrorReportBytes, 4096, 1048576);
        public int MaxErrorTextCharacters => Mathf.Clamp(maxErrorTextCharacters, 1024, 100000);
        public int MaxStackFrames => Mathf.Clamp(maxStackFrames, 1, 128);
        public string EditorSessionStartedEvent => editorSessionStartedEvent;
        public string VersionChangedEvent => versionChangedEvent;
        public string BuildCompletedEvent => buildCompletedEvent;
        public string BuildFailedEvent => buildFailedEvent;

        public bool CanSendLogs =>
            logEventsEnabled && HasValidServiceEndpoint;

        public bool CanSendErrors =>
            errorReportsEnabled && HasValidServiceEndpoint;

        private bool HasValidServiceEndpoint => IsHttpsUrl(serviceEndpoint);

        private static bool IsHttpsUrl(string value)
        {
            return System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri)
                   && uri.Scheme == System.Uri.UriSchemeHttps;
        }
    }
}
