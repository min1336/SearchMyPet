using NUnit.Framework;

namespace CamoHuntAR.Tests.EditMode.Tracking
{
    public sealed class IosCameraAuthorizationTests
    {
        [TestCase(0, CameraPermissionState.Unknown)]
        [TestCase(1, CameraPermissionState.Denied)]
        [TestCase(2, CameraPermissionState.Denied)]
        [TestCase(3, CameraPermissionState.Granted)]
        [TestCase(99, CameraPermissionState.Unknown)]
        public void NativeAuthorizationStatusMapsToTheExpectedPermissionState(
            int nativeStatus,
            CameraPermissionState expected)
        {
            Assert.That(IosCameraAuthorization.FromNativeStatus(nativeStatus), Is.EqualTo(expected));
        }
    }
}
