# Search My Pet 작업 목록

## 현재 목표

첫 마일스톤은 실제 AR 지원 스마트폰에서 사용자가 **수직 벽을 스캔하고 탭했을 때**, 캐릭터가 벽 바깥쪽을 향해 붙어 보이며 카메라를 움직인 뒤에도 같은 물리적 위치에 안정적으로 남는지 검증하는 것이다.

이 디렉터리의 체크박스가 진행 현황의 단일 기준이다. 검증 증거(빌드 로그, 화면 캡처 또는 영상, 기기·버전 정보)가 없으면 완료로 바꾸지 않는다.

## 읽는 순서

1. [CHECKLIST.md](CHECKLIST.md) — 전체 순서와 상태
2. [00_결정과_환경준비.md](00_결정과_환경준비.md) — 대상 기기와 기술 기준 확정
3. [01_AR_벽면탐지_스파이크.md](01_AR_벽면탐지_스파이크.md) — 수직 벽 탐지 확인
4. [02_벽면후보와_프리뷰_UX.md](02_벽면후보와_프리뷰_UX.md) — 벽만 선택하는 UX
5. [03_앵커기반_확정배치.md](03_앵커기반_확정배치.md) — 배치, 재배치, 초기화
6. [04_캐릭터프리팹_계약.md](04_캐릭터프리팹_계약.md) — 모델 축·피벗·스케일
7. [05_실기기_검증과_판정.md](05_실기기_검증과_판정.md) — Go/No-Go 판정

## 공통 원칙

- Unity Editor/XR Simulation 결과는 실기기 ARKit/ARCore 결과가 아니다.
- 바닥이나 책상을 임시 배치 대상으로 대체하지 않는다. 이 마일스톤은 수직 벽만 허용한다.
- `ARPlane`의 자식으로만 붙이지 않고, 확정 배치는 `ARAnchor`를 기준으로 한다.
- 로컬 앵커는 앱 재실행, 다른 기기, 다른 플레이어의 동일 장소 재현을 보장하지 않는다.
- 실제 그림 그리기, 색 위장, 지도, 포획, 로그인, 서버, 다중 사용자 공유는 이 마일스톤의 완료 조건이 아니다.

## 경로 규약

Unity 프로젝트가 아직 생성되지 않았으므로, 구현 시 프로젝트 루트를 `<UNITY_PROJECT_ROOT>`로 표기한다. 프로젝트 생성 후 이 표기를 실제 경로로 일괄 확정한다. 예상 구현 경로는 다음과 같다.

- `<UNITY_PROJECT_ROOT>/Assets/Scenes/WallPlacementValidation.unity`
- `<UNITY_PROJECT_ROOT>/Assets/Scripts/AR/WallPlacementController.cs`
- `<UNITY_PROJECT_ROOT>/Assets/Scripts/AR/PlacementReticle.cs`
- `<UNITY_PROJECT_ROOT>/Assets/Prefabs/ChameleonWallPlacementTarget.prefab`
- `<UNITY_PROJECT_ROOT>/Assets/Settings/WallPlacementConfig.asset`
- `<UNITY_PROJECT_ROOT>/Assets/Tests/EditMode/WallPlacementControllerTests.cs`
