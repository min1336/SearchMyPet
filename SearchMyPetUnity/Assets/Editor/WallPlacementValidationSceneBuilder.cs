using SearchMyPet.AR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.Editor
{
    public static class WallPlacementValidationSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/WallPlacementValidation.unity";

        [MenuItem("Tools/Search My Pet/Build Wall Placement Validation Scene")]
        public static void Build()
        {
            ConfigureXrLoaders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var session = new GameObject("AR Session");
            session.AddComponent<ARSession>();

            var origin = new GameObject("XR Origin (AR)");
            var xrOrigin = origin.AddComponent<XROrigin>();
            var planeManager = origin.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            var raycastManager = origin.AddComponent<ARRaycastManager>();
            var anchorManager = origin.AddComponent<ARAnchorManager>();

            var cameraOffsetObject = new GameObject("Camera Offset");
            cameraOffsetObject.transform.SetParent(origin.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffsetObject;

            var cameraObject = new GameObject("AR Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffsetObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            ConfigureTrackedPoseDriver(cameraObject.AddComponent<TrackedPoseDriver>());
            xrOrigin.Camera = camera;

            var controllerObject = new GameObject("Wall Plane Detection Controller");
            var controller = controllerObject.AddComponent<WallPlaneDetectionController>();
            var (statusText, detailText, retryButton, settingsButton) = CreateStatusCanvas();
            controller.Configure(planeManager, raycastManager, anchorManager, statusText, detailText);
            retryButton.onClick.AddListener(controller.RetryCameraPermission);
            settingsButton.onClick.AddListener(controller.OpenAppSettings);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[WallPlaneDetection] Scene created and registered as Build Settings index 0: " + ScenePath);
        }

        private static void ConfigureXrLoaders()
        {
            const string settingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var settingsGuids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            XRGeneralSettingsPerBuildTarget settingsPerBuildTarget;

            if (settingsGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
                settingsPerBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            }
            else
            {
                settingsPerBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settingsPerBuildTarget, settingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, settingsPerBuildTarget, true);
            AssignLoader(
                settingsPerBuildTarget,
                BuildTargetGroup.Standalone,
                "UnityEngine.XR.Simulation.SimulationLoader");
            AssignLoader(
                settingsPerBuildTarget,
                BuildTargetGroup.iOS,
                "UnityEngine.XR.ARKit.ARKitLoader");
            EditorUtility.SetDirty(settingsPerBuildTarget);
            AssetDatabase.SaveAssets();
        }

        private static void AssignLoader(
            XRGeneralSettingsPerBuildTarget settingsPerBuildTarget,
            BuildTargetGroup buildTargetGroup,
            string loaderTypeName)
        {
            if (!settingsPerBuildTarget.HasManagerSettingsForBuildTarget(buildTargetGroup))
            {
                settingsPerBuildTarget.CreateDefaultManagerSettingsForBuildTarget(buildTargetGroup);
            }

            var generalSettings = settingsPerBuildTarget.SettingsForBuildTarget(buildTargetGroup);
            generalSettings.InitManagerOnStart = true;
            EditorUtility.SetDirty(generalSettings);

            var managerSettings = settingsPerBuildTarget.ManagerSettingsForBuildTarget(buildTargetGroup);
            if (!XRPackageMetadataStore.AssignLoader(managerSettings, loaderTypeName, buildTargetGroup))
            {
                throw new System.InvalidOperationException(
                    $"Failed to assign XR loader '{loaderTypeName}' for {buildTargetGroup}.");
            }
        }

        private static void ConfigureTrackedPoseDriver(TrackedPoseDriver trackedPoseDriver)
        {
            trackedPoseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            trackedPoseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            trackedPoseDriver.positionInput = CreateInputAction(
                "Center Eye Position",
                "Vector3",
                "<XRHMD>/centerEyePosition");
            trackedPoseDriver.rotationInput = CreateInputAction(
                "Center Eye Rotation",
                "Quaternion",
                "<XRHMD>/centerEyeRotation");
            trackedPoseDriver.trackingStateInput = CreateInputAction(
                "Tracking State",
                "Integer",
                "<XRHMD>/trackingState");
        }

        private static InputActionProperty CreateInputAction(
            string name,
            string expectedControlType,
            string binding)
        {
            var action = new InputAction(name, InputActionType.Value, binding);
            action.expectedControlType = expectedControlType;
            return new InputActionProperty(action);
        }

        private static (Text statusText, Text detailText, Button retryButton, Button settingsButton) CreateStatusCanvas()
        {
            var canvasObject = new GameObject("Status Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("Status Panel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -48f);
            panelRect.sizeDelta = new Vector2(980f, 180f);
            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0.08f, 0.12f, 0.78f);

            var statusText = CreateText("Status Text", panelObject.transform, 38, FontStyle.Bold, new Vector2(0f, -14f));
            var detailText = CreateText("Detail Text", panelObject.transform, 26, FontStyle.Normal, new Vector2(0f, -86f));
            var retryButton = CreateButton("권한 다시 확인", panelObject.transform, new Vector2(-160f, -142f));
            var settingsButton = CreateButton("설정 열기", panelObject.transform, new Vector2(160f, -142f));
            return (statusText, detailText, retryButton, settingsButton);
        }

        private static Text CreateText(string name, Transform parent, int fontSize, FontStyle fontStyle, Vector2 position)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(920f, 58f);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = name == "Status Text" ? "AR 준비 중" : "벽면을 천천히 비춰 주세요.";
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 position)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(260f, 48f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.55f, 0.72f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText("Label", buttonObject.transform, 22, FontStyle.Bold, Vector2.zero);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.text = label;
            return button;
        }
    }
}
