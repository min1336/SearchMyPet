using System.Runtime.InteropServices;

namespace CamoHuntAR
{
    /// <summary>Reads the iOS camera authorization state used by ARKit itself.</summary>
    public static class IosCameraAuthorization
    {
        private const int NotDetermined = 0;
        private const int Restricted = 1;
        private const int Denied = 2;
        private const int Authorized = 3;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int CamoHuntARCameraAuthorizationStatus();
#endif

        public static CameraPermissionState GetState()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return FromNativeStatus(CamoHuntARCameraAuthorizationStatus());
#else
            return CameraPermissionState.Granted;
#endif
        }

        public static CameraPermissionState FromNativeStatus(int status)
        {
            return status switch
            {
                Authorized => CameraPermissionState.Granted,
                Denied or Restricted => CameraPermissionState.Denied,
                NotDetermined => CameraPermissionState.Unknown,
                _ => CameraPermissionState.Unknown,
            };
        }
    }
}
