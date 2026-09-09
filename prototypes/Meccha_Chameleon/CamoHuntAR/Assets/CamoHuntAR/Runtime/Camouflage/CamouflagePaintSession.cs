using System;
using System.Collections.Generic;
using UnityEngine;

namespace CamoHuntAR
{
    public enum CamouflagePaintMode { Paint, SampleCharacter, SampleReality }

    /// <summary>Owns the durable paint state; a completed drag is one undo operation.</summary>
    public sealed class CamouflagePaintSession
    {
        private readonly CamouflagePaintHistory history = new CamouflagePaintHistory();
        private readonly List<Vector2> activePoints = new List<Vector2>();
        private Color32 activeColor;
        private float activeRadius;

        public CamouflageData Data { get; private set; } = CamouflageData.CreateEmpty();
        public CamouflageColorPickerState Picker { get; } = new CamouflageColorPickerState();
        public CamouflagePaintMode Mode { get; private set; } = CamouflagePaintMode.Paint;
        public bool IsPainting => activePoints.Count > 0;
        public bool CanUndo => history.CanUndo;

        public void SetMode(CamouflagePaintMode mode) => Mode = mode;
        public void SetHsv(float hue, float saturation, float value) => Picker.SetHsv(hue, saturation, value);

        public bool BeginStroke(Vector2 uv, float radius)
        {
            if (Mode != CamouflagePaintMode.Paint || IsPainting || !IsValidUv(uv) || !IsValidRadius(radius))
                return false;
            history.Capture(Data);
            activeColor = Picker.SelectedColor;
            activeRadius = radius;
            activePoints.Add(uv);
            return true;
        }

        public bool AppendStroke(Vector2 uv)
        {
            if (!IsPainting || !IsValidUv(uv) || activePoints.Count >= 512)
                return false;
            activePoints.Add(uv);
            return true;
        }

        public bool CompleteStroke()
        {
            if (!IsPainting)
                return false;
            var stroke = new CamouflageStroke(activePoints, activeColor, activeRadius);
            activePoints.Clear();
            return Data.TryAddStroke(stroke);
        }

        public void CancelStroke()
        {
            activePoints.Clear();
            if (history.CanUndo)
                history.TryUndo(out _);
        }

        public bool TryUndo(out CamouflageData restored)
        {
            if (!history.TryUndo(out restored))
                return false;
            Data = restored;
            return true;
        }

        public void SampleCharacter(Color32 color)
        {
            Picker.SampleCanvas(color);
            Mode = CamouflagePaintMode.Paint;
        }

        public void SampleReality(Color32 color)
        {
            Picker.SetColor(color);
            Mode = CamouflagePaintMode.Paint;
        }

        private static bool IsValidUv(Vector2 uv) => IsFinite(uv.x) && IsFinite(uv.y) && uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        private static bool IsValidRadius(float radius) => IsFinite(radius) && radius > 0f && radius <= 0.5f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
