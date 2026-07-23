using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace CamoHuntAR.Tests
{
    public sealed class GeneratedProjectTests
    {
        private const string CharacterPrefabPath = "Assets/CamoHuntAR/Prefabs/CamoCritter.prefab";
        private const string CharacterModelPath = "Assets/CamoHuntAR/Art/MeshyOpenArmsCharacter.fbx";
        private const string CharacterPreviewMaterialPath =
            "Assets/CamoHuntAR/Materials/CharacterPreview.mat";
        private const string CharacterPlacedMaterialPath =
            "Assets/CamoHuntAR/Materials/CharacterPlaced.mat";
        private const string PlanePrefabPath = "Assets/CamoHuntAR/Prefabs/DetectedPlane.prefab";
        private const string ScenePath = "Assets/CamoHuntAR/Scenes/ARPlacementScene.unity";

        [Test]
        public void CharacterPrefabHasOneColliderAndPlacementVisual()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(model, Is.Not.Null);
            Assert.That(prefab.GetComponent<PlacementVisual>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.transform.Find("VisualRoot/MeshyOpenArmsCharacter"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Has.Length.EqualTo(1));

            var skinnedRenderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skinnedRenderer, Is.Not.Null);
            Assert.That(skinnedRenderer.sharedMesh, Is.Not.Null);
            Assert.That(skinnedRenderer.sharedMesh.vertexCount, Is.EqualTo(19259));
            Assert.That(skinnedRenderer.sharedMesh.triangles, Has.Length.EqualTo(108576));
            Assert.That(skinnedRenderer.sharedMesh.bindposes, Has.Length.EqualTo(24));

            var importer = AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));

            var importedClips = AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                .ToArray();
            Assert.That(importedClips, Is.Empty);
        }

        [Test]
        public void CharacterPrefabIsNormalizedAndKeepsPreviewMaterialFlow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            var previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterPreviewMaterialPath);
            var placedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CharacterPlacedMaterialPath);
            var instance = Object.Instantiate(prefab);

            try
            {
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var visual = instance.GetComponent<PlacementVisual>();
                Assert.That(renderer.bounds.size.y, Is.EqualTo(0.24f).Within(0.005f));
                Assert.That(renderer.bounds.min.y, Is.EqualTo(0f).Within(0.005f));
                Assert.That(renderer.bounds.center.z, Is.EqualTo(0.035f).Within(0.005f));

                visual.SetPreview(true);
                Assert.That(renderer.sharedMaterial, Is.SameAs(previewMaterial));
                visual.SetPreview(false);
                Assert.That(renderer.sharedMaterial, Is.SameAs(placedMaterial));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PlanePrefabHasTheRequiredVisualizationComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshFilter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LineRenderer>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ARPlaneMeshVisualizer>(), Is.Not.Null);
        }

        [Test]
        public void GeneratedSceneContainsOneCompletePlacementFlow()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                AssertSingle<ARSession>(scene);
                var origin = AssertSingle<XROrigin>(scene);
                Assert.That(origin.Camera, Is.Not.Null);
                var planeManager = AssertSingle<ARPlaneManager>(scene);
                Assert.That(planeManager.planePrefab, Is.Not.Null);
                AssertSingle<ARRaycastManager>(scene);
                AssertSingle<ARAnchorManager>(scene);
                var controller = AssertSingle<ARPlacementController>(scene);
                var tracking = AssertSingle<ARTrackingStatusController>(scene);
                AssertSingle<EventSystem>(scene);
                AssertSingle<InputSystemUIInputModule>(scene);
                AssertSingle<SafeAreaFitter>(scene);
                AssertSingle<RuntimeFontBinder>(scene);

                AssertSerializedReferences(controller,
                    "arCamera",
                    "raycastManager",
                    "planeManager",
                    "anchorManager",
                    "characterPrefab",
                    "confirmButton",
                    "resetButton");
                AssertSerializedReferences(tracking,
                    "planeManager",
                    "placementController",
                    "statusText");
            }
            finally
            {
                if (openedForTest && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void XrLoadersUseTheGeneralSettingsLifecycleForTheirIntendedBuildTargets()
        {
            var settingsGuid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").Single();
            var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                AssetDatabase.GUIDToAssetPath(settingsGuid));

            Assert.That(settings, Is.Not.Null);
            Assert.That(
                settings.SettingsForBuildTarget(BuildTargetGroup.Standalone).InitManagerOnStart,
                Is.True);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone).automaticLoading,
                Is.False);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone).automaticRunning,
                Is.False);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Standalone)
                    .activeLoaders.Select(loader => loader.GetType().FullName),
                Does.Contain("UnityEngine.XR.Simulation.SimulationLoader"));
            Assert.That(
                settings.SettingsForBuildTarget(BuildTargetGroup.iOS).InitManagerOnStart,
                Is.True);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS).automaticLoading,
                Is.False);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS).automaticRunning,
                Is.False);
            Assert.That(
                settings.ManagerSettingsForBuildTarget(BuildTargetGroup.iOS)
                    .activeLoaders.Select(loader => loader.GetType().FullName),
                Does.Contain("UnityEngine.XR.ARKit.ARKitLoader"));
        }

        private static T AssertSingle<T>(Scene scene) where T : Component
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            Assert.That(components, Has.Length.EqualTo(1), typeof(T).Name);
            return components[0];
        }

        private static void AssertSerializedReferences(Object target, params string[] propertyNames)
        {
            var serializedObject = new SerializedObject(target);
            foreach (var propertyName in propertyNames)
            {
                var property = serializedObject.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }
    }
}
