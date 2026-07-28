using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    /// <summary>
    /// Task 01의 수직 벽면 탐지 스파이크 상태를 표시한다.
    /// 배치나 앵커 생성은 이 컴포넌트의 책임이 아니다.
    /// </summary>
    public sealed class WallPlaneDetectionController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Text statusText;
        [SerializeField] private Text detailText;

        private int trackedVerticalPlaneCount;
        private bool hasLoggedFirstVerticalPlane;
        private bool cameraPermissionDenied;
        private bool isRequestingCameraPermission;
        private readonly Dictionary<TrackableId, PlaneSnapshot> lastLoggedPlanes = new();

        private readonly struct PlaneSnapshot
        {
            public PlaneSnapshot(ARPlane plane)
            {
                alignment = plane.alignment;
                trackingState = plane.trackingState;
                extents = plane.extents;
            }

            public readonly PlaneAlignment alignment;
            public readonly TrackingState trackingState;
            public readonly Vector2 extents;
        }

        public void Configure(
            ARPlaneManager configuredPlaneManager,
            ARRaycastManager configuredRaycastManager,
            ARAnchorManager configuredAnchorManager,
            Text configuredStatusText,
            Text configuredDetailText)
        {
            planeManager = configuredPlaneManager;
            raycastManager = configuredRaycastManager;
            anchorManager = configuredAnchorManager;
            statusText = configuredStatusText;
            detailText = configuredDetailText;
        }

        private void Awake()
        {
            if (planeManager == null)
            {
                planeManager = FindFirstObjectByType<ARPlaneManager>();
            }

            if (raycastManager == null)
            {
                raycastManager = FindFirstObjectByType<ARRaycastManager>();
            }

            if (anchorManager == null)
            {
                anchorManager = FindFirstObjectByType<ARAnchorManager>();
            }

            if (planeManager == null || raycastManager == null)
            {
                Debug.LogError("[WallPlaneDetection] ARPlaneManager or ARRaycastManager is missing.");
                SetStatus("AR 구성 오류", "AR Plane과 Raycast Manager 구성을 확인해 주세요.");
                enabled = false;
                return;
            }

            if (anchorManager == null)
            {
                Debug.LogWarning("[WallPlaneDetection] ARAnchorManager is not configured. Task 01 does not create anchors, but the scene should include it for Task 03.");
            }

            planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            SetStatus("카메라 권한 확인 중", "벽에서 1~2m 떨어져 천천히 비춰 주세요.");
        }

        private void OnEnable()
        {
            if (planeManager != null)
            {
                planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
            }

            ARSession.stateChanged += OnSessionStateChanged;
        }

        private void Start()
        {
            StartCoroutine(RequestCameraPermission());
        }

        private void OnDisable()
        {
            if (planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
            }

            ARSession.stateChanged -= OnSessionStateChanged;
        }

        private IEnumerator RequestCameraPermission()
        {
            if (isRequestingCameraPermission)
            {
                yield break;
            }

            isRequestingCameraPermission = true;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                var permissionRequest = Application.RequestUserAuthorization(UserAuthorization.WebCam);
                yield return permissionRequest;
            }

            isRequestingCameraPermission = false;
            cameraPermissionDenied = !Application.HasUserAuthorization(UserAuthorization.WebCam);
            if (cameraPermissionDenied)
            {
                planeManager.enabled = false;
                SetStatus(
                    "카메라 권한이 필요합니다",
                    "설정에서 카메라를 허용한 뒤 앱을 다시 실행해 주세요.");
                Debug.LogWarning("[WallPlaneDetection] Camera permission was denied.");
                yield break;
            }

            planeManager.enabled = true;
            SetStatus("벽면을 탐색 중", "벽에서 1~2m 떨어져 천천히 비춰 주세요.");
        }

        public void RetryCameraPermission()
        {
            if (isRequestingCameraPermission)
            {
                return;
            }

            cameraPermissionDenied = false;
            planeManager.enabled = true;
            StartCoroutine(RequestCameraPermission());
        }

        public void OpenAppSettings()
        {
#if UNITY_IOS
            Application.OpenURL("app-settings:");
#else
            SetStatus("카메라 권한이 필요합니다", "이 기기의 앱 설정에서 카메라 권한을 허용해 주세요.");
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && cameraPermissionDenied && !isRequestingCameraPermission)
            {
                RetryCameraPermission();
            }
        }

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (cameraPermissionDenied)
            {
                return;
            }

            switch (args.state)
            {
                case ARSessionState.Unsupported:
                    SetStatus("이 기기에서는 AR을 사용할 수 없습니다", "ARKit 지원 기기인지 확인해 주세요.");
                    break;
                case ARSessionState.CheckingAvailability:
                case ARSessionState.NeedsInstall:
                case ARSessionState.Installing:
                    SetStatus("AR 준비 중", "잠시 기다려 주세요.");
                    break;
                case ARSessionState.SessionInitializing:
                    SetStatus("벽면을 탐색 중", "밝은 곳에서 벽을 천천히 비춰 주세요.");
                    break;
                case ARSessionState.SessionTracking:
                    RefreshVerticalPlaneCount();
                    break;
                case ARSessionState.None:
                    SetStatus("AR 세션을 시작하지 못했습니다", "밝은 곳에서 카메라를 천천히 움직인 뒤 다시 시도해 주세요.");
                    break;
            }
        }

        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added)
            {
                LogPlane("added", plane);
                ConfigurePlaneVisual(plane);
            }

            foreach (var plane in args.updated)
            {
                LogPlane("updated", plane);
                ConfigurePlaneVisual(plane);
            }

            foreach (var plane in args.removed)
            {
                lastLoggedPlanes.Remove(plane.trackableId);
                Debug.Log($"[WallPlaneDetection] removed id={plane.trackableId}");
            }

            RefreshVerticalPlaneCount();
        }

        private void ConfigurePlaneVisual(ARPlane plane)
        {
            if (plane.alignment != PlaneAlignment.Vertical)
            {
                return;
            }

            if (plane.GetComponent<WallPlaneOutlineVisualizer>() == null)
            {
                plane.gameObject.AddComponent<WallPlaneOutlineVisualizer>();
            }
        }

        private void RefreshVerticalPlaneCount()
        {
            if (planeManager == null)
            {
                return;
            }

            trackedVerticalPlaneCount = 0;
            foreach (var plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.Vertical && plane.trackingState == TrackingState.Tracking)
                {
                    trackedVerticalPlaneCount++;
                }
            }

            if (trackedVerticalPlaneCount > 0)
            {
                SetStatus("수직 벽면 감지됨", $"추적 중인 수직 벽면: {trackedVerticalPlaneCount}");
                if (!hasLoggedFirstVerticalPlane)
                {
                    hasLoggedFirstVerticalPlane = true;
                    Debug.Log($"[WallPlaneDetection] first-tracked-vertical-plane elapsed={Time.realtimeSinceStartup:F1}s count={trackedVerticalPlaneCount} currentDetectionMode={planeManager.currentDetectionMode}");
                }
            }
            else
            {
                SetStatus(
                    "벽면을 탐색 중",
                    $"수직 벽면: 0 | 감지 모드: {planeManager.currentDetectionMode} | 벽에서 1~2m 떨어져 천천히 비춰 주세요.");
            }
        }

        private void LogPlane(string changeType, ARPlane plane)
        {
            var snapshot = new PlaneSnapshot(plane);
            if (changeType == "updated" && lastLoggedPlanes.TryGetValue(plane.trackableId, out var previous)
                && previous.alignment == snapshot.alignment
                && previous.trackingState == snapshot.trackingState
                && previous.extents == snapshot.extents)
            {
                return;
            }

            lastLoggedPlanes[plane.trackableId] = snapshot;
            Debug.Log(
                $"[WallPlaneDetection] {changeType} id={plane.trackableId} alignment={plane.alignment} tracking={plane.trackingState} extents={plane.extents} size={plane.size}");
        }

        private void SetStatus(string status, string detail)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }

            if (detailText != null)
            {
                detailText.text = detail;
            }
        }
    }
}
