using System;
using System.Threading.Tasks;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class DynamicVariables
    {
        private const string SettingsGuid = "ca5572910cc499a4faf1a7986787d6e2";
        private const string VersionEndpoint =
            "https://script.google.com/macros/s/AKfycbyIJ6zUa0LHzdZU5GSO7h0pDWUPCZ1xAKQxWNST88Y9KpgCSsw0fE2u00xHLWW9_S-eng/exec";
        private const int VersionRequestTimeoutSeconds = 10;

        private static readonly Version FallbackVersion = new Version(0, 0, 0);
        private static Version currentVersion;
        private static Version latestVersion;
        private static bool isFetchingLatestVersion;
        private static AvatarPoseSettings cachedSettings;

        public static AvatarPoseSettings Settings => GetSettings();
        public static Version CurrentVersion => GetCurrentVersion();
        public static Version LatestVersion => GetLatestVersion();

        private static AvatarPoseSettings GetSettings()
        {
            if (cachedSettings)
            {
                return cachedSettings;
            }

            var path = AssetDatabase.GUIDToAssetPath(SettingsGuid);
            cachedSettings = AssetDatabase.LoadAssetAtPath<AvatarPoseSettings>(path);
            if (!cachedSettings)
            {
                throw new InvalidOperationException(
                    "AvatarPoseLibrary settings could not be loaded. Reimport the package and try again.");
            }

            return cachedSettings;
        }

        private static Version GetCurrentVersion()
        {
            if (currentVersion != null)
            {
                return currentVersion;
            }

            // 読み込み済みのPackageInfoから同期的に取得し、Editorのメインスレッドを停止させない。
            var packageInfo = PackageInfo.FindForAssembly(typeof(DynamicVariables).Assembly);
            currentVersion = Version.TryParse(packageInfo?.version, out var version)
                ? version
                : FallbackVersion;
            return currentVersion;
        }

        private static Version GetLatestVersion()
        {
            if (latestVersion != null)
            {
                return latestVersion;
            }

            if (!isFetchingLatestVersion)
            {
                isFetchingLatestVersion = true;
                _ = FetchLatestVersionAsync();
            }

            // リモート問い合わせ中は現在のバージョンを返し、インスペクターを待機させない。
            return CurrentVersion;
        }

        private static async Task FetchLatestVersionAsync()
        {
            try
            {
                using (var request = UnityWebRequest.Get(VersionEndpoint))
                {
                    request.timeout = VersionRequestTimeoutSeconds;
                    var completion = new TaskCompletionSource<bool>();
                    request.SendWebRequest().completed += _ => completion.TrySetResult(true);
                    await completion.Task;

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"AvatarPoseLibrary: Version check failed: {request.error}");
                        latestVersion = CurrentVersion;
                        return;
                    }

                    var response = JsonUtility.FromJson<AnalyticsResponse>(request.downloadHandler.text);
                    latestVersion = Version.TryParse(response?.version, out var version)
                        ? version
                        : CurrentVersion;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"AvatarPoseLibrary: Version check failed: {exception.Message}");
                latestVersion = CurrentVersion;
            }
            finally
            {
                isFetchingLatestVersion = false;
            }
        }

        public static CameraSettings GetCameraSettings(AvatarPoseData library)
        {
            if (library == null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            if (library.enableFxAnimator && library.enableLocomotionAnimator)
            {
                return Settings.cameraBoth;
            }

            if (library.enableFxAnimator)
            {
                return Settings.cameraFx;
            }

            return library.enableLocomotionAnimator
                ? Settings.cameraLocomotion
                : new CameraSettings();
        }

        [Serializable]
        private class AnalyticsResponse
        {
            public string version;
        }
    }
}
