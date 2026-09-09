using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CamoHuntAR
{
    /// <summary>Dense HSV color surface: horizontal saturation and vertical value.</summary>
    [DisallowMultipleComponent]
    public sealed class HsvPaletteControl : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RawImage saturationValueImage;
        [SerializeField] private RawImage hueImage;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private RectTransform selectionIndicator;

        private Texture2D texture;
        private Texture2D hueTexture;
        public event Action<float, float, float> Changed;
        public float Hue => hueSlider.value;
        public float Saturation { get; private set; } = 0.8f;
        public float Value { get; private set; } = 0.8f;

        private void Awake()
        {
            texture = new Texture2D(160, 160, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            hueTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            saturationValueImage.texture = texture;
            hueImage.texture = hueTexture;
            DrawHue();
            hueSlider.onValueChanged.AddListener(_ => { Redraw(); Notify(); });
            Redraw();
            UpdateIndicator();
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (hueTexture != null) Destroy(hueTexture);
        }

        public void OnPointerDown(PointerEventData eventData) => Select(eventData);
        public void OnDrag(PointerEventData eventData) => Select(eventData);

        private void Select(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    saturationValueImage.rectTransform, eventData.position, eventData.pressEventCamera, out var local))
                return;
            var rect = saturationValueImage.rectTransform.rect;
            Saturation = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            Value = Mathf.Clamp01((local.y - rect.yMin) / rect.height);
            UpdateIndicator();
            Notify();
        }

        private void DrawHue()
        {
            var pixels = new Color[hueTexture.width];
            for (var x = 0; x < pixels.Length; x++)
                pixels[x] = Color.HSVToRGB(x / (float)(pixels.Length - 1), 1f, 1f);
            hueTexture.SetPixels(pixels);
            hueTexture.Apply(false, true);
        }

        private void Redraw()
        {
            var pixels = new Color[texture.width * texture.height];
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
                pixels[y * texture.width + x] = Color.HSVToRGB(hueSlider.value, x / (float)(texture.width - 1), y / (float)(texture.height - 1));
            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private void UpdateIndicator()
        {
            if (selectionIndicator == null) return;
            var rect = saturationValueImage.rectTransform.rect;
            selectionIndicator.anchoredPosition = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, Saturation),
                Mathf.Lerp(rect.yMin, rect.yMax, Value));
        }

        private void Notify() => Changed?.Invoke(Hue, Saturation, Value);
    }
}
