using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SearchMyPet.AR
{
    public enum PaintBrushTexture
    {
        Soft,
        Rough,
        Dots
    }

    public sealed class CharacterColorPalette : MonoBehaviour
    {
        private sealed class PaintSurface
        {
            public Renderer Renderer;
            public Collider Collider;
            public Material Material;
            public Texture2D Texture;
        }

        private enum Tool
        {
            Brush,
            Size,
            Texture,
            Palette,
            Eyedropper
        }

        private const int TextureSize = 256;
        private static readonly Color Neon = new(0.70f, 1f, 0.05f);
        private static readonly Color Panel = new(0.035f, 0.04f, 0.045f, 0.96f);
        private static readonly Color Card = new(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color Muted = new(0.58f, 0.61f, 0.64f, 1f);
        private readonly Dictionary<Collider, PaintSurface> surfaces = new();
        private readonly List<Object> runtimeAssets = new();
        private readonly List<Object> uiAssets = new();
        private readonly List<RaycastResult> uiHits = new();
        private readonly Image[] toolBackgrounds = new Image[5];
        private readonly Outline[] toolOutlines = new Outline[5];
        private readonly Text[] toolIcons = new Text[5];
        private readonly Text[] toolLabels = new Text[5];
        private GameObject paintUi;
        private GameObject contextPanel;
        private GameObject quickControls;
        private GameObject toolbar;
        private GameObject topBar;
        private GameObject cameraLensSelector;
        private GameObject placementInstructionPanel;
        private AppTabController appTabs;
        private bool cameraLensWasVisible;
        private Font font;
        private Image colorSwatch;
        private Image quickColorSwatch;
        private Color selectedColor = Neon;
        private PaintBrushTexture brushTexture;
        private Tool activeTool;
        private int brushSize = 32;
        private Sprite roundedSprite;
        private Sprite circleSprite;
        private GameObject[] editableToolPanels;
        private Button[] editableSizeButtons;
        private Button[] editableTextureButtons;
        private bool usesEditableUi;

        public bool IsPainting { get; private set; }
        public bool ToolsOpen { get; private set; }

        public void Initialize(Transform canvas)
        {
            if (paintUi != null || canvas == null)
            {
                return;
            }

            font = canvas.GetComponentInChildren<Text>(true)?.font
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cameraLensSelector = GameObject.Find("Camera Lens Selector");
            placementInstructionPanel = GameObject.Find("Placement Instruction Panel");
            placementInstructionPanel?.SetActive(true);

            var editableRoot = canvas.Find("SearchMyPetAppUI/Character Paint UI")
                ?? canvas.Find("Character Paint UI");
            if (editableRoot != null)
            {
                var appRoot = editableRoot.parent as RectTransform;
                if (appRoot != null && appRoot.name == "SearchMyPetAppUI")
                {
                    Stretch(appRoot);
                }
                paintUi = editableRoot.gameObject;
                BindEditableUi();
            }
            else
            {
                paintUi = new GameObject("Character Paint UI", typeof(RectTransform));
                paintUi.transform.SetParent(canvas, false);
                Stretch((RectTransform)paintUi.transform);
                roundedSprite = CreateShapeSprite(false);
                circleSprite = CreateShapeSprite(true);

                var safeArea = new GameObject("Safe Area", typeof(RectTransform));
                safeArea.transform.SetParent(paintUi.transform, false);
                ApplySafeArea((RectTransform)safeArea.transform);
                CreateTopBar(safeArea.transform);
                CreateToolbar(safeArea.transform);
                contextPanel = CreatePanel(safeArea.transform, "Paint Tool Options", new Vector2(0f, 158f), new Vector2(720f, 112f));
                CreateQuickBar(safeArea.transform);
            }
            ShowTool(Tool.Brush);
            CloseToolMenu();
            paintUi.SetActive(true);
        }

        private void BindEditableUi()
        {
            usesEditableUi = true;
            var safeArea = paintUi.transform.Find("Safe Area");
            ApplySafeArea((RectTransform)safeArea);
            contextPanel = safeArea.Find("Paint Tool Options").gameObject;
            quickControls = safeArea.Find("Paint Quick Controls")?.gameObject;
            toolbar = safeArea.Find("Paint Toolbar")?.gameObject;
            topBar = safeArea.Find("Paint Top Bar")?.gameObject;

            BindQuickControls(safeArea);

            var done = safeArea.Find("Paint Top Bar/완료")?.GetComponent<Button>();
            done?.onClick.AddListener(NavigateBack);

            var toolbarTransform = safeArea.Find("Paint Toolbar");
            var toolNames = new[] { "붓", "크기", "질감", "팔레트", "스포이드" };
            for (var index = 0; index < toolNames.Length; index++)
            {
                var tool = (Tool)index;
                var button = toolbarTransform.Find(toolNames[index])?.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }
                button.onClick.AddListener(() => ShowTool(tool));
                toolBackgrounds[index] = button.GetComponent<Image>();
                toolOutlines[index] = button.GetComponent<Outline>();
                toolIcons[index] = button.transform.Find("Icon")?.GetComponent<Text>();
                toolLabels[index] = button.GetComponentInChildren<Text>();
            }

            editableToolPanels = new[]
            {
                contextPanel.transform.Find("Brush Options")?.gameObject,
                contextPanel.transform.Find("Size Options")?.gameObject,
                contextPanel.transform.Find("Texture Options")?.gameObject,
                contextPanel.transform.Find("Palette Options")?.gameObject,
                contextPanel.transform.Find("Eyedropper Options")?.gameObject
            };

            editableSizeButtons = BindOptionButtons("Size Options", new[] { "8", "16", "32", "48" }, (index) =>
            {
                brushSize = new[] { 8, 16, 32, 48 }[index];
                ShowTool(Tool.Size);
            });
            editableTextureButtons = BindOptionButtons("Texture Options", new[] { "부드러움", "거침", "점무늬" }, (index) =>
            {
                brushTexture = (PaintBrushTexture)index;
                ShowTool(Tool.Texture);
            });

            var palettePanel = contextPanel.transform.Find("Palette Options");
            var hue = palettePanel?.Find("Hue")?.GetComponent<Slider>();
            var value = palettePanel?.Find("Brightness")?.GetComponent<Slider>();
            colorSwatch = palettePanel?.Find("Selected Color")?.GetComponent<Image>();
            if (hue != null && value != null)
            {
                hue.value = 0.23f;
                value.value = 1f;
                var hueBackground = hue.transform.Find("Background")?.GetComponent<Image>();
                var valueBackground = value.transform.Find("Background")?.GetComponent<Image>();
                if (hueBackground != null) hueBackground.sprite = CreateHueSprite();
                if (valueBackground != null) valueBackground.sprite = CreateValueSprite();
                void UpdateColor(float _)
                {
                    selectedColor = Color.HSVToRGB(hue.value, 0.85f, Mathf.Max(0.08f, value.value));
                    UpdateSwatch();
                }
                hue.onValueChanged.AddListener(UpdateColor);
                value.onValueChanged.AddListener(UpdateColor);
            }
        }

        private Button[] BindOptionButtons(string panelName, string[] names, System.Action<int> action)
        {
            var buttons = new Button[names.Length];
            var panel = contextPanel.transform.Find(panelName);
            for (var index = 0; index < names.Length; index++)
            {
                var capturedIndex = index;
                buttons[index] = panel?.Find(names[index])?.GetComponent<Button>();
                buttons[index]?.onClick.AddListener(() => action(capturedIndex));
            }
            return buttons;
        }

        public void SetCharacter(GameObject character)
        {
            ClearPaintTarget();
            if (usesEditableUi)
            {
                if (quickControls == null || toolbar == null || topBar == null || contextPanel == null)
                {
                    BindEditableUi();
                }
                else
                {
                    BindQuickControls(paintUi.transform.Find("Safe Area"));
                }
            }
            IsPainting = character != null;
            paintUi?.SetActive(true);
            SetCameraUiVisible(!IsPainting);
            if (character == null)
            {
                ToolsOpen = false;
                return;
            }

            foreach (var renderer in character.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial == null || IsProtectedPart(renderer.name))
                {
                    continue;
                }

                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                var material = new Material(renderer.sharedMaterial) { name = $"{renderer.name} Paint" };
                var texture = CreatePaintTexture();
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetColor("_BaseColor", Color.white);
                }
                else
                {
                    material.mainTexture = texture;
                    material.color = Color.white;
                }

                renderer.sharedMaterial = material;
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                ((MeshCollider)collider).sharedMesh = meshFilter.sharedMesh;
                surfaces.Add(collider, new PaintSurface
                {
                    Renderer = renderer,
                    Collider = collider,
                    Material = material,
                    Texture = texture
                });
                runtimeAssets.Add(material);
                runtimeAssets.Add(texture);
            }

            ShowTool(Tool.Brush);
            CloseToolMenu();
        }

        private void BindQuickControls(Transform safeArea)
        {
            quickControls = safeArea?.Find("Paint Quick Controls")?.gameObject;
            var button = quickControls?.transform.Find("Color Palette Button")?.GetComponent<Button>();
            button?.onClick.RemoveListener(OpenToolMenu);
            button?.onClick.RemoveListener(ToggleToolMenu);
            button?.onClick.AddListener(ToggleToolMenu);
            quickColorSwatch = quickControls?.transform.Find("Color Palette Button/Selected Color")?.GetComponent<Image>();
        }

        public void ToggleToolMenu()
        {
            if (ToolsOpen)
            {
                CloseToolMenu();
                return;
            }

            OpenToolMenu();
        }

        public void OpenToolMenu()
        {
            ToolsOpen = true;
            quickControls?.SetActive(true);
            toolbar?.SetActive(true);
            topBar?.SetActive(true);
            ShowTool(Tool.Brush);
        }

        public void CloseToolMenu()
        {
            ToolsOpen = false;
            quickControls?.SetActive(true);
            toolbar?.SetActive(false);
            topBar?.SetActive(true);
            contextPanel?.SetActive(false);
        }

        public void NavigateBack()
        {
            IsPainting = false;
            ToolsOpen = false;
            paintUi?.SetActive(false);
            SetCameraUiVisible(false);
            appTabs ??= FindAnyObjectByType<AppTabController>();
            appTabs?.SelectTab(AppTab.Map);
        }

        public void CompletePainting()
        {
            IsPainting = false;
            ToolsOpen = false;
            paintUi?.SetActive(false);
            SetCameraUiVisible(true);
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (!IsPainting || !ToolsOpen || pointer == null || !pointer.press.isPressed || Camera.main == null)
            {
                return;
            }

            var screenPosition = pointer.position.ReadValue();
            if (IsOverUi(screenPosition)
                || !Physics.Raycast(Camera.main.ScreenPointToRay(screenPosition), out var hit)
                || !surfaces.TryGetValue(hit.collider, out var surface))
            {
                return;
            }

            if (activeTool == Tool.Eyedropper)
            {
                selectedColor = surface.Texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                UpdateSwatch();
                ShowTool(Tool.Brush);
                return;
            }

            Stamp(surface.Texture, hit.textureCoord, selectedColor, brushSize, brushTexture);
        }

        public static void Stamp(
            Texture2D texture,
            Vector2 uv,
            Color color,
            int size,
            PaintBrushTexture brushTexture)
        {
            if (texture == null || size <= 0)
            {
                return;
            }

            var centerX = Mathf.RoundToInt(uv.x * (texture.width - 1));
            var centerY = Mathf.RoundToInt(uv.y * (texture.height - 1));
            var radius = Mathf.Max(1, size / 2);
            for (var y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(texture.height - 1, centerY + radius); y++)
            {
                for (var x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(texture.width - 1, centerX + radius); x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance > radius)
                    {
                        continue;
                    }

                    var strength = BrushStrength(x, y, distance, radius, brushTexture);
                    if (strength > 0f)
                    {
                        texture.SetPixel(x, y, Color.Lerp(texture.GetPixel(x, y), color, strength));
                    }
                }
            }

            texture.Apply(false);
        }

        private static float BrushStrength(int x, int y, float distance, int radius, PaintBrushTexture brushTexture)
        {
            return brushTexture switch
            {
                PaintBrushTexture.Rough => ((x * 73856093) ^ (y * 19349663)) % 5 == 0 ? 0.25f : 0.9f,
                PaintBrushTexture.Dots => (x / Mathf.Max(2, radius / 3) + y / Mathf.Max(2, radius / 3)) % 2 == 0 ? 1f : 0f,
                _ => Mathf.Clamp01(1f - distance / radius)
            };
        }

        private void ShowTool(Tool tool)
        {
            activeTool = tool;
            UpdateToolSelection();
            if (contextPanel == null)
            {
                return;
            }

            contextPanel.SetActive(ToolsOpen && tool != Tool.Brush);

            if (usesEditableUi)
            {
                for (var index = 0; index < editableToolPanels.Length; index++)
                {
                    editableToolPanels[index]?.SetActive(index == (int)tool);
                }
                UpdateEditableOptionSelection();
                return;
            }

            ClearChildren(contextPanel.transform);
            switch (tool)
            {
                case Tool.Brush:
                    CreateCenteredText(contextPanel.transform, "캐릭터를 문질러 색칠하세요", 24, Neon);
                    break;
                case Tool.Size:
                    CreateSizeOptions(contextPanel.transform);
                    break;
                case Tool.Texture:
                    CreateTextureOptions(contextPanel.transform);
                    break;
                case Tool.Palette:
                    CreatePaletteOptions(contextPanel.transform);
                    break;
                case Tool.Eyedropper:
                    CreateCenteredText(contextPanel.transform, "캐릭터에서 원하는 색을 터치하세요", 24, Color.white);
                    break;
            }
        }

        private void UpdateEditableOptionSelection()
        {
            if (editableSizeButtons != null)
            {
                var sizes = new[] { 8, 16, 32, 48 };
                for (var index = 0; index < editableSizeButtons.Length; index++)
                {
                    SetOptionSelected(editableSizeButtons[index], sizes[index] == brushSize);
                }
            }
            if (editableTextureButtons != null)
            {
                for (var index = 0; index < editableTextureButtons.Length; index++)
                {
                    SetOptionSelected(editableTextureButtons[index], index == (int)brushTexture);
                }
            }
        }

        private static void SetOptionSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }
            button.GetComponent<Image>().color = selected ? Neon : Card;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = selected ? Color.black : Color.white;
        }

        private void CreateTopBar(Transform parent)
        {
            var bar = CreatePanel(parent, "Paint Top Bar", new Vector2(0f, -40f), new Vector2(720f, 64f));
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);

            var title = CreateText(bar.transform, "캐릭터 색칠", 22, Color.white);
            title.alignment = TextAnchor.MiddleLeft;
            SetRect(title.rectTransform, new Vector2(22f, -12f), new Vector2(240f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            topBar = bar;
            var done = CreateButton(bar.transform, "완료", Card, Color.white, NavigateBack);
            done.GetComponentInChildren<Text>().text = "<";
            done.GetComponent<Image>().sprite = circleSprite;
            SetRect((RectTransform)done.transform, new Vector2(16f, -10f), new Vector2(40f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private void CreateToolbar(Transform parent)
        {
            var bar = CreatePanel(parent, "Paint Toolbar", new Vector2(0f, 48f), new Vector2(720f, 84f));
            toolbar = bar;
            var labels = new[] { "붓", "크기", "질감", "팔레트", "스포이드" };
            for (var index = 0; index < labels.Length; index++)
            {
                var tool = (Tool)index;
                var button = CreateButton(bar.transform, labels[index], Card, Color.white, () => ShowTool(tool));
                toolBackgrounds[index] = button.GetComponent<Image>();
                toolLabels[index] = button.GetComponentInChildren<Text>();
                SetRect((RectTransform)button.transform, new Vector2(12f + index * 141f, 12f), new Vector2(132f, 60f), Vector2.zero, Vector2.zero, Vector2.zero);
            }
            UpdateToolSelection();
        }

        private void CreateQuickBar(Transform parent)
        {
            quickControls = CreatePanel(parent, "Paint Quick Controls", new Vector2(0f, 48f), new Vector2(720f, 84f));
            var palette = CreateButton(quickControls.transform, "Color Palette Button", new Color(1f, 1f, 1f, 0.001f), Color.white, ToggleToolMenu);
            palette.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            SetRect((RectTransform)palette.transform, new Vector2(24f, 12f), new Vector2(64f, 60f), Vector2.zero, Vector2.zero, Vector2.zero);
            var capture = CreateButton(quickControls.transform, "Capture Button", Card, Color.white, () => { });
            capture.GetComponentInChildren<Text>().text = string.Empty;
            capture.GetComponent<Image>().sprite = circleSprite;
            SetRect((RectTransform)capture.transform, new Vector2(328f, 8f), new Vector2(68f, 68f), Vector2.zero, Vector2.zero, Vector2.zero);
            var pose = CreateButton(quickControls.transform, "Pose Button", Card, Color.white, () => { });
            pose.GetComponentInChildren<Text>().text = "♙\n1 / 4";
            SetRect((RectTransform)pose.transform, new Vector2(628f, 12f), new Vector2(64f, 60f), Vector2.zero, Vector2.zero, Vector2.zero);
        }

        private void UpdateToolSelection()
        {
            for (var index = 0; index < toolBackgrounds.Length; index++)
            {
                if (toolBackgrounds[index] == null)
                {
                    continue;
                }
                var selected = index == (int)activeTool;
                toolBackgrounds[index].color = selected ? Card : Color.clear;
                if (toolOutlines[index] != null)
                {
                    toolOutlines[index].enabled = selected;
                    toolOutlines[index].effectColor = Neon;
                }
                if (toolIcons[index] != null)
                {
                    toolIcons[index].color = selected ? Neon : Color.white;
                }
                if (toolLabels[index] != null)
                {
                    toolLabels[index].color = selected ? Neon : Color.white;
                }
            }
        }

        private void CreateSizeOptions(Transform parent)
        {
            var sizes = new[] { 8, 16, 32, 48 };
            for (var index = 0; index < sizes.Length; index++)
            {
                var size = sizes[index];
                var selected = size == brushSize;
                var button = CreateButton(parent, size.ToString(), selected ? Neon : Card, selected ? Color.black : Color.white, () =>
                {
                    brushSize = size;
                    ShowTool(Tool.Size);
                });
                SetRect((RectTransform)button.transform, new Vector2(24f + index * 174f, 16f), new Vector2(150f, 80f), Vector2.zero, Vector2.zero, Vector2.zero);
                var dot = CreateImage(button.transform, "Brush Preview", selected ? Color.black : Color.white);
                dot.sprite = circleSprite;
                var diameter = Mathf.Lerp(8f, 34f, size / 48f);
                SetRect(dot.rectTransform, new Vector2(0f, 12f), new Vector2(diameter, diameter), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                var label = button.GetComponentInChildren<Text>();
                SetRect(label.rectTransform, new Vector2(0f, -23f), new Vector2(150f, 28f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
        }

        private void CreateTextureOptions(Transform parent)
        {
            var labels = new[] { "부드러움", "거침", "점무늬" };
            for (var index = 0; index < labels.Length; index++)
            {
                var texture = (PaintBrushTexture)index;
                var selected = texture == brushTexture;
                var button = CreateButton(parent, labels[index], selected ? Neon : Card, selected ? Color.black : Color.white, () =>
                {
                    brushTexture = texture;
                    ShowTool(Tool.Texture);
                });
                SetRect((RectTransform)button.transform, new Vector2(24f + index * 230f, 16f), new Vector2(210f, 80f), Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        private void CreatePaletteOptions(Transform parent)
        {
            var hue = CreateSlider(parent, "Hue", new Vector2(26f, 58f), CreateHueSprite(), Color.white);
            var value = CreateSlider(parent, "Brightness", new Vector2(26f, 18f), CreateValueSprite(), Color.white);
            hue.value = 0.23f;
            value.value = 1f;
            colorSwatch = CreateImage(parent, "Selected Color", selectedColor);
            colorSwatch.sprite = circleSprite;
            SetRect(colorSwatch.rectTransform, new Vector2(-24f, 24f), new Vector2(64f, 64f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));

            void UpdateColor(float _)
            {
                selectedColor = Color.HSVToRGB(hue.value, 0.85f, Mathf.Max(0.08f, value.value));
                UpdateSwatch();
            }

            hue.onValueChanged.AddListener(UpdateColor);
            value.onValueChanged.AddListener(UpdateColor);
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 position, Sprite background, Color handleColor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect((RectTransform)root.transform, position, new Vector2(580f, 28f), Vector2.zero, Vector2.zero, Vector2.zero);

            var backgroundImage = CreateImage(root.transform, "Background", Color.white);
            backgroundImage.sprite = background;
            SetRect(backgroundImage.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            Stretch(backgroundImage.rectTransform);

            var handle = CreateImage(root.transform, "Handle", handleColor);
            SetRect(handle.rectTransform, Vector2.zero, new Vector2(28f, 36f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var slider = root.GetComponent<Slider>();
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private Sprite CreateHueSprite()
        {
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (var x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, 0, Color.HSVToRGB(x / 255f, 0.85f, 1f));
            }
            texture.Apply(false);
            return TrackSprite(texture);
        }

        private Sprite CreateValueSprite()
        {
            var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            for (var x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, 0, Color.Lerp(Color.black, Color.white, x / 255f));
            }
            texture.Apply(false);
            return TrackSprite(texture);
        }

        private Sprite TrackSprite(Texture2D texture)
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            uiAssets.Add(texture);
            uiAssets.Add(sprite);
            return sprite;
        }

        private Texture2D CreatePaintTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }


        private bool IsOverUi(Vector2 position)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        private void UpdateSwatch()
        {
            if (colorSwatch != null)
            {
                colorSwatch.color = selectedColor;
            }
            if (quickColorSwatch != null)
            {
                quickColorSwatch.color = selectedColor;
            }
        }

        private void SetCameraUiVisible(bool visible)
        {
            if (!visible && cameraLensSelector != null)
            {
                cameraLensWasVisible = cameraLensSelector.activeSelf;
                cameraLensSelector.SetActive(false);
            }
            else if (visible && cameraLensWasVisible)
            {
                cameraLensSelector.SetActive(true);
            }
            placementInstructionPanel?.SetActive(visible);
        }

        private void OnDestroy()
        {
            ClearPaintTarget();
            foreach (var asset in uiAssets)
            {
                DestroyObject(asset);
            }
        }

        private void ClearPaintTarget()
        {
            foreach (var surface in surfaces.Values)
            {
                DestroyObject(surface.Collider);
            }
            surfaces.Clear();
            foreach (var asset in runtimeAssets)
            {
                DestroyObject(asset);
            }
            runtimeAssets.Clear();
        }

        private static bool IsProtectedPart(string name)
        {
            return name.Contains("Eye", System.StringComparison.OrdinalIgnoreCase)
                || name.Contains("Pupil", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = Panel;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            SetRect((RectTransform)panel.transform, position, size, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            return panel;
        }

        private Button CreateButton(Transform parent, string label, Color background, Color foreground, UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = background;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.92f, 0.92f, 0.92f),
                pressedColor = new Color(0.78f, 0.78f, 0.78f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.onClick.AddListener(action);
            var text = CreateText(root.transform, label, 20, foreground);
            Stretch(text.rectTransform);
            return button;
        }

        private Text CreateText(Transform parent, string value, int size, Color color)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            return text;
        }

        private void CreateCenteredText(Transform parent, string value, int size, Color color)
        {
            var text = CreateText(parent, value, size, color);
            Stretch(text.rectTransform);
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplySafeArea(RectTransform rect)
        {
            var safe = Screen.safeArea;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Sprite CreateShapeSprite(bool circle)
        {
            const int size = 32;
            const float radius = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var visible = circle
                        ? Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) <= 15.5f
                        : Mathf.Abs(x - 15.5f) <= 15.5f - radius || Mathf.Abs(y - 15.5f) <= 15.5f - radius
                            || Vector2.Distance(new Vector2(Mathf.Abs(x - 15.5f), Mathf.Abs(y - 15.5f)), new Vector2(15.5f - radius, 15.5f - radius)) <= radius;
                    texture.SetPixel(x, y, visible ? Color.white : Color.clear);
                }
            }
            texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(8f, 8f, 8f, 8f));
            uiAssets.Add(texture);
            uiAssets.Add(sprite);
            return sprite;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                DestroyObject(parent.GetChild(index).gameObject);
            }
        }
    }
}
