using UnityEngine;
using UnityEngine.UI;

namespace CamoHuntAR
{
    /// <summary>Scene UI bridge: it never stores a prefab reference and binds only after placement.</summary>
    [DisallowMultipleComponent]
    public sealed class CamouflagePaintUiController : MonoBehaviour
    {
        [SerializeField] private ARPlacementController placementController;
        [SerializeField] private CharacterPaintEditorController editorController;
        [SerializeField] private GameObject toolsPanel;
        [SerializeField] private Button paintButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private Button finishButton;
        [SerializeField] private Button characterEyedropperButton;
        [SerializeField] private Button realityEyedropperButton;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Slider saturationSlider;
        [SerializeField] private Slider valueSlider;
        [SerializeField] private HsvPaletteControl palette;
        [SerializeField] private Image selectedColorPreview;
        [SerializeField] private Text modeLabel;

        private CamouflageSurfacePaintController target;
        private CamouflagePaintMode displayedMode;

        private void Awake()
        {
            paintButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.Paint));
            rotateButton.onClick.AddListener(() => SetRotation(true));
            finishButton.onClick.AddListener(FinishEditing);
            characterEyedropperButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.SampleCharacter));
            realityEyedropperButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.SampleReality));
            hueSlider.onValueChanged.AddListener(_ => ApplyHsv());
            saturationSlider.onValueChanged.AddListener(_ => ApplyHsv());
            valueSlider.onValueChanged.AddListener(_ => ApplyHsv());
            if (palette != null)
                palette.Changed += ApplyHsv;
            placementController.CharacterPlaced += Bind;
            placementController.CharacterReset += Unbind;
            Unbind();
        }

        private void OnDestroy()
        {
            if (placementController == null) return;
            placementController.CharacterPlaced -= Bind;
            placementController.CharacterReset -= Unbind;
            if (palette != null)
                palette.Changed -= ApplyHsv;
        }

        private void Update()
        {
            if (target == null) return;
            selectedColorPreview.color = target.SelectedColor;
            if (target.Mode != displayedMode)
                UpdateModeVisual(target.Mode);
        }

        private void Bind(CamouflageSurfacePaintController controller)
        {
            target = controller;
            toolsPanel.SetActive(target != null);
            realityEyedropperButton.gameObject.SetActive(false);
            if (palette != null)
                ApplyHsv(palette.Hue, palette.Saturation, palette.Value);
            else
                ApplyHsv();
            UpdateModeVisual(CamouflagePaintMode.Paint);
        }

        private void Unbind()
        {
            target = null;
            if (toolsPanel != null) toolsPanel.SetActive(false);
        }

        private void SetMode(CamouflagePaintMode mode)
        {
            editorController?.SetRotationEnabled(false);
            target?.SetMode(mode);
            UpdateModeVisual(mode);
        }

        private void SetRotation(bool enabled)
        {
            editorController?.SetRotationEnabled(enabled);
            if (enabled && modeLabel != null)
                modeLabel.text = "DRAG TO ROTATE CHARACTER";
            SetButtonState(rotateButton, enabled);
            SetButtonState(paintButton, !enabled && target?.Mode == CamouflagePaintMode.Paint);
        }

        private void FinishEditing()
        {
            editorController?.FinishEditing();
            if (toolsPanel != null)
                toolsPanel.SetActive(false);
        }

        private void UpdateModeVisual(CamouflagePaintMode mode)
        {
            displayedMode = mode;
            SetButtonState(paintButton, mode == CamouflagePaintMode.Paint);
            SetButtonState(rotateButton, false);
            SetButtonState(characterEyedropperButton, mode == CamouflagePaintMode.SampleCharacter);
            SetButtonState(realityEyedropperButton, mode == CamouflagePaintMode.SampleReality);
            if (modeLabel != null)
                modeLabel.text = mode switch
                {
                    CamouflagePaintMode.Paint => "DRAW ON CHARACTER",
                    CamouflagePaintMode.SampleCharacter => "TAP CHARACTER COLOR",
                    CamouflagePaintMode.SampleReality => "TAP CAMERA COLOR",
                    _ => string.Empty,
                };
        }

        private static void SetButtonState(Button button, bool selected)
        {
            if (button == null)
                return;
            var colors = button.colors;
            colors.normalColor = selected
                ? new Color(0.18f, 0.90f, 0.72f, 1f)
                : new Color(0.10f, 0.22f, 0.27f, 0.96f);
            colors.highlightedColor = new Color(0.24f, 0.96f, 0.80f, 1f);
            button.colors = colors;
        }
        private void ApplyHsv() => target?.SetHsv(hueSlider.value, saturationSlider.value, valueSlider.value);
        private void ApplyHsv(float hue, float saturation, float value) => target?.SetHsv(hue, saturation, value);
    }
}
