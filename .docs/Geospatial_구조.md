# Geospatial 구조 정리

## 개요
`Geospatial` 폴더는 주변 장소를 검색하고, 건물 단위로 묶고, 현재 시야 기준으로 건물을 선택해 AR 월드 핀/텍스트로 보여주는 로직을 담당한다.

현재 코드 기준으로는:
- 일반 건물 흐름만 사용
- 학교/캠퍼스/병원/정문/삼거리 같은 특수 판별 로직은 주석 처리됨
- 기관/상업/비건물 키워드 배열도 주석 처리됨
- 관련 함수는 일부 파일에 남아 있지만 실행 경로에서는 빠져 있음

흐름은 아래와 같다.
1. 현재 위치 기준으로 Kakao 장소 검색
2. 검색 문서 병합/정리
3. 주소/거리 기준으로 클러스터링
4. `BuildingData` 생성
5. 시야 판정으로 preview 후보/선택 건물 계산
6. 월드 핀과 텍스트 마커 표시

## 파일별 역할

### [GeospatialManager.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.cs)
메인 설정/필드 파일이다.

역할:
- 인스펙터 설정값 보관
- 공통 상태 필드 보관
- Kakao 응답 모델 정의
- 내비 목적지 전용 마커 예외 처리

핵심 함수:
- `ShowNavigationTargetMarker()`
- `ClearNavigationTargetMarker()`
- `UpdateNavigationTargetMarker()`

### [GeospatialManager.Runtime.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Runtime.cs)
런타임 흐름 제어 파일이다.

역할:
- 시작 시 위치/트래킹/UI 이벤트 초기화
- 가로모드/디버그모드 적용
- 탭 입력 처리
- 위치 이동 시 재로드
- 공통 거리 계산

핵심 함수:
- `Start()`
- `Update()`
- `HandleLandscapeModeToggleRequested()`
- `ApplyLandscapeDetectionProfile()`
- `RefreshDetectionForCurrentDisplayMode()`
- `HandleWorldMarkerTap()`
- `CheckMovementAndReload()`
- `ReloadDataAtCurrentLocation()`
- `HaversineDistance()`
- `ClearAllContent()`

### [GeospatialManager.WorldMarkers.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.WorldMarkers.cs)
일반 건물 감지와 월드 핀/텍스트 표시를 담당한다.

역할:
- 현재 시야 기준 건물 선택
- preview 핀 표시
- selected 텍스트 표시
- 앵커 풀 유지
- 월드 마커 배치

핵심 함수:
- `CheckBuildingDetection()`
- `UpdateMarkerScale()`
- `UpdatePreviewMarkers()`
- `ResetAllMarkers()`
- `RequestAnchorPoolForCandidates()`
- `ApplyMarkerPlacement()`
- `CreateAllBuildingAnchors()`
- `SyncAnchorPool()`
- `CreateAnchorForBuilding()`
- `GetBuildingsForAnchorCreation()`
- `WaitForTrackingBeforeAnchors()`
- `AppendAnchorDebug()`

### [GeospatialManager.Search.Flow.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Search.Flow.cs)
검색 전체 흐름을 오케스트레이션한다.

역할:
- 카테고리/키워드 검색 실행
- 검색 결과 병합
- 클러스터링 호출
- 최종 건물 리스트 반영
- 현재는 `campus` 카테고리 검색은 주석 처리로 비활성화됨

핵심 함수:
- `FetchAndClusterNearbyPlaces()`
- `LogSearchDiagnostics()`

### [GeospatialManager.Search.Requests.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Search.Requests.cs)
실제 Kakao API 요청 파일이다.

역할:
- 카테고리 검색 요청
- 키워드 검색 요청
- 검색 파라미터 정리
- 검색 소스 메타데이터 주입

주의:
- `GetCampusCategoryCodes()`는 파일에 남아 있지만 현재 검색 플로우에서는 사용하지 않음

핵심 함수:
- `FetchNearbyPlacesFromKakaoCategory()`
- `FetchNearbyPlacesFromKakaoKeyword()`
- `GetCommercialCategoryCodes()`
- `GetCampusCategoryCodes()`
- `GetKeywordQueries()`
- `PrepareFetchedDocument()`

### [GeospatialManager.Search.Documents.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Search.Documents.cs)
검색 응답 문서 후처리 파일이다.

역할:
- 중복 문서 병합
- 문서 정규화
- 정렬/우선순위 조정

### [GeospatialManager.Clustering.Flow.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Clustering.Flow.cs)
검색 문서를 건물 클러스터로 묶는다.

역할:
- 주소 그룹화
- 거리 기반 클러스터링
- 최종 건물 데이터 생성 호출
- 현재는 기관/병원/캠퍼스 대표 클러스터링은 주석 처리로 비활성화됨

핵심 함수:
- `ClusterAndBuildList()`
- `BuildDistanceAddressClusters()`
- `ShouldUseRepresentativeInstitutionalClustering()`
- `BuildInstitutionalRepresentativeClusters()`
- `TryAssignDocumentToNearestRepresentativeCluster()`

### [GeospatialManager.Clustering.Scoring.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Clustering.Scoring.cs)
대표 후보 점수 계산 파일이다.

역할:
- 어떤 문서가 대표 건물 후보인지 판단
- 대표 후보 점수 계산
- 중심성/토큰 지지 점수 계산

주의:
- `IsRepresentativeBuildingCandidate()`는 현재 `false`를 반환하도록 바뀌어 기관 대표 건물 후보 판정을 사용하지 않음

핵심 함수:
- `SelectRepresentativeBuildingDocuments()`
- `IsRepresentativeBuildingCandidate()`
- `GetRepresentativeBuildingCandidateScore()`
- `BuildRepresentativeCandidateScoreMap()`
- `GetRepresentativeCentralityScore()`
- `GetRepresentativeTokenSupportScore()`

### [GeospatialManager.Clustering.Tokens.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Clustering.Tokens.cs)
문자열/토큰 기반 판별 파일이다.

역할:
- 건물명 정규화
- 대표 토큰 추출
- 기관/상업/비건물 키워드 판별

현재 상태:
- `InstitutionalKeywords`
- `CommercialKeywords`
- `InstitutionalBuildingSuffixes`
- `InstitutionalPrefixTokens`
- `InstitutionalFacilityKeywords`
- `NonBuildingRepresentativeKeywords`

위 상수 배열 선언은 `GeospatialManager.cs`에서 주석 처리됨.

그리고 아래 함수들은 일반 건물 전용 흐름에 맞게 사실상 비활성화됨:
- `LooksLikeInstitutionalFacility()`: 항상 `false`
- `GetClusterGroupingType()`: 항상 `Default`
- `GetInstitutionalBuildingToken()`: 항상 빈 문자열
- `IsInstitutionalBuildingToken()`: 항상 `false`
- `LooksLikeAnchorEligibleBuilding()`: 학교/거리/상업 제외 판정을 더 이상 하지 않음

핵심 함수:
- `GetRepresentativeBuildingClusterKey()`
- `LooksLikeInstitutionalFacility()`
- `LooksLikeAnchorEligibleBuilding()`
- `NormalizeRepresentativeName()`
- `GetClusterGroupingType()`
- `ContainsAnyKeyword()`
- `GetInstitutionalBuildingToken()`
- `ExtractInstitutionalBuildingToken()`
- `IsInstitutionalBuildingToken()`

### [GeospatialManager.Buildings.Factory.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Buildings.Factory.cs)
클러스터를 최종 `BuildingData`로 변환한다.

역할:
- 건물 이름/주소/좌표/설명 생성
- 시설 리스트 생성

주의:
- 기관 토큰 기반 이름 생성은 꺼져 있고, 현재는 일반 대표 문서 기준 이름 생성만 사용

핵심 함수:
- `ProcessClusterIntoBuildingData()`
- `ResolveBuildingDisplayName()`
- `GetBuildingDescription()`
- `BuildFacilityList()`
- `GetDocumentDisplayCategory()`

### [GeospatialManager.Buildings.Selection.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/Geospatial/GeospatialManager.Buildings.Selection.cs)
클러스터 내부 대표 문서를 선택한다.

역할:
- 앵커 대표 문서 선택
- 표시 이름 대표 선택
- 건물 토큰 선택
- 건물 참조 문서 판별

주의:
- `SelectInstitutionalBuildingToken()`은 현재 빈 문자열 반환
- `IsBuildingReferenceDocument()`의 기관 토큰 기반 판별은 주석 처리됨

핵심 함수:
- `SelectAnchorRepresentativeDocument()`
- `SelectRepresentativeDocument()`
- `SelectBuildingNameDocument()`
- `SelectInstitutionalBuildingToken()`
- `IsBuildingReferenceDocument()`

### [AnchorVisibilityPlanner.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/AnchorVisibilityPlanner.cs)
현재 시야에서 어떤 건물이 보이는지 계산한다.

역할:
- visible / preview / top 후보 계산
- focusedBuilding 결정
- 디버그용 후보 포맷 제공

핵심 함수:
- `BuildSelection()`
- `DetermineFocusedBuilding()`
- `GetPreviewCandidates()`
- `BuildDetectionSignature()`
- `FormatCandidateList()`

### [AnchorPoolPlanner.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/AnchorPoolPlanner.cs)
현재 유지할 앵커 풀 대상을 계산한다.

역할:
- preview 후보 중 실제 유지할 건물 목록 계산
- 풀 시그니처 생성

핵심 함수:
- `BuildDesiredBuildings()`
- `BuildPoolSignature()`

### [BuildingMarker.cs](/Users/shindongheun/Desktop/캡디/ARmap_Pro_Max_Ultra_Plus/Assets/Scripts/BuildingMarker.cs)
월드에 보이는 실제 건물 마커 오브젝트를 구성하고 상태를 바꾼다.

역할:
- preview 핀 생성 및 표시
- selected 텍스트 카드 생성 및 표시
- `Hidden / Preview / Selected` 상태 전환
- 빌보드 회전
- 건물명, 부제, 거리 텍스트 갱신

핵심 함수:
- `Initialize()`
- `SetState()`
- `SetBuilding()`
- `SetDistance()`
- `ApplyVisualState()`
- `Update()`
- `UpdatePreviewPinScale()`

관계 정리:
- `AnchorVisibilityPlanner`는 지금 무엇이 보이는지 결정
- `AnchorPoolPlanner`는 그중 어떤 건물 앵커를 유지할지 결정
- `BuildingMarker`는 최종적으로 사용자에게 보이는 핀/텍스트를 렌더링

## 읽는 순서
1. `GeospatialManager.Runtime.cs`
2. `GeospatialManager.Search.Flow.cs`
3. `GeospatialManager.Search.Requests.cs`
4. `GeospatialManager.Search.Documents.cs`
5. `GeospatialManager.Clustering.Flow.cs`
6. `GeospatialManager.Clustering.Scoring.cs`
7. `GeospatialManager.Buildings.Factory.cs`
8. `GeospatialManager.Buildings.Selection.cs`
9. `AnchorVisibilityPlanner.cs`
10. `AnchorPoolPlanner.cs`
11. `GeospatialManager.WorldMarkers.cs`
12. `BuildingMarker.cs`

## 현재 해석 기준
- 실제 동작 기준으로 읽을 때는 일반 건물 흐름만 보면 됨
- 기관/캠퍼스/병원/거리 분기는 “이전 실험용 로직이 주석으로 남아 있는 상태”로 보면 됨
