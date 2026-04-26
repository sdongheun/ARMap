using System;
using System.Collections.Generic;
using System.Linq;


// 클러스터링이 끝난 kakaoDocument 묶음 1개를 최종 BuildingData 1개로 변환하는 팩토리 메서드다. 클러스터링된 문서들을 분석해 대표 이름과 설명, 시설 목록 등을 추출한다. 
//현재는 학교 / 병원 / 캠퍼스용 대표 토큰과 삼거리 / 정문 / 후문 / 병원 / 상업 전용 판별을 모두 사용하지 않고, 클러스터링은 단일 기준으로만 수행한다.
public partial class GeospatialManager
{
    BuildingData ProcessClusterIntoBuildingData(List<KakaoDocument> cluster)
    {
        BuildingData newBuilding = new BuildingData();
        //건물의 전화번호, URL, 설명 같은 “상세 정보” 기준
        KakaoDocument detailRepresentative = SelectRepresentativeDocument(cluster);
        // 현재는 학교/병원/캠퍼스용 대표 토큰 기반 이름 생성은 사용하지 않는다.
        // string institutionalBuildingToken = SelectInstitutionalBuildingToken(cluster);
        // bool useInstitutionalBuildingName = !string.IsNullOrWhiteSpace(institutionalBuildingToken);
        string institutionalBuildingToken = string.Empty;
        bool useInstitutionalBuildingName = false;
        //건물명을 가장 잘 대표하는 문서
        KakaoDocument nameRepresentative = SelectBuildingNameDocument(cluster, detailRepresentative, institutionalBuildingToken);
        //실제 앵커 좌표 기준 문서
        KakaoDocument anchorRepresentative = SelectAnchorRepresentativeDocument(cluster, nameRepresentative);

        newBuilding.buildingName = nameRepresentative.place_name;
        newBuilding.latitude = double.Parse(anchorRepresentative.y);
        newBuilding.longitude = double.Parse(anchorRepresentative.x);
        newBuilding.altitude = 0;
        newBuilding.lotNumberAddress = !string.IsNullOrWhiteSpace(GetLotNumberAddress(nameRepresentative))
            ? GetLotNumberAddress(nameRepresentative)
            : GetLotNumberAddress(detailRepresentative);
        newBuilding.roadAddress = !string.IsNullOrWhiteSpace(GetRoadAddress(nameRepresentative))
            ? GetRoadAddress(nameRepresentative)
            : GetRoadAddress(detailRepresentative);
        newBuilding.fetchedAddress = !string.IsNullOrWhiteSpace(GetBestAddress(nameRepresentative))
            ? GetBestAddress(nameRepresentative)
            : GetBestAddress(detailRepresentative);
        newBuilding.description = GetBuildingDescription(cluster, newBuilding.buildingName, nameRepresentative, detailRepresentative, useInstitutionalBuildingName);

        newBuilding.phoneNumber = detailRepresentative.phone;
        newBuilding.placeUrl = detailRepresentative.place_url;
        newBuilding.facilities = BuildFacilityList(cluster, newBuilding.buildingName, institutionalBuildingToken);

        return newBuilding;
    }


    // string ResolveBuildingDisplayName(List<KakaoDocument> cluster, KakaoDocument nameRepresentative, string buildingToken, bool useInstitutionalBuildingName)
    // {
    //     if (!useInstitutionalBuildingName)
    //     {
    //         return nameRepresentative.place_name;
    //     }

    // KakaoDocument representativeBuildingDocument = cluster
    //     .Where(IsRepresentativeBuildingCandidate)
    //     .OrderByDescending(GetRepresentativeBuildingCandidateScore)
    //     .ThenByDescending(GetDocumentQualityScore)
    //     .FirstOrDefault(document => NormalizeText(GetRepresentativeBuildingClusterKey(document)) == NormalizeText(buildingToken));

    // if (!string.IsNullOrWhiteSpace(representativeBuildingDocument?.place_name))
    // {
    //     return representativeBuildingDocument.place_name;
    // }

    // return !string.IsNullOrWhiteSpace(nameRepresentative?.place_name)
    //     ? nameRepresentative.place_name
    //     : buildingToken;
    // }

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
}
