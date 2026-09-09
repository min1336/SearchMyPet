# XR Simulation 검증 Runbook

이 절차는 실제 iOS 기기 빌드 전에 로컬 배치 흐름을 빠르게 검증합니다. XR Simulation은 Editor용 provider이므로 실기기 ARKit 인수 테스트를 대체하지 않습니다.

## 사전 조건

- Unity 6000.5.4f1
- 패키지 복원이 완료된 `CamoHuntAR` 프로젝트
- `ARPlacementScene`이 Build Settings의 첫 번째 활성 씬
- Standalone에 XR Simulation loader가 지정됨
- 테스트 전에 생성 스크립트를 최소 한 번 실행

## 자동 PlayMode 테스트

저장소 루트의 PowerShell에서 실행합니다.

```powershell
$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe"
$RepoRoot = (Get-Location).Path
$UnityProject = Join-Path $RepoRoot "CamoHuntAR"

& $UnityExe -batchmode `
  -projectPath $UnityProject `
  -runTests -testPlatform PlayMode `
  -testResults (Join-Path $RepoRoot "artifacts\playmode-final.xml") `
  -logFile (Join-Path $RepoRoot "artifacts\playmode-final.log")
```

자동 시나리오는 다음을 검증하도록 구성되어 있습니다.

1. `ARSessionState.SessionTracking` 도달
2. 기본 환경에서 배치 가능한 수평 또는 수직 평면 탐지
3. world-space raycast로 미리보기 생성
4. `Previewing` 상태와 미리보기 오브젝트 1개 확인
5. 확정 버튼의 런타임 listener를 통해 `Placed` 상태, Anchor 1개, 평면 시각화 중지 확인
6. Reset 버튼의 런타임 listener를 통해 `Detecting` 상태, Anchor·캐릭터 0개, 평면 탐지 재개 확인

2026-07-22 자동 실행은 1 / 1 Passed, 실패·건너뜀 0으로 완료됐습니다. `artifacts/playmode-final.log`에도 C# 경고·오류와 처리되지 않은 예외가 없었습니다. 원본 판정 자료는 `artifacts/playmode-final.xml`과 같은 이름의 로그입니다.

## 수동 검증

아래 항목은 자동 흐름과 별개이며 아직 미실행입니다.

1. Unity에서 `Assets/CamoHuntAR/Scenes/ARPlacementScene.unity`를 엽니다.
2. `Window > XR > AR Foundation > XR Environment`를 열고 평면이 있는 환경을 활성화합니다.
3. Play Mode에 진입하고 Game view에 포커스를 둡니다.
4. 기본 navigation 입력인 마우스와 `WASD`, `Q`, `E`, `Shift`를 사용해 카메라를 천천히 움직입니다.

아래 결과를 직접 기록합니다.

- [ ] 세션 초기화 중에는 배치 확정이 비활성화된다.
- [ ] 추적이 시작되면 상태 안내가 평면 탐색 단계로 바뀐다.
- [ ] 수평 평면과 수직 평면이 반투명하게 보인다.
- [ ] 지원되는 평면을 클릭하면 반투명 캐릭터 미리보기 하나만 생성된다.
- [ ] 다른 위치를 클릭하면 같은 미리보기가 이동하고 방향이 표면에 맞는다.
- [ ] 버튼·상태 패널을 클릭해도 캐릭터 위치가 바뀌지 않는다.
- [ ] 확정하면 캐릭터가 불투명해지고 평면 시각화가 중지된다.
- [ ] 확정 후 추가 클릭으로 새 캐릭터가 생기지 않는다.
- [ ] Reset하면 캐릭터와 Anchor가 사라지고 평면 탐지가 재개된다.
- [ ] Reset 뒤 두 번째 배치·확정·Reset도 동일하게 동작한다.
- [ ] Play Mode 종료 시 처리되지 않은 예외가 Console에 남지 않는다.

## 실패 분류

| 증상 | 우선 확인 |
| --- | --- |
| SessionTracking에 도달하지 않음 | Standalone XR Simulation loader, Console의 provider 초기화 오류 |
| 평면이 생기지 않음 | 활성 XR Environment의 geometry, 카메라 이동, plane detection mode |
| 클릭해도 미리보기가 없음 | Game view 포커스, Input System, 평면 polygon과 tracking state |
| 확정 후 Anchor가 없음 | `ARAnchorManager` 활성 상태와 PlayMode 로그 |
| Reset 예외 | Anchor add event가 반영된 프레임과 provider remove 로그 |
| 버튼 클릭이 배치로 전달됨 | `EventSystem`, `InputSystemUIInputModule`, pointer device id |
