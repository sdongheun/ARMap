using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Linq;
using System;
using System.IO;

public class GeospatialManager : MonoBehaviour
{
    public enum MarkerRenderMode
    {
        Screen2D,
        World3D,
        Both
    }

    private enum ClusterGroupingType
    {
        Default,
        Institutional,
        Commercial
    }

    [Serializable]
    private class LocalApiKeys
    {
        public string kakaoRestApiKey;
        public string tmapApiKey;
        public string androidCloudServicesApiKey;
        public string iosCloudServicesApiKey;
    }

    private class SearchDiagnostics
    {
        public int commercialRequestCount;
        public int campusRequestCount;
        public int keywordRequestCount;
        public int commercialResultCount;
        public int campusResultCount;
        public int keywordResultCount;
        public int mergedUniqueCount;
    }

    private static readonly string[] InstitutionalKeywords =
    {
        "학교", "대학교", "병원", "센터", "관", "동", "청사", "연구소", "연구원", "교육원", "대학원", "도서관", "학생회관", "행정실", "학과"
    };

    private static readonly string[] CommercialKeywords =
    {
        "마트", "편의점", "카페", "음식점", "약국", "상가"
    };

    private static readonly string[] InstitutionalBuildingSuffixes =
    {
        "본관", "별관", "기념도서관", "도서관", "학생회관", "기념관", "회관", "학관", "연구동", "행정동", "박물관", "미술관", "체육관", "강당", "센터", "연구소", "연구원", "관", "동"
    };

    private static readonly string[] InstitutionalPrefixTokens =
    {
        "대학교", "대학", "병원", "캠퍼스", "학교"
    };

    private static readonly string[] InstitutionalFacilityKeywords =
    {
        "행정실", "연구실", "사무실", "교학과", "학과", "학부", "대학원", "교육원", "지원센터", "상담센터",
        "랩", "lab", "카페", "편의점", "매점", "식당", "음식점", "서점", "은행", "atm", "우체국", "복사", "인쇄"
    };

    private static readonly string[] NonBuildingRepresentativeKeywords =
    {
        "삼거리", "사거리", "오거리", "교차로", "정문", "후문", "출구", "입구", "횡단보도", "정류장",
        "버스정류장", "지하철역", "주차장", "도로", "로터리", "광장", "거리"
    };

    // --- Inspector Settings ---
    [Header("AR Components")]
    public AREarthManager EarthManager; // 위도 경도
    public ARAnchorManager AnchorManager; // 앵커 관리

    [Header("UI Manager Connection")]
    public ARUIManager arUIManager; // UI 매니저 연결

    [Header("API & Data")]
    public string kakaoRestApiKey; // 카카오 REST API 호출 키
    public string tmapApiKey; // TMAP API 키
    public int searchRadius = 200; // 검색 반경 (미터 단위)
    public float clusterRadius = 20.0f; // 클러스터링 반경 (미터 단위)
    public float institutionalFacilityAssignmentRadius = 45.0f; // 대표 건물 POI 기준 시설 배정 반경
    public List<string> categoryCodes = new List<string> { "MT1", "CS2", "CE7", "HP8", "PM9", "FD6", "SC4" };
    public bool useCommercialCategorySearch = true;
    public bool useCampusCategorySearch = true;
    public bool useKeywordSearch = true;
    public int maxSearchPages = 3;
    public bool logSearchDiagnostics = true;
    public List<string> campusCategoryCodes = new List<string> { "SC4", "AC5", "CT1", "PO3", "AD5", "AT4", "PK6" };
    public List<string> keywordQueries = new List<string>
    {
        "인제대학교",
        "인제대학교 본관",
        "인제대학교 도서관",
        "인제대학교 학생회관",
        "인제대학교 기숙사",
        "인제대 원룸",
        "인제대 술집"
    };
    // 기존 categoryCodes는 상권 검색 기본값으로 유지하고, 캠퍼스/키워드 검색을 병행한다.

    [Header("Marker Settings")]
    public GameObject buildingMarkerPrefab;  // 건물 마커 프리팹
    public bool showNearbyAnchors = true;    // 근처 앵커 항상 표시
    public bool debugForceShowAllWorldMarkers = false; // 시야 판정과 무관하게 생성된 3D 마커 강제 표시
    public bool debugPlaceWorldMarkersInFrontOfCamera = false; // 생성된 3D 마커를 카메라 앞에 강제 배치
    public float debugFrontMarkerDistance = 2.5f; // 카메라 앞 디버그 거리
    public float debugFrontMarkerHeightOffset = -0.2f; // 카메라 기준 높이 오프셋
    public float debugFrontMarkerHorizontalSpacing = 0.7f; // 여러 마커 간 가로 간격
    public float anchorPreviewRadius = 250.0f; // 점으로 보여줄 앵커 반경
    public int maxPreviewAnchors = 3;       // 동시에 보여줄 최대 앵커 수
    public float anchorCreationRadius = 150.0f; // 현재 위치 기준 실제 생성 반경
    public int maxWorldTextMarkers = 20; // 후보로 유지할 최대 건물 수
    public int anchorCreateYieldInterval = 1; // 앵커 몇 개마다 한 프레임 양보할지
    public int textCreateYieldInterval = 1; // 텍스트 몇 개마다 한 프레임 양보할지
    public float selectedAnchorCreateDelay = 0.05f; // 같은 건물을 잠시 바라봤을 때만 앵커 생성
    public bool enableWorldTextMarker = true; // 원인 분리용: 텍스트 마커 생성/표시 토글
    public MarkerRenderMode markerRenderMode = MarkerRenderMode.World3D; // 2D/3D 마커 렌더 방식
    public double markerAltitudeOffsetMeters = 1.0; // 지면 고도 기준 앵커 오프셋
    public float worldMarkerLocalOffsetMeters = 0.0f; // 앵커 기준 3D 마커 추가 높이
    public float worldMarkerShellDistanceScale = 0.675f; // 카메라와 건물 거리 대비 텍스트 전진 비율
    public float worldMarkerShellMinRadius = 1.5f; // 텍스트가 건물 중심에서 최소 떨어지는 거리
    public float worldMarkerShellMaxRadius = 12.0f; // 텍스트가 건물 중심에서 최대 떨어지는 거리
    public float worldMarkerShellNearLimitFactor = 0.35f; // 아주 가까울 때 카메라-건물 거리 대비 상한 비율
    public float worldMarkerHeightDistanceScale = 0.08f; // 카메라-건물 거리 대비 텍스트 시각 높이 비율
    public float worldMarkerMinHeightAboveCamera = 0.25f; // 카메라 기준 최소 텍스트 높이
    public float worldMarkerMaxHeightAboveCamera = 1.2f; // 카메라 기준 최대 텍스트 높이
    public int elevationBatchSize = 20; // 고도 조회 좌표 배치 크기
    public bool showAnchorResolveDebugOverlay = true; // 앵커 해결 상태를 화면에 표시
    public int maxAnchorDebugLines = 10; // 화면에 유지할 디버그 줄 수
    public float anchorTrackingWaitSeconds = 20.0f; // 앵커 생성 전 Tracking 대기 시간
    public float markerHeightOffset = 12.0f; // 레거시 고정 오프셋 값(현재는 동적 오프셋 사용)
    public float markerHeightOffsetNear = 2.5f; // 가까울 때 마커 높이
    public float markerHeightOffsetFar = 7.0f; // 멀 때 마커 높이
    public float markerHeightNearDistance = 15.0f; // 최소 높이 기준 거리
    public float markerHeightFarDistance = 120.0f; // 최대 높이 기준 거리
    public bool showScreenSpaceMarkers = false; // 화면 위 2D 마커 표시
    public float screenMarkerHeightOffsetNear = 5.0f; // 가까울 때 2D 마커 기준 높이
    public float screenMarkerHeightOffsetFar = 12.0f; // 멀 때 2D 마커 기준 높이
    public float screenMarkerHeightNearDistance = 15.0f; // 2D 마커 최소 높이 기준 거리
    public float screenMarkerHeightFarDistance = 120.0f; // 2D 마커 최대 높이 기준 거리
    public float screenMarkerCameraHeightOffset = 0.0f; // 카메라 높이 기준 추가 보정값
    public bool showWorldSpaceInfoMarker = false; // 중앙 건물의 월드 공간 라벨 표시
    [Range(0.1f, 0.95f)] public float infoMarkerLerp = 0.6f; // 카메라-건물 사이 배치 비율
    public float infoMarkerMinDistance = 8.0f; // 카메라와 너무 붙지 않도록 최소 거리
    public float infoMarkerMaxDistance = 30.0f; // 너무 멀리 가지 않도록 최대 거리
    private BuildingMarker _currentActiveMarker; // 현재 선택된 마커 참조

    [Header("Geospatial Content")]
    private List<BuildingData> _autoGeneratedBuildingList = new List<BuildingData>();
    // 건물 인식 및 정보 표시 설정
    public float detectionRadius = 100.0f; // 감지 반경 (미터 단위)
    public float detectionAngle = 130.0f;   // 감지 각도 (도 단위)
    public float verticalAngleLimit = 60.0f;// 수직 각도 제한 (도 단위)
    public float refreshDistance = 40.0f; // 위치 변경에 따른 데이터 새로고침 기준 (미터 단위)
    [Range(0.01f, 0.35f)] public float centerViewportThreshold = 0.42f; // 정보 카드 허용 중심 범위
    [Range(0.01f, 0.25f)] public float forwardGroupViewportThreshold = 0.14f; // 같은 전방 시야선상 판정 범위
    public float groupedCandidateDistanceBias = 6.0f; // 같은 그룹에서 후면 후보를 제거하는 최소 거리차

    // --- Internal Variables ---
    private Transform _cameraTransform; // 카메라 트랜스폼 참조
    private BuildingData _currentDetectedBuilding; // 현재 감지된 건물 정보
    private BuildingData _selectedBuilding;// 사용자가 선택한 건물 정보  
    private Dictionary<string, ARGeospatialAnchor> _buildingAnchors = new Dictionary<string, ARGeospatialAnchor>();
    private List<BuildingData> _anchorCandidateBuildings = new List<BuildingData>();
    private Coroutine _activeAnchorCreationCoroutine;
    private string _lastAnchorPoolSignature = string.Empty;
    // 위치 기반 데이터 관리(건물 이름을 키로 앵커 참조 저장)
    private LocationInfo _lastLoadedLocation;
    private bool _isDataLoaded = false;
    private bool _isViewingInfo = false;
    private bool _isReloadingData = false;
    private bool _isAnchorSetupInProgress = false;
    public bool isNavigationActive = false;
    private readonly List<string> _anchorDebugLines = new List<string>();
    private string _lastDetectionDebugSignature = string.Empty;

    void Awake()
    {
        ApplyMarkerModeConfiguration();
    }

    void OnValidate()
    {
        ApplyMarkerModeConfiguration();
    }

    IEnumerator Start()
    {
        TryLoadApiKeysFromLocalFile();
        _cameraTransform = Camera.main.transform; // 메인 카메라 트랜스폼 참조
        ApplyMarkerModeConfiguration();

        if (arUIManager != null)
        {
            arUIManager.OnClickDetail += () => // 상세보기 버튼 클릭 시 처리
            {
                if (_selectedBuilding != null)
                {
                    arUIManager.OpenDetailView(_selectedBuilding);
                }
            };
            arUIManager.OnDetailOpened += () => _isViewingInfo = true;
            arUIManager.OnDetailClosed += () => _isViewingInfo = false;
        }
        else
        {
            Debug.LogError("ARUIManager Connection Failed");
        }

        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("Location permission denied by user.");
            arUIManager?.SetScanningMode();
            arUIManager?.ShowToast("위치 권한이 필요합니다.");
            yield break;
        }

        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (Input.location.status == LocationServiceStatus.Failed || Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogError($"Location service failed. Status: {Input.location.status}");
            arUIManager?.SetScanningMode();
            arUIManager?.ShowToast("위치 서비스를 시작할 수 없습니다.");
            yield break;
        }

        yield return StartCoroutine(ReloadDataAtCurrentLocation());

        while (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            yield return new WaitForSeconds(1.0f);
        }
    }

    void ApplyMarkerModeConfiguration()
    {
        switch (markerRenderMode)
        {
            case MarkerRenderMode.World3D:
                showNearbyAnchors = true;
                showScreenSpaceMarkers = false;
                showWorldSpaceInfoMarker = false;
                break;
            case MarkerRenderMode.Screen2D:
                showNearbyAnchors = false;
                showScreenSpaceMarkers = true;
                showWorldSpaceInfoMarker = false;
                break;
            case MarkerRenderMode.Both:
                showNearbyAnchors = true;
                showScreenSpaceMarkers = true;
                break;
        }
    }

    void TryLoadApiKeysFromLocalFile()
    {
        if (!string.IsNullOrWhiteSpace(kakaoRestApiKey) && !string.IsNullOrWhiteSpace(tmapApiKey)) return;

        string filePath = Path.Combine(Application.streamingAssetsPath, "LocalApiKeys.json");
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"Local API key file not found: {filePath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            LocalApiKeys keys = JsonUtility.FromJson<LocalApiKeys>(json);
            if (!string.IsNullOrWhiteSpace(keys?.kakaoRestApiKey))
            {
                kakaoRestApiKey = keys.kakaoRestApiKey;
            }
            if (!string.IsNullOrWhiteSpace(keys?.tmapApiKey))
            {
                tmapApiKey = keys.tmapApiKey;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load local API keys: {ex.Message}");
        }
    }

    void Update()
    {
        CheckMovementAndReload(); // 위치 변경에 따른 데이터 새로고침 여부 판단
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return;
        if (_isViewingInfo) return;
        if (isNavigationActive) return;
        if (_isReloadingData) return;
        if (_isAnchorSetupInProgress) return;
        HandleWorldMarkerTap();
        CheckBuildingDetection(); // 건물 감지 로직 실행
    }

    void HandleWorldMarkerTap()
    {
        if (!enableWorldTextMarker || !ShouldRenderWorldMarkers())
        {
            return;
        }

        if (!TryGetPointerDownPosition(out Vector2 screenPosition, out int fingerId, out bool isTouch))
        {
            return;
        }

        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            return;
        }

        Ray ray = currentCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, 500f))
        {
            return;
        }

        BuildingMarker marker = hitInfo.collider != null
            ? hitInfo.collider.GetComponentInParent<BuildingMarker>()
            : null;
        BuildingData tappedBuilding = marker != null ? marker.GetBoundBuilding() : null;
        if (tappedBuilding == null)
        {
            return;
        }

        _selectedBuilding = tappedBuilding;
        _currentDetectedBuilding = tappedBuilding;
        arUIManager?.OpenDetailView(tappedBuilding);
    }

    bool TryGetPointerDownPosition(out Vector2 screenPosition, out int fingerId, out bool isTouch)
    {
        screenPosition = default;
        fingerId = -1;
        isTouch = false;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
            {
                return false;
            }

            screenPosition = touch.position;
            fingerId = touch.fingerId;
            isTouch = true;
            return true;
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    // --- Building Detection Logic ---
    void CheckBuildingDetection() // 카메라 시점과 위치를 기반으로 가장 적합한 건물 감지 및 UI 업데이트
    {
        GeospatialPose pose = EarthManager != null ? EarthManager.CameraGeospatialPose : default(GeospatialPose);
        bool isEarthTracking = EarthManager != null && EarthManager.EarthTrackingState == TrackingState.Tracking;
        AnchorVisibilitySelection selection = AnchorVisibilityPlanner.BuildSelection(
            _anchorCandidateBuildings,
            pose,
            isEarthTracking,
            35.0f,
            anchorPreviewRadius,
            detectionAngle,
            _cameraTransform != null ? _cameraTransform.forward : Vector3.forward,
            verticalAngleLimit,
            detectionRadius,
            forwardGroupViewportThreshold,
            groupedCandidateDistanceBias,
            centerViewportThreshold,
            maxPreviewAnchors);
        List<VisibleBuildingCandidate> visibleCandidates = selection.visibleCandidates;
        List<VisibleBuildingCandidate> topCandidates = selection.topCandidates;
        BuildingData bestTarget = selection.focusedBuilding;
        UpdateDetectionDebugState(selection);
        RequestAnchorPoolForCandidates(topCandidates);
        UpdatePreviewMarkers(topCandidates, bestTarget);

        if (bestTarget != null)
        {
            if (!IsSameBuilding(_selectedBuilding, bestTarget))
            {
                _selectedBuilding = bestTarget;
            }

            if (_currentDetectedBuilding != bestTarget)
            {
                _currentDetectedBuilding = bestTarget;
                UpdateMarkerScale(bestTarget);
            }

            arUIManager.ShowQuickInfo(_selectedBuilding, GetDistanceToBuilding(_selectedBuilding));
        }
        else
        {
            if (_currentDetectedBuilding != null)
            {
                _currentDetectedBuilding = null;
                _currentActiveMarker = null;
            }

            if (_selectedBuilding != null)
            {
                _selectedBuilding = null;
            }

            arUIManager?.SetWorldInfoDetailButtonState(null, false);

            if (visibleCandidates.Count > 0)
            {
                arUIManager.SetDetectedMode();
            }
            else
            {
                arUIManager.SetScanningMode();
            }
        }
    }

    void UpdateDetectionDebugState(AnchorVisibilitySelection selection)
    {
        string signature = AnchorVisibilityPlanner.BuildDetectionSignature(selection);
        if (signature == _lastDetectionDebugSignature)
        {
            return;
        }

        _lastDetectionDebugSignature = signature;
        AppendAnchorDebug($"Visible count: {selection?.visibleCandidates?.Count ?? 0}");
        AppendAnchorDebug($"Visible list: {AnchorVisibilityPlanner.FormatCandidateList(selection?.visibleCandidates)}");
        AppendAnchorDebug($"Top candidates: {AnchorVisibilityPlanner.FormatCandidateList(selection?.topCandidates)}");
        AppendAnchorDebug($"Focused: {TrimDebugLabel(selection?.focusedBuilding?.buildingName ?? "none")}");
    }

    void UpdateMarkerScale(BuildingData targetBuilding)
    {
        if (_currentActiveMarker != null)
        {
            _currentActiveMarker.SetState(BuildingMarker.MarkerVisualState.Preview);
            _currentActiveMarker = null;
        }

        if (!ShouldRenderWorldMarkers())
        {
            arUIManager?.SetWorldInfoDetailButtonState(null, false);
            return;
        }

        if (!enableWorldTextMarker)
        {
            arUIManager?.SetWorldInfoDetailButtonState(null, false);
            return;
        }

        string buildingKey = targetBuilding != null ? GetBuildingAnchorKey(targetBuilding) : null;

        if (!string.IsNullOrWhiteSpace(buildingKey) && _buildingAnchors.ContainsKey(buildingKey))
        {
            ARGeospatialAnchor anchor = _buildingAnchors[buildingKey];
            BuildingMarker marker = anchor != null ? anchor.GetComponentInChildren<BuildingMarker>() : null;

            if (marker != null && anchor != null)
            {
                ApplyMarkerPlacement(marker, 0, 1);
                marker.SetInfoVisible(true);
                marker.SetState(BuildingMarker.MarkerVisualState.Selected);
                _currentActiveMarker = marker;
                arUIManager?.SetWorldInfoDetailButtonState(targetBuilding, true);
                return;
            }
        }

        arUIManager?.SetWorldInfoDetailButtonState(null, false);
    }

    void UpdatePreviewMarkers(List<VisibleBuildingCandidate> previewCandidates, BuildingData selectedBuilding)
    {
        HashSet<string> previewKeys = new HashSet<string>(
            (previewCandidates ?? new List<VisibleBuildingCandidate>())
                .Where(candidate => candidate?.building != null)
                .Select(candidate => GetBuildingAnchorKey(candidate.building))
                .Where(key => !string.IsNullOrWhiteSpace(key)));

        foreach (var building in _autoGeneratedBuildingList)
        {
            string buildingKey = GetBuildingAnchorKey(building);
            if (!_buildingAnchors.TryGetValue(buildingKey, out ARGeospatialAnchor anchor) || anchor == null)
            {
                continue;
            }

            BuildingMarker marker = anchor.GetComponentInChildren<BuildingMarker>();
            if (marker == null)
            {
                continue;
            }

            ApplyMarkerPlacement(marker, 0, 1);
            bool isPreviewTarget = previewKeys.Contains(buildingKey);
            marker.SetInfoVisible(enableWorldTextMarker && isPreviewTarget);
            marker.SetState(enableWorldTextMarker && isPreviewTarget
                ? BuildingMarker.MarkerVisualState.Preview
                : BuildingMarker.MarkerVisualState.Hidden);
        }

        _currentActiveMarker = null;
        arUIManager?.SetWorldInfoDetailButtonState(null, false);

        if (ShouldRenderWorldMarkers() && debugForceShowAllWorldMarkers)
        {
            if (selectedBuilding != null)
            {
                UpdateMarkerScale(selectedBuilding);
            }

            return;
        }

        if (selectedBuilding != null && ShouldRenderWorldMarkers())
        {
            UpdateMarkerScale(selectedBuilding);
        }

    }

    void ResetAllMarkers()
    {
        foreach (var anchor in _buildingAnchors.Values)
        {
            if (anchor == null)
            {
                continue;
            }

            BuildingMarker marker = anchor.GetComponentInChildren<BuildingMarker>();
            if (marker != null)
            {
                marker.SetState(BuildingMarker.MarkerVisualState.Hidden);
                marker.SetInfoVisible(false);
            }
        }

        _currentActiveMarker = null;
        arUIManager?.SetWorldInfoDetailButtonState(null, false);
    }

    void RequestAnchorPoolForCandidates(List<VisibleBuildingCandidate> topCandidates)
    {
        List<BuildingData> desiredBuildings = AnchorPoolPlanner.BuildDesiredBuildings(topCandidates);
        string signature = AnchorPoolPlanner.BuildPoolSignature(desiredBuildings);
        if (signature == _lastAnchorPoolSignature)
        {
            return;
        }

        _lastAnchorPoolSignature = signature;
        AppendAnchorDebug($"Anchor pool target: {desiredBuildings.Count}");

        if (_activeAnchorCreationCoroutine != null)
        {
            StopCoroutine(_activeAnchorCreationCoroutine);
            _activeAnchorCreationCoroutine = null;
        }

        _activeAnchorCreationCoroutine = StartCoroutine(SyncAnchorPool(desiredBuildings));
    }

    float GetDistanceToBuilding(BuildingData building)
    {
        if (building == null)
        {
            return -1f;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            return -1f;
        }

        LocationInfo currentLoc = Input.location.lastData;
        return (float)HaversineDistance(
            currentLoc.latitude,
            currentLoc.longitude,
            building.latitude,
            building.longitude);
    }

    string GetBuildingAnchorKey(BuildingData building)
    {
        if (building == null)
        {
            return string.Empty;
        }

        return $"{NormalizeText(building.buildingName)}|{Math.Round(building.latitude, 5):F5}|{Math.Round(building.longitude, 5):F5}";
    }

    bool IsSameBuilding(BuildingData left, BuildingData right)
    {
        return GetBuildingAnchorKey(left) == GetBuildingAnchorKey(right);
    }

    Vector3 GetDebugMarkerWorldPosition(int index, int totalCount)
    {
        if (_cameraTransform == null)
        {
            return Vector3.zero;
        }

        float totalWidth = Mathf.Max(0, totalCount - 1) * debugFrontMarkerHorizontalSpacing;
        float startOffset = -totalWidth * 0.5f;
        float xOffset = startOffset + (index * debugFrontMarkerHorizontalSpacing);

        Vector3 forward = _cameraTransform.forward.normalized;
        Vector3 right = _cameraTransform.right.normalized;
        Vector3 up = _cameraTransform.up.normalized;

        Vector3 basePosition = _cameraTransform.position + (forward * debugFrontMarkerDistance) + (up * debugFrontMarkerHeightOffset);
        return basePosition + (right * xOffset);
    }

    void ApplyMarkerPlacement(BuildingMarker marker, int index, int totalCount)
    {
        if (marker == null)
        {
            return;
        }

        bool shouldUseDebugFrontPlacement = debugForceShowAllWorldMarkers &&
                                            (debugPlaceWorldMarkersInFrontOfCamera || _buildingAnchors.Count <= 1);

        if (shouldUseDebugFrontPlacement)
        {
            marker.transform.position = GetDebugMarkerWorldPosition(index, totalCount);
            marker.transform.rotation = Quaternion.identity;
            return;
        }

        Transform anchorTransform = marker.transform.parent;
        if (anchorTransform == null)
        {
            marker.transform.localPosition = Vector3.up * worldMarkerLocalOffsetMeters;
            marker.transform.localRotation = Quaternion.identity;
            return;
        }

        Vector3 anchorPosition = anchorTransform.position;
        Vector3 targetPosition = anchorPosition + (Vector3.up * worldMarkerLocalOffsetMeters);

        if (_cameraTransform != null)
        {
            Vector3 horizontalToCamera = _cameraTransform.position - anchorPosition;
            horizontalToCamera.y = 0f;

            if (horizontalToCamera.sqrMagnitude < 0.0001f)
            {
                horizontalToCamera = _cameraTransform.forward;
                horizontalToCamera.y = 0f;
            }

            if (horizontalToCamera.sqrMagnitude > 0.0001f)
            {
                float distanceToAnchor = Vector3.Distance(_cameraTransform.position, anchorPosition);
                float shellRadius = Mathf.Clamp(
                    distanceToAnchor * Mathf.Max(0f, worldMarkerShellDistanceScale),
                    Mathf.Max(0f, worldMarkerShellMinRadius),
                    Mathf.Max(worldMarkerShellMinRadius, worldMarkerShellMaxRadius));
                float nearLimit = distanceToAnchor * Mathf.Max(0f, worldMarkerShellNearLimitFactor);
                if (nearLimit > 0f)
                {
                    shellRadius = Mathf.Min(shellRadius, nearLimit);
                }

                targetPosition = anchorPosition + (horizontalToCamera.normalized * shellRadius) + (Vector3.up * worldMarkerLocalOffsetMeters);

                float targetHeightAboveCamera = Mathf.Clamp(
                    distanceToAnchor * Mathf.Max(0f, worldMarkerHeightDistanceScale),
                    Mathf.Max(0f, worldMarkerMinHeightAboveCamera),
                    Mathf.Max(worldMarkerMinHeightAboveCamera, worldMarkerMaxHeightAboveCamera));
                targetPosition.y = _cameraTransform.position.y + targetHeightAboveCamera - marker.GetVisualHeightOffset();
            }
        }

        marker.transform.position = targetPosition;
        marker.transform.localRotation = Quaternion.identity;
    }

    // --- API & Data Processing ---
    IEnumerator FetchAndClusterNearbyPlaces(double lat, double lon)
    {
        ResetAnchorDebugOverlay();
        Dictionary<string, KakaoDocument> mergedPlaces = new Dictionary<string, KakaoDocument>();
        SearchDiagnostics diagnostics = new SearchDiagnostics();
        int successCount = 0;

        if (useCommercialCategorySearch)
        {
            foreach (string categoryCode in GetCommercialCategoryCodes())
            {
                bool categorySucceeded = false;
                diagnostics.commercialRequestCount++;
                yield return StartCoroutine(FetchNearbyPlacesFromKakaoCategory(lat, lon, categoryCode, "commercial", 100, (requestSucceeded, fetchedDocuments) =>
                {
                    categorySucceeded = requestSucceeded;
                    diagnostics.commercialResultCount += fetchedDocuments.Count;
                    MergeFetchedPlaces(mergedPlaces, fetchedDocuments);
                }));

                if (categorySucceeded)
                {
                    successCount++;
                }
            }
        }

        if (useCampusCategorySearch)
        {
            foreach (string categoryCode in GetCampusCategoryCodes())
            {
                bool categorySucceeded = false;
                diagnostics.campusRequestCount++;
                yield return StartCoroutine(FetchNearbyPlacesFromKakaoCategory(lat, lon, categoryCode, "campus", 200, (requestSucceeded, fetchedDocuments) =>
                {
                    categorySucceeded = requestSucceeded;
                    diagnostics.campusResultCount += fetchedDocuments.Count;
                    MergeFetchedPlaces(mergedPlaces, fetchedDocuments);
                }));

                if (categorySucceeded)
                {
                    successCount++;
                }
            }
        }

        if (useKeywordSearch)
        {
            foreach (string keywordQuery in GetKeywordQueries())
            {
                bool keywordSucceeded = false;
                diagnostics.keywordRequestCount++;
                yield return StartCoroutine(FetchNearbyPlacesFromKakaoKeyword(lat, lon, keywordQuery, 300, (requestSucceeded, fetchedDocuments) =>
                {
                    keywordSucceeded = requestSucceeded;
                    diagnostics.keywordResultCount += fetchedDocuments.Count;
                    MergeFetchedPlaces(mergedPlaces, fetchedDocuments);
                }));

                if (keywordSucceeded)
                {
                    successCount++;
                }
            }
        }

        if (successCount == 0)
        {
            Debug.LogError("All Kakao place requests failed.");
            arUIManager?.ShowToast("주변 장소를 불러오지 못했습니다.");
            ClearAllContent();
            yield break;
        }

        List<KakaoDocument> allFetchedPlaces = mergedPlaces.Values.ToList();
        diagnostics.mergedUniqueCount = allFetchedPlaces.Count;
        LogSearchDiagnostics(diagnostics, allFetchedPlaces);
        ClusterAndBuildList(allFetchedPlaces);

        if (_autoGeneratedBuildingList.Count == 0)
        {
            Debug.LogWarning("No nearby places were found for the current location.");
            arUIManager?.ShowToast("주변 장소 정보가 없습니다.");
        }

        yield return StartCoroutine(CreateAllBuildingAnchors());
    }

    IEnumerator FetchNearbyPlacesFromKakaoCategory(double lat, double lon, string categoryCode, string sourceLabel, int sourcePriority, Action<bool, List<KakaoDocument>> onCompleted)
    {
        List<KakaoDocument> fetchedDocuments = new List<KakaoDocument>();
        bool requestSucceeded = false;

        for (int page = 1; page <= maxSearchPages; page++)
        {
            string url = $"https://dapi.kakao.com/v2/local/search/category.json?category_group_code={categoryCode}&x={lon}&y={lat}&radius={searchRadius}&page={page}";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Authorization", "KakaoAK " + kakaoRestApiKey);
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    requestSucceeded = true;
                    KakaoResponse response = JsonUtility.FromJson<KakaoResponse>(webRequest.downloadHandler.text);
                    if (response?.documents == null || response.documents.Length == 0) break;
                    foreach (var doc in response.documents)
                    {
                        PrepareFetchedDocument(doc, sourceLabel, categoryCode, sourcePriority);
                        fetchedDocuments.Add(doc);
                    }
                    if (response.meta == null || response.meta.is_end) break;
                }
                else
                {
                    Debug.LogWarning($"Kakao category request failed for {categoryCode}, page {page}: {webRequest.error}");
                    break;
                }
            }
        }

        onCompleted?.Invoke(requestSucceeded, fetchedDocuments);
    }

    IEnumerator FetchNearbyPlacesFromKakaoKeyword(double lat, double lon, string query, int sourcePriority, Action<bool, List<KakaoDocument>> onCompleted)
    {
        List<KakaoDocument> fetchedDocuments = new List<KakaoDocument>();
        bool requestSucceeded = false;

        for (int page = 1; page <= maxSearchPages; page++)
        {
            string escapedQuery = UnityWebRequest.EscapeURL(query);
            string url = $"https://dapi.kakao.com/v2/local/search/keyword.json?query={escapedQuery}&x={lon}&y={lat}&radius={searchRadius}&page={page}";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Authorization", "KakaoAK " + kakaoRestApiKey);
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    requestSucceeded = true;
                    KakaoResponse response = JsonUtility.FromJson<KakaoResponse>(webRequest.downloadHandler.text);
                    if (response?.documents == null || response.documents.Length == 0) break;
                    foreach (KakaoDocument doc in response.documents)
                    {
                        PrepareFetchedDocument(doc, "keyword", query, sourcePriority);
                        fetchedDocuments.Add(doc);
                    }
                    if (response.meta == null || response.meta.is_end) break;
                }
                else
                {
                    Debug.LogWarning($"Kakao keyword request failed for '{query}', page {page}: {webRequest.error}");
                    break;
                }
            }
        }

        onCompleted?.Invoke(requestSucceeded, fetchedDocuments);
    }

    IEnumerable<string> GetCommercialCategoryCodes()
    {
        return categoryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct();
    }

    IEnumerable<string> GetCampusCategoryCodes()
    {
        return campusCategoryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Except(GetCommercialCategoryCodes())
            .Distinct();
    }

    IEnumerable<string> GetKeywordQueries()
    {
        return keywordQueries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct();
    }

    void PrepareFetchedDocument(KakaoDocument document, string searchSource, string sourceQuery, int sourcePriority)
    {
        if (document == null)
        {
            return;
        }

        document.search_source = searchSource;
        document.source_query = sourceQuery;
        document.source_priority = sourcePriority;

        if (string.IsNullOrWhiteSpace(document.category_group_name) && searchSource == "keyword")
        {
            document.category_group_name = "키워드 검색";
        }
    }

    void MergeFetchedPlaces(Dictionary<string, KakaoDocument> mergedPlaces, List<KakaoDocument> fetchedDocuments)
    {
        foreach (KakaoDocument document in fetchedDocuments)
        {
            string dedupeKey = GetDocumentDedupeKey(document);
            if (mergedPlaces.TryGetValue(dedupeKey, out KakaoDocument existingDocument))
            {
                if (ShouldReplaceDocument(existingDocument, document))
                {
                    mergedPlaces[dedupeKey] = document;
                }
                continue;
            }

            mergedPlaces[dedupeKey] = document;
        }
    }

    bool ShouldReplaceDocument(KakaoDocument existingDocument, KakaoDocument candidateDocument)
    {
        int existingScore = GetDocumentQualityScore(existingDocument);
        int candidateScore = GetDocumentQualityScore(candidateDocument);
        return candidateScore > existingScore;
    }

    int GetDocumentQualityScore(KakaoDocument document)
    {
        int score = document?.source_priority ?? 0;
        if (!string.IsNullOrWhiteSpace(document?.road_address_name)) score += 20;
        if (!string.IsNullOrWhiteSpace(document?.phone)) score += 5;
        if (!string.IsNullOrWhiteSpace(document?.category_group_name) && document.category_group_name != "키워드 검색") score += 10;
        return score;
    }

    string GetDocumentDedupeKey(KakaoDocument document)
    {
        string normalizedName = NormalizeText(document?.place_name);
        string normalizedAddress = NormalizeText(GetBestAddress(document));
        string normalizedCoords = $"{NormalizeCoordinate(document?.y)}|{NormalizeCoordinate(document?.x)}";

        if (!string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return $"{normalizedName}|{normalizedAddress}";
        }

        return $"{normalizedName}|{normalizedCoords}";
    }

    string GetBestAddress(KakaoDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document?.road_address_name))
        {
            return document.road_address_name;
        }

        return document?.address_name ?? string.Empty;
    }

    string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }

    string NormalizeCoordinate(string value)
    {
        if (!double.TryParse(value, out double parsed))
        {
            return "0";
        }

        return Math.Round(parsed, 5).ToString("F5");
    }

    void LogSearchDiagnostics(SearchDiagnostics diagnostics, List<KakaoDocument> mergedPlaces)
    {
        if (!logSearchDiagnostics)
        {
            return;
        }

        int injeHits = mergedPlaces.Count(doc => !string.IsNullOrWhiteSpace(doc.place_name) && doc.place_name.Contains("인제대"));
        int dormitoryHits = mergedPlaces.Count(doc => !string.IsNullOrWhiteSpace(doc.place_name) && (doc.place_name.Contains("기숙사") || doc.place_name.Contains("원룸")));
        int nightlifeHits = mergedPlaces.Count(doc => !string.IsNullOrWhiteSpace(doc.place_name) && (doc.place_name.Contains("술") || doc.place_name.Contains("주점") || doc.place_name.Contains("포차")));

        Debug.Log(
            $"[SearchDiagnostics] commercial requests/results: {diagnostics.commercialRequestCount}/{diagnostics.commercialResultCount}, " +
            $"campus requests/results: {diagnostics.campusRequestCount}/{diagnostics.campusResultCount}, " +
            $"keyword requests/results: {diagnostics.keywordRequestCount}/{diagnostics.keywordResultCount}, " +
            $"merged unique: {diagnostics.mergedUniqueCount}, " +
            $"inje hits: {injeHits}, dormitory or room hits: {dormitoryHits}, nightlife hits: {nightlifeHits}");
    }

    // Clustering Logic (Distance + Address Match)
    void ClusterAndBuildList(List<KakaoDocument> allPlaces)
    {
        _autoGeneratedBuildingList.Clear();
        List<List<KakaoDocument>> addressGroups = allPlaces
            .Where(document => document != null)
            .GroupBy(document => NormalizeText(GetBestAddress(document)))
            .Select(group => group.ToList())
            .ToList();

        foreach (List<KakaoDocument> addressGroup in addressGroups)
        {
            if (ShouldUseRepresentativeInstitutionalClustering(addressGroup))
            {
                foreach (List<KakaoDocument> cluster in BuildInstitutionalRepresentativeClusters(addressGroup))
                {
                    if (cluster.Count > 0)
                    {
                        _autoGeneratedBuildingList.Add(ProcessClusterIntoBuildingData(cluster));
                    }
                }
                continue;
            }

            foreach (List<KakaoDocument> cluster in BuildDistanceAddressClusters(addressGroup))
            {
                if (cluster.Count > 0)
                {
                    _autoGeneratedBuildingList.Add(ProcessClusterIntoBuildingData(cluster));
                }
            }
        }
    }

    List<List<KakaoDocument>> BuildDistanceAddressClusters(List<KakaoDocument> documents)
    {
        List<List<KakaoDocument>> clusters = new List<List<KakaoDocument>>();
        HashSet<int> processedIndices = new HashSet<int>();

        for (int i = 0; i < documents.Count; i++)
        {
            if (processedIndices.Contains(i)) continue;

            List<KakaoDocument> newCluster = new List<KakaoDocument>();
            KakaoDocument baseDoc = documents[i];
            newCluster.Add(baseDoc);
            processedIndices.Add(i);

            for (int j = i + 1; j < documents.Count; j++)
            {
                if (processedIndices.Contains(j)) continue;

                KakaoDocument compareDoc = documents[j];
                double distance = HaversineDistance(double.Parse(baseDoc.y), double.Parse(baseDoc.x),
                    double.Parse(compareDoc.y), double.Parse(compareDoc.x));

                if (distance <= clusterRadius)
                {
                    newCluster.Add(compareDoc);
                    processedIndices.Add(j);
                }
            }

            clusters.Add(newCluster);
        }

        return clusters;
    }

    bool ShouldUseRepresentativeInstitutionalClustering(List<KakaoDocument> addressGroup)
    {
        return addressGroup != null &&
               addressGroup.Count > 0 &&
               addressGroup.Any(IsRepresentativeBuildingCandidate);
    }

    List<List<KakaoDocument>> BuildInstitutionalRepresentativeClusters(List<KakaoDocument> addressGroup)
    {
        List<List<KakaoDocument>> clusters = new List<List<KakaoDocument>>();
        Dictionary<string, List<KakaoDocument>> clustersByToken = new Dictionary<string, List<KakaoDocument>>();

        foreach (KakaoDocument representative in SelectRepresentativeBuildingDocuments(addressGroup))
        {
            string tokenKey = NormalizeText(GetInstitutionalBuildingToken(representative));
            if (string.IsNullOrWhiteSpace(tokenKey) || clustersByToken.ContainsKey(tokenKey))
            {
                continue;
            }

            List<KakaoDocument> cluster = new List<KakaoDocument> { representative };
            clustersByToken[tokenKey] = cluster;
            clusters.Add(cluster);
        }

        List<KakaoDocument> leftovers = new List<KakaoDocument>();

        foreach (KakaoDocument document in addressGroup)
        {
            if (document == null)
            {
                continue;
            }

            string tokenKey = NormalizeText(GetInstitutionalBuildingToken(document));
            if (!string.IsNullOrWhiteSpace(tokenKey) &&
                clustersByToken.TryGetValue(tokenKey, out List<KakaoDocument> exactMatchCluster))
            {
                if (!exactMatchCluster.Contains(document))
                {
                    exactMatchCluster.Add(document);
                }
                continue;
            }

            if (TryAssignDocumentToNearestRepresentativeCluster(document, clustersByToken, out List<KakaoDocument> nearestCluster))
            {
                if (!nearestCluster.Contains(document))
                {
                    nearestCluster.Add(document);
                }
                continue;
            }

            leftovers.Add(document);
        }

        if (leftovers.Count > 0)
        {
            foreach (List<KakaoDocument> fallbackCluster in BuildDistanceAddressClusters(leftovers))
            {
                if (fallbackCluster.Count > 0)
                {
                    clusters.Add(fallbackCluster);
                }
            }
        }

        return clusters;
    }

    bool TryAssignDocumentToNearestRepresentativeCluster(
        KakaoDocument document,
        Dictionary<string, List<KakaoDocument>> clustersByToken,
        out List<KakaoDocument> matchedCluster)
    {
        matchedCluster = null;
        if (document == null || clustersByToken == null || clustersByToken.Count == 0)
        {
            return false;
        }

        string explicitToken = NormalizeText(GetInstitutionalBuildingToken(document));
        if (!string.IsNullOrWhiteSpace(explicitToken) && !clustersByToken.ContainsKey(explicitToken))
        {
            return false;
        }

        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<string, List<KakaoDocument>> pair in clustersByToken)
        {
            KakaoDocument representative = pair.Value
                .OrderByDescending(GetRepresentativeBuildingCandidateScore)
                .ThenByDescending(GetDocumentQualityScore)
                .FirstOrDefault();
            if (representative == null)
            {
                continue;
            }

            float distance = (float)HaversineDistance(
                double.Parse(document.y), double.Parse(document.x),
                double.Parse(representative.y), double.Parse(representative.x));

            if (distance > institutionalFacilityAssignmentRadius)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                matchedCluster = pair.Value;
            }
        }

        return matchedCluster != null;
    }

    List<KakaoDocument> SelectRepresentativeBuildingDocuments(List<KakaoDocument> addressGroup)
    {
        Dictionary<KakaoDocument, int> scoreMap = BuildRepresentativeCandidateScoreMap(addressGroup);

        return addressGroup
            .Where(IsRepresentativeBuildingCandidate)
            .GroupBy(GetRepresentativeBuildingClusterKey)
            .Select(group => group
                .OrderByDescending(document => scoreMap.TryGetValue(document, out int score) ? score : int.MinValue)
                .ThenByDescending(GetDocumentQualityScore)
                .ThenByDescending(document => !string.IsNullOrWhiteSpace(document.road_address_name))
                .ThenByDescending(document => document.place_name?.Length ?? 0)
                .First())
            .ToList();
    }

    bool IsRepresentativeBuildingCandidate(KakaoDocument document)
    {
        if (document == null)
        {
            return false;
        }

        if (!LooksLikeAnchorEligibleBuilding(document))
        {
            return false;
        }

        if (GetClusterGroupingType(document) != ClusterGroupingType.Institutional)
        {
            return false;
        }

        return GetRepresentativeBuildingCandidateScore(document) > 0;
    }

    int GetRepresentativeBuildingCandidateScore(KakaoDocument document)
    {
        if (document == null)
        {
            return int.MinValue;
        }

        if (!LooksLikeAnchorEligibleBuilding(document))
        {
            return int.MinValue;
        }

        int score = GetDocumentQualityScore(document);
        string normalizedPlaceName = NormalizeText(document.place_name);
        string normalizedToken = NormalizeText(GetInstitutionalBuildingToken(document));
        string normalizedCategory = NormalizeText(document.category_name);
        bool hasBuildingToken = !string.IsNullOrWhiteSpace(normalizedToken);
        bool looksLikeFacility = LooksLikeInstitutionalFacility(document);
        bool isCommercial = GetClusterGroupingType(document) == ClusterGroupingType.Commercial;

        if (NormalizeText(document.category_group_name) == NormalizeText("키워드 검색"))
        {
            score += 45;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
            (normalizedCategory.Contains(NormalizeText("학교부속시설")) ||
             normalizedCategory.Contains(NormalizeText("대학교")) ||
             normalizedCategory.Contains(NormalizeText("학교")) ||
             normalizedCategory.Contains(NormalizeText("병원"))))
        {
            score += 18;
        }

        if (hasBuildingToken)
        {
            score += 35;
        }

        if (normalizedPlaceName == normalizedToken && !string.IsNullOrWhiteSpace(normalizedToken))
        {
            score += 10;
        }

        if (isCommercial)
        {
            score -= 60;
        }

        if (looksLikeFacility)
        {
            score -= 55;
        }

        return score;
    }

    Dictionary<KakaoDocument, int> BuildRepresentativeCandidateScoreMap(List<KakaoDocument> addressGroup)
    {
        Dictionary<KakaoDocument, int> scoreMap = new Dictionary<KakaoDocument, int>();
        if (addressGroup == null)
        {
            return scoreMap;
        }

        foreach (KakaoDocument document in addressGroup)
        {
            scoreMap[document] = GetRepresentativeBuildingCandidateScore(document) +
                                 GetRepresentativeCentralityScore(document, addressGroup) +
                                 GetRepresentativeTokenSupportScore(document, addressGroup);
        }

        return scoreMap;
    }

    int GetRepresentativeCentralityScore(KakaoDocument document, List<KakaoDocument> addressGroup)
    {
        if (document == null || addressGroup == null || addressGroup.Count <= 1)
        {
            return 0;
        }

        int nearbyCount = 0;
        foreach (KakaoDocument other in addressGroup)
        {
            if (other == null || other == document)
            {
                continue;
            }

            double distance = HaversineDistance(
                double.Parse(document.y), double.Parse(document.x),
                double.Parse(other.y), double.Parse(other.x));

            if (distance <= institutionalFacilityAssignmentRadius)
            {
                nearbyCount++;
            }
        }

        return Mathf.Min(nearbyCount * 8, 32);
    }

    int GetRepresentativeTokenSupportScore(KakaoDocument document, List<KakaoDocument> addressGroup)
    {
        if (document == null || addressGroup == null)
        {
            return 0;
        }

        string clusterKey = NormalizeText(GetRepresentativeBuildingClusterKey(document));
        if (string.IsNullOrWhiteSpace(clusterKey))
        {
            return -20;
        }

        int supportCount = addressGroup.Count(other =>
            other != null &&
            NormalizeText(GetRepresentativeBuildingClusterKey(other)) == clusterKey);

        return Mathf.Min(Mathf.Max(0, supportCount - 1) * 10, 40);
    }

    string GetRepresentativeBuildingClusterKey(KakaoDocument document)
    {
        string buildingToken = NormalizeText(GetInstitutionalBuildingToken(document));
        if (!string.IsNullOrWhiteSpace(buildingToken))
        {
            return buildingToken;
        }

        return NormalizeRepresentativeName(document?.place_name);
    }

    bool LooksLikeInstitutionalFacility(KakaoDocument document)
    {
        string normalizedPlaceName = NormalizeText(document?.place_name);
        string normalizedCategory = NormalizeText(document?.category_name);
        string normalizedGroupCategory = NormalizeText(document?.category_group_name);

        return ContainsAnyKeyword(normalizedPlaceName, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedCategory, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedGroupCategory, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
               ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords);
    }

    bool LooksLikeAnchorEligibleBuilding(KakaoDocument document)
    {
        if (document == null)
        {
            return false;
        }

        string normalizedPlaceName = NormalizeText(document.place_name);
        string normalizedCategory = NormalizeText(document.category_name);
        string normalizedGroupCategory = NormalizeText(document.category_group_name);

        if (ContainsAnyKeyword(normalizedPlaceName, NonBuildingRepresentativeKeywords) ||
            ContainsAnyKeyword(normalizedCategory, NonBuildingRepresentativeKeywords) ||
            ContainsAnyKeyword(normalizedGroupCategory, NonBuildingRepresentativeKeywords))
        {
            return false;
        }

        if (ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
            ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords))
        {
            return false;
        }

        return true;
    }

    string NormalizeRepresentativeName(string placeName)
    {
        string normalized = NormalizeText(placeName);
        foreach (string prefixToken in InstitutionalPrefixTokens)
        {
            normalized = normalized.Replace(NormalizeText(prefixToken), string.Empty);
        }

        return normalized;
    }

    // Convert API Data to BuildingData
    BuildingData ProcessClusterIntoBuildingData(List<KakaoDocument> cluster)
    {
        BuildingData newBuilding = new BuildingData();
        KakaoDocument detailRepresentative = SelectRepresentativeDocument(cluster);
        string institutionalBuildingToken = SelectInstitutionalBuildingToken(cluster);
        bool useInstitutionalBuildingName = !string.IsNullOrWhiteSpace(institutionalBuildingToken);
        KakaoDocument nameRepresentative = SelectBuildingNameDocument(cluster, detailRepresentative, institutionalBuildingToken);
        KakaoDocument anchorRepresentative = SelectAnchorRepresentativeDocument(cluster, nameRepresentative);

        newBuilding.buildingName = ResolveBuildingDisplayName(cluster, nameRepresentative, institutionalBuildingToken, useInstitutionalBuildingName);
        newBuilding.latitude = double.Parse(anchorRepresentative.y);
        newBuilding.longitude = double.Parse(anchorRepresentative.x);
        newBuilding.altitude = 0;
        newBuilding.fetchedAddress = !string.IsNullOrWhiteSpace(GetBestAddress(nameRepresentative))
            ? GetBestAddress(nameRepresentative)
            : GetBestAddress(detailRepresentative);
        newBuilding.description = GetBuildingDescription(cluster, newBuilding.buildingName, nameRepresentative, detailRepresentative, useInstitutionalBuildingName);

        newBuilding.phoneNumber = detailRepresentative.phone;
        newBuilding.placeUrl = detailRepresentative.place_url;
        newBuilding.zipCode = detailRepresentative.address_name;

        newBuilding.facilities = BuildFacilityList(cluster, newBuilding.buildingName, institutionalBuildingToken);

        return newBuilding;
    }

    KakaoDocument SelectAnchorRepresentativeDocument(List<KakaoDocument> cluster, KakaoDocument fallbackDocument)
    {
        KakaoDocument buildingLikeRepresentative = cluster
            .Where(LooksLikeAnchorEligibleBuilding)
            .OrderByDescending(GetRepresentativeBuildingCandidateScore)
            .ThenByDescending(GetDocumentQualityScore)
            .ThenByDescending(document => !string.IsNullOrWhiteSpace(document.road_address_name))
            .ThenByDescending(document => document.place_name?.Length ?? 0)
            .FirstOrDefault();

        return buildingLikeRepresentative ?? fallbackDocument;
    }

    string ResolveBuildingDisplayName(List<KakaoDocument> cluster, KakaoDocument nameRepresentative, string buildingToken, bool useInstitutionalBuildingName)
    {
        if (!useInstitutionalBuildingName)
        {
            return nameRepresentative.place_name;
        }

        KakaoDocument representativeBuildingDocument = cluster
            .Where(IsRepresentativeBuildingCandidate)
            .OrderByDescending(GetRepresentativeBuildingCandidateScore)
            .ThenByDescending(GetDocumentQualityScore)
            .FirstOrDefault(document => NormalizeText(GetRepresentativeBuildingClusterKey(document)) == NormalizeText(buildingToken));

        if (!string.IsNullOrWhiteSpace(representativeBuildingDocument?.place_name))
        {
            return representativeBuildingDocument.place_name;
        }

        return !string.IsNullOrWhiteSpace(nameRepresentative?.place_name)
            ? nameRepresentative.place_name
            : buildingToken;
    }

    string GetBuildingDescription(List<KakaoDocument> cluster, string buildingName, KakaoDocument nameRepresentative, KakaoDocument detailRepresentative, bool useInstitutionalBuildingName)
    {
        if (useInstitutionalBuildingName)
        {
            KakaoDocument representativeBuildingDocument = cluster
                .Where(IsRepresentativeBuildingCandidate)
                .OrderByDescending(GetRepresentativeBuildingCandidateScore)
                .ThenByDescending(GetDocumentQualityScore)
                .FirstOrDefault(document => IsBuildingReferenceDocument(document, buildingName, buildingName));

            if (representativeBuildingDocument != null)
            {
                string buildingDisplayCategory = GetDocumentDisplayCategory(representativeBuildingDocument, "학교시설");
                if (!string.IsNullOrWhiteSpace(buildingDisplayCategory) &&
                    buildingDisplayCategory != "키워드 검색")
                {
                    return buildingDisplayCategory;
                }
            }

            string institutionalDisplayCategory = GetDocumentDisplayCategory(nameRepresentative, "학교시설");
            if (!string.IsNullOrWhiteSpace(institutionalDisplayCategory) &&
                institutionalDisplayCategory != "키워드 검색")
            {
                return institutionalDisplayCategory;
            }

            return "학교시설";
        }

        return GetDocumentDisplayCategory(detailRepresentative, "장소 정보");
    }

    List<FacilityInfo> BuildFacilityList(List<KakaoDocument> cluster, string buildingName, string buildingToken)
    {
        Dictionary<string, KakaoDocument> uniqueFacilities = new Dictionary<string, KakaoDocument>();
        string normalizedBuildingName = NormalizeText(buildingName);

        foreach (KakaoDocument document in cluster)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.place_name))
            {
                continue;
            }

            string normalizedPlaceName = NormalizeText(document.place_name);
            if (normalizedPlaceName == normalizedBuildingName)
            {
                continue;
            }

            if (IsBuildingReferenceDocument(document, buildingName, buildingToken))
            {
                continue;
            }

            if (uniqueFacilities.TryGetValue(normalizedPlaceName, out KakaoDocument existingDocument))
            {
                if (ShouldReplaceDocument(existingDocument, document))
                {
                    uniqueFacilities[normalizedPlaceName] = document;
                }
                continue;
            }

            uniqueFacilities[normalizedPlaceName] = document;
        }

        return uniqueFacilities.Values
            .OrderByDescending(GetDocumentQualityScore)
            .ThenBy(document => document.place_name)
            .Select(document => new FacilityInfo
            {
                name = document.place_name,
                phone = document.phone,
                category = GetDocumentDisplayCategory(document, "장소 정보"),
                placeUrl = document.place_url
            })
            .ToList();
    }

    string GetDocumentDisplayCategory(KakaoDocument document, string defaultCategory)
    {
        if (!string.IsNullOrWhiteSpace(document?.category_name))
        {
            return document.category_name;
        }

        if (!string.IsNullOrWhiteSpace(document?.category_group_name))
        {
            return document.category_group_name;
        }

        return defaultCategory;
    }

    KakaoDocument SelectRepresentativeDocument(List<KakaoDocument> cluster)
    {
        return cluster
            .OrderByDescending(GetDocumentQualityScore)
            .ThenByDescending(document => !string.IsNullOrWhiteSpace(document.road_address_name))
            .ThenByDescending(document => !string.IsNullOrWhiteSpace(document.phone))
            .ThenByDescending(document => document.place_name?.Length ?? 0)
            .First();
    }

    KakaoDocument SelectBuildingNameDocument(List<KakaoDocument> cluster, KakaoDocument fallbackDocument, string buildingToken)
    {
        if (cluster == null || cluster.Count == 0)
        {
            return fallbackDocument;
        }

        if (!string.IsNullOrWhiteSpace(buildingToken))
        {
            KakaoDocument tokenDocument = cluster
                .Where(document => NormalizeText(GetRepresentativeBuildingClusterKey(document)) == NormalizeText(buildingToken))
                .OrderByDescending(GetRepresentativeBuildingCandidateScore)
                .ThenByDescending(document => IsBuildingReferenceDocument(document, buildingToken, buildingToken))
                .ThenByDescending(GetDocumentQualityScore)
                .ThenByDescending(document => !string.IsNullOrWhiteSpace(document.road_address_name))
                .FirstOrDefault();

            if (tokenDocument != null)
            {
                return tokenDocument;
            }
        }

        KakaoDocument buildingNameCandidate = cluster
            .Where(IsRepresentativeBuildingCandidate)
            .OrderByDescending(GetRepresentativeBuildingCandidateScore)
            .ThenByDescending(GetDocumentQualityScore)
            .ThenBy(document => document.place_name?.Length ?? int.MaxValue)
            .FirstOrDefault();

        return buildingNameCandidate ?? fallbackDocument;
    }

    string SelectInstitutionalBuildingToken(List<KakaoDocument> cluster)
    {
        return cluster
            .Where(IsRepresentativeBuildingCandidate)
            .Select(document => GetRepresentativeBuildingClusterKey(document))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .GroupBy(token => NormalizeText(token))
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(token => token.Length))
            .Select(group => group.First())
            .FirstOrDefault();
    }

    ClusterGroupingType GetClusterGroupingType(KakaoDocument document)
    {
        string normalizedPlaceName = NormalizeText(document?.place_name);
        string normalizedCategory = NormalizeText(document?.category_group_name);

        if (ContainsAnyKeyword(normalizedPlaceName, CommercialKeywords) || ContainsAnyKeyword(normalizedCategory, CommercialKeywords))
        {
            return ClusterGroupingType.Commercial;
        }

        if (ContainsAnyKeyword(normalizedPlaceName, InstitutionalKeywords) || ContainsAnyKeyword(normalizedCategory, InstitutionalKeywords))
        {
            return ClusterGroupingType.Institutional;
        }

        return ClusterGroupingType.Default;
    }

    bool ContainsAnyKeyword(string normalizedValue, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        foreach (string keyword in keywords)
        {
            if (normalizedValue.Contains(NormalizeText(keyword)))
            {
                return true;
            }
        }

        return false;
    }

    string GetInstitutionalBuildingToken(KakaoDocument document)
    {
        string addressToken = ExtractInstitutionalBuildingToken(GetBestAddress(document));
        if (!string.IsNullOrWhiteSpace(addressToken))
        {
            return addressToken;
        }

        return ExtractInstitutionalBuildingToken(document?.place_name);
    }

    string ExtractInstitutionalBuildingToken(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        string[] tokens = sourceText
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace(",", " ")
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            if (IsInstitutionalBuildingToken(token))
            {
                return token.Trim();
            }
        }

        return string.Empty;
    }

    bool IsInstitutionalBuildingToken(string token)
    {
        string normalizedToken = NormalizeText(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return false;
        }

        if (ContainsAnyKeyword(normalizedToken, CommercialKeywords))
        {
            return false;
        }

        foreach (string suffix in InstitutionalBuildingSuffixes)
        {
            if (normalizedToken.EndsWith(NormalizeText(suffix)))
            {
                return true;
            }
        }

        return false;
    }

    bool IsBuildingReferenceDocument(KakaoDocument document, string buildingName, string buildingToken)
    {
        if (document == null || string.IsNullOrWhiteSpace(document.place_name))
        {
            return false;
        }

        string normalizedBuildingName = NormalizeText(buildingName);
        string normalizedBuildingToken = NormalizeText(string.IsNullOrWhiteSpace(buildingToken) ? buildingName : buildingToken);
        string normalizedPlaceName = NormalizeText(document.place_name);

        if (normalizedPlaceName == normalizedBuildingName || normalizedPlaceName == normalizedBuildingToken)
        {
            return true;
        }

        string extractedToken = NormalizeText(GetInstitutionalBuildingToken(document));
        if (string.IsNullOrWhiteSpace(extractedToken) || extractedToken != normalizedBuildingToken)
        {
            return false;
        }

        string reducedName = normalizedPlaceName.Replace(normalizedBuildingToken, string.Empty);
        foreach (string prefixToken in InstitutionalPrefixTokens)
        {
            reducedName = reducedName.Replace(NormalizeText(prefixToken), string.Empty);
        }

        return string.IsNullOrWhiteSpace(reducedName);
    }

    IEnumerator CreateAllBuildingAnchors()
    {
        _isAnchorSetupInProgress = true;
        if (_activeAnchorCreationCoroutine != null)
        {
            StopCoroutine(_activeAnchorCreationCoroutine);
            _activeAnchorCreationCoroutine = null;
        }

        foreach (var anchor in _buildingAnchors.Values) if (anchor != null) Destroy(anchor.gameObject);
        _buildingAnchors.Clear();
        ResetAllMarkers();
        _lastAnchorPoolSignature = string.Empty;

        List<BuildingData> targetBuildings = GetBuildingsForAnchorCreation();
        _anchorCandidateBuildings = targetBuildings;

        AppendAnchorDebug($"Anchor targets ready: {targetBuildings.Count}/{_autoGeneratedBuildingList.Count}");
        yield return StartCoroutine(WaitForTrackingBeforeAnchors());
        ApplyFallbackAltitudes(targetBuildings);
        AppendAnchorDebug($"Anchor creation deferred until selection");
        _isAnchorSetupInProgress = false;
    }

    IEnumerator SyncAnchorPool(List<BuildingData> desiredBuildings)
    {
        _isAnchorSetupInProgress = true;
        HashSet<string> desiredKeys = new HashSet<string>(
            (desiredBuildings ?? new List<BuildingData>())
                .Where(building => building != null)
                .Select(GetBuildingAnchorKey)
                .Where(key => !string.IsNullOrWhiteSpace(key)));

        List<string> keysToRemove = _buildingAnchors.Keys
            .Where(existingKey => !desiredKeys.Contains(existingKey))
            .ToList();

        foreach (string removeKey in keysToRemove)
        {
            if (_buildingAnchors.TryGetValue(removeKey, out ARGeospatialAnchor existingAnchor) && existingAnchor != null)
            {
                Destroy(existingAnchor.gameObject);
            }

            _buildingAnchors.Remove(removeKey);
        }

        _currentActiveMarker = null;

        foreach (BuildingData building in desiredBuildings)
        {
            if (building == null)
            {
                continue;
            }

            string buildingKey = GetBuildingAnchorKey(building);
            if (string.IsNullOrWhiteSpace(buildingKey) || _buildingAnchors.ContainsKey(buildingKey))
            {
                continue;
            }

            yield return StartCoroutine(CreateAnchorForBuilding(building));
        }

        AppendAnchorDebug($"Anchor pool active: {_buildingAnchors.Count}");
        _isAnchorSetupInProgress = false;
        _activeAnchorCreationCoroutine = null;
    }

    IEnumerator CreateAnchorForBuilding(BuildingData building)
    {
        string buildingKey = GetBuildingAnchorKey(building);
        AppendAnchorDebug($"Anchor create start: {TrimDebugLabel(building.buildingName)}");
        yield return null;

        ARGeospatialAnchor anchor = AnchorManager.AddAnchor(
            building.latitude,
            building.longitude,
            building.altitude,
            Quaternion.identity);

        if (anchor == null)
        {
            AppendAnchorDebug($"Anchor create fail: {TrimDebugLabel(building.buildingName)}");
            yield break;
        }

        _buildingAnchors[buildingKey] = anchor;
        AppendAnchorDebug($"Anchor create ok: {TrimDebugLabel(building.buildingName)}");

        if (enableWorldTextMarker && buildingMarkerPrefab != null)
        {
            yield return null;

            GameObject markerObj = Instantiate(buildingMarkerPrefab, anchor.transform);
            markerObj.transform.localPosition = Vector3.up * worldMarkerLocalOffsetMeters;
            markerObj.transform.localRotation = Quaternion.identity;

            BuildingMarker marker = markerObj.GetComponent<BuildingMarker>();
            if (marker != null)
            {
                ApplyMarkerPlacement(marker, 0, 1);
                marker.BindBuilding(building);
                marker.SetInfoContent(
                    building.buildingName,
                    string.IsNullOrWhiteSpace(building.description) ? "장소 정보" : building.description);
                marker.SetInfoVisible(true);
                marker.SetState(BuildingMarker.MarkerVisualState.Preview, true);
                AppendAnchorDebug($"TEXT ready: {TrimDebugLabel(building.buildingName)}");
            }
            else
            {
                AppendAnchorDebug($"TEXT component missing: {TrimDebugLabel(building.buildingName)}");
            }
        }
        else if (!enableWorldTextMarker)
        {
            AppendAnchorDebug($"TEXT disabled: {TrimDebugLabel(building.buildingName)}");
        }
        else
        {
            AppendAnchorDebug($"TEXT prefab missing: {TrimDebugLabel(building.buildingName)}");
        }
    }

    List<BuildingData> GetBuildingsForAnchorCreation()
    {
        if (_autoGeneratedBuildingList == null || _autoGeneratedBuildingList.Count == 0)
        {
            return new List<BuildingData>();
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            return _autoGeneratedBuildingList
                .Take(Mathf.Max(1, maxWorldTextMarkers))
                .ToList();
        }

        LocationInfo currentLoc = Input.location.lastData;

        return _autoGeneratedBuildingList
            .Where(building => building != null)
            .Select(building => new
            {
                building,
                distance = HaversineDistance(
                    currentLoc.latitude,
                    currentLoc.longitude,
                    building.latitude,
                    building.longitude)
            })
            .Where(item => item.distance <= anchorCreationRadius)
            .OrderBy(item => item.distance)
            .Take(Mathf.Max(1, maxWorldTextMarkers))
            .Select(item => item.building)
            .ToList();
    }

    IEnumerator WaitForTrackingBeforeAnchors()
    {
        if (EarthManager == null)
        {
            AppendAnchorDebug("Earth manager missing");
            yield break;
        }

        float timeout = Mathf.Max(0f, anchorTrackingWaitSeconds);
        float elapsed = 0f;

        while (EarthManager.EarthTrackingState != TrackingState.Tracking && elapsed < timeout)
        {
            AppendAnchorDebug($"Wait tracking: {EarthManager.EarthTrackingState}");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (EarthManager.EarthTrackingState == TrackingState.Tracking)
        {
            AppendAnchorDebug($"Tracking ready: alt={EarthManager.CameraGeospatialPose.Altitude:F1}");
        }
        else
        {
            AppendAnchorDebug($"Tracking timeout: {EarthManager.EarthTrackingState}");
        }
    }

    void ResetAnchorDebugOverlay()
    {
        _anchorDebugLines.Clear();
        _lastDetectionDebugSignature = string.Empty;
        _lastAnchorPoolSignature = string.Empty;
        arUIManager?.ClearDebugOverlay();
    }

    void AppendAnchorDebug(string message)
    {
        Debug.Log($"[AnchorDebug] {message}");

        if (!showAnchorResolveDebugOverlay)
        {
            return;
        }

        _anchorDebugLines.Add(message);
        while (_anchorDebugLines.Count > Mathf.Max(1, maxAnchorDebugLines))
        {
            _anchorDebugLines.RemoveAt(0);
        }

        arUIManager?.SetDebugOverlay(string.Join("\n", _anchorDebugLines));
    }

    void ApplyFallbackAltitudes(List<BuildingData> buildings)
    {
        if (buildings == null)
        {
            return;
        }

        double baseAltitude = 0;
        if (EarthManager != null && EarthManager.EarthTrackingState == TrackingState.Tracking)
        {
            baseAltitude = EarthManager.CameraGeospatialPose.Altitude;
        }

        foreach (BuildingData building in buildings)
        {
            if (building == null)
            {
                continue;
            }

            building.altitude = baseAltitude + markerAltitudeOffsetMeters;
            AppendAnchorDebug($"Anchor alt: {TrimDebugLabel(building.buildingName)} ({building.altitude:F1})");
        }
    }

    string TrimDebugLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        return value.Length <= 18 ? value : $"{value.Substring(0, 18)}...";
    }

    bool ShouldRenderWorldMarkers()
    {
        return showNearbyAnchors && (markerRenderMode == MarkerRenderMode.World3D || markerRenderMode == MarkerRenderMode.Both);
    }

    void CheckMovementAndReload() // 위치 변경에 따른 데이터 새로고침 여부 판단
    {
        if (_isReloadingData)
        {
            return;
        }

        if (Input.location.status == LocationServiceStatus.Running && _isDataLoaded)
        {
            LocationInfo currentLoc = Input.location.lastData;
            double distance = HaversineDistance(_lastLoadedLocation.latitude, _lastLoadedLocation.longitude, currentLoc.latitude, currentLoc.longitude);
            if (distance >= refreshDistance) { StartCoroutine(ReloadDataAtCurrentLocation()); }
        }
    }
    IEnumerator ReloadDataAtCurrentLocation() // 현재 위치에서 데이터 새로고침
    {
        if (_isReloadingData)
        {
            yield break;
        }

        _isReloadingData = true;
        LocationInfo currentLoc = Input.location.lastData;
        _isDataLoaded = false;
        Debug.Log($"Reloading nearby place data. Lat: {currentLoc.latitude}, Lon: {currentLoc.longitude}");
        yield return StartCoroutine(FetchAndClusterNearbyPlaces(currentLoc.latitude, currentLoc.longitude));
        _lastLoadedLocation = currentLoc;
        _isDataLoaded = true;
        _isReloadingData = false;
    }
    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    // 두 지점 간의 거리를 계산하는 Haversine 공식
    {
        const double R = 6371e3; var phi1 = lat1 * (Math.PI / 180.0);
        var phi2 = lat2 * (Math.PI / 180.0); var deltaPhi = (lat2 - lat1) * (Math.PI / 180.0);
        var deltaLambda = (lon2 - lon1) * (Math.PI / 180.0);
        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)); return R * c;
    }
    void ClearAllContent()
    {
        foreach (var anchor in _buildingAnchors.Values) if (anchor != null) Destroy(anchor.gameObject);
        _buildingAnchors.Clear();
        _autoGeneratedBuildingList.Clear();
        _currentDetectedBuilding = null;
        _selectedBuilding = null;
        _currentActiveMarker = null;
        _lastDetectionDebugSignature = string.Empty;
        _lastAnchorPoolSignature = string.Empty;
        arUIManager?.SetScanningMode();
    }
}

// --- Data Classes ---
[System.Serializable]
public class KakaoResponse
{
    public KakaoDocument[] documents;
    public KakaoMeta meta;
}

[System.Serializable]
public class KakaoDocument
{
    public string place_name;
    public string road_address_name;
    public string address_name;
    public string category_name;
    public string category_group_name;
    public string category_group_code;
    public string phone;
    public string place_url;
    public string y;
    public string x;
    public string distance; // 중심 좌표로부터 직선거리(미터 문자열)
    public string search_source;
    public string source_query;
    public int source_priority;
}

[System.Serializable]
public class KakaoMeta
{
    public int total_count;
    public bool is_end;
}
