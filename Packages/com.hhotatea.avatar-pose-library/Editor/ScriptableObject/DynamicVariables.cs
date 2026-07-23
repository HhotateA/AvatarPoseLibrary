using System;
using com.hhotatea.avatar_pose_library.component;
using com.hhotatea.avatar_pose_library.model;
using UnityEditor;
using UnityEngine;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class DynamicVariables
    {
        private const string SettingsGuid = "ca5572910cc499a4faf1a7986787d6e2";
        private static readonly Version FallbackVersion = new Version(0, 0, 0);
        private static Version currentVersion;
        private static AvatarPoseSettings cachedSettings;

        public static AvatarPoseSettings Settings => GetSettings();
        public static APLTelemetryConfiguration TelemetryConfiguration =>
            Settings.TelemetryConfiguration;
        public static Version CurrentVersion => GetCurrentVersion();
        public static Version LatestVersion => LatestVersionService.Get(CurrentVersion);

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

            // FindForAssembly uses already-loaded package information and does not
            // block the editor main thread.
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(DynamicVariables).Assembly);
            currentVersion = Version.TryParse(packageInfo?.version, out var version)
                ? version
                : FallbackVersion;
            return currentVersion;
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
    }
}
