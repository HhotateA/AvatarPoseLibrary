using System;
using System.Linq;
using com.hhotatea.avatar_pose_library.component;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace com.hhotatea.avatar_pose_library.editor
{
    public static class DynamicVariables
    {
        private const string settingsGuid = "ca5572910cc499a4faf1a7986787d6e2";
        private const string packageName = "com.hhotatea.avatar-pose-library";

        private const string analyticsURL =
            "https://script.google.com/macros/s/AKfycbyIJ6zUa0LHzdZU5GSO7h0pDWUPCZ1xAKQxWNST88Y9KpgCSsw0fE2u00xHLWW9_S-eng/exec";

        private static Version currentVersion, latestVersion;

        private static AvatarPoseSettings settingsBuff;

        public static AvatarPoseSettings Settings
        {
            get
            {
                if (settingsBuff) return settingsBuff;

                var filePath = AssetDatabase.GUIDToAssetPath(settingsGuid);
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new NullReferenceException("Settingファイルが見つかりません。再インポートしてください。");
                }

                settingsBuff = AssetDatabase.LoadAssetAtPath<AvatarPoseSettings>(filePath);
                return settingsBuff;
            }
        }

        public static Version CurrentVersion
        {
            get
            {
                if (currentVersion == null)
                {
                    var request = Client.List(true, true);
                    while (!request.IsCompleted) { }

                    if (request.Status == StatusCode.Success)
                    {
                        var versionStr = request.Result.FirstOrDefault(pkg =>
                            pkg.name == packageName)?.version ?? "";

                        if (!string.IsNullOrWhiteSpace(versionStr))
                        {
                            currentVersion = Version.Parse(versionStr);
                        }
                        else
                        {
                            currentVersion = new Version(0, 0, 0); // デフォルト
                        }
                    }
                }

                return currentVersion;
            }
        }

        public static Version LatestVersion
        {
            get
            {
                if (latestVersion == null)
                {
                    UnityWebRequest request = UnityWebRequest.Get(analyticsURL);
                    var operation = request.SendWebRequest();

                    float timeout = 5f; // タイムアウト秒数
                    float startTime = Time.realtimeSinceStartup;

                    while (!operation.isDone)
                    {
                        if (Time.realtimeSinceStartup - startTime > timeout)
                        {
                            Debug.LogError("Error: Request timed out.");
                            request.Abort();
                            return CurrentVersion;
                        }
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var data = JsonUtility.FromJson<AnalyticsResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrWhiteSpace(data.version))
                        {
                            latestVersion = Version.Parse(data.version);
                        }
                        else
                        {
                            latestVersion = CurrentVersion;
                        }
                    }
                    else
                    {
                        Debug.LogError("Error: " + request.error);
                        return CurrentVersion;
                    }
                }

                return latestVersion;
            }
        }

        class AnalyticsResponse
        {
            public string version;
        }
    }
}