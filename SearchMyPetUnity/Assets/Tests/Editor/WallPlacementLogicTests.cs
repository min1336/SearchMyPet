using NUnit.Framework;
using SearchMyPet.AR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.Tests.Editor
{
    public sealed class WallPlacementLogicTests
    {
        [Test]
        public void IsEligible_AcceptsTrackedVerticalPlaneAtMinimumSize()
        {
            Assert.That(
                WallPlacementCandidateRules.IsEligible(
                    PlaneAlignment.Vertical,
                    TrackingState.Tracking,
                    new Vector2(0.4f, 0.3f),
                    new Vector2(0.3f, 0.3f)),
                Is.True);
        }

        [Test]
        public void IsEligible_RejectsHorizontalPlane()
        {
            Assert.That(
                WallPlacementCandidateRules.IsEligible(
                    PlaneAlignment.HorizontalUp,
                    TrackingState.Tracking,
                    new Vector2(1f, 1f),
                    new Vector2(0.3f, 0.3f)),
                Is.False);
        }

        [Test]
        public void IsEligible_RejectsLimitedTracking()
        {
            Assert.That(
                WallPlacementCandidateRules.IsEligible(
                    PlaneAlignment.Vertical,
                    TrackingState.Limited,
                    new Vector2(1f, 1f),
                    new Vector2(0.3f, 0.3f)),
                Is.False);
        }

        [Test]
        public void IsEligible_RejectsPlaneSmallerThanMinimum()
        {
            Assert.That(
                WallPlacementCandidateRules.IsEligible(
                    PlaneAlignment.Vertical,
                    TrackingState.Tracking,
                    new Vector2(0.29f, 1f),
                    new Vector2(0.3f, 0.3f)),
                Is.False);
        }

        [Test]
        public void StabilityTracker_FirstObservationCannotBeStableWithoutAComparisonFrame()
        {
            var tracker = new WallCandidateStabilityTracker(0.5f, 0.02f, 2f, 0.03f);
            var observation = new WallCandidateObservation(
                new TrackableId(1, 2),
                TrackingState.Tracking,
                new Vector3(1f, 2f, 3f),
                Quaternion.identity,
                new Vector2(1f, 1f));

            Assert.That(tracker.Update(observation, 1f), Is.False);
        }

        [Test]
        public void StabilityTracker_PositionJumpRestartsContinuousStableTime()
        {
            var tracker = new WallCandidateStabilityTracker(0.5f, 0.02f, 2f, 0.03f);
            var planeId = new TrackableId(1, 2);

            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.1f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.01f, 0f, 0f), Quaternion.identity, Vector2.one), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.05f, 0f, 0f), Quaternion.identity, Vector2.one), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.051f, 0f, 0f), Quaternion.identity, Vector2.one), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.052f, 0f, 0f), Quaternion.identity, Vector2.one), 0.2f), Is.True);
        }

        [Test]
        public void StabilityTracker_CumulativeCreepBeyondThresholdNeverBecomesStable()
        {
            var tracker = new WallCandidateStabilityTracker(0.5f, 0.02f, 2f, 0.03f);
            var planeId = new TrackableId(1, 2);

            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.1f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.014f, 0f, 0f), Quaternion.identity, Vector2.one), 0.2f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.028f, 0f, 0f), Quaternion.identity, Vector2.one), 0.2f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, new Vector3(0.042f, 0f, 0f), Quaternion.identity, Vector2.one), 0.2f), Is.False);
        }

        [Test]
        public void StabilityTracker_CumulativeRotationAndSizeCreepNeverBecomesStable()
        {
            var tracker = new WallCandidateStabilityTracker(0.5f, 0.02f, 2f, 0.03f);
            var planeId = new TrackableId(1, 2);

            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.1f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 1.4f, 0f), new Vector2(1.02f, 1f)), 0.2f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 2.8f, 0f), new Vector2(1.04f, 1f)), 0.2f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 4.2f, 0f), new Vector2(1.06f, 1f)), 0.2f), Is.False);
        }

        [Test]
        public void StabilityTracker_LongFrameGapRestartsObservationWindow()
        {
            var tracker = new WallCandidateStabilityTracker(0.5f, 0.02f, 2f, 0.03f, 0.2f);
            var observation = new WallCandidateObservation(
                new TrackableId(1, 2),
                TrackingState.Tracking,
                Vector3.zero,
                Quaternion.identity,
                Vector2.one);

            Assert.That(tracker.Update(observation, 0.1f), Is.False);
            Assert.That(tracker.Update(observation, 0.6f), Is.False);
            Assert.That(tracker.Update(observation, 0.2f), Is.False);
        }

        [Test]
        public void StabilityTracker_RotationOrSizeJumpRestartsContinuousStableTime()
        {
            var tracker = new WallCandidateStabilityTracker(0.4f, 0.02f, 2f, 0.03f);
            var planeId = new TrackableId(1, 2);

            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.1f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 1f, 0f), new Vector2(1.01f, 1f)), 0.2f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 5f, 0f), new Vector2(1.01f, 1f)), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.Euler(0f, 5f, 0f), new Vector2(1.08f, 1f)), 0.3f), Is.False);
        }

        [Test]
        public void StabilityTracker_LimitedTrackingClearsAccumulatedStableTime()
        {
            var tracker = new WallCandidateStabilityTracker(0.4f, 0.02f, 2f, 0.03f);
            var planeId = new TrackableId(1, 2);

            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.1f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Limited, Vector3.zero, Quaternion.identity, Vector2.one), 0.3f), Is.False);
            Assert.That(tracker.Update(new WallCandidateObservation(
                planeId, TrackingState.Tracking, Vector3.zero, Quaternion.identity, Vector2.one), 0.3f), Is.False);
        }

        [Test]
        public void FootprintRules_AcceptFiveConsistentSamplesOnOnePlane()
        {
            var planeId = new TrackableId(1, 2);
            var samples = new[]
            {
                new WallFootprintSample(planeId, 2.00f, Vector3.back),
                new WallFootprintSample(planeId, 2.03f, Vector3.back),
                new WallFootprintSample(planeId, 2.01f, Vector3.back),
                new WallFootprintSample(planeId, 2.04f, Vector3.back),
                new WallFootprintSample(planeId, 2.02f, Vector3.back)
            };

            Assert.That(WallFootprintRules.AreConsistent(samples, 5, 0.15f, 5f), Is.True);
        }

        [Test]
        public void FootprintRules_RejectWhenAnyOfFivePointsHitsAnotherPlane()
        {
            var planeId = new TrackableId(1, 2);
            var samples = new[]
            {
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(new TrackableId(3, 4), 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back)
            };

            Assert.That(WallFootprintRules.AreConsistent(samples, 5, 0.15f, 5f), Is.False);
        }

        [Test]
        public void FootprintRules_RejectDepthSpreadBeyondTolerance()
        {
            var planeId = new TrackableId(1, 2);
            var samples = new[]
            {
                new WallFootprintSample(planeId, 2.00f, Vector3.back),
                new WallFootprintSample(planeId, 2.02f, Vector3.back),
                new WallFootprintSample(planeId, 2.01f, Vector3.back),
                new WallFootprintSample(planeId, 2.20f, Vector3.back),
                new WallFootprintSample(planeId, 2.03f, Vector3.back)
            };

            Assert.That(WallFootprintRules.AreConsistent(samples, 5, 0.15f, 5f), Is.False);
        }

        [Test]
        public void FootprintRules_RejectSurfaceNormalDifferenceBeyondTolerance()
        {
            var planeId = new TrackableId(1, 2);
            var samples = new[]
            {
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Quaternion.Euler(0f, 8f, 0f) * Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back),
                new WallFootprintSample(planeId, 2f, Vector3.back)
            };

            Assert.That(WallFootprintRules.AreConsistent(samples, 5, 0.15f, 5f), Is.False);
        }

        [Test]
        public void EnvironmentDepthRules_ApiExistsForOptionalDepthValidation()
        {
            var rulesType = typeof(WallFootprintRules).Assembly.GetType(
                "SearchMyPet.AR.WallEnvironmentDepthRules");

            Assert.That(rulesType, Is.Not.Null,
                "Optional environment-depth validation rules have not been implemented yet.");
        }

        [Test]
        public void EnvironmentDepthRules_PassMeasurementsThatMatchThePlane()
        {
            var expected = new[] { 2.00f, 1.94f, 2.06f, 1.96f, 2.04f };
            var measured = new[] { 2.01f, 1.95f, 2.04f, 1.98f, 2.03f };

            Assert.That(
                WallEnvironmentDepthRules.Evaluate(expected, measured, 3, 0.20f, 0.08f),
                Is.EqualTo(WallEnvironmentDepthResult.Passed));
        }

        [Test]
        public void EnvironmentDepthRules_RejectAForegroundObjectInTheCharacterArea()
        {
            var expected = new[] { 2.00f, 1.98f, 2.02f, 1.99f, 2.01f };
            var measured = new[] { 2.01f, 1.99f, 1.25f, 2.00f, 2.02f };

            Assert.That(
                WallEnvironmentDepthRules.Evaluate(expected, measured, 3, 0.20f, 0.08f),
                Is.EqualTo(WallEnvironmentDepthResult.Rejected));
        }

        [Test]
        public void EnvironmentDepthRules_RejectResidualSpreadInsteadOfRawObliqueWallDepth()
        {
            var expected = new[] { 2.00f, 1.82f, 2.18f, 1.84f, 2.16f };
            var measured = new[] { 2.00f, 1.72f, 2.28f, 1.78f, 2.22f };

            Assert.That(
                WallEnvironmentDepthRules.Evaluate(expected, measured, 3, 0.20f, 0.08f),
                Is.EqualTo(WallEnvironmentDepthResult.Rejected));
        }

        [Test]
        public void EnvironmentDepthRules_UseAvailableSubsetWhenThreeSamplesAreValid()
        {
            var expected = new[] { 2.00f, 1.98f, 2.02f, 1.99f, 2.01f };
            var measured = new[] { 2.01f, float.NaN, 2.03f, 2.00f, 0f };

            Assert.That(
                WallEnvironmentDepthRules.Evaluate(expected, measured, 3, 0.20f, 0.08f),
                Is.EqualTo(WallEnvironmentDepthResult.Passed));
        }

        [Test]
        public void EnvironmentDepthRules_FallBackWhenTooFewSamplesAreValid()
        {
            var expected = new[] { 2.00f, 1.98f, 2.02f, 1.99f, 2.01f };
            var measured = new[] { 2.01f, float.NaN, float.PositiveInfinity, 0f, -1f };

            Assert.That(
                WallEnvironmentDepthRules.Evaluate(expected, measured, 3, 0.20f, 0.08f),
                Is.EqualTo(WallEnvironmentDepthResult.Unavailable));
        }

        [TestCase(WallEnvironmentDepthResult.Unavailable, true)]
        [TestCase(WallEnvironmentDepthResult.Passed, true)]
        [TestCase(WallEnvironmentDepthResult.Rejected, false)]
        public void EnvironmentDepthRules_OnlyRejectAnExplicitDepthFailure(
            WallEnvironmentDepthResult result,
            bool expectedPlacementAllowed)
        {
            Assert.That(
                WallEnvironmentDepthRules.AllowsPlacement(result),
                Is.EqualTo(expectedPlacementAllowed));
        }

        [Test]
        public void EnvironmentDepthCoordinates_MapScreenThroughInverseDisplayMatrix()
        {
            var displayMatrix = Matrix4x4.identity;
            displayMatrix.m00 = 0.5f;
            displayMatrix.m11 = 0.5f;
            displayMatrix.m20 = 0.25f;
            displayMatrix.m21 = 0.125f;

            var mapped = TryMapEnvironmentDepthPixel(
                new Vector2(85f, 45f),
                new Rect(10f, 20f, 200f, 100f),
                displayMatrix,
                new Vector2Int(100, 100),
                out var pixel);

            Assert.That(mapped, Is.True);
            Assert.That(pixel, Is.EqualTo(new Vector2Int(25, 25)));
        }

        [Test]
        public void EnvironmentDepthCoordinates_MapPortraitRotationThroughInverseDisplayMatrix()
        {
            var displayMatrix = Matrix4x4.zero;
            displayMatrix.m10 = 1f;
            displayMatrix.m01 = -1f;
            displayMatrix.m21 = 1f;
            displayMatrix.m22 = 1f;
            displayMatrix.m33 = 1f;

            var mapped = TryMapEnvironmentDepthPixel(
                new Vector2(75f, 75f),
                new Rect(0f, 0f, 100f, 100f),
                displayMatrix,
                new Vector2Int(100, 100),
                out var pixel);

            Assert.That(mapped, Is.True);
            Assert.That(pixel, Is.EqualTo(new Vector2Int(25, 75)));
        }

        [Test]
        public void EnvironmentDepthCoordinates_RejectPointsOutsideTheCameraViewport()
        {
            var mapped = TryMapEnvironmentDepthPixel(
                new Vector2(-1f, 50f),
                new Rect(0f, 0f, 100f, 100f),
                Matrix4x4.identity,
                new Vector2Int(100, 100),
                out _);

            Assert.That(mapped, Is.False);
        }

        private static bool TryMapEnvironmentDepthPixel(
            Vector2 screenPoint,
            Rect cameraPixelRect,
            Matrix4x4 displayMatrix,
            Vector2Int imageDimensions,
            out Vector2Int pixel)
        {
            var utilityType = typeof(WallFootprintRules).Assembly.GetType(
                "SearchMyPet.AR.WallEnvironmentDepthCoordinateUtility");
            Assert.That(utilityType, Is.Not.Null,
                "Screen-to-depth coordinate mapping has not been implemented yet.");

            var method = utilityType.GetMethod("TryMapScreenPointToImagePixel");
            Assert.That(method, Is.Not.Null);
            var arguments = new object[]
            {
                screenPoint,
                cameraPixelRect,
                displayMatrix,
                imageDimensions,
                Vector2Int.zero
            };
            var mapped = (bool)method.Invoke(null, arguments);
            pixel = (Vector2Int)arguments[4];
            return mapped;
        }

        [Test]
        public void FootprintUtility_CreatesCenterAndFourWallSpaceCorners()
        {
            var points = WallFootprintUtility.CreateWorldSamplePoints(
                new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity),
                0.4f,
                0.6f);

            Assert.That(points, Has.Length.EqualTo(5));
            Assert.That(points[0], Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(points[1], Is.EqualTo(new Vector3(0.8f, 2.3f, 3f)));
            Assert.That(points[2], Is.EqualTo(new Vector3(1.2f, 2.3f, 3f)));
            Assert.That(points[3], Is.EqualTo(new Vector3(0.8f, 1.7f, 3f)));
            Assert.That(points[4], Is.EqualTo(new Vector3(1.2f, 1.7f, 3f)));
        }

        [TestCase(0.2f)]
        [TestCase(4.5f)]
        public void ViewingRules_RejectDistanceOutsideConfiguredRange(float distanceMeters)
        {
            Assert.That(
                WallViewingRules.IsAcceptable(
                    distanceMeters,
                    Vector3.forward,
                    Vector3.back,
                    0.4f,
                    4f,
                    60f),
                Is.False);
        }

        [Test]
        public void ViewingRules_RejectGlancingViewBeyondConfiguredAngle()
        {
            var surfaceNormal = Quaternion.Euler(0f, 70f, 0f) * Vector3.back;

            Assert.That(
                WallViewingRules.IsAcceptable(
                    2f,
                    Vector3.forward,
                    surfaceNormal,
                    0.4f,
                    4f,
                    60f),
                Is.False);
        }

        [Test]
        public void ViewingRules_AcceptHeadOnWallWithinConfiguredDistance()
        {
            Assert.That(
                WallViewingRules.IsAcceptable(
                    2f,
                    Vector3.forward,
                    Vector3.back,
                    0.4f,
                    4f,
                    60f),
                Is.True);
        }

        [Test]
        public void TrackingRecoveryGate_RequiresContinuousTrackingBeforeContentReturns()
        {
            var gate = new TrackingRecoveryGate(0.5f);

            Assert.That(gate.Update(true, 0.3f), Is.False);
            Assert.That(gate.Update(false, 0.2f), Is.False);
            Assert.That(gate.Update(true, 0.3f), Is.False);
            Assert.That(gate.Update(true, 0.2f), Is.True);
        }

        [Test]
        public void TrackingRecoveryGate_LongFrameGapDoesNotRestoreContent()
        {
            var gate = new TrackingRecoveryGate(0.5f, 0.2f);

            Assert.That(gate.Update(true, 0.1f), Is.False);
            Assert.That(gate.Update(true, 0.6f), Is.False);
            Assert.That(gate.Update(true, 0.2f), Is.False);
        }

        [TestCase(false, 0, PlaneAlignment.Vertical, TrackingState.Tracking, false)]
        [TestCase(true, 2, PlaneAlignment.Vertical, TrackingState.Tracking, false)]
        [TestCase(true, 0, PlaneAlignment.Vertical, TrackingState.Limited, false)]
        [TestCase(true, 0, PlaneAlignment.HorizontalUp, TrackingState.Tracking, false)]
        [TestCase(true, 0, PlaneAlignment.Vertical, TrackingState.Tracking, true)]
        public void CommitRules_RejectStaleOrCurrentlyInvalidCandidate(
            bool isSessionTracking,
            int candidateAgeFrames,
            PlaneAlignment alignment,
            TrackingState trackingState,
            bool isSubsumed)
        {
            Assert.That(
                WallPlacementCommitRules.IsValid(
                    isSessionTracking,
                    candidateAgeFrames,
                    alignment,
                    trackingState,
                    new Vector2(1f, 1f),
                    new Vector2(0.3f, 0.3f),
                    isSubsumed),
                Is.False);
        }

        [Test]
        public void CommitRules_AcceptFreshTrackedVerticalCandidate()
        {
            Assert.That(
                WallPlacementCommitRules.IsValid(
                    true,
                    1,
                    PlaneAlignment.Vertical,
                    TrackingState.Tracking,
                    new Vector2(0.4f, 0.3f),
                    new Vector2(0.3f, 0.3f),
                    false),
                Is.True);
        }

        [Test]
        public void PreviewMaterial_UsesTransparentSurfaceAndAlpha()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/WallPlacementPreview.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.color.a, Is.InRange(0.35f, 0.5f));
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(material.GetTag("RenderType", false, string.Empty), Is.EqualTo("Transparent"));
            Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent));
        }

        [Test]
        public void ValidationScene_ConfiguresOptionalEnvironmentDepthWithoutASecondCamera()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/WallPlacementValidation.unity");
            var cameraObject = GameObject.Find("AR Camera");

            Assert.That(cameraObject, Is.Not.Null);
            Assert.That(cameraObject.GetComponents<Camera>(), Has.Length.EqualTo(1));
            var originObject = GameObject.Find("XR Origin (AR)");
            Assert.That(originObject, Is.Not.Null);
            Assert.That(originObject.GetComponent<AROcclusionManager>(), Is.Null);
            var occlusionManager = cameraObject.GetComponent<AROcclusionManager>();
            Assert.That(occlusionManager, Is.Not.Null);
            Assert.That(occlusionManager.requestedEnvironmentDepthMode, Is.EqualTo(EnvironmentDepthMode.Fastest));
            Assert.That(occlusionManager.environmentDepthTemporalSmoothingRequested, Is.True);
            Assert.That(occlusionManager.requestedOcclusionPreferenceMode, Is.EqualTo(OcclusionPreferenceMode.NoOcclusion));

            var validator = Object.FindFirstObjectByType<WallEnvironmentDepthValidator>();
            var reticle = Object.FindFirstObjectByType<PlacementReticle>();
            Assert.That(validator, Is.Not.Null);
            Assert.That(reticle, Is.Not.Null);
            Assert.That(GameObject.Find("Wall Placement Reticle").GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            var statusCanvas = GameObject.Find("Status Canvas");
            var placeAction = GameObject.Find("SearchMyPetAppUI").transform
                .Find("Character Paint UI/Safe Area/Paint Quick Controls/Capture Button").gameObject;
            Assert.That(statusCanvas.transform.Find("Place Character Button"), Is.Null);
            Assert.That(placeAction.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(68f, 68f)));
            Assert.That(placeAction.GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("Knob"));
            var whiteFill = placeAction.transform.Find("White Fill").GetComponent<UnityEngine.UI.Image>();
            Assert.That(whiteFill.color, Is.EqualTo(Color.white));
            Assert.That(whiteFill.rectTransform.sizeDelta, Is.EqualTo(new Vector2(58f, 58f)));
            Assert.That(placeAction.GetComponent<UnityEngine.UI.Button>().colors.disabledColor, Is.EqualTo(Color.white));
            var controller = Object.FindFirstObjectByType<WallPlacementController>();
            var controllerProperties = new SerializedObject(controller);
            Assert.That(controllerProperties.FindProperty("placeButton").objectReferenceValue,
                Is.SameAs(placeAction.GetComponent<UnityEngine.UI.Button>()));
            Assert.That(placeAction.GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(placeAction.GetComponent<UnityEngine.UI.Button>().onClick.GetPersistentMethodName(0),
                Is.EqualTo(nameof(WallPlacementController.PlaceAtCandidate)));
            foreach (var lens in statusCanvas.transform.Find("Camera Lens Selector").GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                Assert.That(lens.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(30f, 30f)));
                Assert.That(lens.image.sprite.name, Is.EqualTo("Knob"));
            }

            var validatorProperties = new SerializedObject(validator);
            Assert.That(
                validatorProperties.FindProperty("occlusionManager").objectReferenceValue,
                Is.SameAs(occlusionManager));
            Assert.That(
                validatorProperties.FindProperty("arCamera").objectReferenceValue,
                Is.SameAs(cameraObject.GetComponent<Camera>()));
            Assert.That(
                validatorProperties.FindProperty("cameraManager").objectReferenceValue,
                Is.SameAs(cameraObject.GetComponent<ARCameraManager>()));

            var reticleProperties = new SerializedObject(reticle);
            Assert.That(
                reticleProperties.FindProperty("environmentDepthValidator").objectReferenceValue,
                Is.SameAs(validator));

            var config = AssetDatabase.LoadAssetAtPath<WallPlacementConfig>(
                "Assets/Settings/WallPlacementConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.UseEnvironmentDepthValidation, Is.True);
            Assert.That(config.MinimumValidEnvironmentDepthSamples, Is.EqualTo(3));
            Assert.That(config.MaximumEnvironmentDepthDeviationMeters, Is.EqualTo(0.25f));
            Assert.That(config.MaximumEnvironmentDepthResidualSpreadMeters, Is.EqualTo(0.12f));
            Assert.That(config.EnvironmentDepthValidationIntervalSeconds, Is.EqualTo(0.1f));
            Assert.That(
                reticleProperties.FindProperty("config").objectReferenceValue,
                Is.SameAs(config));
        }

        [Test]
        public void TryCreateWallPose_FacesViewerOffsetsFromWallAndStaysUpright()
        {
            var created = WallPlacementPoseUtility.TryCreateWallPose(
                new Vector3(1f, 2f, 3f),
                Vector3.forward,
                new Vector3(1f, 2f, 0f),
                0.02f,
                out var pose);

            Assert.That(created, Is.True);
            Assert.That(Vector3.Distance(pose.position, new Vector3(1f, 2f, 2.98f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void TryCreateWallPose_RejectsFloorNormal()
        {
            Assert.That(
                WallPlacementPoseUtility.TryCreateWallPose(
                    Vector3.zero,
                    Vector3.up,
                    new Vector3(0f, 1f, -1f),
                    0.02f,
                    out _),
                Is.False);
        }

        [Test]
        public void StateMachine_AllowsOnlyOnePlacementUntilRepositioning()
        {
            var stateMachine = new WallPlacementStateMachine();

            stateMachine.SetCandidateAvailable(true);
            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.CandidateValid));
            Assert.That(stateMachine.TryBeginPlacement(), Is.True);
            stateMachine.CompletePlacement(true);

            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.Placed));
            Assert.That(stateMachine.TryBeginPlacement(), Is.False);
        }

        [Test]
        public void CaptureButton_StaysVisibleAfterPlacementAndStartsRepositioning()
        {
            var root = new GameObject("Controller");
            var character = new GameObject("Character");
            var buttonObject = new GameObject("Capture", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            try
            {
                var controller = root.AddComponent<WallPlacementController>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(WallPlacementController).GetMethod("Awake", flags)?.Invoke(controller, null);
                typeof(WallPlacementController).GetField("placeButton", flags)
                    ?.SetValue(controller, buttonObject.GetComponent<UnityEngine.UI.Button>());
                var stateMachine = (WallPlacementStateMachine)typeof(WallPlacementController)
                    .GetField("stateMachine", flags)?.GetValue(controller);
                stateMachine.SetCandidateAvailable(true);
                stateMachine.TryBeginPlacement();
                stateMachine.CompletePlacement(true);
                ((CharacterColorPalette)typeof(WallPlacementController).GetField("colorPalette", flags)
                    ?.GetValue(controller)).SetCharacter(character);

                typeof(WallPlacementController).GetMethod("RefreshUi", flags)
                    ?.Invoke(controller, new object[] { null });

                var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
                Assert.That(buttonObject.activeSelf, Is.True);
                Assert.That(button.interactable, Is.True);

                controller.PlaceAtCandidate();

                Assert.That(controller.State, Is.EqualTo(WallPlacementState.Scanning));
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(buttonObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StateMachine_FailedAnchorReturnsToCurrentCandidate()
        {
            var stateMachine = new WallPlacementStateMachine();

            stateMachine.SetCandidateAvailable(true);
            Assert.That(stateMachine.TryBeginPlacement(), Is.True);
            stateMachine.CompletePlacement(false);

            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.CandidateValid));
        }

        [Test]
        public void StateMachine_ResetReturnsToScanning()
        {
            var stateMachine = new WallPlacementStateMachine();
            stateMachine.SetCandidateAvailable(true);
            stateMachine.TryBeginPlacement();
            stateMachine.CompletePlacement(true);

            stateMachine.Reset();

            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.Scanning));
        }

        [Test]
        public void StateMachine_RepositioningClearsPlacedLockAndReturnsToCandidate()
        {
            var stateMachine = new WallPlacementStateMachine();
            stateMachine.SetCandidateAvailable(true);
            stateMachine.TryBeginPlacement();
            stateMachine.CompletePlacement(true);

            Assert.That(stateMachine.BeginRepositioning(), Is.True);
            stateMachine.SetCandidateAvailable(true);
            stateMachine.FinishRepositioning();

            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.CandidateValid));
            Assert.That(stateMachine.TryBeginPlacement(), Is.True);
        }

        [Test]
        public void StateMachine_LostAnchorReturnsToScanningInsteadOfStayingPlaced()
        {
            var stateMachine = new WallPlacementStateMachine();
            stateMachine.SetCandidateAvailable(true);
            stateMachine.TryBeginPlacement();
            stateMachine.CompletePlacement(true);

            stateMachine.HandlePlacementLost();

            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.Scanning));
            stateMachine.SetCandidateAvailable(true);
            Assert.That(stateMachine.State, Is.EqualTo(WallPlacementState.CandidateValid));
        }
    }
}
