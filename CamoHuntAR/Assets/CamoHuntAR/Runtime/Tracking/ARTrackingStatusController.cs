using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR
{
    public sealed class ARTrackingStatusController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARPlacementController placementController;
        [SerializeField] private Text statusText;

        private CameraPermissionState _cameraPermissionState = CameraPermissionState.Unknown;

        private IEnumerator Start()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (IosCameraAuthorization.GetState() == CameraPermissionState.Unknown)
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            RefreshCameraPermissionState();
#else
            _cameraPermissionState = CameraPermissionState.Granted;
            yield break;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (hasFocus)
                RefreshCameraPermissionState();
#endif
        }

        private void Update()
        {
            if (statusText == null)
                return;

            var hasPlane = HasPlaceablePlane();
            var placementState = placementController != null
                ? placementController.State
                : PlacementState.Initializing;
            var placementError = placementController != null
                ? placementController.LastError
                : string.Empty;

            statusText.text = TrackingGuidance.GetMessage(
                _cameraPermissionState,
                ARSession.state,
                ARSession.notTrackingReason,
                hasPlane,
                placementState,
                placementError);
        }

        private bool HasPlaceablePlane()
        {
            if (planeManager == null)
                return false;

            foreach (var plane in planeManager.trackables)
            {
                if (plane != null &&
                    PlacementPlaneUtility.IsPlaceable(plane.alignment, plane.trackingState))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void RefreshCameraPermissionState()
        {
            _cameraPermissionState = IosCameraAuthorization.GetState();
        }
#endif
    }
}
