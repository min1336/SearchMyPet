using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SearchMyPet.AR
{
    public sealed class WallPlacementController : MonoBehaviour
    {
        [SerializeField] private PlacementReticle placementReticle;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private WallPlacementConfig config;
        [SerializeField] private Text instructionText;
        [SerializeField] private Button placeButton;
        [SerializeField] private Button repositionButton;
        [SerializeField] private Button resetButton;

        private readonly WallPlacementStateMachine stateMachine = new();
        private ARAnchor placedAnchor;
        private GameObject placedCharacter;
        private WallPlacementState? lastLoggedState;
        private TrackingRecoveryGate trackingRecoveryGate;
        private Pose lastLoggedAnchorPose;
        private TrackingState? lastLoggedAnchorTrackingState;
        private bool hasLastLoggedAnchorPose;
        private bool? lastLoggedContentVisible;
        private string trackingInstructionOverride;
        private string placementStatusOverride;
        private float nextAnchorPoseLogTime;
        private CharacterColorPalette colorPalette;
        private AppTabController appTabs;

        public WallPlacementState State => stateMachine.State;

        private void Awake()
        {
            placementReticle ??= FindAnyObjectByType<PlacementReticle>();
            anchorManager ??= FindAnyObjectByType<ARAnchorManager>();
            colorPalette = GetComponent<CharacterColorPalette>() ?? gameObject.AddComponent<CharacterColorPalette>();
            InitializeTrackingRecoveryGate();
        }

        private void OnEnable()
        {
            if (anchorManager != null)
            {
                anchorManager.trackablesChanged.AddListener(OnAnchorsChanged);
            }

            ARSession.stateChanged += OnSessionStateChanged;
        }

        private void OnDisable()
        {
            if (anchorManager != null)
            {
                anchorManager.trackablesChanged.RemoveListener(OnAnchorsChanged);
            }

            ARSession.stateChanged -= OnSessionStateChanged;
        }

        private void Start()
        {
            var canvas = FindAnyObjectByType<Canvas>()?.transform;
            colorPalette.Initialize(canvas);
            appTabs = GetComponent<AppTabController>() ?? gameObject.AddComponent<AppTabController>();
            appTabs.Initialize(canvas);
            placementReticle?.SetScanningActive(true);
            RefreshUi();
        }

        private void LateUpdate()
        {
            if (stateMachine.State == WallPlacementState.Scanning
                || stateMachine.State == WallPlacementState.CandidateValid)
            {
                var candidateAvailable = placementReticle != null
                    && placementReticle.TryGetCurrentValidCandidate(out _, out _);
                stateMachine.SetCandidateAvailable(candidateAvailable);
                if (candidateAvailable)
                {
                    placementStatusOverride = null;
                }
            }

            UpdatePlacedContentTracking();

            RefreshUi();
        }

        public void PlaceAtCandidate()
        {
            if (stateMachine.State == WallPlacementState.Placed)
            {
                Reposition();
                return;
            }

            ARPlane candidatePlane = null;
            var candidatePose = default(Pose);
            var hasCurrentCandidate = placementReticle != null
                && placementReticle.TryGetCurrentValidCandidate(out candidatePlane, out candidatePose);
            stateMachine.SetCandidateAvailable(hasCurrentCandidate);
            if (!stateMachine.TryBeginPlacement())
            {
                Debug.LogWarning("[WallPlacement] placement-rejected reason=no-stable-vertical-candidate");
                RefreshUi();
                return;
            }

            if (anchorManager == null || characterPrefab == null || config == null)
            {
                FailPlacement("missing-configuration");
                return;
            }
            try
            {
                placedAnchor = anchorManager.AttachAnchor(
                    candidatePlane,
                    candidatePose);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                FailPlacement("anchor-exception");
                return;
            }

            if (placedAnchor == null)
            {
                FailPlacement("anchor-create-failed");
                return;
            }

            placedCharacter = Instantiate(characterPrefab, placedAnchor.transform, false);
            placedCharacter.name = "Placed Chameleon";
            placedCharacter.transform.localPosition = Vector3.down * config.CharacterHeightMeters * 0.5f;
            placedCharacter.transform.localRotation = Quaternion.identity;
            placedCharacter.transform.localScale = Vector3.one * config.CharacterHeightMeters;
            placedCharacter.SetActive(false);
            colorPalette.SetCharacter(placedCharacter);
            InitializeTrackingRecoveryGate();
            ResetAnchorDiagnostics();

            placementReticle.SetScanningActive(false);
            stateMachine.CompletePlacement(true);
            Debug.Log(
                $"[WallPlacement] anchor-created id={placedAnchor.trackableId} plane={candidatePlane.trackableId}");
            Debug.Log(
                $"[WallPlacement] character-placed position={placedAnchor.transform.position} forward={placedAnchor.transform.forward}");
            RefreshUi();
        }

        public void Reposition()
        {
            if (!stateMachine.BeginRepositioning())
            {
                return;
            }

            RemovePlacement();
            placementReticle?.SetScanningActive(true);
            stateMachine.SetCandidateAvailable(false);
            stateMachine.FinishRepositioning();
            placementStatusOverride = null;
            Debug.Log("[WallPlacement] repositioning-started");
            RefreshUi();
        }

        public void ResetPlacement()
        {
            RemovePlacement();
            stateMachine.Reset();
            placementReticle?.SetScanningActive(true);
            placementStatusOverride = null;
            Debug.Log("[WallPlacement] reset-complete anchors=0 characters=0 state=Scanning");
            RefreshUi();
        }

        private void FailPlacement(string reason)
        {
            placedAnchor = null;
            placedCharacter = null;
            stateMachine.CompletePlacement(false);
            Debug.LogWarning($"[WallPlacement] placement-failed reason={reason}");
            RefreshUi("앵커를 만들지 못했습니다. 벽을 다시 조준해 주세요.");
        }

        private void RemovePlacement()
        {
            colorPalette.SetCharacter(null);
            if (placedCharacter != null)
            {
                Destroy(placedCharacter);
                placedCharacter = null;
            }

            if (placedAnchor != null)
            {
                if (anchorManager == null || !anchorManager.TryRemoveAnchor(placedAnchor))
                {
                    Debug.LogWarning($"[WallPlacement] anchor-remove-failed id={placedAnchor.trackableId}");
                }

                placedAnchor = null;
            }

            trackingRecoveryGate?.Reset();
            trackingInstructionOverride = null;
            lastLoggedContentVisible = null;
            ResetAnchorDiagnostics();
        }

        private void InitializeTrackingRecoveryGate()
        {
            trackingRecoveryGate = new TrackingRecoveryGate(
                config == null ? 0.75f : config.TrackingRecoverySeconds,
                config == null ? 0.2f : config.MaximumObservationIntervalSeconds);
        }

        private void UpdatePlacedContentTracking()
        {
            if (stateMachine.State != WallPlacementState.Placed
                || placedAnchor == null
                || placedCharacter == null)
            {
                trackingInstructionOverride = null;
                return;
            }

            trackingRecoveryGate ??= new TrackingRecoveryGate(
                config == null ? 0.75f : config.TrackingRecoverySeconds,
                config == null ? 0.2f : config.MaximumObservationIntervalSeconds);
            var isTracking = ARSession.state == ARSessionState.SessionTracking
                && placedAnchor.trackingState == TrackingState.Tracking
                && !placedAnchor.pending;
            var contentVisible = trackingRecoveryGate.Update(isTracking, Time.deltaTime);
            if (placedCharacter.activeSelf != contentVisible)
            {
                placedCharacter.SetActive(contentVisible);
            }

            trackingInstructionOverride = contentVisible
                ? null
                : TrackingInstructionFor(ARSession.notTrackingReason);

            if (lastLoggedContentVisible != contentVisible)
            {
                Debug.Log(
                    $"[WallPlacement] content-visible={contentVisible} session={ARSession.state} reason={ARSession.notTrackingReason} anchorTracking={placedAnchor.trackingState} pending={placedAnchor.pending}");
                lastLoggedContentVisible = contentVisible;
            }
        }

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log($"[WallPlacement] session-state={args.state} reason={ARSession.notTrackingReason}");
            if (args.state != ARSessionState.SessionTracking)
            {
                trackingRecoveryGate?.Reset();
            }
        }

        private void OnAnchorsChanged(ARTrackablesChangedEventArgs<ARAnchor> args)
        {
            foreach (var anchor in args.added)
            {
                LogPlacedAnchor("added", anchor);
            }

            foreach (var anchor in args.updated)
            {
                LogPlacedAnchor("updated", anchor);
            }

            foreach (var removedAnchor in args.removed)
            {
                if (placedAnchor != null && removedAnchor.Key == placedAnchor.trackableId)
                {
                    Debug.LogWarning($"[WallPlacement] anchor-removed id={removedAnchor.Key}");
                    if (placedCharacter != null)
                    {
                        colorPalette.SetCharacter(null);
                        Destroy(placedCharacter);
                        placedCharacter = null;
                    }

                    placedAnchor = null;
                    trackingRecoveryGate?.Reset();
                    trackingInstructionOverride = null;
                    placementStatusOverride = "벽면 앵커를 잃었습니다 — 벽을 다시 비춰 배치해 주세요.";
                    lastLoggedContentVisible = null;
                    ResetAnchorDiagnostics();
                    stateMachine.HandlePlacementLost();
                    placementReticle?.SetScanningActive(true);
                    RefreshUi();
                }
            }
        }

        private void LogPlacedAnchor(string changeType, ARAnchor anchor)
        {
            if (placedAnchor == null || anchor.trackableId != placedAnchor.trackableId)
            {
                return;
            }

            var pose = new Pose(anchor.transform.position, anchor.transform.rotation);
            var positionDelta = hasLastLoggedAnchorPose
                ? Vector3.Distance(lastLoggedAnchorPose.position, pose.position)
                : 0f;
            var rotationDelta = hasLastLoggedAnchorPose
                ? Quaternion.Angle(lastLoggedAnchorPose.rotation, pose.rotation)
                : 0f;
            var trackingChanged = lastLoggedAnchorTrackingState != anchor.trackingState;

            var poseChangedEnough = positionDelta >= 0.005f || rotationDelta >= 0.5f;
            var poseLogDue = poseChangedEnough && Time.unscaledTime >= nextAnchorPoseLogTime;
            if (changeType == "added" || trackingChanged || poseLogDue)
            {
                Debug.Log(
                    $"[WallPlacement] anchor-{changeType} id={anchor.trackableId} tracking={anchor.trackingState} pending={anchor.pending} position={pose.position} positionDelta={positionDelta:F4}m rotationDelta={rotationDelta:F2}deg");
                lastLoggedAnchorPose = pose;
                lastLoggedAnchorTrackingState = anchor.trackingState;
                hasLastLoggedAnchorPose = true;
                nextAnchorPoseLogTime = Time.unscaledTime + 0.5f;
            }
        }

        private void ResetAnchorDiagnostics()
        {
            lastLoggedAnchorPose = default;
            lastLoggedAnchorTrackingState = null;
            hasLastLoggedAnchorPose = false;
            nextAnchorPoseLogTime = 0f;
        }

        private void RefreshUi(string overrideInstruction = null)
        {
            var state = stateMachine.State;
            if (lastLoggedState != state)
            {
                Debug.Log($"[WallPlacement] state {lastLoggedState?.ToString() ?? "None"}->{state}");
                lastLoggedState = state;
            }

            if (instructionText != null)
            {
                instructionText.text = overrideInstruction
                    ?? placementStatusOverride
                    ?? trackingInstructionOverride
                    ?? InstructionFor(state);
            }

            var hasPlacement = state == WallPlacementState.Placed;
            var painting = hasPlacement && colorPalette.IsPainting;
            var cameraActive = appTabs == null || appTabs.ActiveTab == AppTab.Camera;
            var previewVisible = placementReticle != null && placementReticle.IsPreviewVisible;
            var showPlacementInstruction = cameraActive
                && (state == WallPlacementState.Scanning
                    || (state == WallPlacementState.CandidateValid && !previewVisible));
            colorPalette?.SetPlacementInstructionVisible(showPlacementInstruction);
            if (placeButton != null)
            {
                placeButton.gameObject.SetActive(cameraActive);
                placeButton.interactable = state == WallPlacementState.CandidateValid || hasPlacement;
                placeButton.transform.SetAsLastSibling();
            }
            if (repositionButton != null)
            {
                repositionButton.gameObject.SetActive(cameraActive && hasPlacement && !painting);
            }

            if (resetButton != null)
            {
                resetButton.gameObject.SetActive(cameraActive && hasPlacement && !painting);
            }
        }

        private static string InstructionFor(WallPlacementState state)
        {
            return state switch
            {
                WallPlacementState.CandidateValid => "배치 가능 — 조준한 벽에 캐릭터를 붙이세요.",
                WallPlacementState.Placing => "벽면 앵커를 만드는 중입니다…",
                WallPlacementState.Placed => "배치 완료 — 움직였다가 같은 벽을 다시 확인해 보세요.",
                WallPlacementState.Repositioning => "기존 배치를 지우는 중입니다…",
                _ => "화면 중앙을 수직 벽에 맞추고 천천히 움직여 주세요."
            };
        }

        private static string TrackingInstructionFor(NotTrackingReason reason)
        {
            return reason switch
            {
                NotTrackingReason.ExcessiveMotion => "추적이 흔들렸습니다 — 휴대폰을 더 천천히 움직여 주세요.",
                NotTrackingReason.InsufficientFeatures => "추적할 무늬가 부족합니다 — 벽과 주변 모서리를 함께 비춰 주세요.",
                NotTrackingReason.InsufficientLight => "주변이 어둡습니다 — 조명을 밝히고 벽을 다시 비춰 주세요.",
                NotTrackingReason.Relocalizing => "위치를 다시 찾는 중입니다 — 배치했던 벽을 같은 각도에서 천천히 비춰 주세요.",
                NotTrackingReason.Initializing => "추적을 안정화하는 중입니다 — 잠시 천천히 움직여 주세요.",
                _ => "추적이 안정되면 캐릭터를 다시 표시합니다 — 벽을 천천히 비춰 주세요."
            };
        }
    }
}
