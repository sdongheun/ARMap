using System;
using System.Collections.Generic;
using UnityEngine;

// 네비게이션 상태 열거형
public enum NavigationState
{
    Idle,        // 대기 상태
    Searching,   // 목적지 검색 중
    Routing,     // 경로 계산 중
    Navigating,  // 길안내 진행 중
    Arrived,     // 목적지 도착
    Error        // 오류 발생
}

// 경로 위의 한 점
[Serializable]
public class RoutePoint
{
    public double latitude;
    public double longitude;
    public Vector3 worldPosition;
    public Vector3 eunOffset;  // 기준 앵커로부터의 (east, 0, north) 오프셋 (미터). 카메라 회전과 무관하게 고정
    public bool isAnchorPoint; // 회전점(가이드 포인트) 여부
}

// 경로 안내 정보
[Serializable]
public class RouteGuide
{
    public int type;        // 안내 타입
    public string guidance; // 안내 문구
    public double latitude;
    public double longitude;
    public int distance;    // 다음 안내까지 거리(m)
    public int routePointIndex; // 이 가이드에 해당하는 경로점 인덱스
}

// 목적지 검색 결과
[Serializable]
public class DestinationResult
{
    public string placeName;
    public string addressName;
    public string roadAddressName;
    public double latitude;
    public double longitude;
    public string categoryName;
    public int distance;    // 현재 위치로부터 직선거리(m)
}

// 전체 경로 데이터
[Serializable]
public class NavigationRoute
{
    public List<RoutePoint> points = new List<RoutePoint>();
    public List<RouteGuide> guides = new List<RouteGuide>();
    public int totalDistance;  // 전체 거리(m)
    public int totalDuration;  // 예상 소요 시간(초)
    public DestinationResult destination;
}

// --- 카카오 모빌리티 도보 길찾기 API 응답 파싱 클래스 ---
[Serializable]
public class KakaoDirectionsResponse
{
    public string trans_id;
    public KakaoRoute[] routes;
}

[Serializable]
public class KakaoRoute
{
    public int result_code;
    public string result_msg;
    public KakaoRouteSummary summary;
    public KakaoRouteSection[] sections;
}

[Serializable]
public class KakaoRouteSummary
{
    public int distance;  // 전체 거리(m)
    public int duration;  // 예상 소요 시간(초)
}

[Serializable]
public class KakaoRouteSection
{
    public int distance;
    public int duration;
    public KakaoRoad[] roads;
    public KakaoGuide[] guides;
}

[Serializable]
public class KakaoRoad
{
    public string name;
    public int distance;
    public float[] vertexes; // flat array: lon1, lat1, lon2, lat2, ...
}

[Serializable]
public class KakaoGuide
{
    public string name;
    public double x; // longitude
    public double y; // latitude
    public int distance;
    public int type;
    public string guidance;
}
