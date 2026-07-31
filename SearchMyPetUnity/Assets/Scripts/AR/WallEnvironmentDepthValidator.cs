using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    public sealed class WallEnvironmentDepthValidator : MonoBehaviour
    {
        private const int PatchDiameter = 3;
        private const int PatchSampleCapacity = PatchDiameter * PatchDiameter;
        private const byte MinimumReliableConfidence = 1;
        private const float MaximumCachedScreenDeltaSquared = 4f;
        private const float MaximumCachedExpectedDepthDeltaMeters = 0.01f;

        [SerializeField] private AROcclusionManager occlusionManager;
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private Camera arCamera;

        private readonly float[] measuredDepthsMeters = new float[5];
        private readonly float[] patchSamplesMeters = new float[PatchSampleCapacity];
        private readonly Vector2[] cachedScreenSamplePoints = new Vector2[5];
        private readonly float[] cachedExpectedDepthsMeters = new float[5];
        private Matrix4x4 displayMatrix;
        private bool hasDisplayMatrix;
        private bool hasCachedResult;
        private float nextValidationTime;
        private WallEnvironmentDepthResult cachedResult;
        private string lastLoggedState;

        public WallEnvironmentDepthResult Validate(
            IReadOnlyList<Vector2> screenSamplePoints,
            IReadOnlyList<float> expectedDepthsMeters,
            int minimumValidSampleCount,
            float maximumDepthDeviationMeters,
            float maximumResidualSpreadMeters,
            float minimumValidationIntervalSeconds)
        {
            if (screenSamplePoints == null
                || expectedDepthsMeters == null
                || screenSamplePoints.Count != measuredDepthsMeters.Length
                || expectedDepthsMeters.Count != measuredDepthsMeters.Length)
            {
                return WallEnvironmentDepthResult.Unavailable;
            }

            if (hasCachedResult
                && Time.unscaledTime < nextValidationTime
                && InputsMatchCache(screenSamplePoints, expectedDepthsMeters))
            {
                return cachedResult;
            }

            cachedResult = ValidateCurrentFrame(
                screenSamplePoints,
                expectedDepthsMeters,
                minimumValidSampleCount,
                maximumDepthDeviationMeters,
                maximumResidualSpreadMeters);
            CacheInputs(screenSamplePoints, expectedDepthsMeters);
            hasCachedResult = true;
            nextValidationTime = Time.unscaledTime + Mathf.Max(0.02f, minimumValidationIntervalSeconds);
            return cachedResult;
        }

        private bool InputsMatchCache(
            IReadOnlyList<Vector2> screenSamplePoints,
            IReadOnlyList<float> expectedDepthsMeters)
        {
            for (var index = 0; index < cachedScreenSamplePoints.Length; index++)
            {
                if ((cachedScreenSamplePoints[index] - screenSamplePoints[index]).sqrMagnitude
                        > MaximumCachedScreenDeltaSquared
                    || Mathf.Abs(cachedExpectedDepthsMeters[index] - expectedDepthsMeters[index])
                        > MaximumCachedExpectedDepthDeltaMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheInputs(
            IReadOnlyList<Vector2> screenSamplePoints,
            IReadOnlyList<float> expectedDepthsMeters)
        {
            for (var index = 0; index < cachedScreenSamplePoints.Length; index++)
            {
                cachedScreenSamplePoints[index] = screenSamplePoints[index];
                cachedExpectedDepthsMeters[index] = expectedDepthsMeters[index];
            }
        }

        public void ResetCache()
        {
            hasCachedResult = false;
            nextValidationTime = 0f;
        }

        private void Awake()
        {
            occlusionManager ??= FindAnyObjectByType<AROcclusionManager>();
            cameraManager ??= FindAnyObjectByType<ARCameraManager>();
            arCamera ??= Camera.main;
        }

        private void OnEnable()
        {
            if (cameraManager != null)
            {
                cameraManager.frameReceived += OnCameraFrameReceived;
            }
        }

        private void OnDisable()
        {
            if (cameraManager != null)
            {
                cameraManager.frameReceived -= OnCameraFrameReceived;
            }

            ResetCache();
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
        {
            if (eventArgs.displayMatrix.HasValue)
            {
                displayMatrix = eventArgs.displayMatrix.Value;
                hasDisplayMatrix = true;
            }
        }

        private WallEnvironmentDepthResult ValidateCurrentFrame(
            IReadOnlyList<Vector2> screenSamplePoints,
            IReadOnlyList<float> expectedDepthsMeters,
            int minimumValidSampleCount,
            float maximumDepthDeviationMeters,
            float maximumResidualSpreadMeters)
        {
            if (occlusionManager == null || arCamera == null || occlusionManager.descriptor == null)
            {
                LogState("manager-unavailable", "mode=fallback reason=manager-unavailable");
                return WallEnvironmentDepthResult.Unavailable;
            }

            var support = occlusionManager.descriptor.environmentDepthImageSupported;
            if (support != Supported.Supported)
            {
                var reason = support == Supported.Unknown ? "support-unknown" : "unsupported";
                LogState(reason, $"mode=fallback reason={reason}");
                return WallEnvironmentDepthResult.Unavailable;
            }

            if (occlusionManager.currentEnvironmentDepthMode == EnvironmentDepthMode.Disabled)
            {
                LogState("depth-not-ready", "mode=fallback reason=depth-not-ready");
                return WallEnvironmentDepthResult.Unavailable;
            }

            if (!hasDisplayMatrix)
            {
                LogState("display-matrix-unavailable", "mode=fallback reason=display-matrix-unavailable");
                return WallEnvironmentDepthResult.Unavailable;
            }

            if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var depthImage))
            {
                LogState("cpu-image-unavailable", "mode=fallback reason=cpu-image-unavailable");
                return WallEnvironmentDepthResult.Unavailable;
            }

            try
            {
                var hasConfidenceImage = false;
                var confidenceImage = default(XRCpuImage);
                try
                {
                    if (occlusionManager.descriptor.environmentDepthConfidenceImageSupported == Supported.Supported)
                    {
                        if (!occlusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out confidenceImage))
                        {
                            LogState("confidence-image-unavailable", "mode=fallback reason=confidence-image-unavailable");
                            return WallEnvironmentDepthResult.Unavailable;
                        }

                        hasConfidenceImage = true;
                        if (confidenceImage.format != XRCpuImage.Format.OneComponent8 || confidenceImage.planeCount != 1)
                        {
                            LogState(
                                "unsupported-confidence-format",
                                $"mode=fallback reason=unsupported-confidence-format format={confidenceImage.format}");
                            return WallEnvironmentDepthResult.Unavailable;
                        }
                    }

                if (depthImage.format != XRCpuImage.Format.DepthFloat32 || depthImage.planeCount != 1)
                {
                    LogState("unsupported-image-format", $"mode=fallback reason=unsupported-image-format format={depthImage.format}");
                    return WallEnvironmentDepthResult.Unavailable;
                }

                var plane = depthImage.GetPlane(0);
                var dimensions = new Vector2Int(depthImage.width, depthImage.height);
                var confidencePlane = hasConfidenceImage ? confidenceImage.GetPlane(0) : default;
                var confidenceDimensions = hasConfidenceImage
                    ? new Vector2Int(confidenceImage.width, confidenceImage.height)
                    : default;
                for (var index = 0; index < measuredDepthsMeters.Length; index++)
                {
                    measuredDepthsMeters[index] = float.NaN;
                    var confidenceIsReliable = !hasConfidenceImage
                        || (WallEnvironmentDepthCoordinateUtility.TryMapScreenPointToImagePixel(
                                screenSamplePoints[index],
                                arCamera.pixelRect,
                                displayMatrix,
                                confidenceDimensions,
                                out var confidencePixel)
                            && IsConfidencePatchReliable(
                                confidencePlane,
                                confidenceDimensions,
                                confidencePixel));
                    if (WallEnvironmentDepthCoordinateUtility.TryMapScreenPointToImagePixel(
                            screenSamplePoints[index],
                            arCamera.pixelRect,
                            displayMatrix,
                            dimensions,
                            out var pixel)
                        && confidenceIsReliable
                        && TryReadPatchMedian(plane, dimensions, pixel, out var measuredDepth))
                    {
                        measuredDepthsMeters[index] = measuredDepth;
                    }
                }

                var result = WallEnvironmentDepthRules.Evaluate(
                    expectedDepthsMeters,
                    measuredDepthsMeters,
                    minimumValidSampleCount,
                    maximumDepthDeviationMeters,
                    maximumResidualSpreadMeters);
                switch (result)
                {
                    case WallEnvironmentDepthResult.Passed:
                        LogState("active-passed", "mode=active result=passed");
                        break;
                    case WallEnvironmentDepthResult.Rejected:
                        LogState("active-rejected", "mode=active result=rejected");
                        break;
                    default:
                        LogState("insufficient-valid-samples", "mode=fallback reason=insufficient-valid-samples");
                        break;
                }

                return result;
                }
                finally
                {
                    if (hasConfidenceImage)
                    {
                        confidenceImage.Dispose();
                    }
                }
            }
            finally
            {
                depthImage.Dispose();
            }
        }

        private static bool IsConfidencePatchReliable(
            XRCpuImage.Plane plane,
            Vector2Int imageDimensions,
            Vector2Int centerPixel)
        {
            if (plane.pixelStride != sizeof(byte) || plane.rowStride <= 0 || plane.data.Length == 0)
            {
                return false;
            }

            var reliableCount = 0;
            var sampledCount = 0;
            for (var yOffset = -1; yOffset <= 1; yOffset++)
            {
                var y = centerPixel.y + yOffset;
                if (y < 0 || y >= imageDimensions.y)
                {
                    continue;
                }

                for (var xOffset = -1; xOffset <= 1; xOffset++)
                {
                    var x = centerPixel.x + xOffset;
                    if (x < 0 || x >= imageDimensions.x)
                    {
                        continue;
                    }

                    var byteOffset = y * plane.rowStride + x * plane.pixelStride;
                    if (byteOffset < 0 || byteOffset >= plane.data.Length)
                    {
                        continue;
                    }

                    sampledCount++;
                    if (plane.data[byteOffset] >= MinimumReliableConfidence)
                    {
                        reliableCount++;
                    }
                }
            }

            return sampledCount > 0 && reliableCount * 2 >= sampledCount;
        }

        private bool TryReadPatchMedian(
            XRCpuImage.Plane plane,
            Vector2Int imageDimensions,
            Vector2Int centerPixel,
            out float medianDepthMeters)
        {
            medianDepthMeters = float.NaN;
            const int floatSize = sizeof(float);
            if (plane.pixelStride != floatSize
                || plane.rowStride <= 0
                || plane.rowStride % floatSize != 0
                || plane.data.Length < floatSize)
            {
                return false;
            }

            var floatData = plane.data.Reinterpret<float>(sizeof(byte));
            var validCount = 0;
            for (var yOffset = -1; yOffset <= 1; yOffset++)
            {
                var y = centerPixel.y + yOffset;
                if (y < 0 || y >= imageDimensions.y)
                {
                    continue;
                }

                for (var xOffset = -1; xOffset <= 1; xOffset++)
                {
                    var x = centerPixel.x + xOffset;
                    if (x < 0 || x >= imageDimensions.x)
                    {
                        continue;
                    }

                    var byteOffset = y * plane.rowStride + x * plane.pixelStride;
                    if (byteOffset < 0 || byteOffset + floatSize > plane.data.Length)
                    {
                        continue;
                    }

                    var depth = floatData[byteOffset / floatSize];
                    if (depth > 0f && !float.IsNaN(depth) && !float.IsInfinity(depth))
                    {
                        patchSamplesMeters[validCount++] = depth;
                    }
                }
            }

            if (validCount == 0)
            {
                return false;
            }

            for (var index = 1; index < validCount; index++)
            {
                var value = patchSamplesMeters[index];
                var insertionIndex = index - 1;
                while (insertionIndex >= 0 && patchSamplesMeters[insertionIndex] > value)
                {
                    patchSamplesMeters[insertionIndex + 1] = patchSamplesMeters[insertionIndex];
                    insertionIndex--;
                }

                patchSamplesMeters[insertionIndex + 1] = value;
            }

            medianDepthMeters = patchSamplesMeters[validCount / 2];
            return true;
        }

        private void LogState(string state, string message)
        {
            if (lastLoggedState == state)
            {
                return;
            }

            lastLoggedState = state;
            Debug.Log($"[WallDepth] {message}");
        }
    }
}
