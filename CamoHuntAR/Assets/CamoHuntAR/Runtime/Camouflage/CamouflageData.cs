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

    [Serializable]
    public sealed class CamouflageData
    {
        public const int CurrentPaletteVersion = 1;

        [SerializeField] private int paletteVersion = CurrentPaletteVersion;
        [SerializeField] private List<CamouflageRegionColor> regions =
            new List<CamouflageRegionColor>();

        public int PaletteVersion => paletteVersion;

        public IReadOnlyList<CamouflageRegionColor> Regions =>
            regions == null
                ? Array.Empty<CamouflageRegionColor>()
                : regions.AsReadOnly();

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
            };
        }

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
