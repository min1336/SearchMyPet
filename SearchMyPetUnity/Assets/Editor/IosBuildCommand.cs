using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
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

            var iosTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.iOS);
            PlayerSettings.SetApplicationIdentifier(iosTarget, BundleIdentifier);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.locationUsageDescription = "Search My Pet uses your location to show nearby characters on the map.";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.xcodeProjectType = XcodeProjectType.ObjectiveC;

            var outputRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", "outputs"));
            var outputPath = Path.GetFullPath(
                Path.Combine(outputRoot, "ios-xcode-wall-placement"));
            PrepareCleanOutputDirectory(outputRoot, outputPath);

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

#if UNITY_IOS
            ValidateSwiftLinkerConfiguration(outputPath);
#endif

            Debug.Log(
                $"[SearchMyPet iOS] Xcode export succeeded: {outputPath}, " +
                $"size={report.summary.totalSize}, duration={report.summary.totalTime}");
        }

        private static void PrepareCleanOutputDirectory(string outputRoot, string outputPath)
        {
            var expectedPrefix = outputRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!outputPath.StartsWith(expectedPrefix, System.StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Path.GetFileName(outputPath),
                    "ios-xcode-wall-placement",
                    System.StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    $"Refusing to clean an unexpected iOS export path: {outputPath}");
            }

            Directory.CreateDirectory(outputRoot);
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }
        }

#if UNITY_IOS
        private static void ValidateSwiftLinkerConfiguration(string outputPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(outputPath);
            if (!File.Exists(projectPath))
            {
                throw new BuildFailedException(
                    $"Generated Xcode project file was not found: {projectPath}");
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            ValidateSwiftLinkerConfiguration(
                project,
                project.GetUnityMainTargetGuid(),
                "Unity-iPhone");
            ValidateSwiftLinkerConfiguration(
                project,
                project.GetUnityFrameworkTargetGuid(),
                "UnityFramework");
            ValidatePortableSymbolProcessing(outputPath);
        }

        private static void ValidatePortableSymbolProcessing(string outputPath)
        {
            var projectText = File.ReadAllText(PBXProject.GetPBXProjectPath(outputPath));
            var scriptText = File.ReadAllText(Path.Combine(outputPath, "process_symbols.sh"));
            if (!projectText.Contains("/bin/sh \\\"$PROJECT_DIR/process_symbols.sh\\\"") ||
                !scriptText.Contains("chmod +x \"$PROJECT_DIR/$usymtool\""))
            {
                throw new BuildFailedException(
                    "The generated symbol-processing phase is not portable to macOS after Windows transfer.");
            }
        }

        private static void ValidateSwiftLinkerConfiguration(
            PBXProject project,
            string targetGuid,
            string targetName)
        {
            const string swiftSearchPath =
                "$(DEVELOPER_DIR)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/$(PLATFORM_NAME)";
            const string swift5SearchPath =
                "$(DEVELOPER_DIR)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift-5.0/$(PLATFORM_NAME)";

            var embedSwiftLibraries = project.GetBuildPropertyForAnyConfig(
                targetGuid,
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES");
            var librarySearchPaths = project.GetBuildPropertyForAnyConfig(
                targetGuid,
                "LIBRARY_SEARCH_PATHS");
            var runpathSearchPaths = project.GetBuildPropertyForAnyConfig(
                targetGuid,
                "LD_RUNPATH_SEARCH_PATHS");

            if (!string.Equals(
                    embedSwiftLibraries,
                    "YES",
                    System.StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(librarySearchPaths) ||
                !librarySearchPaths.Contains(swiftSearchPath) ||
                !librarySearchPaths.Contains(swift5SearchPath) ||
                string.IsNullOrEmpty(runpathSearchPaths) ||
                !runpathSearchPaths.Contains("/usr/lib/swift"))
            {
                throw new BuildFailedException(
                    $"The generated {targetName} target is missing ARKit Swift runtime linker settings. " +
                    "Do not transfer this export to the Mac; check IosXcodePostprocessor errors first.");
            }
        }
#endif
    }
}
