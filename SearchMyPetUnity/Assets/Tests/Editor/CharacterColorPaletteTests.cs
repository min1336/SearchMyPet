using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SearchMyPet.AR.Tests
{
    public sealed class CharacterColorPaletteTests
    {
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
        public void PaintUi_UsesSafeAreaAndUpdatesSelectedTool()
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
                var size = toolbar.Find("크기").GetComponent<Image>();

                Assert.That(safeArea, Is.Not.Null);
                Assert.That(toolbar.childCount, Is.EqualTo(5));
                Assert.That(brush.color.a, Is.EqualTo(1f));

                toolbar.Find("크기").GetComponent<Button>().onClick.Invoke();
                Assert.That(size.color.a, Is.EqualTo(1f));
                Assert.That(brush.color.a, Is.EqualTo(0f));
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
        public void EditableUiPrefab_ContainsTabsAndPaintControls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SearchMyPetAppUI.prefab");

            Assert.That(prefab, Is.Not.Null);
            var prefabRect = prefab.GetComponent<RectTransform>();
            Assert.That(prefabRect.sizeDelta, Is.EqualTo(new Vector2(390f, 844f)));
            Assert.That(prefabRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(prefabRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(prefab.transform.Find("App Tab Bar"), Is.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar/팔레트"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Palette Options/Hue"), Is.Not.Null);
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
            Assert.That(prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options/Palette Options").gameObject.activeSelf, Is.True);
            var quickControls = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Quick Controls");
            var toolbar = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar");
            var toolOptions = (RectTransform)prefab.transform.Find("Character Paint UI/Safe Area/Paint Tool Options");
            Assert.That(quickControls.anchoredPosition, Is.EqualTo(new Vector2(0f, 20f)));
            Assert.That(toolbar.anchoredPosition, Is.EqualTo(new Vector2(0f, 104f)));
            Assert.That(toolOptions.anchoredPosition, Is.EqualTo(new Vector2(0f, 186f)));
            var topScrim = prefab.transform.Find("Top Camera Scrim").GetComponent<Image>();
            var bottomScrim = prefab.transform.Find("Bottom Camera Scrim").GetComponent<Image>();
            Assert.That(topScrim.rectTransform.sizeDelta.y, Is.EqualTo(128f));
            Assert.That(bottomScrim.rectTransform.sizeDelta.y, Is.EqualTo(248f));
            Assert.That(topScrim.raycastTarget || bottomScrim.raycastTarget, Is.False);
            foreach (var tool in prefab.transform.Find("Character Paint UI/Safe Area/Paint Toolbar").GetComponentsInChildren<Button>(true))
            {
                Assert.That(tool.transform.Find("Icon"), Is.Not.Null);
                Assert.That(tool.GetComponent<Outline>(), Is.Not.Null);
                Assert.That(tool.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(65f, 56f)));
            }
        }

        [Test]
        public void EditableUiPrefab_BindsWithoutCreatingDuplicateUi()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SearchMyPetAppUI.prefab");
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var controller = new GameObject("Controller");
            try
            {
                var instance = Object.Instantiate(prefab, canvasObject.transform);
                instance.name = "SearchMyPetAppUI";
                var palette = controller.AddComponent<CharacterColorPalette>();
                palette.Initialize(canvasObject.transform);
                var instanceRect = instance.GetComponent<RectTransform>();
                Assert.That(instanceRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(instanceRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(instanceRect.sizeDelta, Is.EqualTo(Vector2.zero));
                var tabs = controller.AddComponent<AppTabController>();
                tabs.Initialize(canvasObject.transform);

                var safeArea = instance.transform.Find("Character Paint UI/Safe Area");
                var paletteButton = safeArea.Find("Paint Quick Controls/Color Palette Button").GetComponent<Button>();
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.True);
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Top Bar").gameObject.activeSelf, Is.True);

                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.False);
                Assert.That(safeArea.Find("Paint Quick Controls").gameObject.activeSelf, Is.True);
                Assert.That(safeArea.Find("Paint Toolbar").gameObject.activeSelf, Is.False);

                paletteButton.onClick.Invoke();
                safeArea.Find("Paint Top Bar/완료").GetComponent<Button>().onClick.Invoke();
                Assert.That(tabs.ActiveTab, Is.EqualTo(AppTab.Map));
                Assert.That(instance.transform.Find("Map Fallback").gameObject.activeSelf, Is.True);
                Assert.That(instance.transform.Find("Character Paint UI").gameObject.activeSelf, Is.False);

                var character = new GameObject("Character");
                paletteButton.onClick.RemoveAllListeners();
                palette.SetCharacter(character);
                paletteButton.onClick.Invoke();
                Assert.That(palette.ToolsOpen, Is.True);
                Object.DestroyImmediate(character);

                Assert.That(canvasObject.transform.Find("SearchMyPetAppUI"), Is.Not.Null);
                Assert.That(canvasObject.transform.Find("Character Paint UI"), Is.Null);
                Assert.That(canvasObject.transform.Find("App Tab Bar"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(controller);
                Object.DestroyImmediate(canvasObject);
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
    }
}
