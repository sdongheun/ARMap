using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class NavigationManager : MonoBehaviour
{
    [Header("AR Components")]
    public AREarthManager EarthManager;
    public ARAnchorManager AnchorManager;

    [Header("Navigation Settings")]
    public GameObject arrowPrefab;
    public GameObject destinationMarkerPrefab;
    public int arrowPoolSize = 30;
    public float arrowSpacing = 8.0f;
    public float arrowVisibleRange = 80.0f;
    public float rerouteThreshold = 30.0f;
    public float arrivalThreshold = 15.0f;
    public float groundOffset = -1.5f;

    [Header("Managers")]
    public ARUIManager arUIManager;
    public GeospatialManager geospatialManager;

    // 상태
    public NavigationState CurrentState { get; private set; } = NavigationState.Idle;

    // 내부 변수
    private NavigationRoute _currentRoute;
    private List<NavigationArrow> _arrowPool = new List<NavigationArrow>();
    private List<NavigationArrow> _activeArrows = new List<NavigationArrow>();
    private GameObject _destinationMarkerInstance;
    private int _currentRouteIndex;
    private int _frameCounter;
    private Transform _cameraTransform;
    private const int MAX_ANCHOR_POINTS = 5;

    // 앵커 기반 안정화
    private List<ARGeospatialAnchor> _routeAnchors = new List<ARGeospatialAnchor>();

    // 단일 기준 앵커 (카메라 회전과 무관한 고정 EUN→World 프레임 제공)
    private ARGeospatialAnchor _routeOriginAnchor;
    private double _routeOriginLat;
    private double _routeOriginLon;

    // 헤딩 스무딩 (레거시, 앵커 기반 좌표 변환 이후 사용 안함)
    private Queue<double> _headingHistory = new Queue<double>();
    private const int HEADING_WINDOW = 8;

    void Start()
    {
        _cameraTransform = Camera.main.transform;

        if (geospatialManager == null)
            geospatialManager = FindObjectOfType<GeospatialManager>();
        if (arUIManager == null)
            arUIManager = FindObjectOfType<ARUIManager>();
        if (EarthManager == null && geospatialManager != null)
            EarthManager = geospatialManager.EarthManager;
        if (AnchorManager == null && geospatialManager != null)
            AnchorManager = geospatialManager.AnchorManager;

        if (arUIManager != null)
        {
            arUIManager.OnNavigateRequested += OnNavigateButtonPressed;
            arUIManager.OnNavigateFromDetailRequested += OnNavigateFromDetail;
            arUIManager.OnStopNavigationRequested += StopNavigation;
            Debug.Log("[NavigationManager] 이벤트 구독 완료");
        }
        else
        {
            Debug.LogError("[NavigationManager] ARUIManager를 찾을 수 없습니다.");
        }

        try
        {
            InitializeArrowPool();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NavigationManager] 화살표 풀 초기화 실패 (런타임에 재시도): {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (arUIManager != null)
        {
            arUIManager.OnNavigateRequested -= OnNavigateButtonPressed;
            arUIManager.OnNavigateFromDetailRequested -= OnNavigateFromDetail;
            arUIManager.OnStopNavigationRequested -= StopNavigation;
        }
    }

    void Update()
    {
        if (CurrentState != NavigationState.Navigating) return;

        // 트래킹 손실 시 경고 표시 후 업데이트 중단
        if (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            arUIManager?.SetTrackingWarning(true);
            return;
        }

        // 정확도 기반 경고 (매 60프레임마다 체크)
        if (_frameCounter % 60 == 0)
        {
            var pose = EarthManager.CameraGeospatialPose;
            bool lowAccuracy = pose.HorizontalAccuracy > 10.0 || pose.HeadingAccuracy > 25.0;
            arUIManager?.SetTrackingWarning(lowAccuracy);
        }

        _frameCounter++;

        UpdateOffScreenIndicator();

        if (_frameCounter % 5 == 0)
        {
            UpdateRouteProgress();
            UpdateArrowPositions();
            UpdateNavigationUI();
        }

        if (_frameCounter % 10 == 0)
        {
            RefreshWorldPositions();
        }

        if (_frameCounter % 60 == 0)
        {
            CheckRerouteNeeded();
            CheckArrival();
        }
    }

    #region Arrow Pool
    void InitializeArrowPool()
    {
        if (arrowPrefab == null)
            arrowPrefab = CreateArrowPrefab();

        for (int i = 0; i < arrowPoolSize; i++)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity, transform);
            NavigationArrow arrow = arrowObj.GetComponent<NavigationArrow>();
            if (arrow == null) arrow = arrowObj.AddComponent<NavigationArrow>();
            arrow.DeactivateImmediate();
            _arrowPool.Add(arrow);
        }
    }

    GameObject CreateArrowPrefab()
    {
        GameObject prefab = new GameObject("NavArrowPrefab");
        prefab.SetActive(false);

        // 쉐브론(>>) 형태 메시 생성 (바닥에 평평하게, XZ 평면)
        Mesh mesh = new Mesh();
        // 두 개의 V자 형태 결합
        Vector3[] vertices = new Vector3[]
        {
            // 뒤쪽 쉐브론
            new Vector3(-0.6f, 0f, -0.5f),   // 0: 왼쪽 뒤
            new Vector3( 0.0f, 0f,  0.1f),   // 1: 중앙 앞
            new Vector3( 0.6f, 0f, -0.5f),   // 2: 오른쪽 뒤
            new Vector3(-0.4f, 0f, -0.5f),   // 3: 왼쪽 뒤 (안쪽)
            new Vector3( 0.0f, 0f, -0.1f),   // 4: 중앙 (안쪽)
            new Vector3( 0.4f, 0f, -0.5f),   // 5: 오른쪽 뒤 (안쪽)

            // 앞쪽 쉐브론
            new Vector3(-0.6f, 0f,  0.1f),   // 6
            new Vector3( 0.0f, 0f,  0.7f),   // 7
            new Vector3( 0.6f, 0f,  0.1f),   // 8
            new Vector3(-0.4f, 0f,  0.1f),   // 9
            new Vector3( 0.0f, 0f,  0.5f),   // 10
            new Vector3( 0.4f, 0f,  0.1f),   // 11
        };

        int[] triangles = new int[]
        {
            // 뒤쪽 쉐브론 왼쪽 다리
            0, 1, 4,   0, 4, 3,
            // 뒤쪽 쉐브론 오른쪽 다리
            1, 2, 5,   1, 5, 4,
            // 앞쪽 쉐브론 왼쪽 다리
            6, 7, 10,  6, 10, 9,
            // 앞쪽 쉐브론 오른쪽 다리
            7, 8, 11,  7, 11, 10,
        };

        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.RecalculateBounds();

        MeshFilter filter = prefab.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateTransparentMaterial(new Color(0.08f, 0.85f, 1.0f, 0.85f));
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        prefab.transform.localScale = new Vector3(1.5f, 1f, 2.0f);
        prefab.AddComponent<NavigationArrow>();

        return prefab;
    }

    NavigationArrow GetArrowFromPool()
    {
        foreach (NavigationArrow arrow in _arrowPool)
        {
            if (!arrow.IsActive)
                return arrow;
        }
        return null;
    }

    void ReturnAllArrows()
    {
        foreach (NavigationArrow arrow in _arrowPool)
        {
            arrow.DeactivateImmediate();
        }
        _activeArrows.Clear();
    }
    #endregion

    #region Navigation Entry Points
    private bool _searchButtonBound;

    void OnNavigateButtonPressed()
    {
        SetState(NavigationState.Searching);
        arUIManager.EnterNavigationMode();
        arUIManager.ShowSearchPanel();
        BindSearchButton();
    }

    void BindSearchButton()
    {
        if (_searchButtonBound) return;
        if (arUIManager.searchButton != null)
        {
            arUIManager.searchButton.onClick.AddListener(OnSearchButtonClicked);
            _searchButtonBound = true;
        }
    }

    void OnNavigateFromDetail(BuildingData building)
    {
        if (building == null) return;
        arUIManager.CloseDetailView();
        StartNavigation(building.latitude, building.longitude, building.buildingName);
    }

    void OnSearchButtonClicked()
    {
        if (arUIManager.destinationInputField == null) return;
        string keyword = arUIManager.destinationInputField.text.Trim();
        if (string.IsNullOrEmpty(keyword)) return;
        StartCoroutine(SearchDestination(keyword));
    }
    #endregion

    #region Destination Search
    IEnumerator SearchDestination(string keyword)
    {
        if (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            arUIManager?.ShowToast("위치 추적이 활성화되지 않았습니다.");
            yield break;
        }

        arUIManager?.ShowSearchLoading();

        var pose = EarthManager.CameraGeospatialPose;
        double lat = pose.Latitude;
        double lon = pose.Longitude;

        string encodedKeyword = UnityWebRequest.EscapeURL(keyword);
        string url = $"https://dapi.kakao.com/v2/local/search/keyword.json?query={encodedKeyword}&x={lon}&y={lat}&radius=20000&sort=distance";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "KakaoAK " + geospatialManager.kakaoRestApiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"목적지 검색 실패: {request.error}");
                arUIManager?.ShowEmptyResults("검색에 실패했습니다");
                yield break;
            }

            KakaoResponse response = JsonUtility.FromJson<KakaoResponse>(request.downloadHandler.text);
            if (response.documents == null || response.documents.Length == 0)
            {
                arUIManager?.ShowEmptyResults("검색 결과가 없습니다");
                yield break;
            }

            List<DestinationResult> results = new List<DestinationResult>();
            foreach (KakaoDocument doc in response.documents)
            {
                int distMeters = 0;
                int.TryParse(doc.distance, out distMeters);
                results.Add(new DestinationResult
                {
                    placeName = doc.place_name,
                    addressName = doc.address_name,
                    roadAddressName = doc.road_address_name,
                    latitude = double.Parse(doc.y),
                    longitude = double.Parse(doc.x),
                    categoryName = doc.category_group_name,
                    distance = distMeters
                });
            }

            arUIManager?.UpdateSearchResults(results, OnDestinationSelected);
        }
    }

    void OnDestinationSelected(DestinationResult destination)
    {
        arUIManager?.HideSearchPanel();
        StartNavigation(destination.latitude, destination.longitude, destination.placeName);
    }
    #endregion

    #region Route Fetching
    public void StartNavigation(double destLat, double destLon, string destName)
    {
        if (_arrowPool.Count == 0)
        {
            try { InitializeArrowPool(); }
            catch (Exception e) { Debug.LogWarning($"[NavigationManager] 화살표 풀 재초기화 실패: {e.Message}"); }
        }

        SetState(NavigationState.Routing);
        arUIManager?.EnterNavigationMode();
        arUIManager?.ShowToast("경로를 계산하고 있습니다...");
        StartCoroutine(FetchRouteAndStart(destLat, destLon, destName));
    }

    IEnumerator FetchRouteAndStart(double destLat, double destLon, string destName)
    {
        if (EarthManager.EarthTrackingState != TrackingState.Tracking)
        {
            arUIManager?.ShowToast("위치 추적이 활성화되지 않았습니다.");
            SetState(NavigationState.Error);
            yield return new WaitForSeconds(2f);
            StopNavigation();
            yield break;
        }

        var pose = EarthManager.CameraGeospatialPose;
        double originLat = pose.Latitude;
        double originLon = pose.Longitude;

        // 정확도 경고
        if (pose.HeadingAccuracy > 25.0)
        {
            arUIManager?.ShowToast("방향 보정을 위해 기기를 8자로 흔들어 주세요.");
        }
        else if (pose.HorizontalAccuracy > 15.0)
        {
            arUIManager?.ShowToast("GPS 신호가 약합니다. 실외로 이동해 주세요.");
        }

        NavigationRoute route = null;
        yield return StartCoroutine(FetchRoute(originLat, originLon, destLat, destLon, destName, r => route = r));

        if (route == null || route.points.Count < 2)
        {
            arUIManager?.ShowToast("경로를 찾을 수 없습니다.");
            SetState(NavigationState.Error);
            yield return new WaitForSeconds(2f);
            StopNavigation();
            yield break;
        }

        _currentRoute = route;
        _currentRouteIndex = 0;
        _frameCounter = 0;

        // 기준 앵커 생성 (경로 첫 점에 카메라 고도로 배치)
        _routeOriginLat = route.points[0].latitude;
        _routeOriginLon = route.points[0].longitude;
        double originAlt = pose.Altitude;
        if (_routeOriginAnchor != null)
        {
            Destroy(_routeOriginAnchor.gameObject);
            _routeOriginAnchor = null;
        }
        _routeOriginAnchor = AnchorManager.AddAnchor(_routeOriginLat, _routeOriginLon, originAlt, Quaternion.identity);
        if (_routeOriginAnchor == null)
        {
            Debug.LogError("[NavigationManager] 기준 앵커 생성 실패 — 경로 중단");
            arUIManager?.ShowToast("앵커 생성 실패. 잠시 후 다시 시도해 주세요.");
            SetState(NavigationState.Error);
            yield return new WaitForSeconds(2f);
            StopNavigation();
            yield break;
        }
        Debug.Log($"[Nav] 앵커 생성 ({_routeOriginLat:F6}, {_routeOriginLon:F6}, {originAlt:F1}m), " +
                  $"Unity pos={_routeOriginAnchor.transform.position}, rot={_routeOriginAnchor.transform.rotation.eulerAngles}");

        RefreshWorldPositions();
        PlaceArrows();
        PlaceDestinationMarker(destLat, destLon, destName);

        SetState(NavigationState.Navigating);
        geospatialManager.isNavigationActive = true;

        arUIManager?.ShowNavigationHUD();
        arUIManager?.UpdateRemainingDistance(route.totalDistance);
        arUIManager?.UpdateETA(route.totalDuration);
        arUIManager?.UpdateProgress(0f);

        if (route.guides.Count > 0)
            arUIManager?.UpdateNextGuide(route.guides[0].guidance, route.guides[0].distance, route.guides[0].type);
    }

    IEnumerator FetchRoute(double originLat, double originLon, double destLat, double destLon, string destName, Action<NavigationRoute> callback)
    {
        string url = $"https://apis-navi.kakaomobility.com/v1/directions?origin={originLon},{originLat}&destination={destLon},{destLat}&priority=RECOMMEND";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "KakaoAK " + geospatialManager.kakaoRestApiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"경로 요청 실패: {request.error}");
                callback?.Invoke(null);
                yield break;
            }

            KakaoDirectionsResponse dirResponse = JsonUtility.FromJson<KakaoDirectionsResponse>(request.downloadHandler.text);
            if (dirResponse.routes == null || dirResponse.routes.Length == 0 || dirResponse.routes[0].result_code != 0)
            {
                Debug.LogError("카카오 경로 응답에 유효한 경로 없음");
                callback?.Invoke(null);
                yield break;
            }

            NavigationRoute route = ParseRoute(dirResponse.routes[0], destName, destLat, destLon);
            callback?.Invoke(route);
        }
    }

    NavigationRoute ParseRoute(KakaoRoute kakaoRoute, string destName, double destLat, double destLon)
    {
        NavigationRoute route = new NavigationRoute();
        route.totalDistance = kakaoRoute.summary.distance;
        route.totalDuration = kakaoRoute.summary.duration;
        route.destination = new DestinationResult
        {
            placeName = destName,
            latitude = destLat,
            longitude = destLon
        };

        HashSet<string> guideCoords = new HashSet<string>();
        foreach (KakaoRouteSection section in kakaoRoute.sections)
        {
            if (section.guides != null)
            {
                foreach (KakaoGuide guide in section.guides)
                {
                    route.guides.Add(new RouteGuide
                    {
                        type = guide.type,
                        guidance = guide.guidance,
                        latitude = guide.y,
                        longitude = guide.x,
                        distance = guide.distance
                    });
                    guideCoords.Add($"{guide.x:F6},{guide.y:F6}");
                }
            }

            if (section.roads != null)
            {
                foreach (KakaoRoad road in section.roads)
                {
                    if (road.vertexes == null) continue;
                    for (int i = 0; i < road.vertexes.Length - 1; i += 2)
                    {
                        double lon = road.vertexes[i];
                        double lat = road.vertexes[i + 1];
                        string coordKey = $"{lon:F6},{lat:F6}";

                        route.points.Add(new RoutePoint
                        {
                            latitude = lat,
                            longitude = lon,
                            isAnchorPoint = guideCoords.Contains(coordKey)
                        });
                    }
                }
            }
        }

        DensifyRoutePoints(route, 4.0); // 4m 간격으로 보간
        MapGuidesToRoutePoints(route);
        ComputeEunOffsets(route);
        return route;
    }

    /// <summary>
    /// 경로점들을 기준점(첫 점)으로부터의 EUN(East, 0, North) 오프셋(미터)으로 변환하여 캐시한다.
    /// 이 값은 카메라/지도 회전과 무관하게 고정되며, 매 프레임 앵커 transform과 결합하여 world 좌표를 만든다.
    /// </summary>
    void ComputeEunOffsets(NavigationRoute route)
    {
        if (route == null || route.points.Count == 0) return;
        double originLat = route.points[0].latitude;
        double originLon = route.points[0].longitude;
        double cosLat = Math.Cos(originLat * Math.PI / 180.0);
        foreach (RoutePoint pt in route.points)
        {
            double dLat = pt.latitude - originLat;
            double dLon = pt.longitude - originLon;
            float east = (float)(dLon * 111320.0 * cosLat);
            float north = (float)(dLat * 111320.0);
            pt.eunOffset = new Vector3(east, 0f, north);
        }
    }

    /// <summary>
    /// 경로 점들 사이를 지정된 간격 이하로 보간하여 화살표가 부드러운 곡선을 따르도록 한다.
    /// </summary>
    void DensifyRoutePoints(NavigationRoute route, double maxSpacingMeters)
    {
        if (route == null || route.points.Count < 2) return;

        List<RoutePoint> dense = new List<RoutePoint>(route.points.Count * 2);
        for (int i = 0; i < route.points.Count - 1; i++)
        {
            RoutePoint a = route.points[i];
            RoutePoint b = route.points[i + 1];
            dense.Add(a);

            double segDist = GeospatialManager.HaversineDistance(a.latitude, a.longitude, b.latitude, b.longitude);
            if (segDist > maxSpacingMeters)
            {
                int divisions = (int)Math.Ceiling(segDist / maxSpacingMeters);
                for (int j = 1; j < divisions; j++)
                {
                    double t = (double)j / divisions;
                    dense.Add(new RoutePoint
                    {
                        latitude = a.latitude + (b.latitude - a.latitude) * t,
                        longitude = a.longitude + (b.longitude - a.longitude) * t,
                        isAnchorPoint = false
                    });
                }
            }
        }
        dense.Add(route.points[route.points.Count - 1]);
        route.points = dense;
    }

    /// <summary>
    /// 가이드 좌표에 가장 가까운 경로점 인덱스를 매핑한다. (역행 방지용)
    /// </summary>
    void MapGuidesToRoutePoints(NavigationRoute route)
    {
        if (route == null || route.guides == null || route.points == null) return;
        foreach (RouteGuide guide in route.guides)
        {
            double minDist = double.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < route.points.Count; i++)
            {
                RoutePoint p = route.points[i];
                double d = GeospatialManager.HaversineDistance(p.latitude, p.longitude, guide.latitude, guide.longitude);
                if (d < minDist)
                {
                    minDist = d;
                    bestIdx = i;
                }
            }
            guide.routePointIndex = bestIdx;
        }
    }
    #endregion

    #region World Position Calculation
    void RefreshWorldPositions()
    {
        if (_currentRoute == null || _routeOriginAnchor == null) return;

        // 기준 앵커의 transform을 EUN→Unity World 변환 프레임으로 사용
        // 앵커는 ARCore가 drift 보정해주며, 카메라 회전과 무관하게 고정됨
        Vector3 originPos = _routeOriginAnchor.transform.position;
        Quaternion eunToWorld = _routeOriginAnchor.transform.rotation;
        float groundY = originPos.y + groundOffset;

        foreach (RoutePoint point in _currentRoute.points)
        {
            Vector3 worldOffset = eunToWorld * point.eunOffset;
            point.worldPosition = new Vector3(
                originPos.x + worldOffset.x,
                groundY,
                originPos.z + worldOffset.z
            );
        }
    }

    /// <summary>
    /// 헤딩 값을 히스토리에 추가하고, 원형 평균(circular mean)으로 스무딩된 헤딩을 반환한다.
    /// 도시 협곡 등에서 헤딩이 크게 튀는 것을 완화한다.
    /// </summary>
    double PushAndGetSmoothedHeading(double headingDeg)
    {
        _headingHistory.Enqueue(headingDeg);
        while (_headingHistory.Count > HEADING_WINDOW) _headingHistory.Dequeue();
        return GetCurrentSmoothedHeading();
    }

    double GetCurrentSmoothedHeading()
    {
        if (_headingHistory.Count == 0) return 0.0;
        double sumSin = 0.0, sumCos = 0.0;
        foreach (double h in _headingHistory)
        {
            double rad = h * Math.PI / 180.0;
            sumSin += Math.Sin(rad);
            sumCos += Math.Cos(rad);
        }
        double meanRad = Math.Atan2(sumSin, sumCos);
        return meanRad * 180.0 / Math.PI;
    }

    Vector3 GeoToWorldPosition(double lat, double lon)
    {
        if (_routeOriginAnchor == null)
            return _cameraTransform.position;

        // 기준 앵커 기준 EUN 오프셋 계산
        double cosLat = Math.Cos(_routeOriginLat * Math.PI / 180.0);
        double dLat = lat - _routeOriginLat;
        double dLon = lon - _routeOriginLon;
        float east = (float)(dLon * 111320.0 * cosLat);
        float north = (float)(dLat * 111320.0);

        Vector3 worldOffset = _routeOriginAnchor.transform.rotation * new Vector3(east, 0f, north);
        Vector3 originPos = _routeOriginAnchor.transform.position;
        return new Vector3(
            originPos.x + worldOffset.x,
            originPos.y + groundOffset,
            originPos.z + worldOffset.z
        );
    }
    #endregion

    #region Arrow Placement & Update
    void PlaceArrows()
    {
        ReturnAllArrows();
        if (_currentRoute == null || _currentRoute.points.Count < 2) return;

        float accumulatedDist = 0f;
        Vector3 camPos = _cameraTransform.position;

        for (int i = 1; i < _currentRoute.points.Count; i++)
        {
            Vector3 prevPos = _currentRoute.points[i - 1].worldPosition;
            Vector3 currPos = _currentRoute.points[i].worldPosition;
            float segmentDist = Vector3.Distance(prevPos, currPos);

            if (segmentDist < 0.01f) continue;

            float segmentAccum = 0f;
            while (segmentAccum < segmentDist)
            {
                float totalDist = accumulatedDist + segmentAccum;
                float t = segmentAccum / segmentDist;
                Vector3 pos = Vector3.Lerp(prevPos, currPos, t);
                float distFromUser = Vector3.Distance(camPos, pos);

                if (distFromUser <= arrowVisibleRange)
                {
                    NavigationArrow arrow = GetArrowFromPool();
                    if (arrow == null) break;

                    Vector3 dir = (currPos - prevPos).normalized;
                    Quaternion rot = dir.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(dir, Vector3.up)
                        : Quaternion.identity;

                    arrow.SetTarget(pos, rot);
                    arrow.SetEmphasis(_activeArrows.Count < 3 ? 1f : 0.45f);
                    arrow.Activate();
                    _activeArrows.Add(arrow);
                }

                segmentAccum += arrowSpacing;
            }

            accumulatedDist += segmentDist;
        }
    }

    // 재사용 가능한 슬롯 버퍼 (GC 방지)
    private readonly List<ArrowSlot> _slotBuffer = new List<ArrowSlot>(64);

    private struct ArrowSlot
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool isCorner;
        public int routePointIndex;
    }

    void UpdateArrowPositions()
    {
        if (_currentRoute == null || _currentRoute.points.Count < 2) return;

        Vector3 camPos = _cameraTransform.position;
        Vector3 camForward = _cameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        // 다음에 사용자가 수행할 턴 지점의 경로점 인덱스
        RouteGuide nextGuide = FindNextGuide();
        int nextTurnPointIdx = nextGuide != null ? nextGuide.routePointIndex : -1;

        // 1) 필요한 화살표 슬롯 계산
        _slotBuffer.Clear();
        float accumulatedDist = 0f;

        for (int i = Mathf.Max(1, _currentRouteIndex); i < _currentRoute.points.Count; i++)
        {
            RoutePoint prevPoint = _currentRoute.points[i - 1];
            Vector3 prevPos = prevPoint.worldPosition;
            Vector3 currPos = _currentRoute.points[i].worldPosition;
            float segmentDist = Vector3.Distance(prevPos, currPos);

            if (segmentDist < 0.01f) continue;

            Vector3 dir = (currPos - prevPos).normalized;
            Quaternion rot = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir, Vector3.up)
                : Quaternion.identity;

            // 이 세그먼트의 시작점(prevPoint)이 코너(앵커)인 경우만 세그먼트 시작 화살표를 코너로 표시
            bool segmentStartIsCorner = prevPoint.isAnchorPoint;

            float segmentAccum = 0f;
            while (segmentAccum < segmentDist)
            {
                float t = segmentAccum / segmentDist;
                Vector3 pos = Vector3.Lerp(prevPos, currPos, t);

                Vector3 toArrow = pos - camPos;
                toArrow.y = 0f;
                float distFromUser = toArrow.magnitude;

                // 카메라 앞쪽(전방 180°) + 가시 범위 이내만 포함
                bool isBehind = distFromUser > 5f && Vector3.Dot(camForward, toArrow.normalized) < 0f;
                if (!isBehind && distFromUser <= arrowVisibleRange)
                {
                    // 세그먼트의 첫 번째 슬롯(t=0)이고 시작점이 앵커일 때만 코너로 간주
                    bool isCorner = (segmentAccum < 0.001f) && segmentStartIsCorner;
                    _slotBuffer.Add(new ArrowSlot
                    {
                        position = pos,
                        rotation = rot,
                        isCorner = isCorner,
                        routePointIndex = i - 1
                    });
                    if (_slotBuffer.Count >= _arrowPool.Count) break;
                }

                segmentAccum += arrowSpacing;
            }

            if (_slotBuffer.Count >= _arrowPool.Count) break;

            accumulatedDist += segmentDist;
            if (accumulatedDist > arrowVisibleRange * 1.2f) break;
        }

        // 2) 다음 턴 슬롯 식별: nextTurnPointIdx와 routePointIndex가 일치/가장 가까운 슬롯
        int slotCount = _slotBuffer.Count;
        int nextTurnSlotIdx = -1;
        if (nextTurnPointIdx >= 0 && slotCount > 0)
        {
            int bestDiff = int.MaxValue;
            for (int i = 0; i < slotCount; i++)
            {
                int diff = Mathf.Abs(_slotBuffer[i].routePointIndex - nextTurnPointIdx);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    nextTurnSlotIdx = i;
                }
            }
            // 너무 멀리 떨어진 매칭은 무효화 (3 슬롯 = 화살표 3개 거리 내만 강조)
            if (bestDiff > 3) nextTurnSlotIdx = -1;
        }

        // 3) 활성 화살표 재활용: 기존 화살표는 새 슬롯으로 이동, 부족하면 풀에서 추가, 남으면 페이드아웃
        int activeCount = _activeArrows.Count;

        // 기존 화살표 위치 업데이트 (먼 거리 재할당 시 즉시 스냅 — 헤엄 방지)
        for (int i = 0; i < Mathf.Min(slotCount, activeCount); i++)
        {
            Vector3 curPos = _activeArrows[i].transform.position;
            float reassignDist = Vector3.Distance(curPos, _slotBuffer[i].position);
            if (reassignDist > 3f)
                _activeArrows[i].SetTargetImmediate(_slotBuffer[i].position, _slotBuffer[i].rotation);
            else
                _activeArrows[i].SetTarget(_slotBuffer[i].position, _slotBuffer[i].rotation);
            // 다음 3개는 강조, 나머지는 흐리게
            _activeArrows[i].SetEmphasis(i < 3 ? 1f : 0.45f);
            // 코너/다음 턴 모드 적용 (nextTurn > corner > normal 우선순위)
            bool isNextTurn = (i == nextTurnSlotIdx);
            _activeArrows[i].SetNextTurnMode(isNextTurn);
            _activeArrows[i].SetCornerMode(!isNextTurn && _slotBuffer[i].isCorner);
        }

        // 부족한 화살표 추가
        for (int i = activeCount; i < slotCount; i++)
        {
            NavigationArrow arrow = GetArrowFromPool();
            if (arrow == null) break;
            arrow.SetTarget(_slotBuffer[i].position, _slotBuffer[i].rotation);
            arrow.SetEmphasis(i < 3 ? 1f : 0.45f);
            bool isNextTurn = (i == nextTurnSlotIdx);
            arrow.SetNextTurnMode(isNextTurn);
            arrow.SetCornerMode(!isNextTurn && _slotBuffer[i].isCorner);
            arrow.Activate();
            _activeArrows.Add(arrow);
        }

        // 남는 화살표 페이드아웃
        for (int i = _activeArrows.Count - 1; i >= slotCount; i--)
        {
            _activeArrows[i].Deactivate();
            _activeArrows.RemoveAt(i);
        }
    }
    #endregion

    #region Destination Marker
    void PlaceDestinationMarker(double lat, double lon, string name)
    {
        ClearDestinationMarker();

        Vector3 worldPos = GeoToWorldPosition(lat, lon);
        worldPos.y += 3.0f;

        if (destinationMarkerPrefab != null)
        {
            _destinationMarkerInstance = Instantiate(destinationMarkerPrefab, worldPos, Quaternion.identity);
        }
        else
        {
            _destinationMarkerInstance = CreateDestinationMarkerObject(worldPos);
        }

        _destinationMarkerInstance.name = "DestinationMarker";

        BuildingMarker marker = _destinationMarkerInstance.GetComponent<BuildingMarker>();
        if (marker != null)
        {
            marker.SetState(BuildingMarker.MarkerVisualState.Selected);
            marker.SetInfoContent(name, "목적지");
            marker.SetInfoVisible(true);
        }
    }

    GameObject CreateDestinationMarkerObject(Vector3 position)
    {
        GameObject markerObj = new GameObject("DestMarker");
        markerObj.transform.position = position;

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pillar.transform.SetParent(markerObj.transform, false);
        pillar.transform.localScale = new Vector3(0.3f, 4.0f, 0.3f);
        pillar.transform.localPosition = new Vector3(0f, -2f, 0f);

        Renderer pillarRend = pillar.GetComponent<Renderer>();
        pillarRend.sharedMaterial = CreateTransparentMaterial(new Color(1.0f, 0.6f, 0.1f, 0.7f));
        pillarRend.shadowCastingMode = ShadowCastingMode.Off;

        Collider col = pillar.GetComponent<Collider>();
        if (col != null) Destroy(col);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(markerObj.transform, false);
        sphere.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        sphere.transform.localPosition = Vector3.zero;

        Renderer sphereRend = sphere.GetComponent<Renderer>();
        sphereRend.sharedMaterial = CreateTransparentMaterial(new Color(1.0f, 0.45f, 0.05f, 0.9f));
        sphereRend.shadowCastingMode = ShadowCastingMode.Off;

        Collider sphereCol = sphere.GetComponent<Collider>();
        if (sphereCol != null) Destroy(sphereCol);

        BuildingMarker bm = markerObj.AddComponent<BuildingMarker>();
        bm.activeScale = 2.5f;
        bm.activeColor = new Color(1.0f, 0.45f, 0.05f, 1f);

        return markerObj;
    }

    Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.color = color;

        if (shader.name.Contains("Universal Render Pipeline"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        return mat;
    }

    void ClearDestinationMarker()
    {
        if (_destinationMarkerInstance != null)
        {
            Destroy(_destinationMarkerInstance);
            _destinationMarkerInstance = null;
        }
    }

    void UpdateDestinationMarkerPosition()
    {
        if (_destinationMarkerInstance == null || _currentRoute?.destination == null) return;

        Vector3 worldPos = GeoToWorldPosition(
            _currentRoute.destination.latitude,
            _currentRoute.destination.longitude
        );
        worldPos.y += 3.0f;
        _destinationMarkerInstance.transform.position = Vector3.Lerp(
            _destinationMarkerInstance.transform.position, worldPos, Time.deltaTime * 3f
        );
    }
    #endregion

    #region Navigation Logic
    void UpdateRouteProgress()
    {
        if (_currentRoute == null || _currentRoute.points.Count == 0) return;

        Vector3 camPos = _cameraTransform.position;
        float minDist = float.MaxValue;
        int closestIndex = _currentRouteIndex;

        // 양방향 탐색: 역행 복귀 감지 + 전진 추적
        int searchStart = Mathf.Max(0, _currentRouteIndex - 5);
        int searchEnd = Mathf.Min(_currentRouteIndex + 30, _currentRoute.points.Count);
        for (int i = searchStart; i < searchEnd; i++)
        {
            float dist = Vector3.Distance(camPos, _currentRoute.points[i].worldPosition);
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        _currentRouteIndex = closestIndex;
    }

    void UpdateNavigationUI()
    {
        if (_currentRoute == null || arUIManager == null) return;

        float remaining = CalculateRemainingDistance();
        arUIManager.UpdateRemainingDistance(remaining);

        // 진행률 바
        if (_currentRoute.totalDistance > 0)
        {
            float progress = 1f - (remaining / _currentRoute.totalDistance);
            arUIManager.UpdateProgress(progress);

            // ETA: 카카오가 제공한 totalDuration을 진행률에 비례 감산
            int etaSeconds = Mathf.RoundToInt(_currentRoute.totalDuration * (1f - progress));
            arUIManager.UpdateETA(etaSeconds);
        }

        RouteGuide nextGuide = FindNextGuide();
        if (nextGuide != null)
        {
            float guideDist = (float)GeospatialManager.HaversineDistance(
                EarthManager.CameraGeospatialPose.Latitude,
                EarthManager.CameraGeospatialPose.Longitude,
                nextGuide.latitude, nextGuide.longitude
            );
            arUIManager.UpdateNextGuide(nextGuide.guidance, guideDist, nextGuide.type);
        }
        else
        {
            arUIManager.UpdateNextGuide("", 0f, 1);
        }
    }

    void UpdateOffScreenIndicator()
    {
        if (arUIManager == null || _currentRoute == null) return;

        Vector3 targetPos;
        if (_currentRoute.points.Count > _currentRouteIndex + 5)
            targetPos = _currentRoute.points[_currentRouteIndex + 5].worldPosition;
        else if (_currentRoute.destination != null)
            targetPos = GeoToWorldPosition(_currentRoute.destination.latitude, _currentRoute.destination.longitude);
        else
            return;

        arUIManager.UpdateOffScreenIndicator(targetPos, Camera.main);
    }

    float CalculateRemainingDistance()
    {
        if (_currentRoute == null || _currentRoute.points.Count == 0) return 0f;
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return 0f;

        // 월드좌표 드리프트에 영향받지 않도록 Haversine(GPS) 기반으로 계산
        var camGeo = EarthManager.CameraGeospatialPose;
        int startIdx = Mathf.Max(_currentRouteIndex, 0);
        if (startIdx >= _currentRoute.points.Count) return 0f;

        // 1) 현재 위치 → 현재 인덱스 다음 경로점
        RoutePoint first = _currentRoute.points[startIdx];
        double dist = GeospatialManager.HaversineDistance(
            camGeo.Latitude, camGeo.Longitude,
            first.latitude, first.longitude
        );

        // 2) 이후 경로점 간 거리 누적
        for (int i = startIdx; i < _currentRoute.points.Count - 1; i++)
        {
            RoutePoint a = _currentRoute.points[i];
            RoutePoint b = _currentRoute.points[i + 1];
            dist += GeospatialManager.HaversineDistance(a.latitude, a.longitude, b.latitude, b.longitude);
        }

        return (float)dist;
    }

    RouteGuide FindNextGuide()
    {
        if (_currentRoute == null || _currentRoute.guides.Count == 0) return null;
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return null;

        // 현재 _currentRouteIndex 이후의 가이드 중 가장 가까운 것 선택 (역행 방지)
        RouteGuide nextGuide = null;
        int bestGuideIdx = int.MaxValue;

        foreach (RouteGuide guide in _currentRoute.guides)
        {
            if (guide.routePointIndex >= _currentRouteIndex && guide.routePointIndex < bestGuideIdx)
            {
                bestGuideIdx = guide.routePointIndex;
                nextGuide = guide;
            }
        }

        return nextGuide;
    }

    void CheckRerouteNeeded()
    {
        if (_currentRoute == null || _currentRoute.points.Count == 0) return;
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return;

        var camGeo = EarthManager.CameraGeospatialPose;
        double minDist = double.MaxValue;

        int searchStart = Mathf.Max(0, _currentRouteIndex - 3);
        int searchEnd = Mathf.Min(_currentRoute.points.Count, _currentRouteIndex + 10);

        for (int i = searchStart; i < searchEnd; i++)
        {
            RoutePoint pt = _currentRoute.points[i];
            double dist = GeospatialManager.HaversineDistance(
                camGeo.Latitude, camGeo.Longitude,
                pt.latitude, pt.longitude
            );

            if (dist < minDist) minDist = dist;
        }

        if (minDist > rerouteThreshold)
        {
            Debug.Log($"경로 이탈 감지 ({minDist:F1}m). 재탐색 시작.");
            arUIManager?.ShowRerouting(true);

            StartCoroutine(RerouteFlow());
        }
    }

    IEnumerator RerouteFlow()
    {
        yield return StartCoroutine(FetchRouteAndStart(
            _currentRoute.destination.latitude,
            _currentRoute.destination.longitude,
            _currentRoute.destination.placeName
        ));
        arUIManager?.ShowRerouting(false);
    }

    void CheckArrival()
    {
        if (_currentRoute?.destination == null) return;
        if (EarthManager.EarthTrackingState != TrackingState.Tracking) return;

        var camGeo = EarthManager.CameraGeospatialPose;
        double dist = GeospatialManager.HaversineDistance(
            camGeo.Latitude, camGeo.Longitude,
            _currentRoute.destination.latitude,
            _currentRoute.destination.longitude
        );

        if (dist <= arrivalThreshold)
        {
            SetState(NavigationState.Arrived);
            arUIManager?.ShowToast("목적지에 도착했습니다!");
            StartCoroutine(FinalizeArrival());
        }
    }

    IEnumerator FinalizeArrival()
    {
        yield return new WaitForSeconds(3f);
        StopNavigation();
    }
    #endregion

    #region State Management
    void SetState(NavigationState newState)
    {
        if (CurrentState == newState) return;
        Debug.Log($"[Navigation] {CurrentState} → {newState}");
        CurrentState = newState;
    }

    public void StopNavigation()
    {
        arUIManager?.ShowRerouting(false);
        arUIManager?.SetTrackingWarning(false);
        ReturnAllArrows();
        ClearDestinationMarker();
        ClearRouteAnchors();

        if (_routeOriginAnchor != null)
        {
            Destroy(_routeOriginAnchor.gameObject);
            _routeOriginAnchor = null;
        }

        _currentRoute = null;
        _currentRouteIndex = 0;
        _frameCounter = 0;
        _headingHistory.Clear();

        geospatialManager.isNavigationActive = false;
        SetState(NavigationState.Idle);
        arUIManager?.ExitNavigationMode();
    }

    void ClearRouteAnchors()
    {
        foreach (ARGeospatialAnchor anchor in _routeAnchors)
        {
            if (anchor != null) Destroy(anchor.gameObject);
        }
        _routeAnchors.Clear();
    }
    #endregion
}
