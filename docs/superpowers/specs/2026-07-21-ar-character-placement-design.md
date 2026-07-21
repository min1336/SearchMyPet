# AR 캐릭터 배치 기능 설계

작성일: 2026-07-21  
대상 프로젝트: CAMO HUNT AR  
단계: 로컬 AR 배치 기술 검증

## 1. 목표

iPhone 또는 iPad의 카메라로 실제 표면을 인식하고, 사용자가 화면을 터치한 3D 위치에 캐릭터를 배치할 수 있는 Unity AR 기능을 만든다.

이 단계의 성공 기준은 다음과 같다.

- 앱 실행 후 후면 카메라 영상이 표시된다.
- 바닥, 책상 또는 벽과 같은 평면을 감지한다.
- 감지된 평면을 터치하면 해당 위치에 캐릭터 미리보기가 나타난다.
- 다른 위치를 터치하면 미리보기가 새 위치로 이동한다.
- 배치를 확정하면 캐릭터가 AR Anchor에 고정된다.
- 초기화 버튼으로 캐릭터를 제거하고 다시 배치할 수 있다.

## 2. 이번 단계의 범위

포함 기능:

- AR 세션과 후면 카메라 실행
- 수평 및 수직 평면 감지
- 화면 터치 위치에서 AR Raycast 실행
- 캐릭터 미리보기 생성 및 이동
- 배치 확정과 로컬 AR Anchor 생성
- 배치 초기화
- 추적 상태와 사용자 행동 안내
- Unity XR Simulation을 이용한 에디터 검증
- iOS 빌드를 고려한 프로젝트 설정

제외 기능:

- Cloud Anchor host/resolve
- 다른 기기와 위치 공유
- GPS, 지도, 서버, 계정
- 캐릭터 꾸미기, 점수, 포획
- 방 전체 3D 메시 또는 RoomPlan
- Android 빌드

## 3. 기술 구성

- Unity 6000.5.4f1
- AR Foundation 6.x
- ARKit XR Plugin 6.x
- XR Plug-in Management
- Input System
- iOS Build Support와 Xcode는 Mac에서 최종 실기기 빌드에 사용

Cloud Anchor 패키지는 이 단계에 추가하지 않는다. 로컬 배치가 실기기에서 안정적으로 작동한 뒤 별도 단계로 통합한다.

## 4. 사용자 흐름

1. 앱이 AR 세션을 시작한다.
2. 화면 상단에 `기기를 천천히 움직여 표면을 찾아주세요`를 표시한다.
3. AR Foundation이 수평 또는 수직 평면을 감지한다.
4. 배치 가능한 화면 위치에 조준점 또는 평면 시각화를 표시한다.
5. 사용자가 평면을 터치하면 캐릭터 미리보기를 Raycast 결과 Pose에 배치한다.
6. 사용자가 다른 평면을 터치하면 기존 미리보기를 이동한다.
7. 사용자가 `배치 확정`을 누르면 해당 Pose에 AR Anchor를 만들고 캐릭터를 Anchor의 자식으로 연결한다.
8. 평면 시각화를 숨기고 캐릭터 위치 변경을 막는다.
9. `다시 배치`를 누르면 Anchor와 캐릭터를 제거하고 2단계로 돌아간다.

## 5. 상태 모델

`Initializing -> Detecting -> Previewing -> Placed`

- `Initializing`: AR 세션과 권한을 준비한다.
- `Detecting`: 평면을 찾고 터치를 기다린다.
- `Previewing`: 캐릭터 미리보기가 존재하며 위치를 변경할 수 있다.
- `Placed`: Anchor가 생성되었고 캐릭터가 고정됐다.

오류 또는 초기화가 발생하면 `Detecting`으로 돌아간다.

## 6. Unity Scene 구성

```text
ARPlacementScene
├─ AR Session
├─ XR Origin (Mobile AR)
│  ├─ Camera Offset
│  │  └─ Main Camera
│  ├─ AR Plane Manager
│  ├─ AR Raycast Manager
│  └─ AR Anchor Manager
├─ App
│  ├─ ARPlacementController
│  └─ ARTrackingStatusController
└─ Canvas
   ├─ StatusText
   ├─ CenterReticle
   ├─ ConfirmButton
   └─ ResetButton
```

프로젝트는 하나의 Scene과 소수의 스크립트로 유지한다. 이번 단계에서는 별도 DI 프레임워크나 복잡한 상태 관리 라이브러리를 사용하지 않는다.

## 7. 컴포넌트 책임

### ARPlacementController

- 터치 입력을 읽는다.
- `ARRaycastManager.Raycast`로 평면을 검사한다.
- 미리보기 캐릭터를 생성하거나 이동한다.
- 확정 시 `ARAnchorManager`를 통해 Anchor를 생성한다.
- 캐릭터를 Anchor 아래에 연결한다.
- 확정 및 초기화를 처리한다.
- 현재 배치 상태 변경 이벤트를 발생시킨다.

### ARTrackingStatusController

- AR Session 추적 상태를 읽는다.
- 추적 준비, 제한, 실패 상태를 사용자 문구로 변환한다.
- 감지된 평면 존재 여부에 따라 배치 안내를 변경한다.

### Character Prefab

- 기존 게임 IP를 사용하지 않는 임시 독자 캐릭터다.
- 초기 구현에서는 간단한 Unity Primitive 조합을 사용한다.
- 바닥 또는 벽에서 확인하기 쉬운 크기와 색상을 사용한다.
- Collider를 포함해 이후 포획 터치 기능을 추가할 수 있게 한다.

## 8. 좌표와 방향 처리

- AR Foundation과 Unity의 위치 단위는 미터로 취급한다.
- 캐릭터 위치는 Raycast가 반환한 Pose를 사용한다.
- 수평면에서는 캐릭터의 위쪽 축이 평면 노멀과 정렬되도록 한다.
- 수직면에서는 캐릭터의 앞쪽 방향이 표면 바깥쪽을 향하도록 보정한다.
- 캐릭터 모델별 원점 차이는 Prefab 내부의 `VisualRoot` 로컬 Transform으로 조정한다.
- 배치 확정 전에는 미리보기 머티리얼을 사용하고, 확정 후 일반 머티리얼로 변경한다.

## 9. 오류와 예외 처리

- 카메라 권한 없음: 권한이 필요하다는 안내를 표시하고 배치를 비활성화한다.
- AR 미지원: 지원되지 않는 기기라는 안내를 표시한다.
- 추적 초기화 중: 기기를 천천히 움직이라는 안내를 표시한다.
- 추적 제한: 조명을 확보하고 기기를 천천히 움직이라는 안내를 표시한다.
- 평면 미감지: 터치 입력을 무시하고 표면 탐색 안내를 유지한다.
- Anchor 생성 실패: 미리보기 상태를 유지하고 다시 확정할 수 있게 한다.
- 중복 터치: 한 프레임에 하나의 Raycast만 처리한다.

오류 때문에 앱이 종료되지 않도록 하고, 사용자가 취할 다음 행동을 항상 화면에 표시한다.

## 10. 검증 방법

### 에디터 검증

- XR Simulation 환경에서 수평면을 터치해 미리보기가 생성되는지 확인한다.
- 다른 위치를 터치했을 때 하나의 미리보기만 이동하는지 확인한다.
- 확정 이후 터치로 위치가 바뀌지 않는지 확인한다.
- 초기화 이후 다시 배치할 수 있는지 확인한다.

### iOS 실기기 검증

- iPhone 14와 iPad Air 5에서 카메라와 AR Session이 시작되는지 확인한다.
- 밝고 무늬가 있는 책상과 벽에서 각각 배치한다.
- 기기를 움직여도 캐릭터가 표면에 안정적으로 남는지 확인한다.
- 앱을 재실행하면 로컬 Anchor가 사라지는 것을 확인한다. 이는 이번 단계의 정상 동작이며, 다음 Cloud Anchor 단계에서 지속성을 추가한다.

## 11. 다음 단계와 연결

로컬 배치가 두 실기기에서 확인되면 현재 Anchor 생성 지점을 Cloud Anchor host 입력으로 사용한다. 다음 단계는 다음 데이터만 추가한다.

- Cloud Anchor ID
- Anchor 기준 캐릭터 로컬 Transform
- 생성 시각과 만료 시각

현재 `ARPlacementController`가 직접 Cloud API를 호출하지 않게 구성해, 이후 `CloudAnchorController`를 추가해도 배치 입력 로직을 유지한다.

## 12. 구현 결정

- 첫 목표는 로컬 AR 배치 하나다.
- 서버와 GPS는 추가하지 않는다.
- 수평면과 수직면을 모두 지원하되, 첫 실기기 검증은 수평면에서 수행한다.
- 독자적인 임시 캐릭터를 사용한다.
- 에디터 시뮬레이션과 iOS 실기기 검증을 모두 완료해야 기능이 완성된 것으로 본다.
