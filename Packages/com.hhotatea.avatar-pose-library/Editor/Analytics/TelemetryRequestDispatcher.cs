using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace com.hhotatea.avatar_pose_library.editor
{
    internal static class TelemetryRequestDispatcher
    {
        private static readonly Dictionary<UnityWebRequestAsyncOperation, UnityWebRequest>
            ActiveRequests =
                new Dictionary<UnityWebRequestAsyncOperation, UnityWebRequest>();

        static TelemetryRequestDispatcher()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        public static void PostJson(
            string url,
            byte[] body,
            int timeoutSeconds,
            bool warnOnFailure)
        {
            if (string.IsNullOrWhiteSpace(url) || body == null)
            {
                return;
            }

            try
            {
                var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(body),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = Math.Max(1, timeoutSeconds)
                };
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                Start(request, warnOnFailure);
            }
            catch (Exception exception)
            {
                if (warnOnFailure)
                {
                    Debug.LogWarning(
                        $"AvatarPoseLibrary: Telemetry request could not start: "
                        + exception.GetType().Name);
                }
            }
        }

        private static void Start(UnityWebRequest request, bool warnOnFailure)
        {
            var operation = request.SendWebRequest();
            ActiveRequests[operation] = request;
            operation.completed += _ => Complete(operation, warnOnFailure);
        }

        private static void Complete(
            UnityWebRequestAsyncOperation operation,
            bool warnOnFailure)
        {
            if (!ActiveRequests.TryGetValue(operation, out var request))
            {
                return;
            }

            ActiveRequests.Remove(operation);
            try
            {
                if (warnOnFailure && request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"AvatarPoseLibrary: Telemetry request failed: {request.error}");
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        private static void DisposeAll()
        {
            foreach (var request in ActiveRequests.Values)
            {
                request?.Dispose();
            }

            ActiveRequests.Clear();
        }
    }
}
