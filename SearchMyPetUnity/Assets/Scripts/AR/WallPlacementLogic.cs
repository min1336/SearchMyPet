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
            Quaternion rotation,
            Vector2 size)
        {
            PlaneId = planeId;
            TrackingState = trackingState;
            Position = position;
            Rotation = rotation;
            Size = size;
        }

        public TrackableId PlaneId { get; }
        public TrackingState TrackingState { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector2 Size { get; }
    }

    public sealed class WallCandidateStabilityTracker
    {
        private readonly float requiredDuration;
        private readonly float maximumPositionDelta;
        private readonly float maximumRotationDelta;
        private readonly float maximumSizeDelta;
        private readonly float maximumObservationInterval;
        private TrackableId currentPlaneId;
        private float elapsed;
        private bool hasCurrentPlane;
        private WallCandidateObservation stabilityBaseline;
        private bool hasStabilityBaseline;

        public WallCandidateStabilityTracker(
            float requiredDuration,
            float maximumPositionDelta,
            float maximumRotationDelta,
            float maximumSizeDelta)
            : this(
                requiredDuration,
                maximumPositionDelta,
                maximumRotationDelta,
                maximumSizeDelta,
                float.PositiveInfinity)
        {
        }

        public WallCandidateStabilityTracker(
            float requiredDuration,
            float maximumPositionDelta,
            float maximumRotationDelta,
            float maximumSizeDelta,
            float maximumObservationInterval)
        {
            this.requiredDuration = Mathf.Max(0f, requiredDuration);
            this.maximumPositionDelta = Mathf.Max(0f, maximumPositionDelta);
            this.maximumRotationDelta = Mathf.Max(0f, maximumRotationDelta);
            this.maximumSizeDelta = Mathf.Max(0f, maximumSizeDelta);
            this.maximumObservationInterval = Mathf.Max(0f, maximumObservationInterval);
        }

        public bool Update(WallCandidateObservation observation, float deltaTime)
        {
            if (observation.PlaneId == TrackableId.invalidId
                || observation.TrackingState != TrackingState.Tracking)
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

            var isStable = Vector3.Distance(stabilityBaseline.Position, observation.Position) <= maximumPositionDelta
                && Quaternion.Angle(stabilityBaseline.Rotation, observation.Rotation) <= maximumRotationDelta
                && Vector2.Distance(stabilityBaseline.Size, observation.Size) <= maximumSizeDelta;

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

    public readonly struct WallFootprintSample
    {
        public WallFootprintSample(TrackableId planeId, float distanceMeters, Vector3 surfaceNormal)
        {
            PlaneId = planeId;
            DistanceMeters = distanceMeters;
            SurfaceNormal = surfaceNormal;
        }

        public TrackableId PlaneId { get; }
        public float DistanceMeters { get; }
        public Vector3 SurfaceNormal { get; }
    }

    public static class WallFootprintRules
    {
        private const float MinimumNormalMagnitudeSquared = 0.000001f;

        public static bool AreConsistent(
            IReadOnlyList<WallFootprintSample> samples,
            int requiredSampleCount,
            float maximumDepthSpreadMeters,
            float maximumNormalAngleDegrees)
        {
            if (samples == null || requiredSampleCount <= 0 || samples.Count != requiredSampleCount)
            {
                return false;
            }

            var referencePlaneId = samples[0].PlaneId;
            var referenceNormal = samples[0].SurfaceNormal;
            if (referencePlaneId == TrackableId.invalidId
                || referenceNormal.sqrMagnitude < MinimumNormalMagnitudeSquared)
            {
                return false;
            }

            referenceNormal.Normalize();
            var minimumDepth = float.PositiveInfinity;
            var maximumDepth = float.NegativeInfinity;
            var allowedNormalAngle = Mathf.Max(0f, maximumNormalAngleDegrees);

            foreach (var sample in samples)
            {
                if (sample.PlaneId != referencePlaneId
                    || sample.DistanceMeters < 0f
                    || float.IsNaN(sample.DistanceMeters)
                    || float.IsInfinity(sample.DistanceMeters)
                    || sample.SurfaceNormal.sqrMagnitude < MinimumNormalMagnitudeSquared
                    || Vector3.Angle(referenceNormal, sample.SurfaceNormal) > allowedNormalAngle)
                {
                    return false;
                }

                minimumDepth = Mathf.Min(minimumDepth, sample.DistanceMeters);
                maximumDepth = Mathf.Max(maximumDepth, sample.DistanceMeters);
            }

            return maximumDepth - minimumDepth <= Mathf.Max(0f, maximumDepthSpreadMeters);
        }
    }

    public enum WallEnvironmentDepthResult
    {
        Unavailable,
        Passed,
        Rejected
    }

    public static class WallEnvironmentDepthRules
    {
        public static bool AllowsPlacement(WallEnvironmentDepthResult result)
        {
            return result != WallEnvironmentDepthResult.Rejected;
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

            var screenUv = new Vector2(
                (screenPoint.x - cameraPixelRect.xMin) / cameraPixelRect.width,
                (screenPoint.y - cameraPixelRect.yMin) / cameraPixelRect.height);
            var imageHomogeneous = displayMatrix.inverse.transpose
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
            int candidateAgeFrames,
            PlaneAlignment alignment,
            TrackingState trackingState,
            Vector2 planeSize,
            Vector2 minimumPlaneSize,
            bool isSubsumed)
        {
            return isSessionTracking
                && candidateAgeFrames >= 0
                && candidateAgeFrames <= 1
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
