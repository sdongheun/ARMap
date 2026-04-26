using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARSubsystems;


//어떤 건물을 지금 보고 있는지 판단하고, 그 건물들에 대한 앵커를 유지하고, preview 핀과 selected 텍스트 카드를 실제 월드에 배치하는 파일
public partial class GeospatialManager
{
    void CheckBuildingDetection()
    {
        GeospatialPose pose = EarthManager != null ? EarthManager.CameraGeospatialPose : default;
        bool isEarthTracking = EarthManager != null && EarthManager.EarthTrackingState == TrackingState.Tracking;
        AnchorVisibilitySelection selection = AnchorVisibilityPlanner.BuildSelection(
            _anchorCandidateBuildings,
            pose,
            isEarthTracking,
            35.0f,
            detectionAngle,
            _cameraTransform != null ? _cameraTransform.forward : Vector3.forward,
            verticalAngleLimit,
            detectionRadius,
            forwardGroupViewportThreshold,
            groupedCandidateDistanceBias,
            centerViewportThreshold,
            maxPreviewAnchors);

        List<VisibleBuildingCandidate> visibleCandidates = selection.visibleCandidates;
        List<VisibleBuildingCandidate> previewCandidates = selection.previewCandidates;
        BuildingData bestTarget = selection.focusedBuilding;
        UpdateDetectionDebugState(selection);
        RequestAnchorPoolForCandidates(previewCandidates);
        UpdatePreviewMarkers(previewCandidates, bestTarget);

        if (bestTarget != null)
        {
            if (!IsSameBuilding(_selectedBuilding, bestTarget))
            {
                _selectedBuilding = bestTarget;
            }

            if (_currentDetectedBuilding != bestTarget)
            {
                _currentDetectedBuilding = bestTarget;
            }

            UpdateMarkerScale(bestTarget);
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
        if (!ShouldRenderWorldMarkers() || !enableWorldTextMarker)
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
                bool isSameActiveMarker = _currentActiveMarker != null &&
                                          IsSameBuilding(_currentActiveMarker.GetBoundBuilding(), targetBuilding);
                if (!isSameActiveMarker && _currentActiveMarker != null)
                {
                    _currentActiveMarker.SetState(BuildingMarker.MarkerVisualState.Preview);
                    _currentActiveMarker = null;
                }

                ApplyMarkerPlacement(marker, 0, 1);
                UpdateMarkerInfoContent(marker, targetBuilding);
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

    void UpdateMarkerInfoContent(BuildingMarker marker, BuildingData building)
    {
        if (marker == null || building == null)
        {
            return;
        }

        marker.SetInfoContent(
            building.buildingName,
            GetMarkerCategoryLabel(building),
            GetMarkerDistanceLabel(building));
    }

    string GetMarkerCategoryLabel(BuildingData building)
    {
        string category = building != null ? building.description : string.Empty;
        if (string.IsNullOrWhiteSpace(category))
        {
            return "장소";
        }

        int lastSeparatorIndex = category.LastIndexOf('>');
        if (lastSeparatorIndex >= 0 && lastSeparatorIndex < category.Length - 1)
        {
            string trailingCategory = category.Substring(lastSeparatorIndex + 1).Trim();
            if (!string.IsNullOrWhiteSpace(trailingCategory))
            {
                return trailingCategory;
            }
        }

        return category.Trim();
    }

    string GetMarkerDistanceLabel(BuildingData building)
    {
        if (building == null || Input.location.status != LocationServiceStatus.Running)
        {
            return string.Empty;
        }

        LocationInfo currentLoc = Input.location.lastData;
        float distanceMeters = (float)HaversineDistance(
            currentLoc.latitude,
            currentLoc.longitude,
            building.latitude,
            building.longitude);
        return FormatMarkerDistance(distanceMeters);
    }

    string FormatMarkerDistance(float distanceMeters)
    {
        if (distanceMeters < 0f)
        {
            return string.Empty;
        }

        if (distanceMeters >= 1000f)
        {
            return $"{distanceMeters / 1000f:0.0}km";
        }

        return $"{Mathf.RoundToInt(distanceMeters)}m";
    }

    void UpdatePreviewMarkers(List<VisibleBuildingCandidate> previewCandidates, BuildingData selectedBuilding)
    {
        List<string> orderedPreviewKeys = (previewCandidates ?? new List<VisibleBuildingCandidate>())
            .Where(candidate => candidate?.building != null)
            .Select(candidate => GetBuildingAnchorKey(candidate.building))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct()
            .ToList();

        HashSet<string> previewKeys = new HashSet<string>(orderedPreviewKeys);
        string selectedKey = GetBuildingAnchorKey(selectedBuilding);
        bool shouldSpreadDebugMarkers = debugForceShowAllWorldMarkers && debugPlaceWorldMarkersInFrontOfCamera;

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
            UpdateMarkerInfoContent(marker, building);
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

    void RequestAnchorPoolForCandidates(List<VisibleBuildingCandidate> previewCandidates)
    {
        List<BuildingData> desiredBuildings = AnchorPoolPlanner.BuildDesiredBuildings(previewCandidates);
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

        bool shouldUseDebugFrontPlacement = debugForceShowAllWorldMarkers && debugPlaceWorldMarkersInFrontOfCamera;
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
            Vector3 anchorToCamera = _cameraTransform.position - anchorPosition;
            float cameraDistance = anchorToCamera.magnitude;

            if (cameraDistance > 0.01f)
            {
                float pullDistance = Mathf.Clamp(
                    cameraDistance * worldMarkerShellDistanceScale,
                    worldMarkerShellMinRadius,
                    worldMarkerShellMaxRadius);

                float nearLimitDistance = cameraDistance * worldMarkerShellNearLimitFactor;
                if (nearLimitDistance > 0f)
                {
                    pullDistance = Mathf.Min(pullDistance, nearLimitDistance);
                }

                targetPosition = anchorPosition
                                 + (anchorToCamera.normalized * pullDistance)
                                 + (Vector3.up * worldMarkerLocalOffsetMeters);
            }
        }

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

        foreach (var anchor in _buildingAnchors.Values)
        {
            if (anchor != null) Destroy(anchor.gameObject);
        }

        _buildingAnchors.Clear();
        ResetAllMarkers();
        _lastAnchorPoolSignature = string.Empty;

        List<BuildingData> targetBuildings = GetBuildingsForAnchorCreation();
        _anchorCandidateBuildings = targetBuildings;

        AppendAnchorDebug($"Anchor targets ready: {targetBuildings.Count}/{_autoGeneratedBuildingList.Count}");
        yield return StartCoroutine(WaitForTrackingBeforeAnchors());
        ApplyFallbackAltitudes(targetBuildings);
        AppendAnchorDebug("Anchor creation deferred until selection");
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
                UpdateMarkerInfoContent(marker, building);
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
                distance = HaversineDistance(currentLoc.latitude, currentLoc.longitude, building.latitude, building.longitude)
            })
            .OrderBy(item => item.distance)
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
}
