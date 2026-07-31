# 2026-07-29 iPhone 벽면 캐릭터 배치 검증

## 판정 요약

- **구현·컴파일·실기기 배치:** 통과
- **Wall Placement Technical Go (iOS):** 보류
- **보류 이유:** 0.5~1.5m 이동, 시선 전환, 재관찰 뒤 같은 물리 위치 유지 여부를 보여 주는 영상 또는 육안 기록이 아직 없다.

## 검증 대상

- Unity 프로젝트: `C:\Projects\SearchMyPet\SearchMyPetUnity`
- Windows iOS export: `C:\Projects\SearchMyPet\outputs\ios-xcode-wall-placement`
- Mac Xcode 프로젝트: `/Users/kimminhyeok/SearchMyPet-iOS-verify/ios-xcode-wall-character-placement-20260729/Unity-iPhone.xcodeproj`
- 실기기: iPhone 14, iOS 27.0 beta (`24A5390f`)
- 도구 버전: Unity `6000.5.4f1`, AR Foundation/ARKit `6.6.0`, Input System `1.19.0`, Xcode `26.6`

개인 식별이 가능한 기기 식별자와 시리얼 번호는 이 문서에 기록하지 않는다.

## 자동 검증

| 구분 | 결과 | 증거 |
| --- | --- | --- |
| EditMode 전체 테스트 | `13/13 Passed`, 실패 0 | `SearchMyPetUnity/Logs/all-editmode-wall-placement-final.xml` |
| EditMode 실행 로그 | 종료 코드 0 | `SearchMyPetUnity/Logs/all-editmode-wall-placement-final.log` |
| Windows iOS export | `Build Finished, Result: Success` | `SearchMyPetUnity/Logs/ios-export-wall-character-placement.log` |
| Mac Xcode signed build | 성공 | Xcode Build activity log `8582DE0A-CFFE-4031-906A-2693D106F73A.xcactivitylog` |
| iPhone 설치·실행 | `Successfully installed`, `Successfully launched SearchMyPetUnity` | 아래 Xcode Run `.xcresult` |

Xcode Run 원본:

```text
/Users/kimminhyeok/Library/Developer/Xcode/DerivedData/Unity-iPhone-egdaxbcwwzasfufezrocrxbvevaf/Logs/Launch/Run-Unity-iPhone-2026.07.29_18-06-50-+0900.xcresult
```

## 실기기 WallPlacement 로그

| 이벤트 | 횟수 |
| --- | ---: |
| `candidate-valid` | 16 |
| `anchor-created` | 5 |
| `character-placed` | 5 |
| `repositioning-started` | 2 |
| `reset-complete` | 2 |
| `placement-failed` | 0 |
| `placement-rejected` | 0 |

- 다섯 번의 `character-placed`는 각각 `anchor-created` 직후 기록됐다.
- Reset 로그는 두 번 모두 `anchors=0 characters=0 state=Scanning`을 기록했다.
- 마지막 배치는 2026-07-29 18:13:23 KST이며, 18:15:12 기기 연결이 끊길 때까지 약 109초 동안 추가 상태 전이 또는 배치 실패가 없었다.
- 사용자는 최신 앱 화면에서 캐릭터가 보였다고 `y`로 확인했다. 이것은 사용자 육안 확인이며 별도 캡처 증거는 아니다.
- Xcode Run의 마지막 실패는 앱 오류가 아니라 USB/CoreDevice 연결 해제로 인한 `lost connection`이다. 이후 기기는 다시 연결됐고 앱 프로세스가 계속 실행 중인 것이 확인됐다.

## 현재 확인된 범위

- 화면 중앙 `PlaneWithinPolygon` raycast
- 추적 중인 최소 크기 이상의 수직 Plane만 후보로 허용
- 후보 안정화 시간 적용
- 벽 법선 방향 pose, world up, 0.02m 벽 offset
- 0.18m 설정 기반의 교체 가능한 임시 캐릭터 Prefab
- Plane 부착 `ARAnchor` 생성과 캐릭터 자식 생성
- 한 번에 캐릭터 하나만 배치
- 재배치와 Reset 경로

## 아직 확인하지 않은 수용 항목

- 배치 뒤 0.5~1.5m 이동하고 다른 곳을 본 뒤 같은 벽을 재관찰했을 때 실제 화면상 같은 위치·방향 유지
- 위 이동·재관찰 시나리오 5회 중 4회 이상, 각 30초 유지
- 바닥·테이블에서 실기기 배치 거부 3회
- 흰색 벽·유색 벽·저조도 또는 반사 벽에서의 가독성과 실패 안내
- 정면·사선 및 서로 다른 방향의 벽에서 캐릭터 축·피벗·간격의 시각 품질
- Plane 병합/subsumption 상황의 명시적 검증

따라서 `CHECKLIST.md`의 Task 02~05와 최종 Go/No-Go는 아직 `[ ]`로 유지한다.

## 알려진 비차단 로그와 정적 검사

- Xcode 콘솔에는 배치 실패는 없었지만, 비-LiDAR 기기의 ARKit Meshing 초기화 실패, 시스템 CoreMotion 설정 파일 권한 경고, `mach_vm_allocate()` 메시지가 있었다. 이번 배치 흐름은 이후 정상 진행됐다.
- 전체 `git diff --check`는 Unity가 생성·수정한 `.unity`/`.asset` YAML 10개 파일의 빈 값 뒤 공백 305건 때문에 실패한다. 새 C# 파일, README와 이번 검증 문서에서는 줄 끝 공백이 검출되지 않았다. 생성 YAML을 수동 정리하면 Unity 재저장 시 다시 생길 수 있어 보류한다.

## 마지막 실기기 확인 절차

1. 캐릭터를 밝은 질감의 수직 벽에 배치한다.
2. 0.5~1.5m 옆으로 이동한다.
3. 카메라를 다른 곳으로 돌렸다가 같은 벽을 다시 본다.
4. 캐릭터가 같은 벽의 같은 상대 위치·방향에 있는지 30초 확인한다.
5. 위 과정을 총 5회 기록하고 성공 횟수를 판정표에 남긴다.
