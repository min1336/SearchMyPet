using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CamoHuntAR.Editor
{
    /// <summary>Generates the Xcode project that must be signed and installed from macOS.</summary>
    public static class IosBuildCommand
    {
        private const string OutputPathEnvironmentVariable = "CAMO_HUNT_IOS_XCODE_PATH";

        [MenuItem("CAMO HUNT/Build iOS Xcode Project")]
        public static void BuildFromCommandLine()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are configured for the iOS build.");

            var outputPath = Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    "outputs",
                    "ios-xcode"));
            }

            Directory.CreateDirectory(outputPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"iOS Xcode project generation failed: {report.summary.result}. " +
                    $"See the Unity build log for details.");
            }

            Debug.Log($"iOS Xcode project generated at: {outputPath}");
        }
    }
}
