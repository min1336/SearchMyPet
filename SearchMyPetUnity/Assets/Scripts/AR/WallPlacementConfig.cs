using UnityEngine;

namespace SearchMyPet.AR
{
    [CreateAssetMenu(fileName = "WallPlacementConfig", menuName = "Search My Pet/Wall Placement Config")]
    public sealed class WallPlacementConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float wallOffsetMeters = 0.02f;
        [SerializeField] private Vector2 minimumPlaneSizeMeters = new(0.3f, 0.3f);
        [SerializeField, Min(0f)] private float candidateStabilitySeconds = 0.75f;
        [SerializeField, Min(0f)] private float maximumPlanePositionDeltaMeters = 0.015f;
        [SerializeField, Min(0f)] private float maximumPlaneRotationDeltaDegrees = 2.5f;
        [SerializeField, Min(0f)] private float maximumPlaneSizeDeltaMeters = 0.03f;
        [SerializeField, Min(0.01f)] private float footprintWidthMeters = 0.18f;
        [SerializeField, Min(0.01f)] private float footprintHeightMeters = 0.18f;
        [SerializeField, Min(0f)] private float maximumFootprintDepthSpreadMeters = 0.2f;
        [SerializeField, Range(0f, 90f)] private float maximumFootprintNormalAngleDegrees = 5f;
        [SerializeField, Min(0f)] private float minimumPlacementDistanceMeters = 0.4f;
        [SerializeField, Min(0f)] private float maximumPlacementDistanceMeters = 4f;
        [SerializeField, Range(0f, 90f)] private float maximumViewAngleDegrees = 60f;
        [SerializeField, Min(0.01f)] private float maximumObservationIntervalSeconds = 0.2f;
        [SerializeField, Min(0f)] private float trackingRecoverySeconds = 0.75f;
        [SerializeField, Min(0.01f)] private float characterHeightMeters = 0.18f;
        [SerializeField] private bool useEnvironmentDepthValidation = true;
        [SerializeField, Range(1, 5)] private int minimumValidEnvironmentDepthSamples = 3;
        [SerializeField, Min(0f)] private float maximumEnvironmentDepthDeviationMeters = 0.25f;
        [SerializeField, Min(0f)] private float maximumEnvironmentDepthResidualSpreadMeters = 0.12f;
        [SerializeField, Min(0.02f)] private float environmentDepthValidationIntervalSeconds = 0.1f;

        public float WallOffsetMeters => wallOffsetMeters;
        public Vector2 MinimumPlaneSizeMeters => minimumPlaneSizeMeters;
        public float CandidateStabilitySeconds => candidateStabilitySeconds;
        public float MaximumPlanePositionDeltaMeters => maximumPlanePositionDeltaMeters;
        public float MaximumPlaneRotationDeltaDegrees => maximumPlaneRotationDeltaDegrees;
        public float MaximumPlaneSizeDeltaMeters => maximumPlaneSizeDeltaMeters;
        public float FootprintWidthMeters => footprintWidthMeters;
        public float FootprintHeightMeters => footprintHeightMeters;
        public float MaximumFootprintDepthSpreadMeters => maximumFootprintDepthSpreadMeters;
        public float MaximumFootprintNormalAngleDegrees => maximumFootprintNormalAngleDegrees;
        public float MinimumPlacementDistanceMeters => minimumPlacementDistanceMeters;
        public float MaximumPlacementDistanceMeters => maximumPlacementDistanceMeters;
        public float MaximumViewAngleDegrees => maximumViewAngleDegrees;
        public float MaximumObservationIntervalSeconds => maximumObservationIntervalSeconds;
        public float TrackingRecoverySeconds => trackingRecoverySeconds;
        public float CharacterHeightMeters => characterHeightMeters;
        public bool UseEnvironmentDepthValidation => useEnvironmentDepthValidation;
        public int MinimumValidEnvironmentDepthSamples => minimumValidEnvironmentDepthSamples;
        public float MaximumEnvironmentDepthDeviationMeters => maximumEnvironmentDepthDeviationMeters;
        public float MaximumEnvironmentDepthResidualSpreadMeters => maximumEnvironmentDepthResidualSpreadMeters;
        public float EnvironmentDepthValidationIntervalSeconds => environmentDepthValidationIntervalSeconds;

    }
}
