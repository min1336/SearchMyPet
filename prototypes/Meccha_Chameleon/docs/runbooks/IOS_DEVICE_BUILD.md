# iOS 기기 빌드 Runbook

## 요구 환경

- macOS 빌드 머신
- Unity 6000.5.4f1과 iOS Build Support 모듈
- Xcode 26 이상
- Apple Developer 서명 계정과 대상 기기가 포함된 provisioning
- iOS 15 이상, ARKit 지원 iPhone 또는 iPad

Apple ARKit XR Plug-in 6.5.0은 Xcode 26 이상을 요구하는 6.4.0 빌드 기준을 계승합니다. Xcode 16 계열에서는 진행하지 않습니다. 근거는 [ARKit package changelog](https://docs.unity.cn/Packages/com.unity.xr.arkit%406.5/changelog/CHANGELOG.html)입니다.

## Unity 준비

1. 저장소를 macOS에 가져오고 Unity Hub에서 `CamoHuntAR` 폴더를 엽니다.
2. 패키지 복원과 스크립트 컴파일이 끝날 때까지 기다립니다.
3. `CAMO HUNT > Build AR Placement Prototype`을 실행합니다.
4. `File > Build Profiles`에서 iOS를 선택하고 활성 플랫폼으로 전환합니다.
5. 다음 설정을 확인합니다.

| 설정 | 기대값 |
| --- | --- |
| Minimum iOS Version | 15.0 |
| Graphics API | Metal |
| Active Input Handling | Input System Package (New) |
| XR Plug-in Management / iOS | Apple ARKit 활성 |
| 첫 빌드 씬 | `ARPlacementScene` 활성 |
| Camera Usage Description | 카메라를 AR 배치에 사용하는 이유가 표시됨 |

기본 bundle identifier는 프로토타입용이므로 실제 Team에서 서명 가능한 고유 값으로 바꿉니다. 공유 인증서나 provisioning 파일을 저장소에 커밋하지 않습니다.

## Xcode 프로젝트 생성과 실행

1. Unity에서 빈 출력 폴더를 지정해 iOS Development Build를 생성합니다.
2. 생성된 Xcode 프로젝트를 Xcode 26 이상에서 엽니다.
3. 앱 target의 Team, bundle identifier, signing 상태를 확인합니다.
4. 대상 기기를 연결하고 잠금 해제·개발자 모드를 확인합니다.
5. 빌드 경고보다 먼저 compile/link/sign 오류를 모두 해결합니다.
6. 기기에 설치하고 첫 실행의 카메라 권한을 허용합니다.
7. Xcode Console을 연결한 상태에서 배치·확정·Reset을 반복합니다.

## 빌드 기록

| 항목 | 기록 |
| --- | --- |
| Git commit |  |
| Unity | 6000.5.4f1 |
| AR Foundation / ARKit | 6.5.0 / 6.5.0 |
| Xcode |  |
| macOS |  |
| Bundle identifier |  |
| Build number |  |
| Signing Team |  |
| 생성 일시 |  |

Windows Editor의 XR Simulation 결과만으로 iOS 빌드 가능 여부를 확정하지 않습니다. Xcode build log와 실제 설치 결과를 릴리스 증거로 보관합니다.

