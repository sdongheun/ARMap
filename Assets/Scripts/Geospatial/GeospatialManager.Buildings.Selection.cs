using System.Collections.Generic;
using System.Linq;

//클러스터 안에서 “어떤 문서를 대표로 쓸지” 고르는 선택 로직 모음이다. 
//건물의 대표 이름, 설명, 앵커 좌표 등을 고르는 데 사용된다. 
//현재는 학교/병원/캠퍼스용 대표 토큰과 삼거리/정문/후문/병원/상업 전용 판별을 모두 사용하지 않고, 클러스터링은 단일 기준으로만 수행한다.

//동작:
// cluster 안 문서 중 LooksLikeAnchorEligibleBuilding 조건을 통과한 것만 봅니다.
// 그다음 정렬 우선순위:
// 1. GetRepresentativeBuildingCandidateScore 높은 순 (대표 후보일수록 높은 점수)
// 2. GetDocumentQualityScore 높은 순 (road_address_name, lot_number_address, phone 등 상세 정보가 있는 문서가 우선)
// 3. road_address_name 있는 문서 우선 (도로명 주소)
// 4. place_name 길이가 긴 문서 우선 (장소이름)
// 가장 앞의 문서를 반환합니다.
// 없으면 fallbackDocument 반환
// 의미:

// 건물처럼 보이는 문서 중에서 가장 그럴듯한 걸 골라
// 그 문서의 x/y 좌표를 실제 건물 앵커 위치 기준으로 씁니다.
// 즉 앱에서 건물 위치를 대표하는 한 점을 정하는 함수입니다.


public partial class GeospatialManager
{
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
        // 현재는 학교/병원/캠퍼스용 대표 토큰을 사용하지 않는다.
        // return cluster
        //     .Where(IsRepresentativeBuildingCandidate)
        //     .Select(document => GetRepresentativeBuildingClusterKey(document))
        //     .Where(token => !string.IsNullOrWhiteSpace(token))
        //     .GroupBy(token => NormalizeText(token))
        //     .OrderByDescending(group => group.Count())
        //     .ThenByDescending(group => group.Max(token => token.Length))
        //     .Select(group => group.First())
        //     .FirstOrDefault();
        return string.Empty;
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

        // 현재는 기관 토큰 기반 건물 참조 판별을 사용하지 않는다.
        // string extractedToken = NormalizeText(GetInstitutionalBuildingToken(document));
        // if (string.IsNullOrWhiteSpace(extractedToken) || extractedToken != normalizedBuildingToken)
        // {
        //     return false;
        // }
        //
        // string reducedName = normalizedPlaceName.Replace(normalizedBuildingToken, string.Empty);
        // foreach (string prefixToken in InstitutionalPrefixTokens)
        // {
        //     reducedName = reducedName.Replace(NormalizeText(prefixToken), string.Empty);
        // }
        //
        // return string.IsNullOrWhiteSpace(reducedName);
        return false;
    }
}
