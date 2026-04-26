using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

//GeospatialManager의 실행 흐름 본체
//앱 시작 시 초기화
//위치 서비스 시작
//키 읽기
//가로모드/디버그모드 전환
//매 프레임 업데이트
//위치 이동 시 재검색
//터치 입력 처리
public partial class GeospatialManager
{
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
        _cameraTransform = Camera.main.transform;
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
            centerViewportThreshold = Mathf.Max(landscapeCenterViewportThreshold, _portraitCenterViewportThreshold);
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
        CheckMovementAndReload();
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return;
        if (_isViewingInfo) return;
        if (isNavigationActive)
        {
            UpdateNavigationTargetMarker();
            return;
        }

        if (_isReloadingData) return;
        if (_isAnchorSetupInProgress) return;
        HandleWorldMarkerTap();
        CheckBuildingDetection();
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

    void CheckMovementAndReload()
    {
        if (isNavigationActive)
        {
            return;
        }

        if (_isReloadingData)
        {
            return;
        }

        if (Input.location.status == LocationServiceStatus.Running && _isDataLoaded)
        {
            LocationInfo currentLoc = Input.location.lastData;
            double distance = HaversineDistance(
                _lastLoadedLocation.latitude,
                _lastLoadedLocation.longitude,
                currentLoc.latitude,
                currentLoc.longitude);

            if (distance >= refreshDistance)
            {
                StartCoroutine(ReloadDataAtCurrentLocation());
            }
        }
    }

    IEnumerator ReloadDataAtCurrentLocation()
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
    {
        const double R = 6371e3;
        var phi1 = lat1 * (Math.PI / 180.0);
        var phi2 = lat2 * (Math.PI / 180.0);
        var deltaPhi = (lat2 - lat1) * (Math.PI / 180.0);
        var deltaLambda = (lon2 - lon1) * (Math.PI / 180.0);
        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2)
                + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    void ClearAllContent()
    {
        foreach (var anchor in _buildingAnchors.Values)
        {
            if (anchor != null) Destroy(anchor.gameObject);
        }

        _buildingAnchors.Clear();
        _autoGeneratedBuildingList.Clear();
        _currentDetectedBuilding = null;
        _selectedBuilding = null;
        _currentActiveMarker = null;
        _lastDetectionDebugSignature = string.Empty;
        _lastAnchorPoolSignature = string.Empty;
        arUIManager?.SetTrackingStabilizingMode();
    }
}
