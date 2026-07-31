#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace SearchMyPet.Editor
{
    /// <summary>
    /// Keeps the generated Objective-C Xcode project compatible with the
    /// Swift-based native code shipped in Apple ARKit XR Plug-in 6.5.
    /// </summary>
    public sealed class IosXcodePostprocessor : IPostprocessBuildWithReport
    {
        private const string SwiftRuntimePath = "/usr/lib/swift";
        private const string SwiftLibrarySearchPath =
            "$(DEVELOPER_DIR)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/$(PLATFORM_NAME)";
        private const string Swift5LibrarySearchPath =
            "$(DEVELOPER_DIR)/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift-5.0/$(PLATFORM_NAME)";

        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            if (!File.Exists(projectPath))
            {
                throw new BuildFailedException(
                    $"Generated Xcode project file was not found: {projectPath}");
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            ConfigureSwiftRuntimeLinking(project, project.GetUnityMainTargetGuid());
            ConfigureSwiftRuntimeLinking(project, project.GetUnityFrameworkTargetGuid());
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "MapKit.framework", false);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreLocation.framework", false);

            project.WriteToFile(projectPath);
            MakeSymbolProcessingPortable(report.summary.outputPath, projectPath);

            Debug.Log(
                "[SearchMyPet iOS] Applied ARKit Swift runtime linker settings " +
                "to Unity-iPhone and UnityFramework.");
        }

        private static void MakeSymbolProcessingPortable(string outputPath, string projectPath)
        {
            foreach (var name in new[] { "process_symbols.sh", "process_symbols_il2cpp.sh" })
            {
                var path = Path.Combine(outputPath, name);
                if (!File.Exists(path)) continue;
                var script = File.ReadAllText(path);
                const string selectionEnd = "\nfi\n";
                if (!script.Contains("chmod +x \"$PROJECT_DIR/$usymtool\"") && script.Contains(selectionEnd))
                {
                    script = script.Replace(
                        selectionEnd,
                        selectionEnd + "\nchmod +x \"$PROJECT_DIR/$usymtool\"\n");
                    File.WriteAllText(path, script);
                }
            }

            var pbx = File.ReadAllText(projectPath);
            pbx = pbx.Replace(
                "shellScript = \"\\\"$PROJECT_DIR/process_symbols.sh\\\"\";",
                "shellScript = \"/bin/sh \\\"$PROJECT_DIR/process_symbols.sh\\\"\";");
            File.WriteAllText(projectPath, pbx);
        }

        private static void ConfigureSwiftRuntimeLinking(PBXProject project, string targetGuid)
        {
            project.SetBuildProperty(
                targetGuid,
                "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES",
                "YES");

            AddBuildPropertyValue(project, targetGuid, "LD_RUNPATH_SEARCH_PATHS", SwiftRuntimePath);
            AddBuildPropertyValue(project, targetGuid, "LIBRARY_SEARCH_PATHS", SwiftRuntimePath);
            AddBuildPropertyValue(project, targetGuid, "LIBRARY_SEARCH_PATHS", SwiftLibrarySearchPath);
            AddBuildPropertyValue(project, targetGuid, "LIBRARY_SEARCH_PATHS", Swift5LibrarySearchPath);
        }

        private static void AddBuildPropertyValue(
            PBXProject project,
            string targetGuid,
            string propertyName,
            string value)
        {
            project.UpdateBuildProperty(
                targetGuid,
                propertyName,
                new[] { value },
                Array.Empty<string>());
        }
    }
}
#endif
