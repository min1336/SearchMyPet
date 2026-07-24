using UnityEngine;

namespace CamoHuntAR
{
    /// <summary>Creates a private material and paints the target renderer's UV texture at runtime.</summary>
    [DisallowMultipleComponent]
    public sealed class CamouflageTexturePainter : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField, Range(32, 2048)] private int textureResolution = 512;

        private Texture2D canvas;
        private Material runtimeMaterial;

        private void Awake()
        {
            if (targetRenderer != null)
                InitializeCanvas(new Color32(255, 255, 255, 255));
        }

        public void ConfigureForPrefab(Renderer renderer, int targetMaterialIndex, int resolution)
        {
            targetRenderer = renderer;
            materialIndex = Mathf.Max(0, targetMaterialIndex);
            textureResolution = Mathf.Clamp(resolution, 32, 2048);
        }

        public bool InitializeCanvas(Color32 background)
        {
            if (targetRenderer == null)
                return false;

            var sourceMaterials = targetRenderer.sharedMaterials;
            if (materialIndex >= sourceMaterials.Length || sourceMaterials[materialIndex] == null)
                return false;

            DisposeRuntimeResources();
            runtimeMaterial = new Material(sourceMaterials[materialIndex]);
            canvas = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false)
            {
                name = "RuntimeCamouflageCanvas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[textureResolution * textureResolution];
            for (var index = 0; index < pixels.Length; index++)
                pixels[index] = background;
            canvas.SetPixels32(pixels);
            canvas.Apply(false, false);

            runtimeMaterial.mainTexture = canvas;
            sourceMaterials[materialIndex] = runtimeMaterial;
            targetRenderer.materials = sourceMaterials;
            return true;
        }

        public bool PaintAtUv(Vector2 uv, Color32 color, float radius)
        {
            if (canvas == null || !IsValidUv(uv) || radius <= 0f)
                return false;

            var centerX = Mathf.RoundToInt(uv.x * (textureResolution - 1));
            var centerY = Mathf.RoundToInt(uv.y * (textureResolution - 1));
            var pixelRadius = Mathf.Max(1, Mathf.CeilToInt(radius * textureResolution));
            var radiusSquared = pixelRadius * pixelRadius;

            for (var y = -pixelRadius; y <= pixelRadius; y++)
            {
                for (var x = -pixelRadius; x <= pixelRadius; x++)
                {
                    if (x * x + y * y > radiusSquared)
                        continue;

                    var pixelX = centerX + x;
                    var pixelY = centerY + y;
                    if (pixelX < 0 || pixelX >= textureResolution || pixelY < 0 || pixelY >= textureResolution)
                        continue;
                    canvas.SetPixel(pixelX, pixelY, color);
                }
            }

            canvas.Apply(false, false);
            return true;
        }

        public bool TrySampleUv(Vector2 uv, out Color32 color)
        {
            color = default;
            if (canvas == null || !IsValidUv(uv))
                return false;

            color = canvas.GetPixel(
                Mathf.RoundToInt(uv.x * (textureResolution - 1)),
                Mathf.RoundToInt(uv.y * (textureResolution - 1)));
            return true;
        }

        public bool Repaint(CamouflageData data, Color32 background)
        {
            if (data == null || !InitializeCanvas(background))
                return false;

            foreach (var stroke in data.Strokes)
            {
                foreach (var point in stroke.Points)
                    PaintAtUv(point, stroke.Color, stroke.Radius);
            }
            return true;
        }

        private static bool IsValidUv(Vector2 uv)
        {
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        private void OnDestroy()
        {
            DisposeRuntimeResources();
        }

        private void DisposeRuntimeResources()
        {
            DestroyObject(canvas);
            DestroyObject(runtimeMaterial);
            canvas = null;
            runtimeMaterial = null;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
