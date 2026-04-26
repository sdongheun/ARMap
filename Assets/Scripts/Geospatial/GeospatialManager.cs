using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Linq;
using System;
using System.IO;

//공통 타입/설정값 보관
//Geospatial 전체 상태 필드 보관
//길찾기 중 목적지 전용 마커 처리
public partial class GeospatialManager : MonoBehaviour
{
    public enum MarkerRenderMode
    {
        Screen2D, // x
        World3D,
        Both // x
    }

    private enum ClusterGroupingType
    {
        Default,
        Institutional, //x 
        Commercial // x
    }

    [Serializable]
    private class LocalApiKeys
    {
        public string kakaoRestApiKey;
        public string tmapApiKey;
    }

    // private static readonly string[] InstitutionalKeywords =
    // {
    //     "학교", "대학교", "병원", "센터", "관", "동", "청사", "연구소", "연구원", "교육원", "대학원", "도서관", "학생회관", "행정실", "학과"
    // };

    // private static readonly string[] CommercialKeywords =
    // {
    //     "마트", "편의점", "카페", "음식점", "약국", "상가"
    // };

    // private static readonly string[] InstitutionalBuildingSuffixes =
    // {
    //     "본관", "별관", "기념도서관", "도서관", "학생회관", "기념관", "회관", "학관", "연구동", "행정동", "박물관", "미술관", "체육관", "강당", "센터", "연구소", "연구원", "관", "동"
    // };

    // private static readonly string[] InstitutionalPrefixTokens =
    // {
    //     "대학교", "대학", "병원", "캠퍼스", "학교"
    // };

    // private static readonly string[] InstitutionalFacilityKeywords =
    // {
    //     "행정실", "연구실", "사무실", "교학과", "학과", "학부", "대학원", "교육원", "지원센터", "상담센터",
    //     "랩", "lab", "카페", "편의점", "매점", "식당", "음식점", "서점", "은행", "atm", "우체국", "복사", "인쇄"
    // };

    // private static readonly string[] NonBuildingRepresentativeKeywords =
    // {
    //     "삼거리", "사거리", "오거리", "교차로", "정문", "후문", "출구", "입구", "횡단보도", "정류장",
    //     "버스정류장", "지하철역", "주차장", "도로", "로터리", "광장", "거리"
    // };

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
    public double markerAltitudeOffsetMeters = 0.0; // 현재 휴대폰(카메라) 고도를 그대로 사용
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
    private ARGeospatialAnchor _navigationTargetAnchor;
    private BuildingMarker _navigationTargetMarker;
    private BuildingData _navigationTargetBuilding;

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
    public float landscapeDetectionRadius = 180.0f; // 가로모드에서 더 넓게 허용할 감지 반경
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

    public bool ShowNavigationTargetMarker(double latitude, double longitude, string buildingName)
    {
        ResetAllMarkers();
        ClearNavigationTargetMarker();

        if (!enableWorldTextMarker || buildingMarkerPrefab == null || AnchorManager == null || EarthManager == null)
        {
            return false;
        }

        if (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            return false;
        }

        _navigationTargetBuilding = new BuildingData
        {
            buildingName = string.IsNullOrWhiteSpace(buildingName) ? "목적지" : buildingName,
            description = "목적지",
            latitude = latitude,
            longitude = longitude,
            altitude = EarthManager.CameraGeospatialPose.Altitude + markerAltitudeOffsetMeters
        };

        _navigationTargetAnchor = AnchorManager.AddAnchor(
            _navigationTargetBuilding.latitude,
            _navigationTargetBuilding.longitude,
            _navigationTargetBuilding.altitude,
            Quaternion.identity);

        if (_navigationTargetAnchor == null)
        {
            _navigationTargetBuilding = null;
            return false;
        }

        GameObject markerObject = Instantiate(buildingMarkerPrefab, _navigationTargetAnchor.transform);
        markerObject.transform.localPosition = Vector3.up * worldMarkerLocalOffsetMeters;
        markerObject.transform.localRotation = Quaternion.identity;

        _navigationTargetMarker = markerObject.GetComponent<BuildingMarker>();
        if (_navigationTargetMarker == null)
        {
            Destroy(_navigationTargetAnchor.gameObject);
            _navigationTargetAnchor = null;
            _navigationTargetBuilding = null;
            return false;
        }

        _navigationTargetMarker.BindBuilding(_navigationTargetBuilding);
        _navigationTargetMarker.SetInfoVisible(true);
        UpdateNavigationTargetMarker();
        _navigationTargetMarker.SetState(BuildingMarker.MarkerVisualState.Selected, true);
        _currentActiveMarker = _navigationTargetMarker;
        return true;
    }

    public void ClearNavigationTargetMarker()
    {
        if (_navigationTargetAnchor != null)
        {
            Destroy(_navigationTargetAnchor.gameObject);
            _navigationTargetAnchor = null;
        }

        _navigationTargetMarker = null;
        _navigationTargetBuilding = null;
        _currentActiveMarker = null;
        arUIManager?.SetWorldInfoDetailButtonState(null, false);
    }

    void UpdateNavigationTargetMarker()
    {
        if (_navigationTargetMarker == null || _navigationTargetBuilding == null)
        {
            return;
        }

        ApplyMarkerPlacement(_navigationTargetMarker, 0, 1);
        _navigationTargetMarker.SetInfoContent(
            _navigationTargetBuilding.buildingName,
            "목적지",
            GetMarkerDistanceLabel(_navigationTargetBuilding));
        _navigationTargetMarker.SetInfoVisible(true);
        _navigationTargetMarker.SetState(BuildingMarker.MarkerVisualState.Selected);
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
