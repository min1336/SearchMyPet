using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR
{
    /// <summary>
    /// Reads one pixel from the current camera frame for the real-world eyedropper.
    /// The CPU image is released immediately; no camera frame is saved to disk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFrameColorSampler : MonoBehaviour
    {
        [SerializeField] private ARCameraManager cameraManager;

        public void Configure(ARCameraManager manager)
        {
            cameraManager = manager;
        }

        public bool TrySampleViewport(Vector2 viewportPosition, out Color32 color)
        {
            color = default;
            if (cameraManager == null ||
                viewportPosition.x < 0f || viewportPosition.x > 1f ||
                viewportPosition.y < 0f || viewportPosition.y > 1f ||
                !cameraManager.TryAcquireLatestCpuImage(out var image))
            {
                return false;
            }

            try
            {
                var parameters = new XRCpuImage.ConversionParams(
                    image,
                    TextureFormat.RGBA32,
                    XRCpuImage.Transformation.None);
                var bytes = new NativeArray<byte>(
                    image.GetConvertedDataSize(parameters),
                    Allocator.Temp);
                try
                {
                    image.Convert(parameters, bytes);
                    var x = Mathf.Clamp(Mathf.RoundToInt(viewportPosition.x * (image.width - 1)), 0, image.width - 1);
                    var y = Mathf.Clamp(Mathf.RoundToInt(viewportPosition.y * (image.height - 1)), 0, image.height - 1);
                    var index = (y * image.width + x) * 4;
                    color = new Color32(bytes[index], bytes[index + 1], bytes[index + 2], bytes[index + 3]);
                    return true;
                }
                finally
                {
                    bytes.Dispose();
                }
            }
            finally
            {
                image.Dispose();
            }
        }
    }
}
