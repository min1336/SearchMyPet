using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SearchMyPet.Editor
{
    public static class IosBuildCommand
    {
        private const string BundleIdentifier = "com.min1336.searchmypet";

        [MenuItem("Tools/Search My Pet/Export iOS Xcode Project")]
        public static void BuildFromMenu()
        {
            BuildFromCommandLine();
        }

        public static void BuildFromCommandLine()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                throw new BuildFailedException(
                    "Unity iOS Build Support is not installed for this Editor. " +
                    "Install it from Unity Hub before exporting the Xcode project.");
            }

            var scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scene is registered in Build Settings.");
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                throw new BuildFailedException("Failed to switch the active build target to iOS.");
            }

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleIdentifier);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;

            var outputPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "outputs", "ios-xcode-wall-placement"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.Development,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"iOS Xcode export failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            }

            Debug.Log(
                $"[SearchMyPet iOS] Xcode export succeeded: {outputPath}, " +
                $"size={report.summary.totalSize}, duration={report.summary.totalTime}");
        }
    }
}
