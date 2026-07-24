using UnityEngine;

namespace CamoHuntAR
{
    public sealed class PlacementVisual : MonoBehaviour
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.55f;

        public void Configure(Renderer[] renderers)
        {
            targetRenderers = renderers;
        }

        public void Configure(Renderer[] renderers, Material preview, Material placed)
        {
            Configure(renderers);
        }

        public void SetPreview(bool isPreview)
        {
            if (targetRenderers == null)
                return;

            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                    continue;

                ApplyAlpha(targetRenderer, isPreview);
            }
        }

        private void ApplyAlpha(Renderer targetRenderer, bool isPreview)
        {
            var material = targetRenderer.sharedMaterial;
            var colorPropertyId = GetColorPropertyId(material);
            if (colorPropertyId == 0)
                return;

            var color = material.GetColor(colorPropertyId);
            color.a = isPreview ? Mathf.Min(color.a, previewAlpha) : color.a;

            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorPropertyId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private static int GetColorPropertyId(Material material)
        {
            if (material == null)
                return 0;

            if (material.HasProperty(BaseColorPropertyId))
                return BaseColorPropertyId;

            return material.HasProperty(ColorPropertyId) ? ColorPropertyId : 0;
        }
    }
}
