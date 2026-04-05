# AR UI And Geospatial Limitations

## 1. 현재 사용 중인 UI 방식

### AR 환경 UI
- 기술: `uGUI`
- 구성 요소:
  - 상단 상태 카드
    - `ScanningCard`
    - `DetectedCard`
  - 하단 퀵 정보 카드
    - `QuickInfoCard`
  - 화면 위 건물 마커 라벨
    - 런타임 생성 `ScreenMarkerRoot`
- 텍스트 렌더링: `TextMeshProUGUI`
- 제어 코드:
  - [`ARUIManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARUIManager.cs)
  - [`GeospatialManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/GeospatialManager.cs)

### 상세정보 UI
- 기술: `UI Toolkit`
- 구성 요소:
  - 구조: [`ARDetailPanel.uxml`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/UI%20Toolkit/DetailPanel/ARDetailPanel.uxml)
  - 스타일: [`ARDetailPanel.uss`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/UI%20Toolkit/DetailPanel/ARDetailPanel.uss)
  - 제어: [`ARDetailPanelDocumentController.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/ARDetailPanelDocumentController.cs)

## 2. 현재 AR 환경 UI의 역할 분리

### 마커
- 건물 위치를 빠르게 인식시키는 역할
- 현재 화면 위 2D 오버레이 라벨과 월드 앵커 마커를 함께 사용

### 하단 퀵 정보 카드
- 선택된 건물의 핵심 정보 표시
- 현재 표시 정보:
  - 건물명
  - 카테고리
  - 거리

### 상세정보 페이지
- 더 많은 정보를 읽는 용도
- 현재 AR 위 오버레이처럼 보이지만, 역할상은 별도 상세 페이지에 가까움

## 3. 현재 프로젝트에서 Geospatial API가 쓰이는 방식

### 핵심 역할
- 위도/경도/고도 기반 앵커 생성
- 카메라 위치/방향과 건물 좌표를 비교
- 어떤 건물이 현재 시야에 있는지 계산
- 거리, 중심 정렬 여부, 전방 후보 판단

### 실제 사용 코드
- [`GeospatialManager.cs`](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/GeospatialManager.cs)
  - `AREarthManager` 사용
  - `ARGeospatialAnchor` 생성 및 관리
  - `AnchorManager.AddAnchor(...)` 로 건물 앵커 생성
  - 화면에 보이는 후보 건물 계산

## 4. Geospatial 기반 판단 방식의 특징

### 장점
- 실제 지도 좌표에 건물 정보를 붙이기 쉬움
- 카메라가 바라보는 방향과 건물 위치를 빠르게 매칭 가능
- 비전 AI 없이도 건물 위치 기반 AR 경험 구현 가능

### 한계
- 카메라 화면에 보이는 건물 외형 자체를 인식하는 것은 아님
- 실제 영상 속 가림 여부를 정확히 판단하지 못함
- 좌표와 방향 기준으로 "보일 가능성이 높은 건물"을 계산하는 구조

## 5. 현재 방식의 대표적인 단점

### 5-1. 앞 건물 뒤의 건물 정보가 같이 뜰 수 있음
- 같은 방향선상에 여러 건물이 있으면
- 실제 화면에서는 뒤 건물이 잘 안 보이거나 가려져 있어도
- 좌표상 후보로 잡히면 마커/라벨이 같이 뜰 수 있음

### 5-2. 실제 시각적 가림을 반영하지 못함
- 앞 건물이 뒤 건물을 완전히 가려도
- Geospatial 계산만으로는 뒤 건물이 안 보인다는 사실을 확실히 알기 어려움

### 5-3. 사용자 입장에서 혼란 가능
- "화면에 안 보이는 건물 정보가 왜 뜨지?"라는 인상이 생길 수 있음
- 건물이 촘촘한 구간에서는 선택 대상이 애매해질 수 있음

### 5-4. 거리와 방향만으로는 실제 인지와 다를 수 있음
- 계산상으로는 맞는 후보라도
- 사용자가 체감하는 "지금 보고 있는 건물"과 다르게 느껴질 수 있음

## 6. 현재 UI 방식과 이 단점의 관계

### 현재 구조에서 생길 수 있는 현상
- 여러 마커가 동시에 화면에 뜸
- 같은 방향선의 앞뒤 건물이 함께 노출될 수 있음
- 하단 퀵 정보는 한 개만 고정되더라도
- 화면 위 마커는 여러 개가 보일 수 있어 혼란이 남을 수 있음

### 현재 구조의 보완 방향
- 마커는 건물명 1줄 정도만 단순 표시
- 하단 퀵 카드에서 선택된 건물 1개만 강조
- 상세정보는 별도 페이지 성격으로 분리
- 같은 시야선상의 뒤 건물은 표시 우선순위를 낮추는 방식 검토

## 7. 정리

- 현재 프로젝트의 AR 환경 UI는 `uGUI` 기반이다.
- 상세정보 UI는 `UI Toolkit` 기반이다.
- 건물 선택 판단은 `Geospatial API`를 적극 사용한다.
- 이 방식은 좌표/거리/방향 계산에 강하지만, 실제 카메라 영상의 가림 관계까지는 정확히 처리하지 못한다.
- 따라서 현재 구조에서는 "보이지 않는 뒤 건물 정보가 같이 뜨는 문제"가 발생할 수 있다.
- 이 한계는 UI 문제이기도 하지만, 근본적으로는 Geospatial 기반 판단 방식의 특성에서 오는 제약이다.
