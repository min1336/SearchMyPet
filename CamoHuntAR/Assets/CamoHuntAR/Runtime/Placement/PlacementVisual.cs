using UnityEngine;

namespace CamoHuntAR
{
    public sealed class PlacementVisual : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Material placedMaterial;

        public void Configure(Renderer[] renderers, Material preview, Material placed)
        {
            targetRenderers = renderers;
            previewMaterial = preview;
            placedMaterial = placed;
        }

        public void SetPreview(bool isPreview)
        {
            var selectedMaterial = isPreview ? previewMaterial : placedMaterial;
            if (selectedMaterial == null || targetRenderers == null)
                return;

            foreach (var targetRenderer in targetRenderers)
            {
                if (targetRenderer != null)
                    targetRenderer.sharedMaterial = selectedMaterial;
            }
        }
    }
}
