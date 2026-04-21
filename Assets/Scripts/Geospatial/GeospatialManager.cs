using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Linq;
using System;
using System.IO;

public partial class GeospatialManager : MonoBehaviour
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
    public float debugFrontMarkerDistance = 10.0f; // 카메라 앞 디버그 거리
    public float debugFrontMarkerHeightOffset = -0.2f; // 카메라 기준 높이 오프셋
    public float debugFrontMarkerHorizontalSpacing = 0.7f; // 여러 마커 간 가로 간격
    public float anchorPreviewRadius = 250.0f; // 점으로 보여줄 앵커 반경
    public int maxPreviewAnchors = 3;       // 동시에 보여줄 최대 앵커 수
    public float anchorCreationRadius = 150.0f; // 현재 위치 기준 실제 생성 반경
    public int maxWorldTextMarkers = 20; // 후보로 유지할 최대 건물 수
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
    [Header("Landscape Detection Profile")]
    public float landscapeDetectionRadius = 140.0f; // 가로모드에서 더 넓게 허용할 감지 반경
    public float landscapeDetectionAngle = 180.0f; // 가로모드에서 좌우로 더 많이 보이도록 확장한 감지 각도
    public int landscapeMaxPreviewAnchors = 5; // 가로모드에서 동시에 유지할 최대 앵커 수
    [Range(0.01f, 0.35f)] public float landscapeCenterViewportThreshold = 0.30f; // 가로모드에서 중앙 텍스트 선택을 더 엄격하게 하는 범위
    [Range(0.01f, 0.25f)] public float landscapeForwardGroupViewportThreshold = 0.10f; // 가로모드에서 좌우 후보를 더 많이 살리기 위한 그룹 판정 범위

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
    private float _portraitDetectionRadius;
    private float _portraitDetectionAngle;
    private int _portraitMaxPreviewAnchors;
    private float _portraitCenterViewportThreshold;
    private float _portraitForwardGroupViewportThreshold;
    private bool _isRuntimeDebugModeEnabled;
    private bool _defaultShowAnchorResolveDebugOverlay;

    void Awake()
    {
        CachePortraitDetectionProfile();
        _defaultShowAnchorResolveDebugOverlay = showAnchorResolveDebugOverlay;
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
        DisableRuntimeDebugFrontMarkerPlacement();
        ApplyMarkerModeConfiguration();

        if (arUIManager != null)
        {
            arUIManager.OnDetailOpened += () => _isViewingInfo = true;
            arUIManager.OnDetailClosed += () => _isViewingInfo = false;
            arUIManager.OnLandscapeModeToggleRequested += HandleLandscapeModeToggleRequested;
            arUIManager.OnDebugModeToggleRequested += HandleDebugModeToggleRequested;
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

    void DisableRuntimeDebugFrontMarkerPlacement()
    {
        _isRuntimeDebugModeEnabled = false;
        debugForceShowAllWorldMarkers = false;
        debugPlaceWorldMarkersInFrontOfCamera = false;
        showAnchorResolveDebugOverlay = false;
        arUIManager?.ClearDebugOverlay();
    }

    void HandleDebugModeToggleRequested(bool enabled)
    {
        _isRuntimeDebugModeEnabled = enabled;
        debugForceShowAllWorldMarkers = enabled;
        debugPlaceWorldMarkersInFrontOfCamera = enabled;
        showAnchorResolveDebugOverlay = enabled && _defaultShowAnchorResolveDebugOverlay;

        if (!showAnchorResolveDebugOverlay)
        {
            ResetAnchorDebugOverlay();
        }

        AppendAnchorDebug(enabled
            ? "Debug front marker mode: ON"
            : "Debug front marker mode: OFF");
        RefreshDetectionForCurrentDisplayMode();
    }

    void CachePortraitDetectionProfile()
    {
        _portraitDetectionRadius = detectionRadius;
        _portraitDetectionAngle = detectionAngle;
        _portraitMaxPreviewAnchors = maxPreviewAnchors;
        _portraitCenterViewportThreshold = centerViewportThreshold;
        _portraitForwardGroupViewportThreshold = forwardGroupViewportThreshold;
    }

    void HandleLandscapeModeToggleRequested(bool enabled)
    {
        ApplyLandscapeDetectionProfile(enabled);
        RefreshDetectionForCurrentDisplayMode();
    }

    void ApplyLandscapeDetectionProfile(bool enabled)
    {
        if (enabled)
        {
            detectionRadius = landscapeDetectionRadius;
            detectionAngle = landscapeDetectionAngle;
            maxPreviewAnchors = landscapeMaxPreviewAnchors;
            centerViewportThreshold = landscapeCenterViewportThreshold;
            forwardGroupViewportThreshold = landscapeForwardGroupViewportThreshold;
            return;
        }

        detectionRadius = _portraitDetectionRadius;
        detectionAngle = _portraitDetectionAngle;
        maxPreviewAnchors = _portraitMaxPreviewAnchors;
        centerViewportThreshold = _portraitCenterViewportThreshold;
        forwardGroupViewportThreshold = _portraitForwardGroupViewportThreshold;
    }

    void RefreshDetectionForCurrentDisplayMode()
    {
        _lastDetectionDebugSignature = string.Empty;
        _lastAnchorPoolSignature = string.Empty;

        if (!ShouldRenderWorldMarkers())
        {
            return;
        }

        if (_isReloadingData || _isAnchorSetupInProgress || _isViewingInfo || isNavigationActive)
        {
            return;
        }

        if (EarthManager == null || EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            return;
        }

        CheckBuildingDetection();
    }

    void ApplyMarkerModeConfiguration()
    {
        switch (markerRenderMode)
        {
            case MarkerRenderMode.World3D:
                showNearbyAnchors = true;
                break;
            case MarkerRenderMode.Screen2D:
                showNearbyAnchors = false;
                break;
            case MarkerRenderMode.Both:
                showNearbyAnchors = true;
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

        if (!TryGetPointerDownPosition(out Vector2 screenPosition))
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

    bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        screenPosition = default;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
            {
                return false;
            }

            screenPosition = touch.position;
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
                bool isMarkerVisibleOnScreen = IsSelectedMarkerVisibleOnScreen(marker);
                arUIManager?.SetWorldInfoDetailButtonState(targetBuilding, isMarkerVisibleOnScreen);
                return;
            }
        }

        arUIManager?.SetWorldInfoDetailButtonState(null, false);
    }

    bool IsSelectedMarkerVisibleOnScreen(BuildingMarker marker)
    {
        if (marker == null || !marker.IsSelectedTextVisible())
        {
            return false;
        }

        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            return false;
        }

        Vector3 viewportPoint = currentCamera.WorldToViewportPoint(marker.GetTextAnchorWorldPosition());
        if (viewportPoint.z <= 0f)
        {
            return false;
        }

        const float horizontalMargin = 0.05f;
        const float verticalMargin = 0.08f;
        return viewportPoint.x >= horizontalMargin &&
               viewportPoint.x <= 1f - horizontalMargin &&
               viewportPoint.y >= verticalMargin &&
               viewportPoint.y <= 1f - verticalMargin;
    }

    void UpdatePreviewMarkers(List<VisibleBuildingCandidate> previewCandidates, BuildingData selectedBuilding)
    {
        List<string> orderedPreviewKeys = (previewCandidates ?? new List<VisibleBuildingCandidate>())
            .Where(candidate => candidate?.building != null)
            .Select(candidate => GetBuildingAnchorKey(candidate.building))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct()
            .ToList();
        HashSet<string> previewKeys = new HashSet<string>(
            orderedPreviewKeys);
        string selectedKey = GetBuildingAnchorKey(selectedBuilding);
        bool shouldSpreadDebugMarkers = debugForceShowAllWorldMarkers &&
                                        debugPlaceWorldMarkersInFrontOfCamera;

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

            bool isPreviewTarget = previewKeys.Contains(buildingKey);
            if (shouldSpreadDebugMarkers && isPreviewTarget)
            {
                ResolvePreviewMarkerPlacement(buildingKey, orderedPreviewKeys, selectedKey, out int placementIndex, out int placementCount);
                ApplyMarkerPlacement(marker, placementIndex, placementCount);
            }
            else
            {
                ApplyMarkerPlacement(marker, 0, 1);
            }

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

    void ResolvePreviewMarkerPlacement(
        string buildingKey,
        List<string> orderedPreviewKeys,
        string selectedKey,
        out int placementIndex,
        out int placementCount)
    {
        placementCount = Mathf.Max(1, orderedPreviewKeys?.Count ?? 0);
        placementIndex = 0;

        if (placementCount <= 1 || string.IsNullOrWhiteSpace(buildingKey) || orderedPreviewKeys == null)
        {
            return;
        }

        int centerSlot = placementCount / 2;
        if (!string.IsNullOrWhiteSpace(selectedKey) && buildingKey == selectedKey)
        {
            placementIndex = centerSlot;
            return;
        }

        int compactIndex = 0;
        foreach (string key in orderedPreviewKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || key == selectedKey)
            {
                continue;
            }

            if (key == buildingKey)
            {
                placementIndex = compactIndex >= centerSlot ? compactIndex + 1 : compactIndex;
                placementIndex = Mathf.Clamp(placementIndex, 0, placementCount - 1);
                return;
            }

            compactIndex++;
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
                                            debugPlaceWorldMarkersInFrontOfCamera;

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

        marker.transform.position = targetPosition;
        marker.transform.localRotation = Quaternion.identity;
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
