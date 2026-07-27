using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CamoHuntAR
{
    public sealed class ARPlacementController : MonoBehaviour
    {
        private const string AnchorCreationError =
            "위치를 고정하지 못했습니다. 추적 상태를 확인한 뒤 다시 시도해주세요.";

        [SerializeField] private Camera arCamera;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button resetButton;

        private readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();
        private readonly PlacementStateMachine _stateMachine = new PlacementStateMachine();
        private GameObject _character;
        private ARAnchor _anchor;
        private Pose _previewPose;
        private TrackableId _previewPlaneId;
        private bool _isConfirming;
        private bool _listenersAttached;

        public PlacementState State => _stateMachine.Current;
        public string LastError { get; private set; } = string.Empty;

        public event Action<PlacementState> StateChanged;
        public event Action<CamouflageSurfacePaintController> CharacterPlaced;
        public event Action CharacterReset;

        private void Awake()
        {
            if (planeManager != null)
            {
                planeManager.requestedDetectionMode =
                    PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            }

            _stateMachine.Changed += OnStateChanged;
            AttachButtonListeners();

            if (!HasRequiredDependencies())
            {
                LastError = "AR 배치 화면 구성이 올바르지 않습니다. 앱을 다시 설치해주세요.";
                enabled = false;
            }

            ApplyControls();
        }

        private void OnDestroy()
        {
            _stateMachine.Changed -= OnStateChanged;
            DetachButtonListeners();
            CleanupOwnedObjects();
        }

        private void Update()
        {
            if (State == PlacementState.Initializing && ARSession.state == ARSessionState.SessionTracking)
                _stateMachine.MarkSessionReady();

            if (ARSession.state != ARSessionState.SessionTracking ||
                (State != PlacementState.Detecting && State != PlacementState.Previewing) ||
                _isConfirming ||
                !PointerPressReader.TryRead(out var press) ||
                IsPointerOverUi(press))
            {
                return;
            }

            TryMovePreview(press.Position);
        }

        public bool TryMovePreview(Vector2 screenPosition)
        {
            if (!CanMovePreview())
                return false;

            _hits.Clear();
            if (!raycastManager.Raycast(screenPosition, _hits, TrackableType.PlaneWithinPolygon) ||
                _hits.Count == 0)
            {
                return false;
            }

            return TryApplyPlacementHit();
        }

        public bool TryMovePreview(Ray worldRay)
        {
            if (!CanMovePreview())
                return false;

            _hits.Clear();
            if (!raycastManager.Raycast(worldRay, _hits, TrackableType.PlaneWithinPolygon) ||
                _hits.Count == 0)
            {
                return false;
            }

            return TryApplyPlacementHit();
        }

        private bool TryApplyPlacementHit()
        {
            if (!TrySelectPlacementHit(out var placementHit))
                return false;

            _previewPlaneId = placementHit.trackableId;
            _previewPose = SurfacePoseUtility.CreatePlacementPose(
                placementHit.pose,
                arCamera.transform.forward);

            if (_character == null && !TryCreatePreview())
                return false;

            _character.transform.SetPositionAndRotation(_previewPose.position, _previewPose.rotation);
            LastError = string.Empty;
            _stateMachine.MarkPreviewAvailable();
            ApplyControls();
            return true;
        }

        private bool CanMovePreview()
        {
            return ARSession.state == ARSessionState.SessionTracking &&
                   (State == PlacementState.Detecting || State == PlacementState.Previewing) &&
                   !_isConfirming;
        }

        public async void ConfirmPlacement()
        {
            if (State != PlacementState.Previewing ||
                ARSession.state != ARSessionState.SessionTracking ||
                _isConfirming ||
                _character == null ||
                !IsPreviewPlaneTracked())
            {
                return;
            }

            _isConfirming = true;
            LastError = string.Empty;
            ApplyControls();

            var manager = anchorManager;
            if (manager == null || !manager.isActiveAndEnabled)
            {
                _isConfirming = false;
                LastError = AnchorCreationError;
                ApplyControls();
                return;
            }

            try
            {
                var result = await manager.TryAddAnchorAsync(_previewPose);

                if (this == null)
                {
                    RemoveOrphanAnchor(manager, result.status.IsSuccess() ? result.value : null);
                    return;
                }

                _isConfirming = false;
                if (!result.status.IsSuccess() || result.value == null)
                {
                    LastError = AnchorCreationError;
                    Debug.LogWarning($"AR Anchor 생성 실패: {result.status}", this);
                    ApplyControls();
                    return;
                }

                _anchor = result.value;
                _character.transform.SetParent(_anchor.transform, true);

                var visual = _character.GetComponent<PlacementVisual>();
                if (visual != null)
                    visual.SetPreview(false);

                _stateMachine.MarkPlaced();
                CharacterPlaced?.Invoke(_character.GetComponent<CamouflageSurfacePaintController>());
                SetPlaneVisualization(false);
                ApplyControls();
            }
            catch (Exception exception)
            {
                if (this == null)
                    return;

                _isConfirming = false;
                LastError = AnchorCreationError;
                Debug.LogException(exception, this);
                ApplyControls();
            }
        }

        public void ResetPlacement()
        {
            if (_isConfirming)
                return;

            if (_anchor != null)
            {
                try
                {
                    if (!anchorManager.TryRemoveAnchor(_anchor))
                    {
                        LastError = "배치를 초기화하지 못했습니다. 다시 시도해주세요.";
                        Debug.LogWarning("AR Anchor 제거 요청이 거부되었습니다.", this);
                        ApplyControls();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    LastError = "배치를 초기화하지 못했습니다. 다시 시도해주세요.";
                    Debug.LogWarning($"AR Anchor 제거 중 오류가 발생했습니다: {exception.Message}", this);
                    ApplyControls();
                    return;
                }

                _anchor = null;
            }

            if (_character != null)
            {
                Destroy(_character);
                _character = null;
            }

            CharacterReset?.Invoke();

            LastError = string.Empty;
            _previewPlaneId = TrackableId.invalidId;
            SetPlaneVisualization(true);
            _stateMachine.Reset();
            ApplyControls();
        }

        private bool TryCreatePreview()
        {
            if (characterPrefab == null)
            {
                LastError = "배치할 캐릭터를 불러오지 못했습니다.";
                ApplyControls();
                return false;
            }

            _character = Instantiate(characterPrefab, _previewPose.position, _previewPose.rotation);
            var visual = _character.GetComponent<PlacementVisual>();
            if (visual == null)
            {
                LastError = "캐릭터 표시 설정을 불러오지 못했습니다.";
                Destroy(_character);
                _character = null;
                ApplyControls();
                return false;
            }

            visual.SetPreview(true);
            return true;
        }

        private bool TrySelectPlacementHit(out ARRaycastHit selectedHit)
        {
            foreach (var hit in _hits)
            {
                if (!planeManager.trackables.TryGetTrackable(hit.trackableId, out var plane) ||
                    plane == null ||
                    !PlacementPlaneUtility.IsPlaceable(plane.alignment, plane.trackingState))
                {
                    continue;
                }

                selectedHit = hit;
                return true;
            }

            selectedHit = default;
            return false;
        }

        private bool IsPreviewPlaneTracked()
        {
            return _previewPlaneId != TrackableId.invalidId &&
                   planeManager.trackables.TryGetTrackable(_previewPlaneId, out var plane) &&
                   plane != null &&
                   PlacementPlaneUtility.IsPlaceable(plane.alignment, plane.trackingState);
        }

        private static void RemoveOrphanAnchor(ARAnchorManager manager, ARAnchor anchor)
        {
            if (manager == null || anchor == null)
                return;

            try
            {
                manager.TryRemoveAnchor(anchor);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"파괴된 화면의 AR Anchor 정리에 실패했습니다: {exception.Message}");
            }
        }

        private void CleanupOwnedObjects()
        {
            if (_anchor != null && anchorManager != null && anchorManager.isActiveAndEnabled)
            {
                try
                {
                    anchorManager.TryRemoveAnchor(_anchor);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"AR Anchor 정리 중 오류가 발생했습니다: {exception.Message}");
                }
            }

            _anchor = null;
            if (_character != null)
            {
                Destroy(_character);
                _character = null;
            }
        }

        private bool HasRequiredDependencies()
        {
            return arCamera != null &&
                   raycastManager != null &&
                   planeManager != null &&
                   anchorManager != null &&
                   characterPrefab != null &&
                   confirmButton != null &&
                   resetButton != null;
        }

        private static bool IsPointerOverUi(PointerPress press)
        {
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(press.PointerId);
        }

        private void SetPlaneVisualization(bool visible)
        {
            if (planeManager == null)
                return;

            planeManager.enabled = visible;
            foreach (var plane in planeManager.trackables)
            {
                if (plane != null)
                    plane.gameObject.SetActive(visible);
            }
        }

        private void ApplyControls()
        {
            if (confirmButton != null)
            {
                var showConfirm = State == PlacementState.Previewing;
                confirmButton.gameObject.SetActive(showConfirm);
                confirmButton.interactable = showConfirm && !_isConfirming;
            }

            if (resetButton != null)
                resetButton.gameObject.SetActive(State == PlacementState.Placed);
        }

        private void AttachButtonListeners()
        {
            if (_listenersAttached)
                return;

            if (confirmButton != null)
                confirmButton.onClick.AddListener(ConfirmPlacement);
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetPlacement);
            _listenersAttached = true;
        }

        private void DetachButtonListeners()
        {
            if (!_listenersAttached)
                return;

            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(ConfirmPlacement);
            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetPlacement);
            _listenersAttached = false;
        }

        private void OnStateChanged(PlacementState state)
        {
            StateChanged?.Invoke(state);
        }
    }
}
