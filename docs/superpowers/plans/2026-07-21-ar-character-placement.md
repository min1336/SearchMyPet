# AR Character Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity AR 카메라에서 실제 수평·수직 평면을 터치해 독자적인 임시 캐릭터를 미리 배치하고, 확정 시 로컬 AR Anchor에 고정하며, 다시 배치할 수 있는 iOS용 프로토타입을 만든다.

**Architecture:** 저장소 안의 `CamoHuntAR/`를 독립 Unity 프로젝트로 두고, 순수 C# 상태·자세·안내 로직과 AR Foundation 의존 MonoBehaviour를 분리한다. `ARPlacementController`가 터치→Raycast→미리보기→비동기 Anchor 생성 흐름을 담당하고, `ARTrackingStatusController`는 추적 상태를 사용자 문구로 변환한다. Scene과 임시 캐릭터 Prefab은 재현 가능한 Editor setup 명령으로 생성한다.

**Tech Stack:** Unity 6000.5.4f1, Built-in Render Pipeline, AR Foundation 6.5.0, Apple ARKit XR Plugin 6.5.0, XR Plug-in Management 4.6.0, Input System 1.19.0, uGUI 2.5.0, Unity Test Framework 1.7.0, C#.

## Global Constraints

- 이번 구현은 로컬 AR 배치만 다룬다. GPS, 지도, 서버, 계정, Cloud Anchor, 타 사용자 공유는 추가하지 않는다.
- Unity 프로젝트 경로는 `C:\Projects\Meccha_Chameleon\CamoHuntAR`로 고정한다.
- Unity 단위 `1`은 현실의 `1m`로 취급하며, 모든 배치 좌표는 AR Raycast의 world-space Pose를 사용한다.
- 배치 캐릭터는 Unity Primitive 조합으로 만든 독자적인 임시 디자인만 사용한다. 기존 게임의 이름, 외형, 사운드, UI를 복제하지 않는다.
- AR Foundation과 ARKit은 같은 안정 릴리스 `6.5.0`으로 고정한다. 초기 계획의 `6.3.0`은 Unity 6000.5.4f1에서 obsolete-as-error API 때문에 컴파일되지 않아 실제 호환 검증 후 교체했다.
- Windows에서는 XR Simulation과 자동 테스트까지 완료한다. iOS 빌드는 Apple Silicon Mac, Xcode 26 이상, 동일 Unity 버전과 iOS Build Support를 사용한다.
- Scene 또는 Prefab YAML을 손으로 작성하지 않는다. Unity Editor API로 생성하고 Unity가 직렬화하게 한다.
- 코드 변경은 테스트 실패 확인 → 최소 구현 → 테스트 통과 → 커밋 순서로 진행한다.
- 각 작업은 아래에 명시한 파일만 스테이징한다. 사용자의 다른 변경은 포함하지 않는다.

## Reference Decisions

- AR Foundation은 ARKit 같은 플랫폼 플러그인과 함께 사용해야 한다: [AR Foundation manual](https://docs.unity.cn/Packages/com.unity.xr.arfoundation%406.0/manual/index.html)
- AR Foundation과 ARKit 패키지는 같은 major/minor 계열을 맞춘다: [Unity XR packages](https://docs.unity.cn/Manual/xr-support-packages.html)
- `ARAnchorManager.TryAddAnchorAsync(Pose)`를 사용하고 성공한 Anchor의 자식으로 콘텐츠를 둔다: [AR Anchor Manager](https://docs.unity.cn/Packages/com.unity.xr.arfoundation%406.1/manual/features/anchors/aranchormanager.html)
- XR Simulation은 plane, raycast, anchor 흐름을 에디터에서 검증하지만 실기기 검증을 대체하지 않는다: [XR Simulation overview](https://docs.unity.cn/Packages/com.unity.xr.arfoundation%406.0/manual/xr-simulation/simulation-overview.html)
- ARKit 6.5.0의 기반 정적 라이브러리는 Xcode 26.0.1로 빌드되어 Xcode 26 이상을 요구한다: [ARKit changelog](https://docs.unity.cn/Packages/com.unity.xr.arkit%406.5/changelog/CHANGELOG.html)
- Unity의 iOS 결과물은 Xcode 프로젝트이며, 실기기 실행에는 Mac의 iOS Build Support와 Xcode가 필요하다: [Unity iOS environment setup](https://docs.unity3d.com/kr/6000.0/Manual/ios-environment-setup.html)

---

### Task 1: Unity 프로젝트를 생성하고 패키지 버전을 고정한다

**Files:**

- Create: `.gitignore`
- Create: `CamoHuntAR/Assets/`
- Create: `CamoHuntAR/Packages/manifest.json`
- Create: `CamoHuntAR/ProjectSettings/ProjectVersion.txt`
- Create: `artifacts/.gitkeep`

- [ ] **Step 1: 현재 저장소 상태와 Unity 실행 파일을 확인한다**

Run:

```powershell
git status --short --branch
Test-Path 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
```

Expected: branch는 `master`, 작업 트리에는 이 계획 문서만 표시되고, Unity 경로는 `True`다.

- [ ] **Step 2: 빈 Unity 프로젝트를 생성한다**

Run:

```powershell
New-Item -ItemType Directory -Force 'C:\Projects\Meccha_Chameleon\artifacts' | Out-Null
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -quit `
  -createProject 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\unity-create.log'
```

Expected: exit code `0`, `CamoHuntAR/Assets`, `Packages`, `ProjectSettings`가 생성된다. 실패 시 다음 단계로 넘어가지 말고 `artifacts/unity-create.log`의 첫 `Error`를 해결한다.

- [ ] **Step 3: Unity 생성물용 `.gitignore`를 추가한다**

`.gitignore`의 핵심 내용:

```gitignore
CamoHuntAR/[Ll]ibrary/
CamoHuntAR/[Tt]emp/
CamoHuntAR/[Oo]bj/
CamoHuntAR/[Bb]uild/
CamoHuntAR/[Bb]uilds/
CamoHuntAR/[Ll]ogs/
CamoHuntAR/[Uu]ser[Ss]ettings/
CamoHuntAR/MemoryCaptures/
CamoHuntAR/Recordings/
CamoHuntAR/.vs/
CamoHuntAR/*.csproj
CamoHuntAR/*.sln
CamoHuntAR/*.user
CamoHuntAR/*.pidb
CamoHuntAR/*.booproj
CamoHuntAR/sysinfo.txt
artifacts/*
!artifacts/.gitkeep
```

- [ ] **Step 4: `Packages/manifest.json`에 안정 버전을 명시한다**

Unity가 생성한 기존 dependency를 보존하고 다음 항목을 추가 또는 교체한다.

```json
"com.unity.inputsystem": "1.19.0",
"com.unity.test-framework": "1.7.0",
"com.unity.ugui": "2.5.0",
"com.unity.xr.arfoundation": "6.5.0",
"com.unity.xr.arkit": "6.5.0",
"com.unity.xr.management": "4.6.0"
```

`com.unity.xr.arcore`, `com.unity.xr.openxr`, Firebase 또는 Google ARCore Extensions는 추가하지 않는다.

- [ ] **Step 5: 패키지 해석과 최초 컴파일을 실행한다**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\unity-import.log'
$LASTEXITCODE
```

Expected: `0`. `Packages/packages-lock.json`에서 AR Foundation과 ARKit이 모두 `6.5.0`이다.

- [ ] **Step 6: 생성물 범위를 확인하고 커밋한다**

Run:

```powershell
git status --short
git add .gitignore artifacts/.gitkeep CamoHuntAR/Assets CamoHuntAR/Packages CamoHuntAR/ProjectSettings docs/superpowers/plans/2026-07-21-ar-character-placement.md
git commit -m "build: scaffold Unity AR project"
```

Expected: `Library`, `Temp`, `Logs`, `UserSettings`는 커밋 대상에 없다.

---

### Task 2: 배치 상태 모델을 테스트 우선으로 구현한다

**Files:**

- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/CamoHuntAR.Runtime.asmdef`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/PlacementState.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/PlacementStateMachine.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/CamoHuntAR.EditModeTests.asmdef`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Placement/PlacementStateMachineTests.cs`

- [ ] **Step 1: runtime과 test assembly definition을 만든다**

`CamoHuntAR.Runtime.asmdef`:

```json
{
  "name": "CamoHuntAR.Runtime",
  "rootNamespace": "CamoHuntAR",
  "references": [
    "Unity.InputSystem",
    "Unity.XR.ARFoundation",
    "Unity.XR.CoreUtils",
    "UnityEngine.UI"
  ],
  "autoReferenced": true
}
```

`CamoHuntAR.EditModeTests.asmdef`:

```json
{
  "name": "CamoHuntAR.EditModeTests",
  "rootNamespace": "CamoHuntAR.Tests",
  "references": ["CamoHuntAR.Runtime"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false
}
```

- [ ] **Step 2: 상태 전이 테스트를 먼저 작성한다**

```csharp
using NUnit.Framework;

namespace CamoHuntAR.Tests
{
    public sealed class PlacementStateMachineTests
    {
        [Test]
        public void StartsInInitializing()
        {
            Assert.That(new PlacementStateMachine().Current, Is.EqualTo(PlacementState.Initializing));
        }

        [Test]
        public void ReadyPreviewAndPlacedFollowTheHappyPath()
        {
            var machine = new PlacementStateMachine();

            Assert.That(machine.MarkSessionReady(), Is.True);
            Assert.That(machine.MarkPreviewAvailable(), Is.True);
            Assert.That(machine.MarkPlaced(), Is.True);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Placed));
        }

        [Test]
        public void CannotPlaceBeforePreviewExists()
        {
            var machine = new PlacementStateMachine();
            machine.MarkSessionReady();

            Assert.That(machine.MarkPlaced(), Is.False);
            Assert.That(machine.Current, Is.EqualTo(PlacementState.Detecting));
        }

        [TestCase(PlacementState.Previewing)]
        [TestCase(PlacementState.Placed)]
        public void ResetReturnsToDetecting(PlacementState state)
        {
            var machine = new PlacementStateMachine();
            machine.MarkSessionReady();
            machine.MarkPreviewAvailable();
            if (state == PlacementState.Placed)
                machine.MarkPlaced();

            machine.Reset();

            Assert.That(machine.Current, Is.EqualTo(PlacementState.Detecting));
        }
    }
}
```

- [ ] **Step 3: 테스트 실패를 확인한다**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\Projects\Meccha_Chameleon\artifacts\editmode-red.xml' `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\editmode-red.log'
```

Expected: `PlacementStateMachine`과 `PlacementState`가 없어서 컴파일 또는 테스트가 실패한다. 다른 패키지 오류가 원인이면 먼저 Task 1을 바로잡는다.

- [ ] **Step 4: 최소 상태 구현을 추가한다**

`PlacementState.cs`:

```csharp
namespace CamoHuntAR
{
    public enum PlacementState
    {
        Initializing,
        Detecting,
        Previewing,
        Placed
    }
}
```

`PlacementStateMachine.cs`:

```csharp
using System;

namespace CamoHuntAR
{
    public sealed class PlacementStateMachine
    {
        public PlacementState Current { get; private set; } = PlacementState.Initializing;
        public event Action<PlacementState> Changed;

        public bool MarkSessionReady()
        {
            return Current == PlacementState.Initializing && Set(PlacementState.Detecting);
        }

        public bool MarkPreviewAvailable()
        {
            if (Current != PlacementState.Detecting && Current != PlacementState.Previewing)
                return false;

            return Set(PlacementState.Previewing);
        }

        public bool MarkPlaced()
        {
            return Current == PlacementState.Previewing && Set(PlacementState.Placed);
        }

        public void Reset()
        {
            Set(PlacementState.Detecting);
        }

        private bool Set(PlacementState next)
        {
            if (Current == next)
                return true;

            Current = next;
            Changed?.Invoke(Current);
            return true;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과를 확인한다**

동일한 Unity test 명령을 `editmode-state.xml`, `editmode-state.log`로 실행한다.

Expected: exit code `0`, XML의 failed count가 `0`이다.

- [ ] **Step 6: 커밋한다**

```powershell
git add CamoHuntAR/Assets/CamoHuntAR/Runtime CamoHuntAR/Assets/CamoHuntAR/Tests
git commit -m "feat: add AR placement state model"
```

---

### Task 3: 평면 방향에 맞는 캐릭터 Pose를 테스트 우선으로 구현한다

**Files:**

- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/SurfacePoseUtility.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Placement/SurfacePoseUtilityTests.cs`

- [ ] **Step 1: 수평면과 수직면 자세 테스트를 작성한다**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace CamoHuntAR.Tests
{
    public sealed class SurfacePoseUtilityTests
    {
        [Test]
        public void HorizontalSurfaceKeepsCharacterUpOnTheSurfaceAndFacingCamera()
        {
            var hit = new Pose(new Vector3(1f, 0.5f, 2f), Quaternion.identity);

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.forward);

            Assert.That(result.position, Is.EqualTo(hit.position));
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, Vector3.back), Is.GreaterThan(0.999f));
        }

        [Test]
        public void VerticalSurfacePointsCharacterForwardAlongSurfaceNormal()
        {
            var wallNormal = Vector3.back;
            var hit = new Pose(Vector3.zero, Quaternion.FromToRotation(Vector3.up, wallNormal));

            var result = SurfacePoseUtility.CreatePlacementPose(hit, Vector3.forward);

            Assert.That(Vector3.Dot(result.rotation * Vector3.forward, wallNormal), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(result.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }
    }
}
```

- [ ] **Step 2: 새 테스트가 실패하는지 확인한다**

Task 2의 EditMode test 명령을 `editmode-pose-red.xml`로 실행한다.

Expected: `SurfacePoseUtility`가 없어서 실패한다.

- [ ] **Step 3: 자세 계산을 구현한다**

```csharp
using UnityEngine;

namespace CamoHuntAR
{
    public static class SurfacePoseUtility
    {
        private const float HorizontalThreshold = 0.75f;
        private const float DirectionEpsilon = 0.0001f;

        public static Pose CreatePlacementPose(Pose raycastPose, Vector3 cameraForward)
        {
            var normal = (raycastPose.rotation * Vector3.up).normalized;
            Quaternion rotation;

            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) >= HorizontalThreshold)
            {
                var towardCamera = Vector3.ProjectOnPlane(-cameraForward, normal);
                if (towardCamera.sqrMagnitude < DirectionEpsilon)
                    towardCamera = Vector3.ProjectOnPlane(Vector3.forward, normal);

                rotation = Quaternion.LookRotation(towardCamera.normalized, normal);
            }
            else
            {
                var upright = Vector3.ProjectOnPlane(Vector3.up, normal);
                if (upright.sqrMagnitude < DirectionEpsilon)
                    upright = Vector3.forward;

                rotation = Quaternion.LookRotation(normal, upright.normalized);
            }

            return new Pose(raycastPose.position, rotation);
        }
    }
}
```

- [ ] **Step 4: 전체 EditMode 테스트를 통과시킨다**

Expected: exit code `0`; 상태 테스트와 자세 테스트가 모두 통과한다.

- [ ] **Step 5: 커밋한다**

```powershell
git add CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/SurfacePoseUtility.cs CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Placement/SurfacePoseUtilityTests.cs
git commit -m "feat: orient characters on AR surfaces"
```

---

### Task 4: 추적 상태 안내 문구를 테스트 우선으로 구현한다

**Files:**

- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Tracking/TrackingGuidance.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Tracking/CameraPermissionState.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Tracking/ARTrackingStatusController.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Tracking/TrackingGuidanceTests.cs`

- [ ] **Step 1: 사용자가 다음 행동을 알 수 있는 문구 테스트를 작성한다**

테스트는 최소 다음 표를 고정한다.

| 조건 | 기대 문구 |
|---|---|
| iOS 카메라 권한 거부 | `카메라 권한이 필요합니다. iOS 설정에서 권한을 허용해주세요.` |
| `ARSessionState.Unsupported` | `이 기기는 AR을 지원하지 않습니다.` |
| 세션 초기화 중 | `AR을 준비하는 중입니다.` |
| 추적 중이나 plane 없음 | `기기를 천천히 움직여 표면을 찾아주세요.` |
| plane 있음 + Detecting | `표면을 터치해 캐릭터를 놓아보세요.` |
| Previewing | `위치를 조정하거나 배치 확정을 눌러주세요.` |
| Placed | `캐릭터가 이 AR 세션에 배치되었습니다.` |
| Anchor 오류 있음 | 오류 문구를 다른 안내보다 우선 표시 |

대표 테스트:

```csharp
[Test]
public void DetectingWithAPlaneInvitesPlacement()
{
    var text = TrackingGuidance.GetMessage(
        CameraPermissionState.Granted,
        ARSessionState.SessionTracking,
        NotTrackingReason.None,
        hasPlane: true,
        PlacementState.Detecting,
        placementError: string.Empty);

    Assert.That(text, Is.EqualTo("표면을 터치해 캐릭터를 놓아보세요."));
}
```

- [ ] **Step 2: 실패를 확인한 뒤 `TrackingGuidance`를 구현한다**

`GetMessage`의 시그니처를 다음으로 고정한다.

```csharp
public static string GetMessage(
    CameraPermissionState cameraPermissionState,
    ARSessionState sessionState,
    NotTrackingReason notTrackingReason,
    bool hasPlane,
    PlacementState placementState,
    string placementError)
```

`CameraPermissionState`는 `Unknown`, `Granted`, `Denied` 세 값이다. 우선순위는 `placementError` → camera denied → unsupported → initializing/limited tracking → placed → previewing → plane 유무다. `InsufficientLight`는 `주변을 밝게 하고 기기를 천천히 움직여주세요.`, `ExcessiveMotion`은 `기기를 조금 더 천천히 움직여주세요.`로 매핑한다.

- [ ] **Step 3: `ARTrackingStatusController`를 구현한다**

Serialized dependency:

```csharp
[SerializeField] private ARPlaneManager planeManager;
[SerializeField] private ARPlacementController placementController;
[SerializeField] private Text statusText;
```

`Start`에서는 iOS 빌드일 때만 `Application.HasUserAuthorization(UserAuthorization.WebCam)`을 확인하고, 아직 허용되지 않았으면 `Application.RequestUserAuthorization(UserAuthorization.WebCam)`을 한 번 요청한다. 결과를 `CameraPermissionState`로 보관한다. Editor와 비-iOS 빌드는 XR Simulation을 막지 않도록 `Granted`로 처리한다.

매 프레임 `ARSession.state`, `ARSession.notTrackingReason`, `planeManager.trackables.count`, `placementController.State`, `placementController.LastError`, camera permission 상태를 읽어 `TrackingGuidance.GetMessage` 결과를 `statusText.text`에 넣는다. 이 컴포넌트는 배치 상태를 변경하거나 Anchor를 만들지 않는다.

- [ ] **Step 4: 전체 EditMode 테스트와 컴파일을 확인한다**

Expected: exit code `0`, failed `0`, `error CS` 로그 없음.

- [ ] **Step 5: 커밋한다**

```powershell
git add CamoHuntAR/Assets/CamoHuntAR/Runtime/Tracking CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Tracking
git commit -m "feat: add actionable AR tracking guidance"
```

---

### Task 5: 터치, Raycast, 미리보기, Anchor 확정 흐름을 구현한다

**Files:**

- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Input/PointerPressReader.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/PlacementVisual.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement/ARPlacementController.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Placement/PlacementVisualTests.cs`

- [ ] **Step 1: `PlacementVisual`의 머티리얼 전환 테스트를 먼저 작성한다**

테스트에서 root와 child `MeshRenderer`, 서로 다른 preview/placed `Material`을 만든다. `SetPreview(true)` 뒤 모든 renderer의 `sharedMaterial`이 preview이고, `SetPreview(false)` 뒤 placed인지 검증한다. `TearDown`에서 생성한 Unity Object를 `DestroyImmediate`로 정리한다.

- [ ] **Step 2: 실패를 확인하고 `PlacementVisual`을 구현한다**

공개 계약:

```csharp
public sealed class PlacementVisual : MonoBehaviour
{
    public void Configure(Renderer[] targetRenderers, Material preview, Material placed);
    public void SetPreview(bool isPreview);
}
```

`Configure`는 Editor setup에서만 호출해 직렬화 값을 넣고, `SetPreview`는 런타임에 `sharedMaterial`만 변경한다. 프레임마다 material 인스턴스를 만들지 않는다.

- [ ] **Step 3: 새 Input System 기반 단일 press reader를 구현한다**

공개 계약:

```csharp
public readonly struct PointerPress
{
    public PointerPress(Vector2 position, int pointerId);
    public Vector2 Position { get; }
    public int PointerId { get; }
}

public static class PointerPressReader
{
    public static bool TryRead(out PointerPress press);
}
```

처리 순서:

1. `Touchscreen.current.primaryTouch.press.wasPressedThisFrame`이면 touch position과 touch id를 반환한다.
2. 없고 `Mouse.current.leftButton.wasPressedThisFrame`이면 mouse position과 `-1`을 반환한다. 이 경로는 XR Simulation용이다.
3. 둘 다 아니면 `false`다.

- [ ] **Step 4: `ARPlacementController`를 구현한다**

Serialized dependency를 다음으로 고정한다.

```csharp
[SerializeField] private Camera arCamera;
[SerializeField] private ARRaycastManager raycastManager;
[SerializeField] private ARPlaneManager planeManager;
[SerializeField] private ARAnchorManager anchorManager;
[SerializeField] private GameObject characterPrefab;
[SerializeField] private Button confirmButton;
[SerializeField] private Button resetButton;
```

외부 읽기 계약:

```csharp
public PlacementState State { get; }
public string LastError { get; }
```

`Update` 알고리즘:

1. 상태가 `Initializing`이고 `ARSession.state == ARSessionState.SessionTracking`이면 `MarkSessionReady()`를 호출한다.
2. `Detecting` 또는 `Previewing`이 아니거나 Anchor 생성 중이면 반환한다.
3. `PointerPressReader.TryRead`가 false면 반환한다.
4. `EventSystem.current`가 해당 pointer를 UI 위로 판정하면 반환한다.
5. `ARRaycastManager.Raycast(position, hits, TrackableType.PlaneWithinPolygon)`을 한 번만 호출한다.
6. hit가 없으면 기존 미리보기를 유지한다.
7. 첫 hit Pose와 `arCamera.transform.forward`를 `SurfacePoseUtility.CreatePlacementPose`에 전달한다.
8. 캐릭터가 없으면 하나만 instantiate하고 `PlacementVisual.SetPreview(true)`를 호출한다.
9. 캐릭터 world position/rotation을 새 Pose로 갱신하고 `MarkPreviewAvailable()`을 호출한다.

Anchor 확정 핵심 코드는 AR Foundation 6 API를 그대로 사용한다.

```csharp
public async void ConfirmPlacement()
{
    if (State != PlacementState.Previewing || _isConfirming || _character == null)
        return;

    _isConfirming = true;
    LastError = string.Empty;
    ApplyControls();

    var result = await anchorManager.TryAddAnchorAsync(_previewPose);
    if (this == null)
        return;

    _isConfirming = false;
    if (!result.TryGetResult(out _anchor))
    {
        LastError = "위치를 고정하지 못했습니다. 다시 배치 확정을 눌러주세요.";
        ApplyControls();
        return;
    }

    _character.transform.SetParent(_anchor.transform, worldPositionStays: true);
    _character.GetComponent<PlacementVisual>().SetPreview(false);
    _stateMachine.MarkPlaced();
    SetPlaneVisualization(false);
    ApplyControls();
}
```

`ResetPlacement` 알고리즘:

1. Anchor 생성 중이면 무시한다.
2. 캐릭터를 destroy한다.
3. Anchor가 있으면 `anchorManager.TryRemoveAnchor(_anchor)`를 호출한다. 실패는 warning으로 기록하되 앱을 종료하지 않는다.
4. 참조와 오류 문구를 비운다.
5. plane manager를 다시 활성화하고 기존 plane GameObject도 표시한다.
6. 상태를 `Detecting`으로 돌리고 버튼 상태를 갱신한다.

버튼 규칙:

- `ConfirmButton`: `Previewing && !_isConfirming`에서만 표시하고 interactable.
- `ResetButton`: `Placed`에서만 표시.
- 생성 중에는 Confirm을 비활성화해 중복 Anchor를 막는다.

- [ ] **Step 5: EditMode 테스트와 headless compile을 실행한다**

Expected: 모든 단위 테스트 통과, `ARPlacementController` 컴파일 오류 없음.

- [ ] **Step 6: 커밋한다**

```powershell
git add CamoHuntAR/Assets/CamoHuntAR/Runtime/Input CamoHuntAR/Assets/CamoHuntAR/Runtime/Placement CamoHuntAR/Assets/CamoHuntAR/Tests/EditMode/Placement
git commit -m "feat: place and anchor a character on AR planes"
```

---

### Task 6: Scene, 임시 캐릭터, plane 시각화와 UI를 재현 가능하게 생성한다

**Files:**

- Create: `CamoHuntAR/Assets/CamoHuntAR/Editor/CamoHuntAR.Editor.asmdef`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Editor/ARPlacementProjectSetup.cs`
- Create: `CamoHuntAR/Assets/CamoHuntAR/Runtime/UI/RuntimeFontBinder.cs`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Materials/CharacterPreview.mat`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Materials/CharacterPlaced.mat`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Materials/DetectedPlane.mat`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Prefabs/CamoCritter.prefab`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Prefabs/DetectedPlane.prefab`
- Generated: `CamoHuntAR/Assets/CamoHuntAR/Scenes/ARPlacementScene.unity`

- [ ] **Step 1: Editor assembly를 만든다**

```json
{
  "name": "CamoHuntAR.Editor",
  "rootNamespace": "CamoHuntAR.Editor",
  "references": [
    "CamoHuntAR.Runtime",
    "Unity.InputSystem",
    "Unity.XR.ARFoundation",
    "Unity.XR.CoreUtils",
    "UnityEngine.UI"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
```

- [ ] **Step 2: idempotent setup entry point를 구현한다**

`ARPlacementProjectSetup`은 다음 두 진입점을 제공한다.

```csharp
[MenuItem("CAMO HUNT/Build AR Placement Prototype")]
public static void BuildFromMenu();

public static void BuildFromCommandLine();
```

두 메서드는 하나의 private `Build()`를 호출한다. `Build()`는 대상 asset이 이미 있으면 덮어써 같은 결과를 만들고, 예외가 나면 batch mode exit code가 실패하도록 예외를 삼키지 않는다.

- [ ] **Step 3: Unity 공식 메뉴로 기본 AR hierarchy를 만든다**

Builder는 빈 Scene을 연 뒤 아래 메뉴를 실행한다.

```csharp
EditorApplication.ExecuteMenuItem("GameObject/XR/AR Session");
EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (Mobile AR)");
```

그 후 이름으로 `AR Session`, `XR Origin`, `Main Camera`를 찾는다. 하나라도 없으면 명확한 `InvalidOperationException`을 던진다. 이렇게 하면 `TrackedPoseDriver`의 `<HandheldARInputDevice>/devicePosition`과 `deviceRotation` binding을 Unity의 검증된 기본 설정에서 가져온다.

`XR Origin`에 다음 컴포넌트를 하나씩 추가한다.

- `ARPlaneManager` with `requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical`
- `ARRaycastManager`
- `ARAnchorManager`

- [ ] **Step 4: 머티리얼과 plane prefab을 생성한다**

- preview: cyan 계열, alpha `0.55`, Standard shader transparent mode.
- placed: 기존 IP와 무관한 green/blue 계열 opaque material.
- detected plane: 밝은 cyan, alpha `0.18`, 양면에 가까운 시인성.
- plane prefab root에는 `ARPlane`, `MeshFilter`, `MeshRenderer`, `LineRenderer`, `ARPlaneMeshVisualizer`를 둔다.
- `ARPlaneManager.planePrefab`에 생성한 plane prefab을 지정한다.

- [ ] **Step 5: 독자적인 `CamoCritter` prefab을 만든다**

Hierarchy:

```text
CamoCritter
├─ VisualRoot
│  ├─ Body          (Sphere, scale 0.16 x 0.10 x 0.22m)
│  ├─ Head          (Sphere, scale 0.12m)
│  ├─ LeftEye       (Sphere, scale 0.025m)
│  ├─ RightEye      (Sphere, scale 0.025m)
│  └─ CurledTail    (3 small spheres, decreasing scale)
└─ CapsuleCollider
```

Primitive child에 자동으로 생긴 Collider는 제거하고 root에 `CapsuleCollider` 하나만 둔다. Root에 `PlacementVisual`을 추가하고 모든 child renderer와 두 character material을 `Configure`로 연결한다. 이름, 비율, 색상은 기존 게임 캐릭터를 참조하지 않는다.

- [ ] **Step 6: App과 Canvas를 만들고 serialized reference를 연결한다**

최종 hierarchy:

```text
ARPlacementScene
├─ AR Session
├─ XR Origin
│  ├─ Camera Offset
│  │  └─ Main Camera
│  ├─ AR Plane Manager
│  ├─ AR Raycast Manager
│  └─ AR Anchor Manager
├─ App
│  ├─ ARPlacementController
│  └─ ARTrackingStatusController
└─ Canvas
   ├─ StatusPanel
   │  └─ StatusText
   ├─ CenterReticle
   ├─ ConfirmButton
   └─ ResetButton
```

UI 요구사항:

- `CanvasScaler`는 `Scale With Screen Size`, reference `1170 x 2532`, match `0.5`.
- Status panel은 safe-area를 크게 침범하지 않도록 상단 120px 아래에 둔다.
- Confirm과 Reset은 하단 중앙에 360 x 96 크기로 두되 동시에 보이지 않는다.
- Reticle은 화면 중앙 24 x 24이며 입력을 가로채지 않도록 `raycastTarget = false`다.
- 모든 `Text`에는 `LegacyRuntime.ttf`를 직렬화 fallback으로 지정하고, Canvas root의 `RuntimeFontBinder`가 실행 시 `Apple SD Gothic Neo`, `Malgun Gothic`, `Arial Unicode MS` 순으로 `Font.CreateDynamicFontFromOSFont`를 생성해 한국어 glyph가 있는 OS font를 할당한다.
- `EventSystem`과 `InputSystemUIInputModule`을 Scene에 한 개만 둔다.
- private serialized field 연결은 `SerializedObject`/`SerializedProperty`로 수행하고, hierarchy 이름에 의존하는 runtime lookup을 남기지 않는다.

`RuntimeFontBinder`의 핵심 구현:

```csharp
public sealed class RuntimeFontBinder : MonoBehaviour
{
    [SerializeField] private Text[] targets;

    private void Awake()
    {
        var font = Font.CreateDynamicFontFromOSFont(
            new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial Unicode MS" },
            32);

        if (font == null)
            return;

        foreach (var target in targets)
        {
            if (target != null)
                target.font = font;
        }
    }
}
```

- [ ] **Step 7: Scene을 저장하고 Build Settings에 하나만 등록한다**

`Assets/CamoHuntAR/Scenes/ARPlacementScene.unity`에 저장하고 `EditorBuildSettings.scenes`를 해당 Scene 하나로 설정한다. `AssetDatabase.SaveAssets()`와 `EditorSceneManager.SaveScene()`을 호출한다.

- [ ] **Step 8: batch mode로 setup을 실행한다**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -executeMethod CamoHuntAR.Editor.ARPlacementProjectSetup.BuildFromCommandLine `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\scene-setup.log'
```

Expected: exit code `0`, 세 material, 두 prefab, Scene이 생성되고 Console exception이 없다. 동일 명령을 한 번 더 실행해 중복 GameObject나 asset이 생기지 않는 것도 확인한다.

- [ ] **Step 9: 커밋한다**

```powershell
git add CamoHuntAR/Assets/CamoHuntAR/Editor CamoHuntAR/Assets/CamoHuntAR/Materials CamoHuntAR/Assets/CamoHuntAR/Prefabs CamoHuntAR/Assets/CamoHuntAR/Scenes
git commit -m "feat: generate AR placement scene and original prototype character"
```

---

### Task 7: XR Simulation에서 전체 사용자 흐름을 검증한다

**Files:**

- Modify: `CamoHuntAR/ProjectSettings/XRPackageSettings.asset`
- Modify: `CamoHuntAR/Assets/XR/Resources/XRGeneralSettings.asset`
- Modify: `CamoHuntAR/Assets/XR/Loaders/Simulation Loader.asset`
- Create: `docs/runbooks/XR_SIMULATION_TEST.md`

- [ ] **Step 1: Windows Editor용 XR Simulation loader를 활성화한다**

Unity에서 `Edit > Project Settings > XR Plug-in Management > Windows, Mac, Linux`를 열고 `XR Simulation`만 활성화한다. Windows Editor 단계에서 ARKit loader를 활성화하지 않는다.

- [ ] **Step 2: 기본 simulation environment를 선택한다**

`Window > XR > AR Foundation > XR Environment`를 열고 `DefaultSimulationEnvironment`를 선택한다. 추가 sample environment 다운로드는 필요 없다.

- [ ] **Step 3: Play Mode smoke test를 수행한다**

`ARPlacementScene`을 열고 Play를 누른다. Game view에서 우클릭+WASD로 책상 또는 바닥이 보이게 이동한다.

검증 순서:

1. 초기 안내가 나타난다.
2. plane 시각화가 나타난다.
3. plane을 왼쪽 클릭하면 캐릭터 미리보기가 하나만 생긴다.
4. 다른 위치를 클릭하면 같은 미리보기만 이동한다.
5. `배치 확정`을 누르면 preview material이 placed material로 바뀐다.
6. 확정 뒤 다른 곳을 클릭해도 위치가 변하지 않는다.
7. `다시 배치`를 누르면 캐릭터와 Anchor가 사라지고 plane이 다시 보인다.
8. 벽에서도 캐릭터의 앞면이 벽 바깥쪽을 향한다.

- [ ] **Step 4: runbook에 실제 조작과 합격 기준을 기록한다**

`XR_SIMULATION_TEST.md`에는 Unity 버전, scene 경로, loader 활성화 경로, 조작 키, 위 8개 체크 항목, XR Simulation이 실기기 카메라 품질을 검증하지 못한다는 한계를 기록한다.

- [ ] **Step 5: 자동 테스트를 다시 실행한다**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\Projects\Meccha_Chameleon\artifacts\editmode-final.xml' `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\editmode-final.log'
```

Expected: exit code `0`, failed `0`.

- [ ] **Step 6: simulation 설정과 문서를 커밋한다**

```powershell
git add CamoHuntAR/ProjectSettings CamoHuntAR/Assets/XR docs/runbooks/XR_SIMULATION_TEST.md
git commit -m "test: verify local AR placement in XR Simulation"
```

---

### Task 8: Mac과 iPhone/iPad 실기기 빌드 준비를 완료한다

**Files:**

- Modify: `CamoHuntAR/ProjectSettings/ProjectSettings.asset`
- Create: `docs/runbooks/IOS_DEVICE_BUILD.md`
- Create: `docs/runbooks/IOS_AR_ACCEPTANCE.md`

- [ ] **Step 1: iOS Player Settings를 고정한다**

설정값:

- Product Name: `CAMO HUNT AR Prototype`
- Bundle Identifier: `com.kidal.camohuntar.prototype`
- Camera Usage Description: `실제 표면을 인식하고 AR 캐릭터를 배치하기 위해 카메라를 사용합니다.`
- Target minimum iOS: `15.0`
- Architecture: `ARM64`
- Orientation: Auto Rotation, Portrait와 Landscape Left/Right 허용
- Graphics API: Metal
- Active Input Handling: Input System Package (New)

- [ ] **Step 2: Mac 준비 절차를 문서화한다**

`IOS_DEVICE_BUILD.md`에 다음 순서를 정확히 적는다.

1. M1 Mac에 Unity Hub를 설치한다.
2. Unity `6000.5.4f1`과 `iOS Build Support` 모듈을 설치한다.
3. Xcode `26` 이상을 설치하고 한 번 실행해 license와 component 설치를 끝낸다.
4. 저장소를 Mac으로 clone 또는 복사하되 `CamoHuntAR/Library`는 옮기지 않는다.
5. Unity에서 `CamoHuntAR` 프로젝트를 연다.
6. `Project Settings > XR Plug-in Management > iOS`에서 `Apple ARKit`을 활성화한다.
7. Project Validation의 fix 가능한 항목을 적용한 뒤 unresolved error가 0인지 확인한다.
8. `File > Build Profiles`에서 iOS profile을 활성화하고 `ARPlacementScene` 하나만 포함한다.
9. 새 빈 폴더로 Xcode project를 `Build`한다.
10. Xcode에서 Personal Team 또는 등록된 Team을 선택하고 iPhone 14를 target으로 실행한다.

- [ ] **Step 3: 두 실기기 acceptance checklist를 만든다**

`IOS_AR_ACCEPTANCE.md`의 기기별 체크 항목:

- iPhone 14 / iPad Air 5 각각 앱 설치와 카메라 권한 요청 성공.
- 밝고 무늬가 있는 책상에서 10초 안에 plane 감지.
- 터치 위치 20cm 이내에 캐릭터 미리보기 표시.
- 3회 연속 위치 이동 시 캐릭터가 중복 생성되지 않음.
- 확정 뒤 기기를 좌우 1m 이동해도 캐릭터가 같은 표면 위치에 안정적으로 남음.
- 벽 배치 시 캐릭터 앞면이 사용자 쪽을 향함.
- 다시 배치가 3회 연속 성공.
- 앱 종료 후 재실행하면 이전 캐릭터가 사라짐. 이는 로컬 Anchor 단계의 정상 동작.
- 조명이 부족하거나 빠르게 움직일 때 앱이 종료되지 않고 행동 안내를 표시.

- [ ] **Step 4: 최종 자동 검증과 작업 트리 검사를 실행한다**

Run:

```powershell
git diff --check
rg -n "CloudAnchor|Firebase|ARCoreExtensions|GPS|LocationService" CamoHuntAR/Assets/CamoHuntAR
& 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\Projects\Meccha_Chameleon\CamoHuntAR' `
  -runTests -testPlatform EditMode `
  -testResults 'C:\Projects\Meccha_Chameleon\artifacts\editmode-release.xml' `
  -logFile 'C:\Projects\Meccha_Chameleon\artifacts\editmode-release.log'
git status --short
```

Expected:

- `git diff --check` 출력 없음.
- 범위 밖 기능 검색 결과 없음.
- test exit code `0`, failed `0`.
- status에는 Task 8의 settings와 두 runbook만 남아 있다.

- [ ] **Step 5: iOS 준비 변경을 커밋한다**

```powershell
git add CamoHuntAR/ProjectSettings/ProjectSettings.asset docs/runbooks/IOS_DEVICE_BUILD.md docs/runbooks/IOS_AR_ACCEPTANCE.md
git commit -m "docs: prepare iOS AR device validation"
```

---

## Final Verification Gate

- [ ] `CamoHuntAR/Packages/packages-lock.json`에서 AR Foundation/ARKit이 정확히 `6.5.0`이다.
- [ ] EditMode test 결과의 failed count가 `0`이다.
- [ ] XR Simulation에서 수평면과 수직면 전체 흐름을 통과했다.
- [ ] Scene에는 캐릭터가 최대 하나, Anchor가 최대 하나만 존재한다.
- [ ] Reset 후 plane detection과 재배치가 다시 가능하다.
- [ ] 코드와 Scene에 GPS, 서버, Cloud Anchor 의존성이 없다.
- [ ] 생성한 캐릭터가 기존 게임 IP의 이름·실루엣·색 조합을 복제하지 않는다.
- [ ] iOS 실기기 검증 전에는 기능 상태를 `실기기 검증 완료`로 표현하지 않는다.
- [ ] 마지막 `git status --short`가 clean이다.

## Definition of Done

Windows 작업 단계의 완료 조건은 자동 테스트 통과와 XR Simulation 8개 항목 통과다. 전체 기능 완료 조건은 여기에 iPhone 14와 iPad Air 5 acceptance checklist까지 통과한 상태다. 앱 재실행 또는 다른 기기에서 동일 위치가 복구되지 않는 것은 이번 로컬 Anchor 프로토타입의 의도된 제한이며, 그 기능은 다음 Cloud Anchor 단계에서 별도로 설계한다.
