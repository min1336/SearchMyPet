# CAMO HUNT AR — 로컬 배치 프로토타입

이 저장소에는 카메라 추적, 수평·수직 평면 탐지, 캐릭터 미리보기, 로컬 AR Anchor 확정, 초기화까지 이어지는 Unity Phase 0 프로토타입이 들어 있습니다. 현재 구현은 한 기기 안에서 배치 감각과 기술 가능성을 검증하는 범위입니다.

## 기술 기준

| 항목 | 버전·설정 |
| --- | --- |
| Unity | 6000.5.4f1 |
| AR Foundation / Apple ARKit XR Plug-in | 6.5.0 / 6.5.0 |
| Input System | 1.19.0 |
| XR Plug-in Management | 4.6.0 |
| iOS | 15.0 이상, Metal, ARKit loader |
| Xcode | 26 이상 |

ARKit 6.5.0은 6.4.0에서 Xcode 26.0.1로 다시 빌드된 정적 라이브러리를 계승하므로 iOS 빌드에는 Xcode 26 이상이 필요합니다. 자세한 내용은 [Apple ARKit XR Plug-in changelog](https://docs.unity.cn/Packages/com.unity.xr.arkit%406.5/changelog/CHANGELOG.html)를 참고하세요.

## 빠른 시작

저장소 루트의 PowerShell에서 실행합니다.

```powershell
$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"
$RepoRoot = (Get-Location).Path
$UnityProject = Join-Path $RepoRoot "CamoHuntAR"

& $UnityExe -batchmode -quit `
  -projectPath $UnityProject `
  -executeMethod CamoHuntAR.Editor.ARPlacementProjectSetup.BuildFromCommandLine `
  -logFile (Join-Path $RepoRoot "artifacts\setup.log")
```

이 생성기는 반복 실행해도 같은 에셋과 설정을 갱신하도록 설계되었습니다. Unity 메뉴의 `CAMO HUNT > Build AR Placement Prototype`으로도 실행할 수 있습니다. 이후 [ARPlacementScene.unity](CamoHuntAR/Assets/CamoHuntAR/Scenes/ARPlacementScene.unity)을 열고 Play Mode에서 XR Simulation을 사용합니다.

## 구조

| 영역 | 책임 |
| --- | --- |
| `PlacementStateMachine` | `Initializing → Detecting → Previewing → Placed` 상태와 Reset 전이 |
| `ARPlacementController` | 입력, UI 터치 차단, 평면 raycast, 미리보기, 비동기 Anchor 생성·제거 |
| `SurfacePoseUtility` | 수평·수직 표면별 캐릭터 위치와 방향 계산 |
| `ARTrackingStatusController` | 권한·세션·추적 상태를 사용자 안내 문구로 변환 |
| `SafeAreaFitter`, `RuntimeFontBinder` | 노치·회전 대응 UI와 런타임 폰트 바인딩 |
| `ARPlacementProjectSetup` | 씬, 프리팹, 머티리얼, XR loader, iOS 설정을 재현 가능하게 생성 |

생성되는 주요 산출물은 다음과 같습니다.

- 씬: `CamoHuntAR/Assets/CamoHuntAR/Scenes/ARPlacementScene.unity`
- 캐릭터: `CamoHuntAR/Assets/CamoHuntAR/Prefabs/CamoCritter.prefab`
- 캐릭터 모델: `CamoHuntAR/Assets/CamoHuntAR/Art/MeshyOpenArmsCharacter.fbx`
- 탐지 평면: `CamoHuntAR/Assets/CamoHuntAR/Prefabs/DetectedPlane.prefab`
- 머티리얼: `CharacterPreview.mat`, `CharacterPlaced.mat`, `DetectedPlane.mat`
- XR 설정: Standalone의 XR Simulation loader, iOS의 ARKit loader

## 캐릭터 에셋

기본 캐릭터 원본은
`SourceAssets/Meshy/Meshy_AI_Open_Arms_biped_Character_output.glb`에 보관합니다.
Unity 프로젝트에는 별도 GLB importer를 추가하지 않고 Blender 5.2 LTS로 FBX를 생성해 사용합니다.

변환 과정은 리깅용 보조 `Icosphere`와 GLB 안에 포함돼 있던 걷기 동작을 제거하고,
팔을 벌린 REST 포즈만 내보냅니다. 생성기는 모델 높이를 0.24m로 정규화하고 발을
배치 표면에 맞춘 뒤 기존 미리보기·확정 머티리얼을 적용합니다.

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b `
  --python tools/blender/prepare_character.py -- `
  --source SourceAssets/Meshy/Meshy_AI_Open_Arms_biped_Character_output.glb `
  --output CamoHuntAR/Assets/CamoHuntAR/Art/MeshyOpenArmsCharacter.fbx `
  --report artifacts/meshy-character-report.json
```

씬에는 `AR Session`, Mobile AR용 `XR Origin`, 수평·수직 `ARPlaneManager`, `ARRaycastManager`, `ARAnchorManager`, 상태 안내 UI, 중앙 reticle, 확정·초기화 버튼과 Input System 기반 `EventSystem`이 포함됩니다.

## 자동 검증

EditMode 검증은 상태 전이, 표면 pose, 추적 안내, 프리뷰 외형, 생성 에셋·씬 참조와 XR loader 설정을 확인합니다.

```powershell
& $UnityExe -batchmode `
  -projectPath $UnityProject `
  -runTests -testPlatform EditMode `
  -testResults (Join-Path $RepoRoot "artifacts\editmode-final.xml") `
  -logFile (Join-Path $RepoRoot "artifacts\editmode-final.log")
```

PlayMode 검증은 XR Simulation 세션 시작, 평면 탐지, raycast, 미리보기, Anchor 확정, Reset 흐름을 대상으로 합니다.

```powershell
& $UnityExe -batchmode `
  -projectPath $UnityProject `
  -runTests -testPlatform PlayMode `
  -testResults (Join-Path $RepoRoot "artifacts\playmode-final.xml") `
  -logFile (Join-Path $RepoRoot "artifacts\playmode-final.log")
```

2026-07-22 최종 실행 결과는 다음과 같습니다. 두 로그 모두 C# 경고·오류와 처리되지 않은 예외가 없었습니다.

| Suite | 결과 | 통과 / 전체 |
| --- | --- | --- |
| EditMode | Passed | 40 / 40 |
| PlayMode XR Simulation | Passed | 1 / 1 |

생성 스크립트도 연속 실행하여 두 번째 실행에서 생성 대상 35개 파일의 내용 변경이 0건임을 확인했습니다. 결과 판정의 원본은 로컬 `artifacts/editmode-final.xml`, `artifacts/playmode-final.xml`과 각 로그입니다.

## 범위와 다음 단계

이 Phase 0에는 Android/ARCore, Cloud Anchors, GPS, 서버 동기화, 다른 사용자와의 공유, 50m 콘텐츠 경계가 포함되지 않습니다. 이 기능들을 로컬 Anchor와 혼동해 확장하지 말고, 배치 UX와 실제 iOS 기기 안정성이 확인된 뒤 별도 단계로 설계해야 합니다.

- [구현 상태](docs/IMPLEMENTATION_STATUS.md)
- [XR Simulation 검증](docs/runbooks/XR_SIMULATION_TEST.md)
- [iOS 빌드 절차](docs/runbooks/IOS_DEVICE_BUILD.md)
- [iOS 실기기 인수 체크리스트](docs/runbooks/IOS_AR_ACCEPTANCE.md)
