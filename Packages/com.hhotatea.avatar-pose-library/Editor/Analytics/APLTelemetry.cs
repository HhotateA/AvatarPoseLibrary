using System;
using System.Collections.Generic;
using System.Diagnostics;
using com.hhotatea.avatar_pose_library.component;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class APLTelemetry
    {
        private static readonly Dictionary<int, BuildState> ActiveBuilds =
            new Dictionary<int, BuildState>();

        public static void SendVersionChanged(string previousVersion)
        {
            SendEvent(
                DynamicVariables.TelemetryConfiguration?.VersionChangedEvent,
                TelemetryBuildMetrics.Empty,
                0,
                previousVersion,
                string.Empty,
                string.Empty);
        }

        public static void BeginBuild(
            GameObject avatarRoot,
            AvatarPoseLibrary[] components)
        {
            try
            {
                if (avatarRoot == null)
                {
                    return;
                }

                var metrics = TelemetryBuildMetrics.Create(avatarRoot, components);
                if (metrics.component_count > 0)
                {
                    ActiveBuilds[avatarRoot.GetInstanceID()] = new BuildState(metrics);
                }
            }
            catch (Exception exception)
            {
                Warn("Build telemetry initialization failed", exception);
            }
        }

        public static void CompleteBuild(GameObject avatarRoot)
        {
            try
            {
                if (!TryTakeBuild(avatarRoot, out var state))
                {
                    return;
                }

                state.Stop();
                SendEvent(
                    DynamicVariables.TelemetryConfiguration?.BuildCompletedEvent,
                    state.Metrics,
                    state.ElapsedMilliseconds,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }
            catch (Exception exception)
            {
                Warn("Build telemetry completion failed", exception);
            }
        }

        public static void FailBuild(
            GameObject avatarRoot,
            AvatarPoseLibrary[] components,
            Exception exception,
            string stage)
        {
            try
            {
                var result = FinishBuild(avatarRoot, components);
                SendEvent(
                    DynamicVariables.TelemetryConfiguration?.BuildFailedEvent,
                    result.Metrics,
                    result.ElapsedMilliseconds,
                    string.Empty,
                    stage,
                    exception?.GetType().FullName ?? "UnknownException");
                SendOrOfferErrorReport(
                    result.Metrics,
                    result.ElapsedMilliseconds,
                    exception,
                    stage);
            }
            catch (Exception telemetryException)
            {
                Warn("Error report preparation failed", telemetryException);
            }
        }

        private static void SendEvent(
            string eventName,
            TelemetryBuildMetrics metrics,
            long durationMilliseconds,
            string previousAplVersion,
            string buildStage,
            string exceptionType)
        {
            try
            {
                if (!TryGetLogConfiguration(out var configuration)
                    || string.IsNullOrWhiteSpace(eventName))
                {
                    return;
                }

                var payload = TelemetryPayload.CreateEvent(
                    configuration,
                    eventName,
                    metrics,
                    durationMilliseconds,
                    previousAplVersion,
                    buildStage,
                    exceptionType);
                if (!TelemetryPayload.TrySerialize(
                        payload,
                        configuration.MaxLogRequestBytes,
                        out var body))
                {
                    Debug.LogWarning(
                        "AvatarPoseLibrary: Telemetry event exceeded the configured size limit.");
                    return;
                }

                TelemetryRequestDispatcher.PostJson(
                    configuration.ServiceEndpoint,
                    body,
                    configuration.RequestTimeoutSeconds,
                    false);
            }
            catch (Exception exception)
            {
                Warn("Telemetry event could not be prepared", exception);
            }
        }

        private static void SendOrOfferErrorReport(
            TelemetryBuildMetrics metrics,
            long durationMilliseconds,
            Exception exception,
            string stage)
        {
            var configuration = DynamicVariables.TelemetryConfiguration;
            if (configuration == null || !configuration.CanSendErrors)
            {
                return;
            }

            var report = TelemetryErrorReporter.Create(
                configuration,
                metrics,
                durationMilliseconds,
                exception,
                stage);
            if (TelemetryPreferences.IsDetailed)
            {
                TelemetryErrorReporter.Send(configuration, report);
                return;
            }

            if (!Application.isBatchMode)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                    APLTelemetryBootstrap.RequestDetailedErrorConsent(accepted =>
                    {
                        if (accepted)
                        {
                            TelemetryErrorReporter.Send(configuration, report);
                        }
                    });
            }
        }

        private static BuildResult FinishBuild(
            GameObject avatarRoot,
            AvatarPoseLibrary[] components)
        {
            if (!TryTakeBuild(avatarRoot, out var state))
            {
                return new BuildResult(
                    TelemetryBuildMetrics.Create(avatarRoot, components),
                    0);
            }

            state.Stop();
            return new BuildResult(state.Metrics, state.ElapsedMilliseconds);
        }

        private static bool TryTakeBuild(GameObject avatarRoot, out BuildState state)
        {
            state = null;
            if (avatarRoot == null)
            {
                return false;
            }

            var key = avatarRoot.GetInstanceID();
            if (!ActiveBuilds.TryGetValue(key, out state))
            {
                return false;
            }

            ActiveBuilds.Remove(key);
            return true;
        }

        private static bool TryGetLogConfiguration(
            out APLTelemetryConfiguration configuration)
        {
            configuration = DynamicVariables.TelemetryConfiguration;
            return TelemetryPreferences.HasSelection
                   && configuration != null
                   && configuration.CanSendLogs
                   && !string.IsNullOrWhiteSpace(
                       TelemetryPreferences.GetOrCreateClientId());
        }

        private static void Warn(string message, Exception exception)
        {
            Debug.LogWarning(
                $"AvatarPoseLibrary: {message}: {exception.GetType().Name}");
        }

        private sealed class BuildState
        {
            private readonly Stopwatch timer = Stopwatch.StartNew();

            public BuildState(TelemetryBuildMetrics metrics)
            {
                Metrics = metrics;
            }

            public TelemetryBuildMetrics Metrics { get; }
            public long ElapsedMilliseconds => timer.ElapsedMilliseconds;

            public void Stop()
            {
                timer.Stop();
            }
        }

        private readonly struct BuildResult
        {
            public BuildResult(
                TelemetryBuildMetrics metrics,
                long elapsedMilliseconds)
            {
                Metrics = metrics;
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public TelemetryBuildMetrics Metrics { get; }
            public long ElapsedMilliseconds { get; }
        }
    }
}