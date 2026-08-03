using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    public static class WallPlacementCandidateRules
    {
        public static bool IsEligible(
            PlaneAlignment alignment,
            TrackingState trackingState,
            Vector2 planeSize,
            Vector2 minimumPlaneSize)
        {
            return alignment == PlaneAlignment.Vertical
                && trackingState == TrackingState.Tracking
                && planeSize.x >= minimumPlaneSize.x
                && planeSize.y >= minimumPlaneSize.y;
        }
    }

    public readonly struct WallCandidateObservation
    {
        public WallCandidateObservation(
            TrackableId planeId,
            TrackingState trackingState,
            Vector3 position,
            Vector3 surfaceNormal)
        {
            PlaneId = planeId;
            TrackingState = trackingState;
            Position = position;
            SurfaceNormal = surfaceNormal;
        }

        public TrackableId PlaneId { get; }
        public TrackingState TrackingState { get; }
        public Vector3 Position { get; }
        public Vector3 SurfaceNormal { get; }
    }

    public sealed class WallCandidateStabilityTracker
    {
        private readonly float requiredDuration;
        private readonly float maximumPlaneDistanceDelta;
        private readonly float maximumNormalAngleDelta;
        private readonly float maximumObservationInterval;
        private TrackableId currentPlaneId;
        private float elapsed;
        private bool hasCurrentPlane;
        private WallCandidateObservation stabilityBaseline;
        private bool hasStabilityBaseline;

        public WallCandidateStabilityTracker(
            float requiredDuration,
            float maximumPlaneDistanceDelta,
            float maximumNormalAngleDelta)
            : this(
                requiredDuration,
                maximumPlaneDistanceDelta,
                maximumNormalAngleDelta,
                float.PositiveInfinity)
        {
        }

        public WallCandidateStabilityTracker(
            float requiredDuration,
            float maximumPlaneDistanceDelta,
            float maximumNormalAngleDelta,
            float maximumObservationInterval)
        {
            this.requiredDuration = Mathf.Max(0f, requiredDuration);
            this.maximumPlaneDistanceDelta = Mathf.Max(0f, maximumPlaneDistanceDelta);
            this.maximumNormalAngleDelta = Mathf.Max(0f, maximumNormalAngleDelta);
            this.maximumObservationInterval = Mathf.Max(0f, maximumObservationInterval);
        }

        public bool Update(WallCandidateObservation observation, float deltaTime)
        {
            if (observation.PlaneId == TrackableId.invalidId
                || observation.TrackingState != TrackingState.Tracking
                || observation.SurfaceNormal.sqrMagnitude < 0.000001f)
            {
                Reset();
                return false;
            }

            if (!hasCurrentPlane || currentPlaneId != observation.PlaneId || !hasStabilityBaseline)
            {
                currentPlaneId = observation.PlaneId;
                elapsed = 0f;
                hasCurrentPlane = true;
                stabilityBaseline = observation;
                hasStabilityBaseline = true;
                return false;
            }

            if (deltaTime > maximumObservationInterval)
            {
                elapsed = 0f;
                stabilityBaseline = observation;
                return false;
            }

            var baselineNormal = stabilityBaseline.SurfaceNormal.normalized;
            var currentNormal = observation.SurfaceNormal.normalized;
            var perpendicularDelta = Mathf.Abs(Vector3.Dot(
                observation.Position - stabilityBaseline.Position,
                baselineNormal));
            var normalAngleDelta = Mathf.Acos(Mathf.Clamp(
                Mathf.Abs(Vector3.Dot(baselineNormal, currentNormal)),
                -1f,
                1f)) * Mathf.Rad2Deg;
            var isStable = perpendicularDelta <= maximumPlaneDistanceDelta
                && normalAngleDelta <= maximumNormalAngleDelta;

            if (!isStable)
            {
                elapsed = 0f;
                stabilityBaseline = observation;
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            return elapsed >= requiredDuration;
        }

        public void Reset()
        {
            currentPlaneId = TrackableId.invalidId;
            elapsed = 0f;
            hasCurrentPlane = false;
            stabilityBaseline = default;
            hasStabilityBaseline = false;
        }
    }

    public static class WallPlaneBoundaryUtility
    {
        private const float EdgeEpsilon = 0.00001f;

        public static bool ContainsAll(
            IReadOnlyList<Vector2> boundary,
            IReadOnlyList<Vector2> points)
        {
            if (boundary == null || boundary.Count < 3 || points == null || points.Count == 0)
            {
                return false;
            }

            foreach (var point in points)
            {
                if (!ContainsPoint(boundary, point))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsPoint(IReadOnlyList<Vector2> boundary, Vector2 point)
        {
            var inside = false;
            for (var currentIndex = 0; currentIndex < boundary.Count; currentIndex++)
            {
                var previousIndex = currentIndex == 0 ? boundary.Count - 1 : currentIndex - 1;
                var start = boundary[previousIndex];
                var end = boundary[currentIndex];
                var segment = end - start;
                var fromStart = point - start;
                var cross = segment.x * fromStart.y - segment.y * fromStart.x;
                var projection = Vector2.Dot(fromStart, segment);
                if (segment.sqrMagnitude > EdgeEpsilon * EdgeEpsilon
                    && Mathf.Abs(cross) <= EdgeEpsilon
                    && projection >= -EdgeEpsilon
                    && projection <= segment.sqrMagnitude + EdgeEpsilon)
                {
                    return true;
                }

                if ((start.y > point.y) != (end.y > point.y)
                    && point.x < (end.x - start.x) * (point.y - start.y) / (end.y - start.y) + start.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }

    public enum WallEnvironmentDepthResult
    {
        Unavailable,
        Pending,
        Passed,
        Rejected
    }

    public static class WallEnvironmentDepthRules
    {
        public static bool AllowsPlacement(WallEnvironmentDepthResult result)
        {
            return result == WallEnvironmentDepthResult.Unavailable
                || result == WallEnvironmentDepthResult.Passed;
        }

        public static WallEnvironmentDepthResult Evaluate(
            IReadOnlyList<float> expectedDepthsMeters,
            IReadOnlyList<float> measuredDepthsMeters,
            int minimumValidSampleCount,
            float maximumDepthDeviationMeters,
            float maximumResidualSpreadMeters)
        {
            if (expectedDepthsMeters == null
                || measuredDepthsMeters == null
                || expectedDepthsMeters.Count == 0
                || expectedDepthsMeters.Count != measuredDepthsMeters.Count
                || minimumValidSampleCount <= 0
                || minimumValidSampleCount > expectedDepthsMeters.Count)
            {
                return WallEnvironmentDepthResult.Unavailable;
            }

            var maximumDeviation = Mathf.Max(0f, maximumDepthDeviationMeters);
            var maximumResidualSpread = Mathf.Max(0f, maximumResidualSpreadMeters);
            var minimumResidual = float.PositiveInfinity;
            var maximumResidual = float.NegativeInfinity;
            var validSampleCount = 0;
            var exceedsMaximumDeviation = false;

            for (var index = 0; index < expectedDepthsMeters.Count; index++)
            {
                var expectedDepth = expectedDepthsMeters[index];
                var measuredDepth = measuredDepthsMeters[index];
                if (!IsValidDepth(expectedDepth) || !IsValidDepth(measuredDepth))
                {
                    continue;
                }

                var residual = measuredDepth - expectedDepth;
                minimumResidual = Mathf.Min(minimumResidual, residual);
                maximumResidual = Mathf.Max(maximumResidual, residual);
                exceedsMaximumDeviation |= Mathf.Abs(residual) > maximumDeviation;
                validSampleCount++;
            }

            if (validSampleCount < minimumValidSampleCount)
            {
                return WallEnvironmentDepthResult.Unavailable;
            }

            if (exceedsMaximumDeviation || maximumResidual - minimumResidual > maximumResidualSpread)
            {
                return WallEnvironmentDepthResult.Rejected;
            }

            return WallEnvironmentDepthResult.Passed;
        }

        private static bool IsValidDepth(float depthMeters)
        {
            return depthMeters > 0f && !float.IsNaN(depthMeters) && !float.IsInfinity(depthMeters);
        }
    }

    public static class WallEnvironmentDepthCoordinateUtility
    {
        private const float MinimumMatrixDeterminant = 0.000001f;

        public static bool TryMapScreenPointToImagePixel(
            Vector2 screenPoint,
            Rect cameraPixelRect,
            Matrix4x4 displayMatrix,
            Vector2Int imageDimensions,
            out Vector2Int pixel)
        {
            pixel = default;
            if (cameraPixelRect.width <= 0f
                || cameraPixelRect.height <= 0f
                || imageDimensions.x <= 0
                || imageDimensions.y <= 0
                || !cameraPixelRect.Contains(screenPoint)
                || Mathf.Abs(displayMatrix.determinant) < MinimumMatrixDeterminant)
            {
                return false;
            }

            return TryMapScreenPointToImagePixelFromMatrix(
                screenPoint,
                cameraPixelRect,
                displayMatrix.inverse.transpose,
                imageDimensions,
                out pixel);
        }

        internal static bool TryMapScreenPointToImagePixelFromMatrix(
            Vector2 screenPoint,
            Rect cameraPixelRect,
            Matrix4x4 screenToImageMatrix,
            Vector2Int imageDimensions,
            out Vector2Int pixel)
        {
            pixel = default;
            if (cameraPixelRect.width <= 0f
                || cameraPixelRect.height <= 0f
                || imageDimensions.x <= 0
                || imageDimensions.y <= 0
                || !cameraPixelRect.Contains(screenPoint))
            {
                return false;
            }

            var screenUv = new Vector2(
                (screenPoint.x - cameraPixelRect.xMin) / cameraPixelRect.width,
                (screenPoint.y - cameraPixelRect.yMin) / cameraPixelRect.height);
            var imageHomogeneous = screenToImageMatrix
                * new Vector4(screenUv.x, screenUv.y, 1f, 0f);
            if (!IsFinite(imageHomogeneous.x)
                || !IsFinite(imageHomogeneous.y)
                || imageHomogeneous.x < 0f
                || imageHomogeneous.x > 1f
                || imageHomogeneous.y < 0f
                || imageHomogeneous.y > 1f)
            {
                return false;
            }

            pixel = new Vector2Int(
                Mathf.Min(Mathf.FloorToInt(imageHomogeneous.x * imageDimensions.x), imageDimensions.x - 1),
                Mathf.Min(Mathf.FloorToInt(imageHomogeneous.y * imageDimensions.y), imageDimensions.y - 1));
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class WallFootprintUtility
    {
        public static Vector3[] CreateWorldSamplePoints(Pose wallPose, float widthMeters, float heightMeters)
        {
            var points = new Vector3[5];
            FillWorldSamplePoints(wallPose, widthMeters, heightMeters, points);
            return points;
        }

        public static void FillWorldSamplePoints(
            Pose wallPose,
            float widthMeters,
            float heightMeters,
            Vector3[] destination)
        {
            if (destination == null || destination.Length < 5)
            {
                throw new ArgumentException("A footprint sample buffer must contain at least five elements.", nameof(destination));
            }

            var halfWidth = Mathf.Max(0f, widthMeters) * 0.5f;
            var halfHeight = Mathf.Max(0f, heightMeters) * 0.5f;
            var right = wallPose.rotation * Vector3.right;
            var up = wallPose.rotation * Vector3.up;

            destination[0] = wallPose.position;
            destination[1] = wallPose.position - right * halfWidth + up * halfHeight;
            destination[2] = wallPose.position + right * halfWidth + up * halfHeight;
            destination[3] = wallPose.position - right * halfWidth - up * halfHeight;
            destination[4] = wallPose.position + right * halfWidth - up * halfHeight;
        }
    }

    public static class WallViewingRules
    {
        private const float MinimumDirectionMagnitudeSquared = 0.000001f;

        public static bool IsAcceptable(
            float distanceMeters,
            Vector3 viewDirection,
            Vector3 surfaceNormal,
            float minimumDistanceMeters,
            float maximumDistanceMeters,
            float maximumViewAngleDegrees)
        {
            if (float.IsNaN(distanceMeters)
                || float.IsInfinity(distanceMeters)
                || maximumDistanceMeters < minimumDistanceMeters
                || distanceMeters < minimumDistanceMeters
                || distanceMeters > maximumDistanceMeters
                || viewDirection.sqrMagnitude < MinimumDirectionMagnitudeSquared
                || surfaceNormal.sqrMagnitude < MinimumDirectionMagnitudeSquared)
            {
                return false;
            }

            var headOnAlignment = Mathf.Abs(Vector3.Dot(viewDirection.normalized, surfaceNormal.normalized));
            var viewAngle = Mathf.Acos(Mathf.Clamp(headOnAlignment, -1f, 1f)) * Mathf.Rad2Deg;
            return viewAngle <= Mathf.Clamp(maximumViewAngleDegrees, 0f, 90f);
        }
    }

    public static class WallPlacementCommitRules
    {
        public static bool IsValid(
            bool isSessionTracking,
            float candidateAgeSeconds,
            float maximumCandidateAgeSeconds,
            PlaneAlignment alignment,
            TrackingState trackingState,
            Vector2 planeSize,
            Vector2 minimumPlaneSize,
            bool isSubsumed)
        {
            return isSessionTracking
                && candidateAgeSeconds >= 0f
                && candidateAgeSeconds <= Mathf.Max(0f, maximumCandidateAgeSeconds)
                && !isSubsumed
                && WallPlacementCandidateRules.IsEligible(
                    alignment,
                    trackingState,
                    planeSize,
                    minimumPlaneSize);
        }
    }

    public sealed class TrackingRecoveryGate
    {
        private readonly float requiredDuration;
        private readonly float maximumObservationInterval;
        private float elapsed;

        public TrackingRecoveryGate(float requiredDuration)
            : this(requiredDuration, float.PositiveInfinity)
        {
        }

        public TrackingRecoveryGate(float requiredDuration, float maximumObservationInterval)
        {
            this.requiredDuration = Mathf.Max(0f, requiredDuration);
            this.maximumObservationInterval = Mathf.Max(0f, maximumObservationInterval);
        }

        public bool Update(bool isTracking, float deltaTime)
        {
            if (!isTracking)
            {
                elapsed = 0f;
                return false;
            }

            if (deltaTime > maximumObservationInterval)
            {
                elapsed = 0f;
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            return elapsed >= requiredDuration;
        }

        public void Reset()
        {
            elapsed = 0f;
        }
    }

    public static class WallPlacementPoseUtility
    {
        private const float MinimumNormalMagnitudeSquared = 0.000001f;

        public static bool TryCreateWallPose(
            Vector3 hitPosition,
            Vector3 surfaceNormal,
            Vector3 viewerPosition,
            float wallOffset,
            out Pose pose)
        {
            var horizontalNormal = Vector3.ProjectOnPlane(surfaceNormal, Vector3.up);
            if (horizontalNormal.sqrMagnitude < MinimumNormalMagnitudeSquared)
            {
                pose = default;
                return false;
            }

            horizontalNormal.Normalize();
            var directionToViewer = Vector3.ProjectOnPlane(viewerPosition - hitPosition, Vector3.up);
            if (directionToViewer.sqrMagnitude >= MinimumNormalMagnitudeSquared
                && Vector3.Dot(horizontalNormal, directionToViewer) < 0f)
            {
                horizontalNormal = -horizontalNormal;
            }

            pose = new Pose(
                hitPosition + horizontalNormal * Mathf.Max(0f, wallOffset),
                Quaternion.LookRotation(horizontalNormal, Vector3.up));
            return true;
        }
    }

    public enum WallPlacementState
    {
        Scanning,
        CandidateValid,
        Placing,
        Placed,
        Repositioning
    }

    public sealed class WallPlacementStateMachine
    {
        private bool candidateAvailable;

        public WallPlacementState State { get; private set; } = WallPlacementState.Scanning;

        public void SetCandidateAvailable(bool available)
        {
            candidateAvailable = available;
            if (State == WallPlacementState.Scanning || State == WallPlacementState.CandidateValid)
            {
                State = available ? WallPlacementState.CandidateValid : WallPlacementState.Scanning;
            }
        }

        public bool TryBeginPlacement()
        {
            if (State != WallPlacementState.CandidateValid || !candidateAvailable)
            {
                return false;
            }

            State = WallPlacementState.Placing;
            return true;
        }

        public void CompletePlacement(bool succeeded)
        {
            if (State != WallPlacementState.Placing)
            {
                return;
            }

            State = succeeded
                ? WallPlacementState.Placed
                : candidateAvailable
                    ? WallPlacementState.CandidateValid
                    : WallPlacementState.Scanning;
        }

        public bool BeginRepositioning()
        {
            if (State != WallPlacementState.Placed)
            {
                return false;
            }

            State = WallPlacementState.Repositioning;
            return true;
        }

        public void FinishRepositioning()
        {
            if (State == WallPlacementState.Repositioning)
            {
                State = candidateAvailable ? WallPlacementState.CandidateValid : WallPlacementState.Scanning;
            }
        }

        public void Reset()
        {
            candidateAvailable = false;
            State = WallPlacementState.Scanning;
        }

        public void HandlePlacementLost()
        {
            candidateAvailable = false;
            State = WallPlacementState.Scanning;
        }
    }
}
