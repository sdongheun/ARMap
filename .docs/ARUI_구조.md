# ARUI 구조 정리

## 개요
`ARUI` 폴더는 `ARUIManager`를 중심으로 AR 화면 UI를 기능별 partial 파일로 나눈 구조다.

핵심 책임은 아래와 같다.
- 메인 UI 초기화와 내비 검색/HUD 생성
- 상단 상태 배지 표시
- 하단 플로팅 액션바 관리
- 상세 패널 열기/닫기 연결
- 디버그 오버레이와 중앙 레티클 표시

## 파일별 역할

### [ARUIManager.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUI/ARUIManager.cs)
AR UI의 루트 엔트리 파일이다.

역할:
- 인스펙터 참조 보관
- 버튼 이벤트 연결
- 검색 패널/HUD 생성
- 내비 UI 진입/종료 처리
- 공통 UI 헬퍼 제공

핵심 함수:
- `Awake()`: 카드/캔버스 참조 캐시
- `Start()`: 전체 UI 초기화
- `EnsureSearchPanel()`: 목적지 검색 패널 생성
- `ShowSearchPanel()`, `HideSearchPanel()`: 검색 패널 표시 제어
- `UpdateSearchResults()`: 검색 결과 목록 갱신
- `EnsureNavigationHUD()`, `ShowNavigationHUD()`, `HideNavigationHUD()`: 내비 HUD 생성/표시
- `EnterNavigationMode()`, `ExitNavigationMode()`: 내비 UI 모드 전환
- `BringNavigationSurfaceToFront()`: 내비 UI를 다른 UI보다 위로 올림
- `EnsureNavigationManager()`: `NavigationManager` 보장
- `DispatchNavigateRequested()`, `DispatchNavigateFromDetailRequested()`: 길찾기 이벤트 공통 진입점

### [ARUIManager.Cards.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUI/ARUIManager.Cards.cs)
상단 상태 배지와 스캔 상태 표시를 담당한다.

역할:
- 스캔/감지/퀵인포 상태 전환
- 상태 배지 생성
- 상태 배지 애니메이션

핵심 함수:
- `SetScanningMode()`
- `SetDetectedMode()`
- `SetBuildingCountStatus()`
- `SetTrackingStabilizingMode()`
- `ShowQuickInfo()`
- `EnsureStatusBadge()`
- `SetStatusBadgeMessage()`
- `RefreshStatusBadgeLayout()`
- `ShowStatusBadge()`, `HideStatusBadge()`

### [ARUIManager.Detail.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUI/ARUIManager.Detail.cs)
상세 패널과 상세 버튼 연결을 담당한다.

역할:
- 월드 상세 버튼 활성 상태 갱신
- 상세 패널 열기/닫기
- 전화/지도 열기

핵심 함수:
- `SetWorldInfoDetailButtonState()`
- `OpenDetailView()`
- `CloseDetailView()`
- `HandleUIToolkitDetailClosed()`
- `ShowToast()`
- `OnCallPhone()`
- `OnOpenMap()`

### [ARUIManager.Overlays.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUI/ARUIManager.Overlays.cs)
화면 보조 레이어를 담당한다.

역할:
- 중앙 조준점 생성/애니메이션
- 디버그 오버레이 생성/갱신
- 공통 TMP 스타일 적용

핵심 함수:
- `EnsureCenterReticle()`
- `EnsureDebugOverlay()`
- `SetDebugOverlay()`
- `ClearDebugOverlay()`
- `ApplySharedTextStyle()`
- `CreateCenterReticleBar()`
- `UpdateCenterReticleAnimation()`

### [ARUIManager.BottomBarToolkit.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUI/ARUIManager.BottomBarToolkit.cs)
하단 플로팅 액션바를 UI Toolkit 기반으로 구성한다.

역할:
- 길찾기/가로모드/상세정보 버튼 생성
- 세로/가로/내비 상태에 맞춰 레이아웃 변경
- 상세 패널이나 내비 중 하단바 숨김 처리

핵심 함수:
- `EnsureBottomActionBarToolkit()`
- `CreateToolkitActionButton()`
- `RefreshBottomActionBarToolkitLayout()`
- `SetBottomActionBarToolkitVisible()`
- `ApplyToolkitButtonSize()`
- `OnToolkitLandscapeButtonClicked()`
- `OnToolkitDetailButtonClicked()`
- `UpdateToolkitLandscapeButtonState()`
- `UpdateToolkitDetailButtonState()`

## 상세 패널 본체

### [ARDetailPanelDocumentController.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARDetailPanelDocumentController.cs)
실제 상세 패널 UIDocument 제어 파일이다.

역할:
- `BuildingData`를 UI Toolkit 상세 패널에 바인딩
- 시설 목록 드롭다운 처리
- 주소/전화/지도 버튼 처리
- 복사 토스트 처리

핵심 함수:
- `Initialize()`
- `Show()`, `ShowPreview()`, `Hide()`
- `Bind()`
- `BindCurrentPlace()`
- `BuildPlaceOptions()`
- `RefreshFacilitySelector()`
- `PopulateFacilityList()`
- `OnFacilityOptionSelected()`
- `CopyAddress()`
- `ShowCopyToast()`

## 읽는 순서
1. `ARUIManager.cs`
2. `ARUIManager.BottomBarToolkit.cs`
3. `ARUIManager.Cards.cs`
4. `ARUIManager.Detail.cs`
5. `ARDetailPanelDocumentController.cs`
6. `ARUIManager.Overlays.cs`
