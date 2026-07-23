using NUnit.Framework;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR.Tests
{
    public sealed class TrackingGuidanceTests
    {
        [Test]
        public void PlacementErrorTakesPriorityOverEveryOtherMessage()
        {
            const string error = "앵커를 만들 수 없습니다.";

            var text = GetMessage(
                CameraPermissionState.Denied,
                ARSessionState.Unsupported,
                NotTrackingReason.InsufficientLight,
                hasPlane: false,
                PlacementState.Placed,
                error);

            Assert.That(text, Is.EqualTo(error));
        }

        [Test]
        public void DeniedCameraPermissionExplainsHowToRecover()
        {
            var text = GetMessage(
                CameraPermissionState.Denied,
                ARSessionState.SessionTracking,
                NotTrackingReason.None,
                hasPlane: true,
                PlacementState.Detecting);

            Assert.That(text, Is.EqualTo("카메라 권한이 필요합니다. iOS 설정에서 권한을 허용해주세요."));
        }

        [Test]
        public void UnsupportedSessionExplainsDeviceLimitation()
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.Unsupported,
                NotTrackingReason.Unsupported,
                hasPlane: false,
                PlacementState.Initializing);

            Assert.That(text, Is.EqualTo("이 기기는 AR을 지원하지 않습니다."));
        }

        [TestCase(CameraPermissionState.Unknown, ARSessionState.None)]
        [TestCase(CameraPermissionState.Granted, ARSessionState.CheckingAvailability)]
        [TestCase(CameraPermissionState.Granted, ARSessionState.Ready)]
        [TestCase(CameraPermissionState.Granted, ARSessionState.SessionInitializing)]
        public void InitializationStatesShowPreparingMessage(
            CameraPermissionState permissionState,
            ARSessionState sessionState)
        {
            var text = GetMessage(
                permissionState,
                sessionState,
                NotTrackingReason.Initializing,
                hasPlane: false,
                PlacementState.Initializing);

            Assert.That(text, Is.EqualTo("AR을 준비하는 중입니다."));
        }

        [TestCase(NotTrackingReason.InsufficientLight, "주변을 밝게 하고 기기를 천천히 움직여주세요.")]
        [TestCase(NotTrackingReason.ExcessiveMotion, "기기를 조금 더 천천히 움직여주세요.")]
        [TestCase(NotTrackingReason.InsufficientFeatures, "특징이 있는 표면을 비추고 기기를 천천히 움직여주세요.")]
        [TestCase(NotTrackingReason.Relocalizing, "이전 위치를 다시 찾는 중입니다. 기기를 천천히 움직여주세요.")]
        [TestCase(NotTrackingReason.CameraUnavailable, "카메라를 사용할 수 없습니다. 다른 앱의 카메라 사용을 종료해주세요.")]
        public void LimitedTrackingReasonsProvideActionableGuidance(
            NotTrackingReason reason,
            string expected)
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.SessionInitializing,
                reason,
                hasPlane: true,
                PlacementState.Previewing);

            Assert.That(text, Is.EqualTo(expected));
        }

        [Test]
        public void TrackingWithoutAPlaneInvitesScanning()
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.SessionTracking,
                NotTrackingReason.None,
                hasPlane: false,
                PlacementState.Detecting);

            Assert.That(text, Is.EqualTo("기기를 천천히 움직여 표면을 찾아주세요."));
        }

        [Test]
        public void DetectingWithAPlaneInvitesPlacement()
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.SessionTracking,
                NotTrackingReason.None,
                hasPlane: true,
                PlacementState.Detecting);

            Assert.That(text, Is.EqualTo("표면을 터치해 캐릭터를 놓아보세요."));
        }

        [Test]
        public void PreviewingExplainsAdjustmentAndConfirmation()
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.SessionTracking,
                NotTrackingReason.None,
                hasPlane: true,
                PlacementState.Previewing);

            Assert.That(text, Is.EqualTo("위치를 조정하거나 배치 확정을 눌러주세요."));
        }

        [Test]
        public void PlacedConfirmsSessionScopedPlacement()
        {
            var text = GetMessage(
                CameraPermissionState.Granted,
                ARSessionState.SessionTracking,
                NotTrackingReason.None,
                hasPlane: false,
                PlacementState.Placed);

            Assert.That(text, Is.EqualTo("캐릭터가 이 AR 세션에 배치되었습니다."));
        }

        private static string GetMessage(
            CameraPermissionState cameraPermissionState,
            ARSessionState sessionState,
            NotTrackingReason notTrackingReason,
            bool hasPlane,
            PlacementState placementState,
            string placementError = "")
        {
            return TrackingGuidance.GetMessage(
                cameraPermissionState,
                sessionState,
                notTrackingReason,
                hasPlane,
                placementState,
                placementError);
        }
    }
}
