using System;
using System.Linq;
using System.Threading.Tasks;
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

        private static bool isFetchedLatestVersion = false;

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
                // 取得済みならそれを返す
                if (latestVersion != null)
                {
                    return latestVersion;
                }

                // 取得中でなければタスクを開始
                if (!isFetchedLatestVersion)
                {
                    isFetchedLatestVersion = true;
                    FetchLatestVersionAsync();
                }

                // 完了するまではCurrentVersionを返す
                return CurrentVersion;
            }
        }

        private static async Task FetchLatestVersionAsync()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(analyticsURL))
            {
                var tcs = new TaskCompletionSource<bool>();

                var operation = request.SendWebRequest();
                operation.completed += _ => tcs.SetResult(true);

                await tcs.Task;

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
                    Debug.LogError("Failed to fetch latest version: " + request.error);
                    latestVersion = CurrentVersion;
                }
            }
        }

        class AnalyticsResponse
        {
            public string version;
        }
    }
}