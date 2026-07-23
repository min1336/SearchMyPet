# Phase 0 구현 상태

기준일: 2026-07-23

## 현재 결과

로컬 AR 캐릭터 배치의 공통 코어가 구현되어 있습니다. 세션 추적이 준비된 뒤 수평·수직 평면을 탐지하고, 지원되는 추적 중 평면에만 미리보기를 만들며, 사용자가 확정하면 로컬 AR Anchor에 연결합니다. Reset은 Anchor와 캐릭터를 제거하고 평면 탐지 상태로 돌아갑니다.

| 영역 | 상태 | 비고 |
| --- | --- | --- |
| Unity 프로젝트와 패키지 잠금 | 구현 | Unity 6000.5.4f1, AR Foundation/ARKit 6.5.0 |
| 기본 캐릭터 모델 | 구현 | 제공 GLB에서 보조 메시·애니메이션을 제거한 REST 포즈 FBX, 높이 0.24m |
| 상태 머신·표면 pose·추적 안내 | 구현 | EditMode 테스트 포함 |
| 터치·마우스 입력과 UI 차단 | 구현 | Input System 1.19.0 |
| 수평·수직 평면 raycast | 구현 | Tracking 상태와 polygon 내부 hit만 허용 |
| 미리보기·확정·Reset | 구현 | 비동기 Anchor 실패와 수명주기 방어 포함 |
| 생성형 씬·프리팹·XR 설정 | 구현 | 메뉴·CLI에서 반복 실행 가능 |
| Safe Area UI | 구현 | 세로·가로 회전 설정 포함 |
| EditMode 자동 검증 | 통과 | 40 / 40, 실패·건너뜀 0 |
| XR Simulation 흐름 검증 | 통과 | 1 / 1, 세션 → 평면 → 배치 → Anchor → Reset |
| iOS Xcode 빌드 | 미검증 | macOS, Xcode 26+, 서명 환경 필요 |
| iPhone 14 / iPad Air 5 실기기 | 미검증 | 인수 체크리스트의 모든 항목 미실행 |

## 구현 경계

Phase 0의 저장 단위는 현재 AR 세션에 속한 로컬 Anchor입니다. 앱 재실행 후 복구, 다른 기기와 공유, 지리적 위치 기반 노출은 지원하지 않습니다.

다음 항목은 의도적으로 제외했습니다.

- Android 및 ARCore provider
- Cloud Anchors 또는 다른 공간 Anchor 공유 서비스
- GPS, 서버 API, 인증, 데이터베이스, 동기화
- 50m 현재 위치 콘텐츠 제한
- 실시간 멀티플레이와 소셜·제작자 보상

## 완료 판정 순서

1. 생성 스크립트를 두 번 실행해 두 번째 실행에서 불필요한 에셋 변경이 없는지 확인합니다.
2. EditMode와 PlayMode 결과 XML에서 실패와 예외가 없는지 확인합니다.
3. [XR Simulation 수동 체크리스트](runbooks/XR_SIMULATION_TEST.md)를 완료합니다.
4. Xcode 26 이상에서 iOS Development Build를 생성합니다.
5. [iPhone 14 및 iPad Air 5 체크리스트](runbooks/IOS_AR_ACCEPTANCE.md)를 두 기기 모두 완료합니다.

## 알려진 검증 한계

자동 XR Simulation 흐름은 통과했지만 Game view의 수동 시각·포인터 체크리스트는 아직 실행하지 않았습니다. 또한 XR Simulation 통과는 ARKit 카메라 권한, 실제 조명, 기기 열 상태, 센서 추적, 앱 수명주기를 보증하지 않습니다. Phase 0 기기 검증을 완료하려면 최소 두 대상 기기에서 반복 배치와 Reset을 확인해야 합니다.
