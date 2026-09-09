# Search My Pet (SMP) — AR 수직 벽면 인터랙션 게임

[![Unity](https://img.shields.io/badge/Unity-6000.5.4f1-black.svg?logo=unity)](https://unity.com/)
[![ARKit](https://img.shields.io/badge/ARKit%20XR-6.5.0-blue.svg?logo=apple)](https://developer.apple.com/augmented-reality/arkit/)
[![Platform](https://img.shields.io/badge/iOS-16.0%2B-lightgrey.svg?logo=apple)](https://developer.apple.com/ios/)

> **"매일 걷던 동네 골목과 벽면이 나만의 캐릭터를 숨기고 찾는 보물찾기 필드가 된다."**  
> *Search My Pet*은 인기 인터랙션 게임 *Meccha Chameleon*에서 영감을 받아, 현실의 **수직 벽면(Vertical Wall)만을 정밀하게 감지**하여 캐릭터를 배치하고, **3D 실시간 UV 텍스처 페인팅**으로 위장(Camouflage)시키며, **GPS 지도(MapKit)**와 카메라 광학 줌을 활용해 탐색·수집하는 증강현실(AR) 프로젝트입니다.

---

## 📂 프로젝트 구조 (Monorepo)

본 저장소는 Phase 0 프로토타입([`Meccha_Chameleon`](https://github.com/min1336/Meccha_Chameleon.git))의 커밋 히스토리를 보존한 상태로 통합(Git Subtree) 관리됩니다.

```text
SMP/
├── SearchMyPetUnity/             # [Phase 1] 메인 Unity AR 프로젝트
│   ├── Assets/
│   │   ├── Scripts/AR/           # 수직 벽면 감지, 3D 페인팅, 네이티브 제어 C# 스크립트
│   │   ├── Plugins/iOS/          # iOS 네이티브 C/Obj-C++ 브릿지 (카메라 줌, MapKit)
│   │   ├── Editor/               # Xcode 빌드 후처리기 및 자동화 파이프라인
│   │   └── Tests/Editor/         # EditMode 13개 단위 테스트
│   └── ProjectSettings/
├── prototypes/
│   └── Meccha_Chameleon/         # [Phase 0] 초기 배치 프로토타입 (CamoHuntAR)
│       ├── CamoHuntAR/           # 초기 Unity 프로젝트 및 상태 머신
│       ├── SourceAssets/         # Meshy AI 3D 원본 에셋
│       └── tools/blender/        # Blender 자동 정규화 스크립트
├── mds/
│   └── Tasks/                    # 세부 기술 태스크 명세 및 실기기 검증 기록(Evidence)
└── README.md
```

---

## 🚀 주요 기능 (Key Features)

1. **정밀 수직 벽면 인식 & 안정도 검증 (Vertical Wall Detection)**
   - 수평 바닥 배치를 배제하고 오직 유효한 크기·각도의 수직 벽면만 필터링
   - `WallCandidateStabilityTracker`: 시간 게이트 기반 노이즈 필터링
   - `WallPlaneBoundaryUtility` & `WallEnvironmentDepthRules`: 5점 풋프린트 폴리곤 경계 검사 및 LiDAR 심도 데이터 교차 검증
2. **3D 캐릭터 실시간 UV 텍스처 페인팅 (`CharacterColorPalette`)**
   - 3D MeshCollider 터치 좌표의 UV Raycast 텍스처 스탬핑
   - Soft / Rough / Dots 브러시 텍스처, 컬러 스포이드, 메모리 효율적인 스냅샷 기반 Undo/Redo 엔진
   - 4가지 포즈 변환 지원 (Raised Arms, Sitdown, Laydown, Default)
3. **iOS 하드웨어 네이티브 브릿지 (Native C/Objective-C++)**
   - `SearchMyPetARCameraLens.mm`: 가짜 UI 줌이 아닌 실제 ARKit Capture Device 제어 (`0.5× / 1× / 2×` 광학/디지털 줌 램핑)
   - `SearchMyPetMap.mm`: Apple MapKit (`MKMapView`) 오버레이 및 탭 전환 시 ARSession 절전 수명주기 제어
4. **빌드 파이프라인 자동화 (`IosXcodePostprocessor`)**
   - Unity iOS export 시 ARKit 6.5+ Swift 런타임 링커 경로 자동 주입
   - MapKit / CoreLocation 프레임워크 자동 링크 및 심볼 실행 권한 자동 패치

---

## 🛠 기술 스택

* **Engine**: Unity `6000.5.4f1`
* **AR Framework**: AR Foundation `6.5.0` / Apple ARKit XR Plug-in `6.5.0`
* **Target Device**: iPhone (iOS 16.0 이상 권장, Metal 지원)
* **Build Tool**: Xcode `26.0+`
* **Native**: C, Objective-C++, MapKit, AVFoundation
* **3D Pipeline**: Blender 5.2 LTS (Python headless automation)

---

## 👥 개발자

* **김민혁 (1336)** — AI 글래스 개발자 아카데미 (AI Glass Developer Academy)
