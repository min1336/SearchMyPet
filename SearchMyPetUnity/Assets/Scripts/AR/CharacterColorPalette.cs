using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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

        private sealed class PaintSnapshot
        {
            public PaintSurface Surface;
            public Color32[] Before;
            public Color32[] After;
        }

        private sealed class PaintHistoryEntry
        {
            public readonly List<PaintSnapshot> Snapshots = new();
            public bool HasChanges;

            public PaintSnapshot GetOrAdd(PaintSurface surface)
            {
                foreach (var snapshot in Snapshots)
                {
                    if (ReferenceEquals(snapshot.Surface, surface))
                    {
                        return snapshot;
                    }
                }

                var created = new PaintSnapshot
                {
                    Surface = surface,
                    Before = surface.Texture.GetPixels32()
                };
                Snapshots.Add(created);
                return created;
            }

            public void CaptureAfter()
            {
                foreach (var snapshot in Snapshots)
                {
                    snapshot.After = snapshot.Surface.Texture.GetPixels32();
                }
            }

            public void Apply(bool undo)
            {
                foreach (var snapshot in Snapshots)
                {
                    var pixels = undo ? snapshot.Before : snapshot.After;
                    if (snapshot.Surface?.Texture == null || pixels == null)
                    {
                        continue;
                    }

                    snapshot.Surface.Texture.SetPixels32(pixels);
                    snapshot.Surface.Texture.Apply(false);
                }
            }
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
        private GameObject historyControls;
        private GameObject cameraLensSelector;
        private GameObject placementInstructionPanel;
        private Button undoButton;
        private Button redoButton;
        private ARCameraManager arCameraManager;
        private Matrix4x4? cameraDisplayMatrix;
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
        private Slider brushSizeSlider;
        private Button[] editableTextureButtons;
        private bool usesEditableUi;
        private readonly Stack<PaintHistoryEntry> undoHistory = new();
        private readonly Stack<PaintHistoryEntry> redoHistory = new();
        private PaintHistoryEntry activeHistoryEntry;

        public bool IsPainting { get; private set; }
        public bool ToolsOpen { get; private set; }
        public bool CanUndo => undoHistory.Count > 0;
        public bool CanRedo => redoHistory.Count > 0;

        public void Initialize(Transform canvas)
        {
            if (paintUi != null || canvas == null)
            {
                return;
            }

            font = canvas.GetComponentInChildren<Text>(true)?.font
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cameraLensSelector = GameObject.Find("Camera Lens Selector");
            placementInstructionPanel = canvas.Find("Placement Instruction Panel")?.gameObject
                ?? GameObject.Find("Placement Instruction Panel");
            placementInstructionPanel?.SetActive(true);
            arCameraManager = FindAnyObjectByType<ARCameraManager>();
            if (arCameraManager != null)
            {
                arCameraManager.frameReceived += OnCameraFrameReceived;
            }

            roundedSprite ??= CreateShapeSprite(false);
            circleSprite ??= CreateShapeSprite(true);

            var editableRoot = canvas.Find("SearchMyPetAppUI/Character Paint UI")
                ?? canvas.Find("Character Paint UI");
            if (editableRoot != null)
            {
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
                CreateHistoryControls(safeArea.transform);
            }
            ShowTool(Tool.Brush);
            CloseToolMenu();
            paintUi.SetActive(true);
        }

        private void BindEditableUi()
        {
            usesEditableUi = true;
            var safeArea = paintUi.transform.Find("Safe Area");
            contextPanel = safeArea.Find("Paint Tool Options").gameObject;
            quickControls = safeArea.Find("Paint Quick Controls")?.gameObject;
            toolbar = safeArea.Find("Paint Toolbar")?.gameObject;
            topBar = safeArea.Find("Paint Top Bar")?.gameObject;

            BindQuickControls(safeArea);
            BindHistoryControls(safeArea);

            var done = safeArea.Find("Paint Top Bar/완료")?.GetComponent<Button>();
            done?.onClick.AddListener(NavigateBack);

            var toolbarTransform = safeArea.Find("Paint Toolbar");
            var toolNames = new[] { "붓", string.Empty, "질감", "팔레트", "스포이드" };
            for (var index = 0; index < toolNames.Length; index++)
            {
                if (string.IsNullOrEmpty(toolNames[index]))
                {
                    continue;
                }

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

            var brushOptions = contextPanel.transform.Find("Brush Options")?.gameObject;
            editableToolPanels = new[]
            {
                brushOptions,
                null,
                contextPanel.transform.Find("Texture Options")?.gameObject,
                contextPanel.transform.Find("Palette Options")?.gameObject,
                contextPanel.transform.Find("Eyedropper Options")?.gameObject
            };

            BindBrushSizeSlider(brushOptions?.transform);
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

        private void BindBrushSizeSlider(Transform parent)
        {
            brushSizeSlider = parent?.Find("Brush Size Slider")?.GetComponent<Slider>();
            if (brushSizeSlider == null)
            {
                Debug.LogError("CharacterColorPalette requires a scene Brush Size Slider under Brush Options.");
                return;
            }

            ConfigureBrushSizeSlider(brushSizeSlider);
        }

        private void ConfigureBrushSizeSlider(Slider slider)
        {
            slider.minValue = 8f;
            slider.maxValue = 48f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(brushSize);
            slider.onValueChanged.RemoveListener(OnBrushSizeChanged);
            slider.onValueChanged.AddListener(OnBrushSizeChanged);
        }

        private void OnBrushSizeChanged(float value)
        {
            brushSize = Mathf.Clamp(Mathf.RoundToInt(value), 8, 48);
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
            if (character == null)
            {
                ToolsOpen = false;
            }
            paintUi?.SetActive(true);
            placementInstructionPanel?.SetActive(!IsPainting);
            cameraLensSelector?.SetActive(!ToolsOpen);
            if (character == null)
            {
                return;
            }

            foreach (var renderer in character.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial == null || IsProtectedPart(renderer.name))
                {
                    continue;
                }

                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null && renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    mesh = skinnedRenderer.sharedMesh;
                }

                if (mesh == null)
                {
                    continue;
                }

                var material = new Material(renderer.sharedMaterial) { name = $"{renderer.name} Paint" };
                var baseTexture = material.HasProperty("_BaseMap")
                    ? material.GetTexture("_BaseMap") as Texture2D
                    : material.mainTexture as Texture2D;
                var baseColor = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;
                var texture = CreatePaintTexture(baseTexture, baseColor);
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
                ((MeshCollider)collider).sharedMesh = mesh;
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

        private void BindHistoryControls(Transform safeArea)
        {
            var history = safeArea?.Find("Paint History Controls");
            if (history == null)
            {
                Debug.LogError("CharacterColorPalette requires scene Paint History Controls.");
                historyControls = null;
                return;
            }

            historyControls = history?.gameObject;
            undoButton = history?.Find("Undo Button")?.GetComponent<Button>();
            redoButton = history?.Find("Redo Button")?.GetComponent<Button>();
            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(Undo);
                undoButton.onClick.AddListener(Undo);
            }
            if (redoButton != null)
            {
                redoButton.onClick.RemoveListener(Redo);
                redoButton.onClick.AddListener(Redo);
            }
            SetHistoryControlsVisible(false);
            UpdateHistoryUi();
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
            cameraLensSelector?.SetActive(false);
            quickControls?.SetActive(true);
            toolbar?.SetActive(true);
            topBar?.SetActive(true);
            ShowTool(Tool.Brush);
        }

        public void CloseToolMenu()
        {
            ToolsOpen = false;
            cameraLensSelector?.SetActive(true);
            quickControls?.SetActive(true);
            toolbar?.SetActive(false);
            topBar?.SetActive(true);
            contextPanel?.SetActive(false);
        }

        public void Undo()
        {
            ResetPaintStroke();
            if (!undoHistory.TryPop(out var entry))
            {
                return;
            }

            entry.Apply(true);
            redoHistory.Push(entry);
            UpdateHistoryUi();
        }

        public void Redo()
        {
            ResetPaintStroke();
            if (!redoHistory.TryPop(out var entry))
            {
                return;
            }

            entry.Apply(false);
            undoHistory.Push(entry);
            UpdateHistoryUi();
        }

        public void NavigateBack()
        {
            IsPainting = false;
            ToolsOpen = false;
            SetHistoryControlsVisible(false);
            paintUi?.SetActive(false);
            SetCameraUiVisible(false);
            appTabs ??= FindAnyObjectByType<AppTabController>();
            appTabs?.SelectTab(AppTab.Map);
        }

        public void CompletePainting()
        {
            IsPainting = false;
            ToolsOpen = false;
            SetHistoryControlsVisible(false);
            paintUi?.SetActive(false);
            SetCameraUiVisible(true);
        }

        public void SetPlacementInstructionVisible(bool visible)
        {
            placementInstructionPanel?.SetActive(visible && !IsPainting);
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (!IsPainting || !ToolsOpen || pointer == null || !pointer.press.isPressed || Camera.main == null)
            {
                ResetPaintStroke();
                return;
            }

            var screenPosition = pointer.position.ReadValue();
            if (IsOverUi(screenPosition))
            {
                ResetPaintStroke();
                return;
            }

            var surface = default(PaintSurface);
            var hitPaintSurface = Physics.Raycast(Camera.main.ScreenPointToRay(screenPosition), out var hit)
                && surfaces.TryGetValue(hit.collider, out surface);
            if (activeTool == Tool.Eyedropper)
            {
                ResetPaintStroke();
                if (hitPaintSurface)
                {
                    selectedColor = surface.Texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                }
                else if (!TrySampleCamera(screenPosition, out selectedColor))
                {
                    return;
                }

                UpdateSwatch();
                ShowTool(Tool.Brush);
                return;
            }

            if (!hitPaintSurface)
            {
                ResetPaintStroke();
                return;
            }

            TrackHistorySurface(surface);
            PaintStamp(surface, hit.textureCoord);

            activeHistoryEntry.HasChanges = true;
            SetHistoryControlsVisible(true);
        }

        private void ResetPaintStroke()
        {
            FinishPaintStroke();
        }

        private void TrackHistorySurface(PaintSurface surface)
        {
            activeHistoryEntry ??= new PaintHistoryEntry();
            activeHistoryEntry.GetOrAdd(surface);
        }

        private void FinishPaintStroke()
        {
            if (activeHistoryEntry == null)
            {
                return;
            }

            activeHistoryEntry.CaptureAfter();
            if (activeHistoryEntry.HasChanges)
            {
                undoHistory.Push(activeHistoryEntry);
                redoHistory.Clear();
            }
            activeHistoryEntry = null;
            UpdateHistoryUi();
        }

        private void PaintStamp(PaintSurface surface, Vector2 uv)
        {
            StampPixels(surface.Texture, uv, selectedColor, brushSize, brushTexture);
            surface.Texture.Apply(false);
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (args.displayMatrix.HasValue)
            {
                cameraDisplayMatrix = args.displayMatrix.Value;
            }
        }

        private bool TrySampleCamera(Vector2 screenPosition, out Color color)
        {
            color = default;
            if (arCameraManager == null
                || !cameraDisplayMatrix.HasValue
                || !arCameraManager.TryAcquireLatestCpuImage(out var image))
            {
                return false;
            }

            using (image)
            {
                if (!image.FormatSupported(TextureFormat.RGBA32))
                {
                    return false;
                }

                var uv = ScreenToCameraUv(
                    screenPosition,
                    new Vector2(Screen.width, Screen.height),
                    cameraDisplayMatrix.Value);
                var x = Mathf.Clamp(Mathf.FloorToInt(uv.x * image.width), 0, image.width - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(uv.y * image.height), 0, image.height - 1);
                var conversion = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(x, y, 1, 1),
                    outputDimensions = Vector2Int.one,
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None
                };
                using var pixel = new NativeArray<byte>(4, Allocator.Temp);
                image.Convert(conversion, new NativeSlice<byte>(pixel));
                color = new Color32(pixel[0], pixel[1], pixel[2], pixel[3]);
                return true;
            }
        }

        private static Vector2 ScreenToCameraUv(
            Vector2 screenPosition,
            Vector2 screenSize,
            Matrix4x4 displayMatrix)
        {
            var screenUv = new Vector4(
                screenPosition.x / screenSize.x,
                screenPosition.y / screenSize.y,
                1f,
                1f);
            var cameraUv = displayMatrix.transpose * screenUv;
            return new Vector2(cameraUv.x, cameraUv.y);
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

            StampPixels(texture, uv, color, size, brushTexture);
            texture.Apply(false);
        }

        public static void StampStroke(
            Texture2D texture,
            Vector2 fromUv,
            Vector2 toUv,
            Color color,
            int size,
            PaintBrushTexture brushTexture)
        {
            if (texture == null || size <= 0)
            {
                return;
            }

            StampPixels(texture, toUv, color, size, brushTexture);
            texture.Apply(false);
        }

        private static void StampPixels(
            Texture2D texture,
            Vector2 uv,
            Color color,
            int size,
            PaintBrushTexture brushTexture)
        {
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

                    var strength = BrushStrength(x - centerX, y - centerY, radius, brushTexture);
                    if (strength > 0f)
                    {
                        texture.SetPixel(x, y, Color.Lerp(texture.GetPixel(x, y), color, strength));
                    }
                }
            }
        }

        private static float BrushStrength(int offsetX, int offsetY, int radius, PaintBrushTexture brushTexture)
        {
            var normalizedX = offsetX / (float)radius;
            var normalizedY = offsetY / (float)radius;
            var distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            var edge = Mathf.Clamp01(1f - distance);
            if (edge <= 0f)
            {
                return 0f;
            }

            return brushTexture switch
            {
                PaintBrushTexture.Rough => edge * Mathf.Lerp(
                    0.35f,
                    1f,
                    Mathf.Abs(Mathf.Sin((offsetX * 12.9898f + offsetY * 78.233f) * 0.17f))),
                PaintBrushTexture.Dots => edge * DotAlpha(normalizedX, normalizedY),
                _ => edge
            };
        }

        private static float DotAlpha(float x, float y)
        {
            const float grid = 3.5f;
            var cellX = Mathf.Repeat((x + 1f) * grid, 1f) - 0.5f;
            var cellY = Mathf.Repeat((y + 1f) * grid, 1f) - 0.5f;
            return Mathf.Clamp01(1f - Mathf.Sqrt(cellX * cellX + cellY * cellY) / 0.34f);
        }

        private void ShowTool(Tool tool)
        {
            activeTool = tool;
            UpdateToolSelection();
            if (contextPanel == null)
            {
                return;
            }

            contextPanel.SetActive(ToolsOpen && tool != Tool.Eyedropper);

            if (usesEditableUi)
            {
                var panelTool = tool == Tool.Size ? Tool.Brush : tool;
                for (var index = 0; index < editableToolPanels.Length; index++)
                {
                    editableToolPanels[index]?.SetActive(index == (int)panelTool);
                }
                UpdateEditableOptionSelection();
                return;
            }

            ClearChildren(contextPanel.transform);
            switch (tool)
            {
                case Tool.Brush:
                case Tool.Size:
                    CreateBrushSizeOptions(contextPanel.transform);
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
            brushSizeSlider?.SetValueWithoutNotify(brushSize);
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

        private void CreateHistoryControls(Transform parent)
        {
            if (parent == null || parent.Find("Paint History Controls") != null)
            {
                return;
            }

            var root = new GameObject("Paint History Controls", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            historyControls = root;
            var rootRect = (RectTransform)root.transform;
            SetRect(rootRect, new Vector2(12f, -137f), new Vector2(44f, 88f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rootRect.anchoredPosition3D = new Vector3(12f, -137f, 0f);

            var undo = CreateButton(root.transform, "Undo Button", Card, Color.white, Undo);
            undo.GetComponentInChildren<Text>().text = "↶";
            var undoImage = undo.GetComponent<Image>();
            undoImage.sprite = circleSprite;
            undoImage.type = Image.Type.Simple;
            SetRect((RectTransform)undo.transform, new Vector2(2f, -2f), new Vector2(40f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var redo = CreateButton(root.transform, "Redo Button", Card, Color.white, Redo);
            redo.GetComponentInChildren<Text>().text = "↷";
            var redoImage = redo.GetComponent<Image>();
            redoImage.sprite = circleSprite;
            redoImage.type = Image.Type.Simple;
            SetRect((RectTransform)redo.transform, new Vector2(2f, -46f), new Vector2(40f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            undoButton = undo;
            redoButton = redo;
            root.transform.SetAsLastSibling();
            SetHistoryControlsVisible(false);
            UpdateHistoryUi();
        }

        private void SetHistoryControlsVisible(bool visible)
        {
            historyControls?.SetActive(visible && IsPainting);
        }

        private void UpdateHistoryUi()
        {
            SetHistoryButtonState(undoButton, CanUndo);
            SetHistoryButtonState(redoButton, CanRedo);
        }

        private static void SetHistoryButtonState(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = enabled ? Color.white : Muted;
            }
        }

        private void CreateToolbar(Transform parent)
        {
            var bar = CreatePanel(parent, "Paint Toolbar", new Vector2(0f, 48f), new Vector2(720f, 84f));
            toolbar = bar;
            var labels = new[] { "붓", "질감", "팔레트", "스포이드" };
            var tools = new[] { Tool.Brush, Tool.Texture, Tool.Palette, Tool.Eyedropper };
            for (var index = 0; index < labels.Length; index++)
            {
                var tool = tools[index];
                var toolIndex = (int)tool;
                var button = CreateButton(bar.transform, labels[index], Card, Color.white, () => ShowTool(tool));
                toolBackgrounds[toolIndex] = button.GetComponent<Image>();
                toolLabels[toolIndex] = button.GetComponentInChildren<Text>();
                SetRect((RectTransform)button.transform, new Vector2(12f + index * 186f, 12f), new Vector2(132f, 60f), Vector2.zero, Vector2.zero, Vector2.zero);
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

        private void CreateBrushSizeOptions(Transform parent)
        {
            var label = CreateText(parent, "붓 크기", 18, Color.white);
            SetRect(label.rectTransform, new Vector2(24f, 72f), new Vector2(160f, 24f), Vector2.zero, Vector2.zero);
            brushSizeSlider = CreateSlider(parent, "Brush Size Slider", new Vector2(70f, 34f), CreateValueSprite(), Neon);
            SetRect(brushSizeSlider.GetComponent<RectTransform>(), new Vector2(70f, 34f), new Vector2(580f, 28f), Vector2.zero, Vector2.zero);
            ConfigureBrushSizeSlider(brushSizeSlider);
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
            SetRect(handle.rectTransform, Vector2.zero, new Vector2(4f, 24f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
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

        private Texture2D CreatePaintTexture(Texture2D source, Color baseColor)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = baseColor;
            }

            if (source != null && source.isReadable)
            {
                for (var y = 0; y < TextureSize; y++)
                {
                    var v = y / (TextureSize - 1f);
                    for (var x = 0; x < TextureSize; x++)
                    {
                        var u = x / (TextureSize - 1f);
                        pixels[y * TextureSize + x] = source.GetPixelBilinear(u, v) * baseColor;
                    }
                }
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
            if (arCameraManager != null)
            {
                arCameraManager.frameReceived -= OnCameraFrameReceived;
            }
            ClearPaintTarget();
            foreach (var asset in uiAssets)
            {
                DestroyObject(asset);
            }
        }

        private void ClearPaintTarget()
        {
            ResetPaintStroke();
            undoHistory.Clear();
            redoHistory.Clear();
            SetHistoryControlsVisible(false);
            UpdateHistoryUi();
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
