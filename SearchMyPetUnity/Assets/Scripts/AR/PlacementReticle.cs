using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    public sealed class PlacementReticle : MonoBehaviour
    {
        private static readonly List<ARRaycastHit> RaycastHits = new();
        private static readonly List<ARRaycastHit> FootprintRaycastHits = new();
        private static readonly List<WallFootprintSample> FootprintSamples = new(5);

        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private WallEnvironmentDepthValidator environmentDepthValidator;
        [SerializeField] private WallPlacementConfig config;
        [SerializeField] private Image reticleImage;
        [SerializeField] private GameObject previewPrefab;
        [SerializeField] private Material previewMaterial;

        private WallCandidateStabilityTracker stabilityTracker;
        private readonly Vector3[] footprintWorldSamplePoints = new Vector3[5];
        private readonly Vector2[] footprintScreenSamplePoints = new Vector2[5];
        private readonly float[] footprintExpectedDepthsMeters = new float[5];
        private GameObject previewInstance;
        private TrackableId lastLoggedCandidateId = TrackableId.invalidId;
        private int candidateValidatedFrame = -1;
        private bool scanningActive = true;

        public bool HasValidCandidate { get; private set; }
        public bool IsPreviewVisible => previewInstance != null && previewInstance.activeSelf;
        public Pose CandidatePose { get; private set; }
        public ARPlane CandidatePlane { get; private set; }

        public bool TryGetCurrentValidCandidate(out ARPlane plane, out Pose pose)
        {
            plane = CandidatePlane;
            pose = CandidatePose;
            var candidateAgeFrames = candidateValidatedFrame < 0
                ? int.MaxValue
                : Time.frameCount - candidateValidatedFrame;
            var isValid = HasValidCandidate
                && plane != null
                && config != null
                && WallPlacementCommitRules.IsValid(
                    ARSession.state == ARSessionState.SessionTracking,
                    candidateAgeFrames,
                    plane.alignment,
                    plane.trackingState,
                    plane.size,
                    config.MinimumPlaneSizeMeters,
                    plane.subsumedBy != null);
            if (!isValid)
            {
                plane = null;
                pose = default;
            }

            return isValid;
        }

        private void Awake()
        {
            raycastManager ??= FindAnyObjectByType<ARRaycastManager>();
            planeManager ??= FindAnyObjectByType<ARPlaneManager>();
            arCamera ??= Camera.main;
            environmentDepthValidator ??= FindAnyObjectByType<WallEnvironmentDepthValidator>();
            InitializeStabilityTracker();
        }

        private void OnDisable()
        {
            SetCandidateUnavailable(true);
        }

        private void Update()
        {
            if (!scanningActive || raycastManager == null || planeManager == null || arCamera == null || config == null)
            {
                SetCandidateUnavailable(true);
                return;
            }

            RaycastHits.Clear();
            var screenCenter = arCamera.pixelRect.center;
            if (!raycastManager.Raycast(screenCenter, RaycastHits, TrackableType.PlaneWithinPolygon))
            {
                SetCandidateUnavailable(true);
                return;
            }

            var hit = RaycastHits[0];
            var plane = planeManager.GetPlane(hit.trackableId);
            if (plane == null || plane.alignment != PlaneAlignment.Vertical)
            {
                SetCandidateUnavailable(true);
                return;
            }

            var eligible = WallPlacementCandidateRules.IsEligible(
                plane.alignment,
                plane.trackingState,
                plane.size,
                config.MinimumPlaneSizeMeters);
            if (!eligible)
            {
                ResetStabilityAndDepthValidation();
                SetReticleColor(new Color(1f, 0.75f, 0.1f, 0.95f));
                SetCandidateUnavailable(false);
                return;
            }

            var viewDirection = hit.pose.position - arCamera.transform.position;
            if (!WallViewingRules.IsAcceptable(
                    hit.distance,
                    viewDirection,
                    plane.normal,
                    config.MinimumPlacementDistanceMeters,
                    config.MaximumPlacementDistanceMeters,
                    config.MaximumViewAngleDegrees))
            {
                ResetStabilityAndDepthValidation();
                SetReticleColor(new Color(1f, 0.75f, 0.1f, 0.95f));
                SetCandidateUnavailable(false);
                return;
            }

            if (!WallPlacementPoseUtility.TryCreateWallPose(
                    hit.pose.position,
                    plane.normal,
                    arCamera.transform.position,
                    config.WallOffsetMeters,
                    out var pose))
            {
                SetCandidateUnavailable(true);
                return;
            }

            if (!TryValidateFootprint(plane, hit, pose))
            {
                ResetStabilityAndDepthValidation();
                SetReticleColor(new Color(1f, 0.75f, 0.1f, 0.95f));
                SetCandidateUnavailable(false);
                return;
            }

            var observation = new WallCandidateObservation(
                plane.trackableId,
                plane.trackingState,
                plane.transform.position,
                plane.transform.rotation,
                plane.size);
            var stable = stabilityTracker.Update(observation, Time.deltaTime);
            SetReticleColor(stable ? new Color(0.1f, 1f, 0.45f, 0.95f) : new Color(1f, 0.75f, 0.1f, 0.95f));
            if (!stable)
            {
                SetCandidateUnavailable(false);
                return;
            }

            if (config.UseEnvironmentDepthValidation && environmentDepthValidator != null)
            {
                var depthResult = environmentDepthValidator.Validate(
                    footprintScreenSamplePoints,
                    footprintExpectedDepthsMeters,
                    config.MinimumValidEnvironmentDepthSamples,
                    config.MaximumEnvironmentDepthDeviationMeters,
                    config.MaximumEnvironmentDepthResidualSpreadMeters,
                    config.EnvironmentDepthValidationIntervalSeconds);
                if (!WallEnvironmentDepthRules.AllowsPlacement(depthResult))
                {
                    ResetStabilityAndDepthValidation();
                    SetReticleColor(new Color(1f, 0.35f, 0.12f, 0.95f));
                    SetCandidateUnavailable(false);
                    return;
                }
            }

            SetCandidate(plane, pose);
        }

        public void SetScanningActive(bool active)
        {
            scanningActive = active;
            if (!active)
            {
                SetCandidateUnavailable(true);
            }
        }

        private void InitializeStabilityTracker()
        {
            stabilityTracker = config == null
                ? new WallCandidateStabilityTracker(0.75f, 0.015f, 2.5f, 0.03f, 0.2f)
                : new WallCandidateStabilityTracker(
                    config.CandidateStabilitySeconds,
                    config.MaximumPlanePositionDeltaMeters,
                    config.MaximumPlaneRotationDeltaDegrees,
                    config.MaximumPlaneSizeDeltaMeters,
                    config.MaximumObservationIntervalSeconds);
        }

        private bool TryValidateFootprint(ARPlane centerPlane, ARRaycastHit centerHit, Pose wallPose)
        {
            FootprintSamples.Clear();
            FootprintSamples.Add(new WallFootprintSample(
                centerPlane.trackableId,
                centerHit.distance,
                centerPlane.normal));
            footprintScreenSamplePoints[0] = arCamera.pixelRect.center;
            footprintExpectedDepthsMeters[0] = GetCameraForwardDepth(centerHit.pose.position);

            var wallSurfacePose = new Pose(centerHit.pose.position, wallPose.rotation);
            WallFootprintUtility.FillWorldSamplePoints(
                wallSurfacePose,
                config.FootprintWidthMeters,
                config.FootprintHeightMeters,
                footprintWorldSamplePoints);

            for (var index = 1; index < footprintWorldSamplePoints.Length; index++)
            {
                var screenPoint = arCamera.WorldToScreenPoint(footprintWorldSamplePoints[index]);
                if (screenPoint.z <= 0f || !arCamera.pixelRect.Contains(screenPoint))
                {
                    return false;
                }

                FootprintRaycastHits.Clear();
                if (!raycastManager.Raycast(screenPoint, FootprintRaycastHits, TrackableType.PlaneWithinPolygon)
                    || FootprintRaycastHits.Count == 0)
                {
                    return false;
                }

                var footprintHit = FootprintRaycastHits[0];
                var footprintPlane = planeManager.GetPlane(footprintHit.trackableId);
                if (footprintPlane == null)
                {
                    return false;
                }

                FootprintSamples.Add(new WallFootprintSample(
                    footprintHit.trackableId,
                    footprintHit.distance,
                    footprintPlane.normal));
                footprintScreenSamplePoints[index] = screenPoint;
                footprintExpectedDepthsMeters[index] = GetCameraForwardDepth(footprintHit.pose.position);
            }

            return WallFootprintRules.AreConsistent(
                FootprintSamples,
                5,
                config.MaximumFootprintDepthSpreadMeters,
                config.MaximumFootprintNormalAngleDegrees);
        }

        private float GetCameraForwardDepth(Vector3 worldPosition)
        {
            return Vector3.Dot(
                worldPosition - arCamera.transform.position,
                arCamera.transform.forward);
        }

        private void SetCandidate(ARPlane plane, Pose pose)
        {
            HasValidCandidate = true;
            CandidatePlane = plane;
            CandidatePose = pose;
            candidateValidatedFrame = Time.frameCount;
            SetReticleColor(new Color(0.1f, 1f, 0.45f, 0.95f));

            if (previewInstance == null && previewPrefab != null)
            {
                previewInstance = Instantiate(previewPrefab);
                previewInstance.name = "Wall Placement Preview";
                ApplyPreviewMaterial(previewInstance);
            }

            if (previewInstance != null)
            {
                previewInstance.transform.SetPositionAndRotation(
                    pose.position - pose.rotation * Vector3.up * config.CharacterHeightMeters * 0.5f,
                    pose.rotation);
                previewInstance.transform.localScale = Vector3.one * config.CharacterHeightMeters;
                previewInstance.SetActive(true);
            }

            if (lastLoggedCandidateId != plane.trackableId)
            {
                lastLoggedCandidateId = plane.trackableId;
                Debug.Log(
                    $"[WallPlacement] candidate-valid plane={plane.trackableId} position={pose.position} normal={pose.rotation * Vector3.forward}");
            }
        }

        private void SetCandidateUnavailable(bool resetStability)
        {
            HasValidCandidate = false;
            CandidatePlane = null;
            CandidatePose = default;
            candidateValidatedFrame = -1;
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }

            if (resetStability)
            {
                ResetStabilityAndDepthValidation();
                lastLoggedCandidateId = TrackableId.invalidId;
                SetReticleColor(new Color(1f, 1f, 1f, 0.72f));
            }
        }

        private void ResetStabilityAndDepthValidation()
        {
            stabilityTracker?.Reset();
            environmentDepthValidator?.ResetCache();
        }

        private void SetReticleColor(Color color)
        {
            if (reticleImage != null)
            {
                reticleImage.color = color;
            }
        }

        private void ApplyPreviewMaterial(GameObject target)
        {
            if (previewMaterial == null)
            {
                return;
            }

            foreach (var targetRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                var materials = targetRenderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    materials[index] = previewMaterial;
                }

                targetRenderer.sharedMaterials = materials;
            }
        }
    }
}
