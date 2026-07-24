using UnityEngine;

namespace CamoHuntAR
{
    /// <summary>Small UI-independent state shared by the HSV picker and both eyedroppers.</summary>
    public sealed class CamouflageColorPickerState
    {
        // A dark default makes the first brush stroke visible on the white canvas.
        public Color32 SelectedColor { get; private set; } = new Color32(20, 20, 20, 255);

        public void SetHsv(float hue, float saturation, float value)
        {
            SetColor(Color.HSVToRGB(
                Mathf.Repeat(hue, 1f),
                Mathf.Clamp01(saturation),
                Mathf.Clamp01(value)));
        }

        public void SetColor(Color color)
        {
            SelectedColor = (Color32)color;
        }

        public void SetColor(Color32 color)
        {
            SelectedColor = color;
        }

        public void SampleCanvas(Color32 color)
        {
            SetColor(color);
        }
    }
}
