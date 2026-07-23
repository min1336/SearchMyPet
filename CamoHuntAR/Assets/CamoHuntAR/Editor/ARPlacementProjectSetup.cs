using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace CamoHuntAR.Editor
{
    public static class ARPlacementProjectSetup
    {
        private const string RootFolder = "Assets/CamoHuntAR";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string ArtFolder = RootFolder + "/Art";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string PreviewMaterialPath = MaterialsFolder + "/CharacterPreview.mat";
        private const string PlacedMaterialPath = MaterialsFolder + "/CharacterPlaced.mat";
        private const string PlaneMaterialPath = MaterialsFolder + "/DetectedPlane.mat";
        private const string CharacterModelPath = ArtFolder + "/MeshyOpenArmsCharacter.fbx";
        private const string CharacterPrefabPath = PrefabsFolder + "/CamoCritter.prefab";
        private const string PlanePrefabPath = PrefabsFolder + "/DetectedPlane.prefab";
        private const string ScenePath = ScenesFolder + "/ARPlacementScene.unity";
        private const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string SimulationLoaderType = "UnityEngine.XR.Simulation.SimulationLoader";
        private const string ArKitLoaderType = "UnityEngine.XR.ARKit.ARKitLoader";
        private const float CharacterHeight = 0.24f;
        private const float CharacterSurfaceOffset = 0.035f;

        [MenuItem("CAMO HUNT/Build AR Placement Prototype")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void Build()
        {
            EnsureFolders();
            ConfigureCharacterModelImporter();
            ConfigurePlayerSettings();
            ConfigureXrLoaders();

            var previewMaterial = CreateOrUpdateMaterial(
                PreviewMaterialPath,
                new Color(0.15f, 0.93f, 0.95f, 0.55f),
                transparent: true);
            var placedMaterial = CreateOrUpdateMaterial(
                PlacedMaterialPath,
                new Color(0.12f, 0.68f, 0.52f, 1f),
                transparent: false);
            var planeMaterial = CreateOrUpdateMaterial(
                PlaneMaterialPath,
                new Color(0.15f, 0.88f, 1f, 0.18f),
                transparent: true);

            var planePrefab = CreateOrUpdatePlanePrefab(planeMaterial);
            var characterPrefab = CreateOrUpdateCharacterPrefab(previewMaterial, placedMaterial);
            CreateOrUpdateScene(planePrefab, characterPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedAssets();
            Debug.Log("CAMO HUNT AR placement prototype setup completed successfully.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "CamoHuntAR");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Art");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder("Assets", "XR");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigureCharacterModelImporter()
        {
            var importer = AssetImporter.GetAtPath(CharacterModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Character model importer is missing: {CharacterModelPath}");

            var changed = false;
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                changed = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "CAMO HUNT AR Prototype";
            PlayerSettings.companyName = "Independent Prototype";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kidal.camohuntar.prototype");
            PlayerSettings.iOS.cameraUsageDescription =
                "실제 표면을 인식하고 AR 캐릭터를 배치하기 위해 카메라를 사용합니다.";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
        }

        private static void ConfigureXrLoaders()
        {
            var perBuildTarget = LoadOrCreateXrSettings();
            AssignLoader(perBuildTarget, BuildTargetGroup.Standalone, SimulationLoaderType);
            AssignLoader(perBuildTarget, BuildTargetGroup.iOS, ArKitLoaderType);
            EditorUtility.SetDirty(perBuildTarget);
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreateXrSettings()
        {
            var guid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
            var settings = string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    AssetDatabase.GUIDToAssetPath(guid));

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                settings.name = "XRGeneralSettingsPerBuildTarget";
                AssetDatabase.CreateAsset(settings, XrSettingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settings, true);
            return settings;
        }

        private static void AssignLoader(
            XRGeneralSettingsPerBuildTarget perBuildTarget,
            BuildTargetGroup targetGroup,
            string loaderType)
        {
            if (!perBuildTarget.HasSettingsForBuildTarget(targetGroup))
                perBuildTarget.CreateDefaultSettingsForBuildTarget(targetGroup);
            if (!perBuildTarget.HasManagerSettingsForBuildTarget(targetGroup))
                perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(targetGroup);

            var generalSettings = perBuildTarget.SettingsForBuildTarget(targetGroup);
            generalSettings.InitManagerOnStart = true;
            // XRGeneralSettings owns the loader lifecycle when InitManagerOnStart is enabled.
            // Leaving the legacy manager callbacks enabled makes inactive build targets call
            // StopSubsystems during asset unload even though they were never initialized.
            generalSettings.Manager.automaticLoading = false;
            generalSettings.Manager.automaticRunning = false;
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(generalSettings.Manager);

            if (!generalSettings.Manager.activeLoaders.Any(loader =>
                    loader != null && loader.GetType().FullName == loaderType) &&
                !XRPackageMetadataStore.AssignLoader(generalSettings.Manager, loaderType, targetGroup))
            {
                throw new InvalidOperationException(
                    $"Failed to assign XR loader '{loaderType}' for {targetGroup}.");
            }
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, bool transparent)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("Built-in Standard shader was not found.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.color = color;
            material.SetFloat("_Glossiness", transparent ? 0f : 0.25f);
            material.SetFloat("_Metallic", 0f);
            ConfigureStandardTransparency(material, transparent);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureStandardTransparency(Material material, bool transparent)
        {
            if (transparent)
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Mode", 0f);
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = -1;
            }
        }

        private static GameObject CreateOrUpdatePlanePrefab(Material material)
        {
            GameObject root = null;
            try
            {
                root = new GameObject(
                    "DetectedPlane",
                    typeof(MeshFilter),
                    typeof(MeshRenderer),
                    typeof(LineRenderer),
                    typeof(ARPlaneMeshVisualizer));

                var meshRenderer = root.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var lineRenderer = root.GetComponent<LineRenderer>();
                lineRenderer.sharedMaterial = material;
                lineRenderer.startColor = material.color;
                lineRenderer.endColor = material.color;
                lineRenderer.startWidth = 0.006f;
                lineRenderer.endWidth = 0.006f;
                lineRenderer.loop = true;
                lineRenderer.useWorldSpace = false;

                return SavePrefab(root, PlanePrefabPath);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateOrUpdateCharacterPrefab(Material preview, Material placed)
        {
            GameObject root = null;
            try
            {
                root = new GameObject("CamoCritter");
                var visualRoot = new GameObject("VisualRoot").transform;
                visualRoot.SetParent(root.transform, false);
                visualRoot.localPosition = Vector3.zero;

                var modelAsset = RequireAsset<GameObject>(CharacterModelPath);
                var modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (modelInstance == null)
                    throw new InvalidOperationException($"Failed to instantiate character model: {CharacterModelPath}");

                modelInstance.name = "MeshyOpenArmsCharacter";
                modelInstance.transform.SetParent(visualRoot, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("The character model contains no renderers.");

                NormalizeCharacterModel(modelInstance.transform, renderers);
                foreach (var renderer in renderers)
                {
                    var materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                    renderer.sharedMaterials = Enumerable.Repeat(placed, materialCount).ToArray();
                }

                var collider = root.AddComponent<CapsuleCollider>();
                var characterBounds = CalculateRendererBounds(renderers);
                collider.center = root.transform.InverseTransformPoint(characterBounds.center);
                collider.radius = Mathf.Max(characterBounds.size.x, characterBounds.size.z) * 0.5f;
                collider.height = Mathf.Max(characterBounds.size.y, collider.radius * 2f);
                collider.direction = 1;

                var visual = root.AddComponent<PlacementVisual>();
                visual.Configure(renderers, preview, placed);
                visual.SetPreview(false);
                return SavePrefab(root, CharacterPrefabPath);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        private static void NormalizeCharacterModel(Transform model, Renderer[] renderers)
        {
            var sourceBounds = CalculateRendererBounds(renderers);
            if (sourceBounds.size.y <= Mathf.Epsilon)
                throw new InvalidOperationException("The character model has an invalid height.");

            model.localScale = Vector3.one * (CharacterHeight / sourceBounds.size.y);
            var scaledBounds = CalculateRendererBounds(renderers);
            var parent = model.parent;
            var localCenter = parent.InverseTransformPoint(scaledBounds.center);
            var localBottom = parent.InverseTransformPoint(
                new Vector3(scaledBounds.center.x, scaledBounds.min.y, scaledBounds.center.z));
            model.localPosition += new Vector3(
                -localCenter.x,
                -localBottom.y,
                CharacterSurfaceOffset - localCenter.z);
        }

        private static Bounds CalculateRendererBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                throw new InvalidOperationException("At least one renderer is required.");

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            if (!success || prefab == null)
                throw new InvalidOperationException($"Failed to save prefab at '{path}'.");
            return prefab;
        }

        private static void CreateOrUpdateScene(GameObject planePrefab, GameObject characterPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                var existingScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                try
                {
                    ValidateScene(existingScene);
                    UpdateBuildSettings();
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Existing AR placement scene is incomplete and will be rebuilt: {exception.Message}");
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Selection.activeObject = null;
            if (!EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session"))
                throw new InvalidOperationException("Failed to create AR Session from the Unity menu.");

            Selection.activeObject = null;
            if (!EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)"))
                throw new InvalidOperationException("Failed to create XR Origin from the Unity menu.");

            var arSession = FindSingleComponent<ARSession>(scene);
            var xrOrigin = FindSingleComponent<XROrigin>(scene);
            var arCamera = xrOrigin.Camera;
            if (arCamera == null)
                throw new InvalidOperationException("XR Origin does not have an AR camera.");

            var planeManager = GetOrAddComponent<ARPlaneManager>(xrOrigin.gameObject);
            var raycastManager = GetOrAddComponent<ARRaycastManager>(xrOrigin.gameObject);
            var anchorManager = GetOrAddComponent<ARAnchorManager>(xrOrigin.gameObject);
            planeManager.requestedDetectionMode =
                PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            planeManager.planePrefab = planePrefab;

            var app = new GameObject("App");
            var placementObject = new GameObject("ARPlacementController");
            placementObject.transform.SetParent(app.transform, false);
            var placementController = placementObject.AddComponent<ARPlacementController>();
            var trackingObject = new GameObject("ARTrackingStatusController");
            trackingObject.transform.SetParent(app.transform, false);
            var trackingController = trackingObject.AddComponent<ARTrackingStatusController>();

            var canvas = CreateCanvas();
            var safeArea = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea.transform.SetParent(canvas.transform, false);
            Stretch(safeArea.GetComponent<RectTransform>());

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var statusText = CreateStatusPanel(safeArea.transform, font);
            CreateCenterReticle(safeArea.transform);
            var confirmButton = CreateButton(
                safeArea.transform,
                "ConfirmButton",
                "배치 확정",
                new Color(0.10f, 0.68f, 0.55f, 0.96f),
                font);
            var resetButton = CreateButton(
                safeArea.transform,
                "ResetButton",
                "다시 배치",
                new Color(0.12f, 0.34f, 0.44f, 0.96f),
                font);

            var fontBinder = canvas.gameObject.AddComponent<RuntimeFontBinder>();
            SetObjectReferences(fontBinder, "targets", new[]
            {
                statusText,
                confirmButton.GetComponentInChildren<Text>(true),
                resetButton.GetComponentInChildren<Text>(true)
            });

            CreateEventSystem();
            SetObjectReference(placementController, "arCamera", arCamera);
            SetObjectReference(placementController, "raycastManager", raycastManager);
            SetObjectReference(placementController, "planeManager", planeManager);
            SetObjectReference(placementController, "anchorManager", anchorManager);
            SetObjectReference(placementController, "characterPrefab", characterPrefab);
            SetObjectReference(placementController, "confirmButton", confirmButton);
            SetObjectReference(placementController, "resetButton", resetButton);
            SetObjectReference(trackingController, "planeManager", planeManager);
            SetObjectReference(trackingController, "placementController", placementController);
            SetObjectReference(trackingController, "statusText", statusText);

            if (arSession == null)
                throw new InvalidOperationException("AR Session was not created.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Failed to save scene at '{ScenePath}'.");

            UpdateBuildSettings();
            ValidateScene(scene);
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Text CreateStatusPanel(Transform parent, Font font)
        {
            var panel = new GameObject("StatusPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.offsetMin = new Vector2(28f, -232f);
            panelRect.offsetMax = new Vector2(-28f, -82f);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.08f, 0.12f, 0.78f);

            var textObject = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            Stretch(textRect);
            textRect.offsetMin = new Vector2(28f, 16f);
            textRect.offsetMax = new Vector2(-28f, -16f);

            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 34;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "AR을 준비하는 중입니다.";
            return text;
        }

        private static void CreateCenterReticle(Transform parent)
        {
            var root = new GameObject("CenterReticle", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(32f, 32f);

            CreateReticleLine(root.transform, "Horizontal", new Vector2(32f, 4f));
            CreateReticleLine(root.transform, "Vertical", new Vector2(4f, 32f));
        }

        private static void CreateReticleLine(Transform parent, string name, Vector2 size)
        {
            var line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = line.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.9f);
            image.raycastTarget = false;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color color,
            Font font)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 72f);
            rect.sizeDelta = new Vector2(420f, 104f);

            var image = buttonObject.GetComponent<Image>();
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            Stretch(textObject.GetComponent<RectTransform>());
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = label;
            return button;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void UpdateBuildSettings()
        {
            var sceneEntries = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            sceneEntries.AddRange(EditorBuildSettings.scenes.Where(scene =>
                !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase)));
            EditorBuildSettings.scenes = sceneEntries.ToArray();
        }

        private static void ValidateGeneratedAssets()
        {
            RequireAsset<Material>(PreviewMaterialPath);
            RequireAsset<Material>(PlacedMaterialPath);
            RequireAsset<Material>(PlaneMaterialPath);
            RequireAsset<GameObject>(CharacterModelPath);
            RequireAsset<GameObject>(CharacterPrefabPath);
            RequireAsset<GameObject>(PlanePrefabPath);
            RequireAsset<SceneAsset>(ScenePath);
        }

        private static void ValidateScene(Scene scene)
        {
            FindSingleComponent<ARSession>(scene);
            var xrOrigin = FindSingleComponent<XROrigin>(scene);
            var planeManager = FindSingleComponent<ARPlaneManager>(scene);
            FindSingleComponent<ARRaycastManager>(scene);
            FindSingleComponent<ARAnchorManager>(scene);
            var placementController = FindSingleComponent<ARPlacementController>(scene);
            var trackingController = FindSingleComponent<ARTrackingStatusController>(scene);
            FindSingleComponent<EventSystem>(scene);
            FindSingleComponent<InputSystemUIInputModule>(scene);
            FindSingleComponent<SafeAreaFitter>(scene);
            FindSingleComponent<RuntimeFontBinder>(scene);
            FindSingleComponent<Canvas>(scene);

            if (xrOrigin.Camera == null)
                throw new InvalidOperationException("XR Origin camera reference is missing.");
            if (planeManager.planePrefab == null)
                throw new InvalidOperationException("AR Plane Manager prefab reference is missing.");

            RequireObjectReference(placementController, "arCamera");
            RequireObjectReference(placementController, "raycastManager");
            RequireObjectReference(placementController, "planeManager");
            RequireObjectReference(placementController, "anchorManager");
            RequireObjectReference(placementController, "characterPrefab");
            RequireObjectReference(placementController, "confirmButton");
            RequireObjectReference(placementController, "resetButton");
            RequireObjectReference(trackingController, "planeManager");
            RequireObjectReference(trackingController, "placementController");
            RequireObjectReference(trackingController, "statusText");
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Required asset is missing: {path}");
            return asset;
        }

        private static T FindSingleComponent<T>(Scene scene) where T : Component
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {typeof(T).Name}, found {components.Length}.");
            }
            return components[0];
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out var component)
                ? component
                : gameObject.AddComponent<T>();
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized property '{propertyName}' was not found on {target}.");
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReferences(Object target, string propertyName, IReadOnlyList<Object> values)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new InvalidOperationException($"Serialized array '{propertyName}' was not found on {target}.");

            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireObjectReference(Object target, string propertyName)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"Serialized reference '{propertyName}' is missing on {target.name}.");
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
