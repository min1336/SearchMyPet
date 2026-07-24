using System;
using System.Collections.Generic;
using UnityEngine;

namespace CamoHuntAR
{
    public sealed class CamouflageRenderer : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private PaletteDefinition palette;
        [SerializeField] private CamouflageRegionBinding[] regionBindings = Array.Empty<CamouflageRegionBinding>();

        private readonly Dictionary<Renderer, Material[]> _runtimeMaterials =
            new Dictionary<Renderer, Material[]>();

        public IReadOnlyCollection<string> RegionIds
        {
            get
            {
                var regionIds = new List<string>(regionBindings.Length);
                foreach (var binding in regionBindings)
                    regionIds.Add(binding.RegionId);

                return regionIds.AsReadOnly();
            }
        }

        public void Configure(PaletteDefinition paletteDefinition, CamouflageRegionBinding[] bindings)
        {
            ValidateConfiguration(paletteDefinition, bindings);
            ClearRuntimeMaterials();
            palette = paletteDefinition;
            regionBindings = (CamouflageRegionBinding[])bindings.Clone();
        }

        public void Apply(CamouflageData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (palette == null)
                throw new InvalidOperationException("A palette must be configured before applying camouflage.");

            var regionIds = RegionIds;
            if (!data.TryValidate(palette, regionIds, out var error))
                throw new ArgumentException(error, nameof(data));

            foreach (var binding in regionBindings)
            {
                var material = GetRuntimeMaterial(binding);
                if (!palette.TryGetColor(GetPaletteIndex(data, binding.RegionId), out var color))
                    throw new InvalidOperationException("A validated camouflage color could not be resolved.");

                SetColor(material, color);
            }
        }

        private void OnDestroy()
        {
            ClearRuntimeMaterials();
        }

        private static void ValidateConfiguration(
            PaletteDefinition paletteDefinition,
            CamouflageRegionBinding[] bindings)
        {
            if (paletteDefinition == null)
                throw new ArgumentNullException(nameof(paletteDefinition));

            if (paletteDefinition.Count != PaletteDefinition.RequiredColorCount)
                throw new ArgumentException("The palette must contain exactly eight colors.", nameof(paletteDefinition));

            if (bindings == null || bindings.Length == 0)
                throw new ArgumentException("At least one region binding is required.", nameof(bindings));

            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (binding == null ||
                    string.IsNullOrWhiteSpace(binding.RegionId) ||
                    binding.Renderer == null ||
                    binding.MaterialIndex < 0 ||
                    binding.MaterialIndex >= binding.Renderer.sharedMaterials.Length ||
                    !regionIds.Add(binding.RegionId))
                {
                    throw new ArgumentException("Camouflage region bindings must be unique and reference valid material slots.", nameof(bindings));
                }
            }
        }

        private Material GetRuntimeMaterial(CamouflageRegionBinding binding)
        {
            if (!_runtimeMaterials.TryGetValue(binding.Renderer, out var materials))
            {
                var sharedMaterials = binding.Renderer.sharedMaterials;
                materials = new Material[sharedMaterials.Length];
                for (var index = 0; index < sharedMaterials.Length; index++)
                    materials[index] = new Material(sharedMaterials[index]);

                binding.Renderer.sharedMaterials = materials;
                _runtimeMaterials.Add(binding.Renderer, materials);
            }

            return materials[binding.MaterialIndex];
        }

        private static int GetPaletteIndex(CamouflageData data, string regionId)
        {
            foreach (var region in data.Regions)
            {
                if (string.Equals(region.RegionId, regionId, StringComparison.Ordinal))
                    return region.PaletteIndex;
            }

            throw new InvalidOperationException("A validated camouflage region could not be resolved.");
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorPropertyId))
            {
                material.SetColor(BaseColorPropertyId, color);
                return;
            }

            if (material.HasProperty(ColorPropertyId))
            {
                material.SetColor(ColorPropertyId, color);
                return;
            }

            throw new InvalidOperationException("The configured material does not expose a supported color property.");
        }

        private void ClearRuntimeMaterials()
        {
            foreach (var materials in _runtimeMaterials.Values)
            {
                foreach (var material in materials)
                {
                    if (material != null)
                    {
                        if (!Application.isPlaying || Application.isBatchMode)
                        {
                            DestroyImmediate(material);
                            continue;
                        }
                        if (Application.isPlaying)
                            Destroy(material);
                        else
                            DestroyImmediate(material);
                    }
                }
            }

            _runtimeMaterials.Clear();
        }
    }
}
