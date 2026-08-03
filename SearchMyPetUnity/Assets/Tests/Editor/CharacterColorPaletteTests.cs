using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SearchMyPet.AR.Tests
{
    public sealed class CharacterColorPaletteTests
    {
        private const string ScenePath = "Assets/Scenes/WallPlacementValidation.unity";
        [Test]
        public void Stamp_PaintsBrushAreaWithoutChangingDistantPixels()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[texture.width * texture.height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }
            texture.SetPixels(pixels);

            CharacterColorPalette.Stamp(
                texture,
                new Vector2(0.5f, 0.5f),
                Color.blue,
                12,
                PaintBrushTexture.Soft);

            Assert.That(texture.GetPixel(16, 16).b, Is.GreaterThan(0.99f));
            Assert.That(texture.GetPixel(16, 16).r, Is.LessThan(0.2f));
            Assert.That(texture.GetPixel(0, 0), Is.EqualTo(Color.white));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void StampStroke_PaintsOnlyTheCurrentBrushPoint()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[texture.width * texture.height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }
            texture.SetPixels(pixels);

            CharacterColorPalette.StampStroke(
                texture,
                new Vector2(0.15f, 0.5f),
                new Vector2(0.85f, 0.5f),
                Color.blue,
                8,
                PaintBrushTexture.Soft);

            Assert.That(texture.GetPixel(32, 32), Is.EqualTo(Color.white));
            Assert.That(texture.GetPixel(54, 32).b, Is.GreaterThan(0.99f));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void ScreenToCameraUv_AppliesDisplayRotationAndCrop()
        {
            var displayMatrix = Matrix4x4.identity;
            displayMatrix.m00 = 0f;
            displayMatrix.m10 = 0.5f;
            displayMatrix.m30 = 0.25f;
            displayMatrix.m01 = -1f;
            displayMatrix.m11 = 0f;
            displayMatrix.m31 = 1f;
            var method = typeof(CharacterColorPalette).GetMethod(
                "ScreenToCameraUv",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var uv = (Vector2)method.Invoke(
                null,
                new object[] { new Vector2(300f, 200f), new Vector2(400f, 400f), displayMatrix });

            Assert.That(uv.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(uv.y, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void PaintingMode_ProtectsEyesAndCompletesBackToAr()
        {
            var root = new GameObject("Character");
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var sourceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var body = CreatePart("Body", root.transform, sourceMaterial);
                var eye = CreatePart("Left Eye", root.transform, sourceMaterial);
                var palette = paletteObject.AddComponent<CharacterColorPalette>();

                palette.Initialize(canvasObject.transform);
                palette.SetCharacter(root);

                Assert.That(palette.IsPainting, Is.True);
                Assert.That(palette.ToolsOpen, Is.False);
                Assert.That(canvasObject.transform.Find("Character Paint UI").gameObject.activeSelf, Is.True);
                Assert.That(body.sharedMaterial, Is.Not.SameAs(sourceMaterial));
                Assert.That(eye.sharedMaterial, Is.SameAs(sourceMaterial));

                palette.CompletePainting();
                Assert.That(palette.IsPainting, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void PaintUi_UsesSafeAreaAndBrushSlider()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                var safeArea = canvasObject.transform.Find("Character Paint UI/Safe Area");
                var toolbar = safeArea.Find("Paint Toolbar");
                var brush = toolbar.Find("붓").GetComponent<Image>();
                var slider = safeArea.Find("Paint Tool Options/Brush Size Slider").GetComponent<Slider>();

                Assert.That(safeArea, Is.Not.Null);
                Assert.That(toolbar.childCount, Is.EqualTo(4));
                Assert.That(toolbar.Find("크기"), Is.Null);
                Assert.That(brush.color.a, Is.EqualTo(1f));

                palette.OpenToolMenu();
                slider = safeArea.Find("Paint Tool Options/Brush Size Slider").GetComponent<Slider>();
                Assert.That(slider.gameObject.activeSelf, Is.True);
                slider.value = 48f;
                Assert.That(slider.value, Is.EqualTo(48f));
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void Initialize_ShowsInactivePlacementInstructionPanel()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var instructionPanel = new GameObject("Placement Instruction Panel");
            instructionPanel.transform.SetParent(canvasObject.transform, false);
            instructionPanel.SetActive(false);
            try
            {
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);

                Assert.That(instructionPanel.activeSelf, Is.True);
                palette.SetPlacementInstructionVisible(false);
                Assert.That(instructionPanel.activeSelf, Is.False);
                palette.SetPlacementInstructionVisible(true);
                Assert.That(instructionPanel.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void FallbackUi_PaletteButtonTogglesToolMenu()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                var paletteButton = canvasObject.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Color Palette Button").GetComponent<Button>();

                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.True);
                var safeArea = canvasObject.transform.Find("Character Paint UI/Safe Area");
                safeArea.Find("Paint Toolbar/팔레트").GetComponent<Button>().onClick.Invoke();
                Assert.That(((RectTransform)safeArea.Find("Paint Tool Options/Hue/Handle")).sizeDelta,
                    Is.EqualTo(new Vector2(4f, 24f)));
                safeArea.Find("Paint Toolbar/스포이드").GetComponent<Button>().onClick.Invoke();
                Assert.That(safeArea.Find("Paint Tool Options").gameObject.activeSelf, Is.False);
                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void HistoryUi_IsVerticalAndStartsDisabled()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);

                var history = canvasObject.transform.Find("Character Paint UI/Safe Area/Paint History Controls");
                Assert.That(history, Is.Not.Null);
                Assert.That(history.gameObject.activeSelf, Is.False);
                Assert.That(history.Find("Undo Button").GetComponent<Button>().interactable, Is.False);
                Assert.That(history.Find("Redo Button").GetComponent<Button>().interactable, Is.False);

                var undo = (RectTransform)history.Find("Undo Button");
                var redo = (RectTransform)history.Find("Redo Button");
                Assert.That(undo.anchoredPosition.y, Is.GreaterThan(redo.anchoredPosition.y));
                Assert.That(palette.CanUndo, Is.False);
                Assert.That(palette.CanRedo, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void PoseButton_OpensCenteredTwoByTwoPosePopupAndTogglesClosed()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var character = new GameObject("Character");
            try
            {
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                palette.SetCharacter(character);

                var paintUi = canvasObject.transform.Find("Character Paint UI");
                var poseButton = paintUi.Find("Safe Area/Paint Quick Controls/Pose Button").GetComponent<Button>();
                var popup = paintUi.Find("Pose Popup");
                var card = (RectTransform)popup.Find("Pose Popup Card");

                Assert.That(popup.gameObject.activeSelf, Is.False);
                Assert.That(card.Find("Label"), Is.Null);
                Assert.That(card.Find("Close Pose Popup"), Is.Null);
                var options = card.GetComponentsInChildren<Button>(true);
                Assert.That(options, Has.Length.EqualTo(3));
                Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(card.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(card.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(options[0].GetComponent<Outline>().enabled, Is.True);
                Assert.That(options[1].GetComponent<Outline>().enabled, Is.False);
                var initialRotation = character.transform.localRotation;

                poseButton.onClick.Invoke();
                Assert.That(palette.IsPosePopupVisible, Is.True);
                Assert.That(popup.gameObject.activeSelf, Is.True);

                options[2].onClick.Invoke();
                Assert.That(palette.SelectedPoseIndex, Is.EqualTo(2));
                Assert.That(options[0].GetComponent<Outline>().enabled, Is.False);
                Assert.That(options[2].GetComponent<Outline>().enabled, Is.True);
                Assert.That(Quaternion.Angle(initialRotation, character.transform.localRotation), Is.GreaterThan(0f));

                poseButton.onClick.Invoke();
                Assert.That(palette.IsPosePopupVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void PoseAssets_ReplaceVisibleCharacterModel()
        {
            var paletteObject = new GameObject("Palette");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var character = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var laydown = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Chameleon/Poses/ChameleonLaydown.fbx");
            var sitdown = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Chameleon/Poses/ChameleonSitdown.fbx");
            try
            {
                Assert.That(laydown, Is.Not.Null);
                Assert.That(sitdown, Is.Not.Null);
                var palette = paletteObject.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                palette.SetPosePrefabs(laydown, sitdown);
                character.SetActive(false);
                palette.SetCharacter(character);

                var baseRenderer = character.GetComponent<Renderer>();
                var baseMaterial = baseRenderer.sharedMaterial;
                palette.SelectPose(1);
                var laydownInstance = character.transform.Find("ChameleonLaydown Pose");
                Assert.That(laydownInstance, Is.Not.Null);
                Assert.That(baseRenderer.gameObject.activeSelf, Is.False);
                Assert.That(laydownInstance.gameObject.activeSelf, Is.True);
                Assert.That(laydownInstance.GetComponentInChildren<Renderer>(true), Is.Not.Null);

                palette.SelectPose(2);
                var sitdownInstance = character.transform.Find("ChameleonSitdown Pose");
                Assert.That(laydownInstance.gameObject.activeSelf, Is.False);
                Assert.That(sitdownInstance, Is.Not.Null);
                Assert.That(sitdownInstance.gameObject.activeSelf, Is.True);

                palette.SelectPose(0);
                Assert.That(baseRenderer.gameObject.activeSelf, Is.True);
                Assert.That(sitdownInstance.gameObject.activeSelf, Is.False);
                Assert.That(baseRenderer.sharedMaterial, Is.SameAs(baseMaterial));
            }
            finally
            {
                Object.DestroyImmediate(paletteObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void StampStroke_DoesNotBridgeUvSeams()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[texture.width * texture.height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }
            texture.SetPixels(pixels);

            CharacterColorPalette.StampStroke(
                texture,
                new Vector2(0.98f, 0.5f),
                new Vector2(0.02f, 0.5f),
                Color.blue,
                8,
                PaintBrushTexture.Soft);

            Assert.That(texture.GetPixel(32, 32), Is.EqualTo(Color.white));
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void AppTabs_StartOnCameraAndSwitchToMap()
        {
            var root = new GameObject("Tabs");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var tabs = root.AddComponent<AppTabController>();
                tabs.Initialize(canvasObject.transform);

                Assert.That(tabs.ActiveTab, Is.EqualTo(AppTab.Camera));
                Assert.That(canvasObject.transform.Find("App Tab Bar"), Is.Null);

                tabs.SelectTab(AppTab.Map);
                Assert.That(tabs.ActiveTab, Is.EqualTo(AppTab.Map));
                Assert.That(canvasObject.transform.Find("Map Fallback").gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void SceneUi_IsUnpackedAndStacksPaintPanels()
        {
            var openedScene = default(Scene);
            try
            {
            var prefab = GetSceneUi(out openedScene);
            Assert.That(PrefabUtility.GetPrefabInstanceStatus(prefab), Is.EqualTo(PrefabInstanceStatus.NotAPrefab));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SearchMyPetAppUI.prefab"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Editor/PaintUiPrefabStyler.cs"), Is.Null);
            Assert.That(prefab.transform.Find("App Tab Bar"), Is.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar/팔레트"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Palette Options/Hue"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Palette Options/Eyedropper Shortcut"), Is.Null);
            foreach (var sliderName in new[] { "Hue", "Brightness" })
            {
                var handle = prefab.transform.Find($"Character Paint UI/Safe Area/Paint Tool Options/Palette Options/{sliderName}/Handle")
                    .GetComponent<Image>();
                Assert.That(handle.rectTransform.sizeDelta, Is.EqualTo(new Vector2(7.5f, 15f)));
                Assert.That(handle.sprite, Is.Null);
            }
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Color Palette Button/Selected Color"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Color Palette Button/Palette Icon").GetComponent<Image>().sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ColorPalette.png")));
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Color Palette Button").GetComponent<CanvasRenderer>().cullTransparentMesh, Is.False);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Capture Button/White Fill"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls/Pose Button"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI").gameObject.activeSelf, Is.True);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Top Bar/완료/Label").GetComponent<Text>().text, Is.EqualTo("<"));
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls").gameObject.activeSelf, Is.True);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar").gameObject.activeSelf, Is.False);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Top Bar").gameObject.activeSelf, Is.True);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options").gameObject.activeSelf, Is.False);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Brush Options").gameObject.activeInHierarchy, Is.False);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Palette Options").gameObject.activeSelf, Is.True);
            var quickControls = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls");
            var toolbar = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar");
            var toolOptions = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options");
            var historyControls = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint History Controls");
            Assert.That(quickControls.anchoredPosition, Is.EqualTo(new Vector2(0f, 75f)));
            Assert.That(toolbar.anchoredPosition, Is.EqualTo(new Vector2(0f, 205f)));
            Assert.That(toolbar.sizeDelta, Is.EqualTo(new Vector2(320f, 58f)));
            Assert.That(toolOptions.anchoredPosition, Is.EqualTo(new Vector2(0f, 267f)));
            Assert.That(toolOptions.sizeDelta, Is.EqualTo(new Vector2(320f, 82f)));
            Assert.That(historyControls.anchoredPosition, Is.EqualTo(new Vector2(12f, -137f)));
            Assert.That(historyControls.anchoredPosition3D.z, Is.EqualTo(0f));
            var safeAreaRect = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area");
            var captureButton = (RectTransform)quickControls.Find("Capture Button");
            GetVerticalBounds(captureButton, safeAreaRect, out _, out var quickTop);
            GetVerticalBounds(toolbar, safeAreaRect, out var toolbarBottom, out var toolbarTop);
            GetVerticalBounds(toolOptions, safeAreaRect, out var optionsBottom, out _);
            Assert.That(toolbarBottom - quickTop, Is.GreaterThanOrEqualTo(8f));
            Assert.That(optionsBottom - toolbarTop, Is.EqualTo(4f).Within(0.01f));
            var topScrim = prefab.transform.Find("Top Camera Scrim").GetComponent<Image>();
            var bottomScrim = prefab.transform.Find("Bottom Camera Scrim").GetComponent<Image>();
            Assert.That(topScrim.rectTransform.sizeDelta.y, Is.EqualTo(128f));
            Assert.That(bottomScrim.rectTransform.sizeDelta.y, Is.EqualTo(200.5f));
            Assert.That(topScrim.raycastTarget || bottomScrim.raycastTarget, Is.False);
            foreach (var tool in prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar").GetComponentsInChildren<Button>(true))
            {
                Assert.That(tool.transform.Find("Icon"), Is.Not.Null);
                Assert.That(tool.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(tool.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(56f, 46f)));
                Assert.That(tool.GetComponentInChildren<Text>().fontSize, Is.EqualTo(9));
            }
            foreach (var size in prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Brush Options").GetComponentsInChildren<Button>(true))
            {
                Assert.That(size.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(70f, 58f)));
                Assert.That(size.GetComponentInChildren<Text>().fontSize, Is.EqualTo(16));
            }
            foreach (var texture in prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Texture Options").GetComponentsInChildren<Button>(true))
            {
                Assert.That(texture.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(96f, 58f)));
                Assert.That(texture.GetComponentInChildren<Text>().fontSize, Is.EqualTo(14));
            }
            }
            finally
            {
                CloseSceneIfOpened(openedScene);
            }
        }

        [Test]
        public void SceneUi_BindsWithoutCreatingDuplicateUi()
        {
            var openedScene = default(Scene);
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var controller = new GameObject("Controller");
            try
            {
                var prefab = GetSceneUi(out openedScene);
                var instance = Object.Instantiate(prefab, canvasObject.transform);
                instance.name = "SearchMyPetAppUI";
                var palette = controller.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                var safeAreaRect = (RectTransform)instance.transform.Find("Character Paint UI/Safe Area");
                Assert.That(safeAreaRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeAreaRect.anchorMax, Is.EqualTo(Vector2.one));
                var paintUi = instance.transform.Find("Character Paint UI");
                Assert.That(paintUi.gameObject.activeInHierarchy, Is.True);
                palette.SetCharacter(null);
                Assert.That(paintUi.gameObject.activeInHierarchy, Is.True);
                var instanceRect = instance.GetComponent<RectTransform>();
                Assert.That(instanceRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(instanceRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(instanceRect.sizeDelta, Is.EqualTo(Vector2.zero));
                var tabs = controller.AddComponent<AppTabController>();
                tabs.Initialize(canvasObject.transform);

                var safeArea = instance.transform.Find("Character Paint UI/Safe Area");
                var paletteButton = safeArea.Find("Paint Quick Controls/Color Palette Button").GetComponent<Button>();
                var lensSelector = GameObject.Find("Status Canvas").transform.Find("Camera Lens Selector").gameObject;
                var quickRect = (RectTransform)safeArea.Find("Paint Quick Controls");
                var toolbarRect = (RectTransform)safeArea.Find("Paint Toolbar");
                var optionsRect = (RectTransform)safeArea.Find("Paint Tool Options");
                var historyRect = (RectTransform)safeArea.Find("Paint History Controls");
                var initialQuickPosition = quickRect.anchoredPosition;
                var initialToolbarPosition = toolbarRect.anchoredPosition;
                var initialOptionsPosition = optionsRect.anchoredPosition;
                var initialHistoryPosition = historyRect.anchoredPosition;
                Assert.That(lensSelector.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.True);
                Assert.That(lensSelector.activeSelf, Is.False);
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.True);
                Assert.That(quickRect.anchoredPosition, Is.EqualTo(initialQuickPosition));
                Assert.That(toolbarRect.anchoredPosition, Is.EqualTo(initialToolbarPosition));
                Assert.That(optionsRect.anchoredPosition, Is.EqualTo(initialOptionsPosition));
                Assert.That(historyRect.anchoredPosition, Is.EqualTo(initialHistoryPosition));
                Assert.That(safeArea.Find("Paint Toolbar/크기").gameObject.activeSelf, Is.False);
                var brushOptions = safeArea.Find("Paint Tool Options/Brush Options");
                Assert.That(brushOptions, Is.Not.Null);
                Assert.That(brushOptions.Find("Brush Size Slider").GetComponent<Slider>().gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Top Bar").gameObject.activeSelf, Is.True);
                safeArea.Find("Paint Toolbar/팔레트").GetComponent<Button>().onClick.Invoke();
                Assert.That(safeArea.Find("Paint Tool Options").gameObject.activeSelf, Is.True);
                safeArea.Find("Paint Toolbar/스포이드").GetComponent<Button>().onClick.Invoke();
                Assert.That(safeArea.Find("Paint Tool Options").gameObject.activeSelf, Is.False);

                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.False);
                Assert.That(lensSelector.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.False);
                Assert.That(safeArea.Find("Paint Tool Options").gameObject.activeSelf, Is.False);

                paletteButton.onClick.Invoke();
                safeArea.Find("Paint Top Bar/완료").GetComponent<Button>().onClick.Invoke();
                Assert.That(tabs.ActiveTab, Is.EqualTo(AppTab.Map));
                Assert.That(instance.transform.Find("Map Fallback").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("Character Paint UI").gameObject.activeSelf, Is.False);

                var character = new GameObject("Character");
                paletteButton.onClick.RemoveAllListeners();
                palette.SetCharacter(character);
                Assert.That(lensSelector.activeSelf, Is.True);
                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.True);
                Assert.That(lensSelector.activeSelf, Is.False);
                Object.DestroyImmediate(character);

                Assert.That(canvasObject.transform.Find("SearchMyPetAppUI"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("Character Paint UI"), Is.Null);
                Assert.That(canvasObject.transform.Find("App Tab Bar"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(canvasObject);
                CloseSceneIfOpened(openedScene);
            }
        }

        private static Renderer CreatePart(string name, Transform parent, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static GameObject GetSceneUi(out Scene openedScene)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            openedScene = default;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                openedScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                scene = openedScene;
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "SearchMyPetAppUI") return root;
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (transform.name == "SearchMyPetAppUI") return transform.gameObject;
                    }
                }

                Assert.Fail("SearchMyPetAppUI not found");
                return null;
            }
            catch
            {
                CloseSceneIfOpened(openedScene);
                openedScene = default;
                throw;
            }
        }

        private static void CloseSceneIfOpened(Scene scene)
        {
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        private static void GetVerticalBounds(RectTransform rect, RectTransform relativeTo, out float bottom, out float top)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            bottom = relativeTo.InverseTransformPoint(corners[0]).y;
            top = relativeTo.InverseTransformPoint(corners[1]).y;
        }
    }
}
