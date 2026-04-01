# UI Toolkit Setup

이 문서는 AR 프로젝트에서 상세 패널을 UI Toolkit으로 병행 사용하기 위한 초기 세팅 문서다.

## Why We Split It

- 마커와 AR 오버레이는 카메라/앵커와 강하게 연결되므로 기존 `uGUI`가 더 적합하다.
- 상세 패널은 앱형 정보 UI이므로 `UI Toolkit`이 구조화와 스타일 관리에 더 적합하다.
- 따라서 이 프로젝트는 `AR 오버레이는 uGUI`, `상세 패널은 UI Toolkit`으로 분리하는 방향이 맞다.

## Files Added

- `Assets/Scripts/ARDetailPanelDocumentController.cs`
- `Assets/UI Toolkit/DetailPanel/ARDetailPanel.uxml`
- `Assets/UI Toolkit/DetailPanel/ARDetailPanel.uss`
- `Assets/Editor/ARDetailPanelToolkitSetup.cs`

## What Each File Does

- `ARDetailPanelDocumentController.cs`
  - UI Toolkit 상세 패널의 런타임 브리지다.
  - 건물 데이터를 받아 제목, 위치, 전화번호, 주요시설, 카카오맵 버튼 상태를 갱신한다.
- `ARDetailPanel.uxml`
  - 상세 패널의 구조 정의다.
  - 오버레이, 시트, 섹션 카드, 버튼, 시설 리스트를 포함한다.
- `ARDetailPanel.uss`
  - 상세 패널의 스타일 정의다.
  - 바텀 시트형 레이아웃, 카드 스타일, 버튼 스타일, 리스트 스타일을 포함한다.
- `ARDetailPanelToolkitSetup.cs`
  - Unity 에디터 메뉴에서 한 번에 설치하기 위한 세팅 스크립트다.
  - `PanelSettings`, `UIDocument`, `ARDetailPanelDocumentController`를 생성/연결한다.

## How To Apply In Unity

1. Unity 에디터에서 프로젝트를 연다.
2. 상단 메뉴에서 `Tools > AR > Setup Detail Panel UI Toolkit` 를 실행한다.
3. Hierarchy에 `ARDetailPanelUIDocument` 오브젝트가 생성되었는지 확인한다.
4. 기존 씬의 `ARUIManager`에 `uiToolkitDetailPanel` 참조가 연결되었는지 확인한다.
5. 플레이 모드에서 상세 패널이 UI Toolkit 버전으로 열리는지 확인한다.

## Current Integration State

- `ARUIManager`는 `uiToolkitDetailPanel`이 연결되어 있으면 기존 `uGUI` 상세 패널 대신 UI Toolkit 패널을 사용한다.
- 연결되어 있지 않으면 기존 `uGUI` 경로를 그대로 사용한다.
- 즉, 현재 세팅은 안전한 병행 구조다.

## Recommended Next Step

- UI Toolkit 패널이 정상적으로 열리는지 먼저 확인한다.
- 그 다음 기존 `uGUI` 상세 패널 사용을 단계적으로 줄인다.
- 최종적으로 상세 패널 기능이 모두 UI Toolkit으로 옮겨지면 기존 상세 패널 오브젝트 정리를 검토한다.
