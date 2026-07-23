using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using nadena.dev.ndmf;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;
using VRC.SDK3.Avatars.Components;
using StackTrace = System.Diagnostics.StackTrace;

namespace com.hhotatea.avatar_pose_library.editor
{
    internal static class LatestVersionService
    {
        private static Version latestVersion;
        private static bool isFetching;

        public static Version Get(Version currentVersion)
        {
            if (latestVersion != null)
            {
                return latestVersion;
            }

            // The version request also records the editor session.
            if (TelemetryPreferences.HasSelection && !isFetching)
            {
                isFetching = true;
                _ = Fetch(currentVersion);
            }

            return currentVersion;
        }

        private static async Task Fetch(Version currentVersion)
        {
            try
            {
                var configuration = DynamicVariables.TelemetryConfiguration;
                if (configuration == null || !configuration.CanSendLogs)
                {
                    latestVersion = currentVersion;
                    return;
                }

                var url = TelemetryPayload.CreateSessionUrl(configuration);
                if (string.IsNullOrWhiteSpace(url))
                {
                    latestVersion = currentVersion;
                    return;
                }

                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = configuration.RequestTimeoutSeconds;
                    var completion = new TaskCompletionSource<bool>();
                    request.SendWebRequest().completed += _ =>
                        completion.TrySetResult(true);
                    await completion.Task;

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning(
                            $"AvatarPoseLibrary: Version check failed: {request.error}");
                        latestVersion = currentVersion;
                        return;
                    }

                    var response = JsonUtility.FromJson<VersionResponse>(
                        request.downloadHandler.text);
                    latestVersion = Version.TryParse(
                        response?.version,
                        out var parsed)
                        ? parsed
                        : currentVersion;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AvatarPoseLibrary: Version check failed: {exception.Message}");
                latestVersion = currentVersion;
            }
            finally
            {
                isFetching = false;
            }
        }

        [Serializable]
        private sealed class VersionResponse
        {
            public string version;
        }
    }

    internal static class TelemetryPayload
    {
        private static readonly long SessionId =
            Math.Max(1, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public static string CreateSessionUrl(
            APLTelemetryConfiguration configuration)
        {
            return CreateGetUrl(
                configuration.ServiceEndpoint,
                CreateSession(configuration));
        }

        public static object CreateSession(APLTelemetryConfiguration configuration)
        {
            if (!TelemetryPreferences.IsDetailed)
            {
                return CreateBase<MinimalEvent>(configuration, configuration.EditorSessionStartedEvent);
            }

            var payload = CreateBase<DetailedSessionEvent>(
                configuration,
                configuration.EditorSessionStartedEvent);
            PopulateEnvironment(payload);
            return payload;
        }

        public static object CreateEvent(
            APLTelemetryConfiguration configuration,
            string eventName,
            TelemetryBuildMetrics metrics,
            long durationMilliseconds,
            string previousAplVersion,
            string buildStage,
            string exceptionType)
        {
            if (!TelemetryPreferences.IsDetailed)
            {
                return CreateBase<MinimalEvent>(configuration, eventName);
            }

            var payload = CreateBase<DetailedEvent>(configuration, eventName);
            PopulateEnvironment(payload);
            payload.previous_apl_version = previousAplVersion ?? string.Empty;
            payload.build_duration_ms = Math.Max(0, durationMilliseconds);
            payload.component_count = metrics.component_count;
            payload.library_count = metrics.library_count;
            payload.category_count = metrics.category_count;
            payload.pose_count = metrics.pose_count;
            payload.humanoid = metrics.humanoid;
            payload.audio_enabled = metrics.audio_enabled;
            payload.locomotion_enabled = metrics.locomotion_enabled;
            payload.fx_enabled = metrics.fx_enabled;
            payload.cache_enabled = metrics.cache_enabled;
            payload.auto_reset_enabled = metrics.auto_reset_enabled;
            payload.build_stage = buildStage ?? string.Empty;
            payload.exception_type = exceptionType ?? string.Empty;
            return payload;
        }

        public static bool TrySerialize(
            object payload,
            int maximumBytes,
            out byte[] body)
        {
            body = payload == null
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            return body.Length > 0 && body.Length <= maximumBytes;
        }

        public static string CreateGetUrl(string endpoint, object payload)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || payload == null)
            {
                return string.Empty;
            }

            var query = string.Join(
                "&",
                payload.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .OrderBy(field => field.MetadataToken)
                    .Select(field =>
                        Escape(field.Name)
                        + "="
                        + Escape(ToInvariantString(field.GetValue(payload)))));
            return endpoint + (endpoint.Contains("?") ? "&" : "?") + query;
        }

        public static string PackageVersion(Type markerType)
        {
            try
            {
                var package = PackageInfo.FindForAssembly(markerType.Assembly);
                return !string.IsNullOrWhiteSpace(package?.version)
                    ? package.version
                    : markerType.Assembly.GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static T CreateBase<T>(
            APLTelemetryConfiguration configuration,
            string eventName)
            where T : MinimalEvent, new()
        {
            return new T
            {
                schema_version = configuration.SchemaVersion,
                telemetry_mode =
                    TelemetryPreferences.IsDetailed ? "detailed" : "minimal",
                event_name = eventName,
                client_id = TelemetryPreferences.GetOrCreateClientId(),
                apl_version = DynamicVariables.CurrentVersion.ToString()
            };
        }

        private static void PopulateEnvironment(DetailedSessionEvent payload)
        {
            payload.event_id = Guid.NewGuid().ToString("N");
            payload.unity_version = Application.unityVersion;
            payload.vrcsdk_version = PackageVersion(typeof(VRCAvatarDescriptor));
            payload.ndmf_version = PackageVersion(typeof(BuildContext));
            payload.session_id = SessionId;
            payload.engagement_time_msec = 1;
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }

        private static string ToInvariantString(object value)
        {
            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value?.ToString() ?? string.Empty;
        }

        [Serializable]
        private class MinimalEvent
        {
            public int schema_version;
            public string telemetry_mode;
            public string event_name;
            public string client_id;
            public string apl_version;
        }

        [Serializable]
        private class DetailedSessionEvent : MinimalEvent
        {
            public string event_id;
            public string unity_version;
            public string vrcsdk_version;
            public string ndmf_version;
            public long session_id;
            public long engagement_time_msec;
        }

        [Serializable]
        private sealed class DetailedEvent : DetailedSessionEvent
        {
            public string previous_apl_version;
            public long build_duration_ms;
            public int component_count;
            public int library_count;
            public int category_count;
            public int pose_count;
            public int humanoid;
            public int audio_enabled;
            public int locomotion_enabled;
            public int fx_enabled;
            public int cache_enabled;
            public int auto_reset_enabled;
            public string build_stage;
            public string exception_type;
        }
    }

    internal sealed class TelemetryBuildMetrics
    {
        private const int MaximumSettingsSnapshots = 64;

        public static readonly TelemetryBuildMetrics Empty = new TelemetryBuildMetrics
        {
            libraries = Array.Empty<TelemetryLibrarySettings>()
        };

        public int component_count;
        public int library_count;
        public int category_count;
        public int pose_count;
        public int humanoid;
        public int audio_enabled;
        public int locomotion_enabled;
        public int fx_enabled;
        public int cache_enabled;
        public int auto_reset_enabled;
        public TelemetryLibrarySettings[] libraries;

        public static TelemetryBuildMetrics Create(
            GameObject avatarRoot,
            AvatarPoseLibrary[] components)
        {
            var validComponents = components?
                .Where(component => component != null && component.data != null)
                .ToArray()
                ?? Array.Empty<AvatarPoseLibrary>();
            var libraries = validComponents
                .Select(component => component.data)
                .Distinct()
                .ToArray();
            var animator = avatarRoot != null
                ? avatarRoot.GetComponent<Animator>()
                : null;

            return new TelemetryBuildMetrics
            {
                component_count = validComponents.Length,
                library_count = libraries.Length,
                category_count = libraries.Sum(item => item.categories?.Count ?? 0),
                pose_count = libraries.Sum(item => item.PoseCount),
                humanoid = Bool(animator != null && animator.isHuman),
                audio_enabled = Bool(libraries.Any(item => item.EnableAudioMode)),
                locomotion_enabled =
                    Bool(libraries.Any(item => item.enableLocomotionAnimator)),
                fx_enabled = Bool(libraries.Any(item => item.enableFxAnimator)),
                cache_enabled = Bool(libraries.Any(item => item.enableUseCache)),
                auto_reset_enabled =
                    Bool(libraries.Any(item => item.enableAutoResetAnim)),
                libraries = libraries
                    .Take(MaximumSettingsSnapshots)
                    .Select(TelemetryLibrarySettings.Create)
                    .ToArray()
            };
        }

        private static int Bool(bool value)
        {
            return value ? 1 : 0;
        }
    }

    [Serializable]
    internal sealed class TelemetryLibrarySettings
    {
        public int category_count;
        public int pose_count;
        public int enable_height;
        public int enable_speed;
        public int enable_mirror;
        public int enable_tracking;
        public int enable_deep_sync;
        public int enable_pose_space;
        public int enable_cache;
        public int enable_auto_reset;
        public int enable_locomotion;
        public int enable_fx;
        public int suppress_additive;
        public int audio_enabled;
        public string write_defaults;

        public static TelemetryLibrarySettings Create(AvatarPoseData data)
        {
            return new TelemetryLibrarySettings
            {
                category_count = data.categories?.Count ?? 0,
                pose_count = data.PoseCount,
                enable_height = Bool(data.enableHeightParam),
                enable_speed = Bool(data.enableSpeedParam),
                enable_mirror = Bool(data.enableMirrorParam),
                enable_tracking = Bool(data.enableTrackingParam),
                enable_deep_sync = Bool(data.enableDeepSync),
                enable_pose_space = Bool(data.enablePoseSpace),
                enable_cache = Bool(data.enableUseCache),
                enable_auto_reset = Bool(data.enableAutoResetAnim),
                enable_locomotion = Bool(data.enableLocomotionAnimator),
                enable_fx = Bool(data.enableFxAnimator),
                suppress_additive = Bool(data.suppressAdditiveAnimator),
                audio_enabled = Bool(data.EnableAudioMode),
                write_defaults = data.writeDefaultType.ToString()
            };
        }

        private static int Bool(bool value)
        {
            return value ? 1 : 0;
        }
    }

    internal static class TelemetryErrorReporter
    {
        public static TelemetryErrorReport Create(
            APLTelemetryConfiguration configuration,
            TelemetryBuildMetrics metrics,
            long durationMilliseconds,
            Exception exception,
            string stage)
        {
            var errorText = LimitText(
                exception?.ToString() ?? "UnknownException",
                configuration.MaxErrorTextCharacters);

            return new TelemetryErrorReport
            {
                request_type = "error_report",
                schema_version = configuration.SchemaVersion,
                report_id = Guid.NewGuid().ToString("N"),
                occurred_at_utc = DateTime.UtcNow.ToString("O"),
                client_id = TelemetryPreferences.GetOrCreateClientId(),
                apl_version = DynamicVariables.CurrentVersion.ToString(),
                unity_version = Application.unityVersion,
                vrcsdk_version = TelemetryPayload.PackageVersion(
                    typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor)),
                ndmf_version = TelemetryPayload.PackageVersion(
                    typeof(nadena.dev.ndmf.BuildContext)),
                editor_platform = Application.platform.ToString(),
                build_stage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage,
                build_duration_ms = Math.Max(0, durationMilliseconds),
                exception_type = exception?.GetType().FullName ?? "UnknownException",
                error_text = errorText,
                stack_frames = StackFrames(exception, configuration.MaxStackFrames),
                component_count = metrics.component_count,
                library_count = metrics.library_count,
                category_count = metrics.category_count,
                pose_count = metrics.pose_count,
                humanoid = metrics.humanoid,
                libraries = metrics.libraries
            };
        }

        public static void Send(
            APLTelemetryConfiguration configuration,
            TelemetryErrorReport report)
        {
            try
            {
                var json = SerializeWithinLimit(
                    report,
                    configuration.MaxErrorReportBytes);
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning(
                        "AvatarPoseLibrary: Error report exceeded the configured size limit.");
                    return;
                }

                TelemetryRequestDispatcher.PostJson(
                    configuration.ServiceEndpoint,
                    Encoding.UTF8.GetBytes(json),
                    configuration.RequestTimeoutSeconds,
                    true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"AvatarPoseLibrary: Error report could not be prepared: "
                    + exception.GetType().Name);
            }
        }

        private static string LimitText(string value, int maximumCharacters)
        {
            return value.Length <= maximumCharacters
                ? value
                : value.Substring(0, maximumCharacters);
        }

        private static string[] StackFrames(Exception exception, int maximum)
        {
            if (exception == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                return new StackTrace(exception, false)
                    .GetFrames()?
                    .Take(maximum)
                    .Select(frame =>
                    {
                        var method = frame.GetMethod();
                        var typeName = method?.DeclaringType?.FullName ?? "UnknownType";
                        return $"{typeName}.{method?.Name ?? "UnknownMethod"}";
                    })
                    .ToArray()
                    ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string SerializeWithinLimit(
            TelemetryErrorReport report,
            int maximumBytes)
        {
            var json = JsonUtility.ToJson(report, true);
            while (Encoding.UTF8.GetByteCount(json) > maximumBytes
                   && report.error_text.Length > 1024)
            {
                report.error_text =
                    report.error_text.Substring(0, report.error_text.Length / 2);
                report.error_text_truncated = 1;
                json = JsonUtility.ToJson(report, true);
            }

            if (Encoding.UTF8.GetByteCount(json) > maximumBytes)
            {
                report.stack_frames = Array.Empty<string>();
                report.error_text_truncated = 1;
                json = JsonUtility.ToJson(report, true);
            }

            return Encoding.UTF8.GetByteCount(json) <= maximumBytes
                ? json
                : string.Empty;
        }
    }

    [Serializable]
    internal sealed class TelemetryErrorReport
    {
        public string request_type;
        public int schema_version;
        public string report_id;
        public string occurred_at_utc;
        public string client_id;
        public string apl_version;
        public string unity_version;
        public string vrcsdk_version;
        public string ndmf_version;
        public string editor_platform;
        public string build_stage;
        public long build_duration_ms;
        public string exception_type;
        public string error_text;
        public int error_text_truncated;
        public string[] stack_frames;
        public int component_count;
        public int library_count;
        public int category_count;
        public int pose_count;
        public int humanoid;
        public TelemetryLibrarySettings[] libraries;
    }
}
