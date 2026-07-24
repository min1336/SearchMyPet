using System;
using System.Collections.Generic;
using UnityEngine;

namespace CamoHuntAR
{
    [Serializable]
    public struct CamouflageRegionColor
    {
        [SerializeField] private string regionId;
        [SerializeField] private int paletteIndex;

        public CamouflageRegionColor(string regionId, int paletteIndex)
        {
            this.regionId = regionId;
            this.paletteIndex = paletteIndex;
        }

        public string RegionId => regionId;
        public int PaletteIndex => paletteIndex;
    }

    /// <summary>
    /// One brush mark on the character's UV map.  The region-based fields are
    /// retained below only so older local prototypes can still be opened.
    /// </summary>
    [Serializable]
    public struct CamouflageStroke
    {
        [SerializeField] private List<Vector2> points;
        [SerializeField] private Color32 color;
        [SerializeField] private float radius;

        public CamouflageStroke(Vector2 uv, Color32 color, float radius)
        {
            points = new List<Vector2> { uv };
            this.color = color;
            this.radius = radius;
        }

        public CamouflageStroke(IEnumerable<Vector2> points, Color32 color, float radius)
        {
            this.points = points == null ? null : new List<Vector2>(points);
            this.color = color;
            this.radius = radius;
        }

        public IReadOnlyList<Vector2> Points =>
            points == null ? Array.Empty<Vector2>() : points.AsReadOnly();
        public Vector2 Uv => Points.Count == 0 ? default : Points[0];
        public Color32 Color => color;
        public float Radius => radius;
    }

    [Serializable]
    public sealed class CamouflageData
    {
        public const int CurrentPaletteVersion = 1;

        [SerializeField] private int paletteVersion = CurrentPaletteVersion;
        [SerializeField] private List<CamouflageRegionColor> regions =
            new List<CamouflageRegionColor>();
        [SerializeField] private List<CamouflageStroke> strokes =
            new List<CamouflageStroke>();

        public int PaletteVersion => paletteVersion;

        public IReadOnlyList<CamouflageRegionColor> Regions =>
            regions == null
                ? Array.Empty<CamouflageRegionColor>()
                : regions.AsReadOnly();

        public IReadOnlyList<CamouflageStroke> Strokes =>
            strokes == null ? Array.Empty<CamouflageStroke>() : strokes.AsReadOnly();

        public static CamouflageData CreateEmpty()
        {
            return new CamouflageData
            {
                paletteVersion = CurrentPaletteVersion,
                regions = new List<CamouflageRegionColor>(),
                strokes = new List<CamouflageStroke>(),
            };
        }

        public static CamouflageData CreateDefault(IEnumerable<string> regionIds)
        {
            if (regionIds == null)
                throw new ArgumentNullException(nameof(regionIds));

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            var defaultRegions = new List<CamouflageRegionColor>();

            foreach (var regionId in regionIds)
            {
                if (string.IsNullOrWhiteSpace(regionId) || !uniqueIds.Add(regionId))
                {
                    throw new ArgumentException(
                        "Region IDs must be non-empty and unique.",
                        nameof(regionIds));
                }

                defaultRegions.Add(new CamouflageRegionColor(regionId, 0));
            }

            if (defaultRegions.Count == 0)
            {
                throw new ArgumentException(
                    "At least one camouflage region is required.",
                    nameof(regionIds));
            }

            return new CamouflageData
            {
                paletteVersion = CurrentPaletteVersion,
                regions = defaultRegions,
                strokes = new List<CamouflageStroke>(),
            };
        }

        public CamouflageData Clone()
        {
            return new CamouflageData
            {
                paletteVersion = paletteVersion,
                regions = regions == null
                    ? new List<CamouflageRegionColor>()
                    : new List<CamouflageRegionColor>(regions),
                strokes = strokes == null
                    ? new List<CamouflageStroke>()
                    : new List<CamouflageStroke>(strokes),
            };
        }

        public bool TryAddStroke(Vector2 uv, Color32 color, float radius)
        {
            return TryAddStroke(new CamouflageStroke(uv, color, radius));
        }

        public bool TryAddStroke(CamouflageStroke stroke)
        {
            if (stroke.Points.Count == 0 || stroke.Points.Count > 512 ||
                stroke.Radius <= 0f || stroke.Radius > 0.5f ||
                !IsFinite(stroke.Radius))
                return false;

            foreach (var uv in stroke.Points)
            {
                if (!IsFinite(uv.x) || !IsFinite(uv.y) ||
                    uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                    return false;
            }

            if (strokes == null)
                strokes = new List<CamouflageStroke>();
            if (strokes.Count >= 4096)
                return false;

            strokes.Add(new CamouflageStroke(stroke.Points, stroke.Color, stroke.Radius));
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool TrySetColor(
            string regionId,
            int paletteIndex,
            PaletteDefinition palette)
        {
            if (string.IsNullOrWhiteSpace(regionId) ||
                palette == null ||
                !palette.TryGetColor(paletteIndex, out _) ||
                regions == null)
            {
                return false;
            }

            var matchingIndex = -1;
            for (var index = 0; index < regions.Count; index++)
            {
                if (!string.Equals(
                        regions[index].RegionId,
                        regionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (matchingIndex >= 0)
                    return false;

                matchingIndex = index;
            }

            if (matchingIndex < 0)
                return false;

            regions[matchingIndex] =
                new CamouflageRegionColor(regionId, paletteIndex);
            return true;
        }

        public bool TryValidate(
            PaletteDefinition palette,
            IReadOnlyCollection<string> expectedRegionIds,
            out string error)
        {
            if (palette == null || palette.Count != PaletteDefinition.RequiredColorCount)
                return Fail("The palette must contain exactly eight colors.", out error);

            if (expectedRegionIds == null || expectedRegionIds.Count == 0)
                return Fail("At least one expected region is required.", out error);

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var regionId in expectedRegionIds)
            {
                if (string.IsNullOrWhiteSpace(regionId) || !expected.Add(regionId))
                    return Fail("Expected region IDs must be non-empty and unique.", out error);
            }

            if (paletteVersion != CurrentPaletteVersion)
                return Fail("The palette version is not supported.", out error);

            if (regions == null || regions.Count != expected.Count)
                return Fail("The camouflage region count does not match.", out error);

            var observed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in regions)
            {
                if (string.IsNullOrWhiteSpace(region.RegionId) ||
                    !expected.Contains(region.RegionId))
                {
                    return Fail("The camouflage data contains an unknown region.", out error);
                }

                if (!observed.Add(region.RegionId))
                    return Fail("The camouflage data contains a duplicate region.", out error);

                if (!palette.TryGetColor(region.PaletteIndex, out _))
                    return Fail("The camouflage data contains an invalid palette index.", out error);
            }

            if (!observed.SetEquals(expected))
                return Fail("The camouflage data is missing an expected region.", out error);

            error = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
