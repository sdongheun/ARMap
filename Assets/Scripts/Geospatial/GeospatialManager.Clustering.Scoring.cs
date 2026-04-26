using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// X
public partial class GeospatialManager
{
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
        // 현재는 일반 건물만 사용하므로 기관/병원 대표 후보 판정은 비활성화한다.
        // if (document == null)
        // {
        //     return false;
        // }
        //
        // if (!LooksLikeAnchorEligibleBuilding(document))
        // {
        //     return false;
        // }
        //
        // if (GetClusterGroupingType(document) != ClusterGroupingType.Institutional)
        // {
        //     return false;
        // }
        //
        // return GetRepresentativeBuildingCandidateScore(document) > 0;
        return false;
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
}
