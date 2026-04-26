using System;
using System.Collections.Generic;

//건물 정보 데이터
[Serializable]
public class FacilityInfo // 카드 하나에 들어갈 정보
{
    public string name;  // 가게 이름
    public string phone; // 전화번호
    public string category; // 카테고리
    public string placeUrl; // 카카오맵 주소
}

[Serializable]
public class BuildingData
{
    public string buildingName;
    public string description; // 카테고리
    public string fetchedAddress;
    public string lotNumberAddress;
    public string roadAddress;

    // ★ 전화, 지도
    public string phoneNumber;
    public string placeUrl;

    // ★ 입점 상가 리스트 
    public List<FacilityInfo> facilities = new List<FacilityInfo>();

    // 위치 정보
    public double latitude;
    public double longitude;
    public double altitude;
}
