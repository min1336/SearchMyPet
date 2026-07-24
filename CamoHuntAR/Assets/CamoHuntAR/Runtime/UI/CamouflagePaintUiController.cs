using UnityEngine;
using UnityEngine.UI;

namespace CamoHuntAR
{
    /// <summary>Scene UI bridge: it never stores a prefab reference and binds only after placement.</summary>
    [DisallowMultipleComponent]
    public sealed class CamouflagePaintUiController : MonoBehaviour
    {
        [SerializeField] private ARPlacementController placementController;
        [SerializeField] private GameObject toolsPanel;
        [SerializeField] private Button paintButton;
        [SerializeField] private Button characterEyedropperButton;
        [SerializeField] private Button realityEyedropperButton;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Slider saturationSlider;
        [SerializeField] private Slider valueSlider;
        [SerializeField] private Image selectedColorPreview;

        private CamouflageSurfacePaintController target;

        private void Awake()
        {
            paintButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.Paint));
            characterEyedropperButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.SampleCharacter));
            realityEyedropperButton.onClick.AddListener(() => SetMode(CamouflagePaintMode.SampleReality));
            hueSlider.onValueChanged.AddListener(_ => ApplyHsv());
            saturationSlider.onValueChanged.AddListener(_ => ApplyHsv());
            valueSlider.onValueChanged.AddListener(_ => ApplyHsv());
            placementController.CharacterPlaced += Bind;
            placementController.CharacterReset += Unbind;
            Unbind();
        }

        private void OnDestroy()
        {
            if (placementController == null) return;
            placementController.CharacterPlaced -= Bind;
            placementController.CharacterReset -= Unbind;
        }

        private void Update()
        {
            if (target == null) return;
            selectedColorPreview.color = target.SelectedColor;
        }

        private void Bind(CamouflageSurfacePaintController controller)
        {
            target = controller;
            toolsPanel.SetActive(target != null);
            ApplyHsv();
        }

        private void Unbind()
        {
            target = null;
            if (toolsPanel != null) toolsPanel.SetActive(false);
        }

        private void SetMode(CamouflagePaintMode mode) => target?.SetMode(mode);
        private void ApplyHsv() => target?.SetHsv(hueSlider.value, saturationSlider.value, valueSlider.value);
    }
}
