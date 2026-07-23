using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR
{
    public static class TrackingGuidance
    {
        public const string CameraPermissionRequired =
            "카메라 권한이 필요합니다. iOS 설정에서 권한을 허용해주세요.";

        public const string ArUnsupported = "이 기기는 AR을 지원하지 않습니다.";
        public const string ArPreparing = "AR을 준비하는 중입니다.";
        public const string FindSurface = "기기를 천천히 움직여 표면을 찾아주세요.";
        public const string PlaceCharacter = "표면을 터치해 캐릭터를 놓아보세요.";
        public const string AdjustOrConfirm = "위치를 조정하거나 배치 확정을 눌러주세요.";
        public const string CharacterPlaced = "캐릭터가 이 AR 세션에 배치되었습니다.";
        public const string ImproveLighting = "주변을 밝게 하고 기기를 천천히 움직여주세요.";
        public const string SlowDown = "기기를 조금 더 천천히 움직여주세요.";
        public const string FindDetailedSurface = "특징이 있는 표면을 비추고 기기를 천천히 움직여주세요.";
        public const string Relocalizing = "이전 위치를 다시 찾는 중입니다. 기기를 천천히 움직여주세요.";
        public const string CameraUnavailable = "카메라를 사용할 수 없습니다. 다른 앱의 카메라 사용을 종료해주세요.";

        public static string GetMessage(
            CameraPermissionState cameraPermissionState,
            ARSessionState sessionState,
            NotTrackingReason notTrackingReason,
            bool hasPlane,
            PlacementState placementState,
            string placementError)
        {
            if (!string.IsNullOrWhiteSpace(placementError))
                return placementError;

            if (cameraPermissionState == CameraPermissionState.Denied)
                return CameraPermissionRequired;

            if (sessionState == ARSessionState.Unsupported)
                return ArUnsupported;

            if (cameraPermissionState == CameraPermissionState.Unknown)
                return ArPreparing;

            if (sessionState != ARSessionState.SessionTracking || notTrackingReason != NotTrackingReason.None)
                return GetLimitedTrackingMessage(notTrackingReason);

            if (placementState == PlacementState.Placed)
                return CharacterPlaced;

            if (placementState == PlacementState.Previewing)
                return AdjustOrConfirm;

            if (placementState == PlacementState.Initializing)
                return ArPreparing;

            return hasPlane ? PlaceCharacter : FindSurface;
        }

        private static string GetLimitedTrackingMessage(NotTrackingReason notTrackingReason)
        {
            switch (notTrackingReason)
            {
                case NotTrackingReason.InsufficientLight:
                    return ImproveLighting;
                case NotTrackingReason.ExcessiveMotion:
                    return SlowDown;
                case NotTrackingReason.InsufficientFeatures:
                    return FindDetailedSurface;
                case NotTrackingReason.Relocalizing:
                    return Relocalizing;
                case NotTrackingReason.CameraUnavailable:
                    return CameraUnavailable;
                default:
                    return ArPreparing;
            }
        }
    }
}
