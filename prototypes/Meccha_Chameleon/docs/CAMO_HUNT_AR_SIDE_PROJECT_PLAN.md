# CAMO HUNT AR - 사이드프로젝트 기획

> 상태: 기획 기준선 v0.1  
> 작성일: 2026-07-21  
> 대상 환경: Unity 6000.5.4f1, Android 우선  
> 근거 문서: `CAMO_HUNT_AR_Agent_Handoff.docx`

## 1. 한 줄 정의

한 사람이 실제 표면에 AR 생물을 숨기고, 다른 사람이 같은 장소에서 휴대폰 카메라로 그 생물을 찾아 탭하는 비동기 AR 숨바꼭질이다.

## 2. 이번 프로젝트의 목표

이 사이드프로젝트의 목적은 완성된 위치 기반 게임을 출시하는 것이 아니라, 다음 한 가지 질문에 빠르게 답하는 것이다.

> 서로 다른 두 휴대폰이 같은 실제 표면을 충분히 안정적으로 재인식하여 숨기기와 찾기가 재미있는가?

성공하면 최소 게임 루프까지 확장한다. 실패하면 지도, 드로잉, 계정, 운영 기능에 시간을 쓰지 않고 원인을 기록한 뒤 방향을 재검토한다.

### 목표

- A폰에서 질감 있는 벽이나 테이블을 인식하고 간단한 생물을 배치한다.
- Cloud Anchor를 host하고 짧은 공유 코드로 B폰에 전달한다.
- B폰이 같은 장소를 스캔하여 같은 표면과 방향에 생물을 표시한다.
- B폰 사용자가 생물을 탭하면 포획 완료 화면을 본다.
- 성공률, resolve 시간, 위치 오차와 실패 이유를 기록한다.

### 이번 범위에서 하지 않는 것

- 결제, 광고, 시즌, 랭킹, 친구, 실시간 멀티플레이
- 정식 회원 체계와 부정행위 방지
- 완성형 드로잉 도구와 디자인 마켓
- 자동 콘텐츠 모더레이션과 운영자 도구
- 대규모 트래픽, 고가용성, 마이크로서비스
- 정식 출시용 법무·등급·위치정보사업 절차의 완료
- 365일 앵커 운영과 자동 재호스팅

## 3. 핵심 사용자 경험

### 숨기기

1. 사용자가 카메라를 열고 질감과 조명이 충분한 표면을 찾는다.
2. 화면 중앙의 가이드가 추적 상태와 스캔 품질을 알려 준다.
3. 사용자가 표면을 탭해 기본 생물을 배치하고 위치와 방향을 간단히 조정한다.
4. 앱이 여러 각도에서 천천히 이동하도록 안내한다.
5. 품질이 충분하면 Cloud Anchor를 host한다.
6. 성공하면 짧은 공유 코드와 테스트 만료 시각을 보여 준다.

### 찾기

1. 다른 사용자가 공유 코드를 입력한다.
2. 앱이 주변을 천천히 스캔하도록 안내한다.
3. Cloud Anchor resolve가 성공한 경우에만 생물을 표시한다.
4. resolve 전에는 임의 위치나 카메라 앞에 대체 생물을 표시하지 않는다.
5. 사용자가 생물을 탭하면 포획 성공으로 처리한다.

## 4. 가벼운 기술 구성

### 클라이언트

- Unity `6000.5.4f1`
- AR Foundation 6.x
- Android: ARCore XR Plugin 6.x
- iOS 확장 시: ARKit XR Plugin 6.x
- ARCore Extensions for AR Foundation의 `arf6` 호환 릴리스
- 초기 렌더 파이프라인은 Built-in 또는 최소 구성 URP 중 프로젝트 생성 시 하나만 선택하고 중간에 바꾸지 않는다.

AR Foundation, ARCore XR Plugin, ARKit XR Plugin은 호환되는 동일 계열 버전으로 맞추고 `Packages/manifest.json`과 lock 파일에 고정한다. ARCore Extensions도 테스트가 끝난 정확한 커밋이나 릴리스 태그로 고정한다.

### 공유 방식

단계별로 복잡도를 올린다.

1. 첫 Cloud Anchor 실험은 Anchor ID를 개발자 화면에서 직접 복사하거나 QR로 전달한다.
2. 실험이 성공한 뒤에만 짧은 공유 코드와 Anchor ID를 연결하는 최소 저장소를 추가한다.
3. 저장소가 필요하면 Supabase의 Postgres와 Edge Function을 우선 검토한다. 사용자 계정 없이 익명 테스트 세션만 둔다.

### 최소 저장 데이터

| 필드 | 목적 |
|---|---|
| `share_code` | 사람이 입력할 짧은 테스트 코드 |
| `cloud_anchor_id` | resolve에 필요한 식별자 |
| `creature_seed` | 색상과 간단한 외형 재현 |
| `local_transform` | 앵커 기준 위치·회전·크기 |
| `created_at` | 생성 시각 |
| `expires_at` | 테스트 데이터 정리 시각 |

첫 기술 검증에서는 1일 TTL과 제한된 API 키를 사용해 인증 구성을 단순화한다. 1일을 넘기는 유지가 실제로 필요해진 시점에만 keyless 인증과 장기 TTL을 별도 작업으로 올린다. 비밀 키와 서비스 계정 자격 증명은 저장소에 넣지 않는다.

## 5. 상태와 실패 처리

앱 화면은 최소한 다음 상태를 구분해야 한다.

`Idle -> Tracking -> Placed -> Scanning -> Hosting -> Hosted`

`Idle -> Tracking -> Resolving -> Resolved -> Captured`

다음 실패는 하나의 “실패” 문구로 뭉치지 않고 이유와 다음 행동을 보여 준다.

| 실패 | 사용자 안내 | 기록할 값 |
|---|---|---|
| 추적 불안정 | 잠시 멈추고 주변을 천천히 비추기 | tracking state, 시간 |
| 특징점 부족 | 흰 벽·유리·반사면을 피하고 각도를 바꾸기 | Feature Map Quality |
| host 실패 | 네트워크 확인 후 다시 스캔 | CloudAnchorState, 경과 시간 |
| resolve 시간 초과 | 원래 위치와 같은 방향에서 다시 시도 | timeout, 시도 횟수 |
| Anchor ID 없음·만료 | 새 공유 코드를 요청 | 서버 응답, 만료 시각 |
| 패키지 호환 오류 | 테스트 빌드 버전 확인 | 앱·SDK·OS·기기 버전 |

host와 resolve는 취소와 시간 제한을 가져야 한다. `Update()`에서 네트워크 요청이나 Anchor 작업을 매 프레임 호출하지 않는다.

## 6. 검증 실험

### 우선순위

1. Android -> Android
2. Android -> iPhone
3. iPhone -> Android

iOS 검증에는 Mac, Xcode, 개발자 서명과 실기기가 필요하므로 Android 간 실험이 성공한 뒤 진행한다.

### 기본 실험 조건

- 장소: 통제 가능한 실내 한 곳
- 주 표면: 포스터, 무늬, 모서리처럼 특징이 있는 벽 또는 테이블
- 비교 표면: 빈 흰 벽, 유리·반사면
- 조명: 밝은 조건을 기본으로 하고 어두운 조건은 비교 실험으로만 사용
- 기기: 서로 다른 Android 모델 2대
- 반복: 주 조건 10회, 비교 조건은 각각 3회

### 기록 항목

- host 성공 여부와 소요 시간
- Feature Map Quality 변화
- resolve 성공 여부와 소요 시간
- 첫 성공까지 사용자가 이동한 시간과 횟수
- 생물이 원래 표면에 붙어 보이는지
- 기준점 대비 대략적인 위치 오차와 회전 오차
- 기기, OS, 앱 빌드, ARCore Extensions 버전
- CloudAnchorState와 네트워크 오류

### 가벼운 Go/No-Go 기준

주 조건 10회에서 다음을 만족하면 최소 게임 루프로 진행한다.

- resolve 성공 7회 이상
- 성공한 시도의 resolve 중앙값 20초 이하
- 생물이 다른 벽이나 공중이 아니라 원래 표면에 나타남
- 위치 오차가 대체로 20cm 이내이고 회전 오차가 플레이를 방해하지 않음
- 사용자가 별도 설명 없이 화면 안내만 보고 스캔을 완료할 수 있음

기준을 만족하지 못하면 지도와 백엔드 기능을 추가하지 않는다. 먼저 스캔 안내, 표면 조건, 패키지 호환성, 기기별 차이를 조사하고 한 번만 재실험한다. 두 번째에도 실패하면 Cloud Anchor를 핵심 메커니즘으로 유지할지 재검토한다.

## 7. 3~4회 작업 세션 로드맵

### 세션 1 - 로컬 AR 확인

- AR 세션과 카메라 실행
- 평면 탐지와 탭 배치
- 위치·회전·크기 조정
- 추적 상태 진단 표시
- 완료 조건: 한 기기에서 생물이 표면에 안정적으로 붙어 있음

### 세션 2 - Cloud Anchor 기술 검증

- Feature Map Quality 안내
- host와 resolve
- Anchor ID 직접 전달
- 오류·시간·버전 로그
- 완료 조건: Android 두 기기에서 같은 표면 재현

### 세션 3 - 최소 게임 루프

- 짧은 공유 코드
- 기본 생물 외형 재현
- 숨기기·찾기 화면 분리
- 탭 포획과 한 줄 결과
- 완료 조건: 다른 사람이 설명 없이 한 번의 흐름을 완료

### 세션 4 - 선택 확장

아래 중 하나만 선택한다.

- iPhone 교차 검증
- GPS 50m 조건과 텍스트 거리 안내
- 간단한 색칠 또는 색상 프리셋
- 실패 진단과 스캔 안내 개선

한 세션에서 둘 이상을 선택하지 않는다.

## 8. 지도와 위치 기능의 보류 원칙

핵심 실험에는 지도 SDK가 필요하지 않다. 첫 버전은 공유 코드와 현장 안내만 사용한다. Cloud Anchor 검증이 성공한 뒤에만 현재 위치와 숨김 지점의 대략 거리로 50m 진입 여부를 판단한다.

지도 화면이 필요해지면 다음 순서로 비교한다.

1. 한국 지도 품질
2. Android와 iOS 네이티브 SDK를 Unity에 연결하는 비용
3. 원·구역 오버레이 지원
4. 가격과 일·월 쿼터
5. 개인정보와 위치정보 처리 요구사항

Mapbox Unity v2는 현재 활발히 개발되지 않고 Unity 6 수정 작업이 필요하므로 기본 선택에서 제외한다. Kakao와 NAVER는 한국 지도 품질 검증 후보지만 공식 Unity SDK가 아니라 네이티브 브리지 작업을 별도로 추정해야 한다. 지도 없이 텍스트 거리와 간단한 원형 레이더만으로 재미가 유지되면 지도 도입을 계속 미룬다.

## 9. 백엔드 보류 원칙

사이드프로젝트 단계에서는 모듈형 모놀리스 API나 별도 워커를 미리 만들지 않는다.

- 기술 검증: Anchor ID 직접 전달
- 최소 공유: Supabase Edge Function + Postgres 한 테이블
- 50m 검색이 필요할 때: PostGIS 활성화 및 GiST 인덱스 추가
- 실제 공개 베타로 전환할 때: 서버 권한, 중복 포획, 24시간 생성 제한, 신고, 만료 작업을 재설계

클라이언트에서 점수나 생성 제한을 강제하는 구조는 공개 베타로 가져가지 않는다. 하지만 사적인 실험에서는 점수 자체를 저장하지 않아도 된다.

## 10. 안전과 프라이버시의 최소선

정식 규제 대응 문서를 지금 만들지는 않지만 다음 최소선은 처음부터 지킨다.

- 공개적으로 안전한 장소나 참여자에게 허가받은 실내에서만 테스트한다.
- 도로 횡단, 운전, 자전거 이동 중 AR 사용을 안내하지 않는다.
- 카메라 영상과 AR 프레임을 앱 서버에 저장하지 않는다.
- 정확한 GPS 이동 이력을 로그에 남기지 않는다.
- 카메라와 위치 권한은 해당 화면 진입 직전에 설명하고 요청한다.
- 위치는 앱이 보이는 동안만 사용하며 백그라운드 추적은 하지 않는다.
- 다른 사람에게 공개되는 UGC와 실제 위치 배치를 추가하기 전 신고·차단·금지구역 정책을 별도 기획한다.

## 11. 주요 리스크

| 리스크 | 가능성 | 영향 | 지금 할 대응 |
|---|---:|---:|---|
| 다른 기기에서 resolve가 불안정함 | 높음 | 매우 높음 | 가장 먼저 10회 반복 실험 |
| 흰 벽·반사면에서 특징점 부족 | 높음 | 높음 | 표면 선택 가이드와 품질 게이트 |
| Unity·AR 패키지 호환 문제 | 중간 | 높음 | 버전 고정, 빌드 정보 기록 |
| iOS 빌드 환경이 없음 | 중간 | 중간 | Android 우선, 교차 검증은 선택 세션 |
| 지도 브리지 작업이 커짐 | 높음 | 중간 | 핵심 실험에서 지도 제외 |
| 기능 욕심으로 범위가 커짐 | 높음 | 높음 | 매 세션 하나의 완료 조건만 유지 |
| 공개 위치·UGC 문제가 생김 | 중간 | 높음 | 사적 테스트로 제한, 공개 전 별도 게이트 |

## 12. 완료 정의

사이드프로젝트의 첫 버전은 다음 장면을 영상으로 남길 수 있으면 완료다.

> A폰이 질감 있는 실제 표면에 기본 생물을 숨기고 공유 코드를 만든다. B폰이 같은 장소를 스캔해 같은 표면에서 생물을 발견하고 탭해 포획한다. 진단 기록에서 host·resolve 결과와 소요 시간을 확인할 수 있다.

완료 후 다음 질문에 답하고 프로젝트를 계속할지 결정한다.

- 찾는 순간이 실제로 재미있는가?
- 사용자가 스캔 방법을 이해하는가?
- 표면 오차가 위장 게임을 깨지 않는가?
- 실패했을 때 다시 시도할 이유와 방법이 명확한가?
- 다음 한 가지 확장으로 지도, 드로잉, iOS 중 무엇이 가장 가치 있는가?

## 13. 결정 로그

| 항목 | 현재 결정 | 다시 볼 시점 |
|---|---|---|
| 프로젝트 규모 | 사적 사이드프로젝트 | 공개 베타 결정 시 |
| 우선 플랫폼 | Android 2대 | Android 실험 성공 후 |
| Anchor TTL | 기술 검증은 1일 | 재방문 플레이 필요 시 |
| 지도 | 사용하지 않음 | 50m 탐색을 붙일 때 |
| 백엔드 | 직접 전달 후 최소 Supabase | 공유 코드가 필요할 때 |
| 캐릭터 | 기본 프리미티브 또는 단순 모델 | 게임 루프 성공 후 |
| 드로잉 | 제외 | 발견 재미 검증 후 |
| 운영·법무 | 공개 전 게이트 | 외부 사용자 모집 전 |

## 14. 공식 참고자료

2026-07-21 기준으로 구현 직전에 다시 확인한다.

- [ARCore Extensions: AR Foundation 6 업그레이드](https://developers.google.com/ar/develop/unity-arf/upgrade-to-ar-foundation-6)
- [ARCore 최신 변경 사항](https://developers.google.com/ar/whatsnew-arcore)
- [Unity AR Foundation용 Cloud Anchors 개발 가이드](https://developers.google.com/ar/develop/unity-arf/cloud-anchors/developer-guide)
- [Cloud Anchors 개요](https://developers.google.com/ar/develop/cloud-anchors)
- [ARAnchorManagerExtensions API](https://developers.google.com/ar/reference/unity-arf/class/Google/XR/ARCoreExtensions/ARAnchorManagerExtensions)
- [Unity에서 AR Foundation 시작하기](https://developers.google.com/ar/develop/unity-arf/getting-started-ar-foundation)
- [Supabase PostGIS 지리 쿼리](https://supabase.com/docs/guides/database/extensions/postgis)
- [Supabase Edge Functions](https://supabase.com/docs/guides/functions)
- [KakaoMaps Android SDK](https://apis.map.kakao.com/android_v2/docs/)
- [Kakao API 쿼터](https://developers.kakao.com/docs/ko/getting-started/quota)
- [NAVER Cloud Dynamic Map](https://api.ncloud-docs.com/docs/en/application-maps-dynamic)
- [Mapbox Maps SDK for Unity 설치 및 지원 상태](https://docs.mapbox.com/unity/maps/guides/install/)
- [Google Play 사용자 제작 콘텐츠 정책](https://support.google.com/googleplay/android-developer/answer/9876937)
- [Apple App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [대한민국 위치정보법 시행령](https://law.go.kr/lsInfoP.do?ancYnChk=0&chrClsCd=010202&efYd=20260210&lsiSeq=283269&urlMode=lsEfInfoR&viewCls=lsRvsDocInfoR)

