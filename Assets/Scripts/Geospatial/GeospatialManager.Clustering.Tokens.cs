using System;


// 사용 X - 현재는 대표 토큰과 그룹핑 타입을 모두 사용하지 않고, 클러스터링은 단일 기준으로만 수행한다.
public partial class GeospatialManager
{
    string GetRepresentativeBuildingClusterKey(KakaoDocument document)
    {
        // 현재는 학교/병원/캠퍼스용 대표 토큰을 사용하지 않는다.
        // string buildingToken = NormalizeText(GetInstitutionalBuildingToken(document));
        // if (!string.IsNullOrWhiteSpace(buildingToken))
        // {
        //     return buildingToken;
        // }
        return NormalizeRepresentativeName(document?.place_name);
    }

    bool LooksLikeInstitutionalFacility(KakaoDocument document)
    {
        // 현재는 기관/상업 시설 판별을 사용하지 않는다.
        // string normalizedPlaceName = NormalizeText(document?.place_name);
        // string normalizedCategory = NormalizeText(document?.category_name);
        // string normalizedGroupCategory = NormalizeText(document?.category_group_name);
        //
        // return ContainsAnyKeyword(normalizedPlaceName, InstitutionalFacilityKeywords) ||
        //        ContainsAnyKeyword(normalizedCategory, InstitutionalFacilityKeywords) ||
        //        ContainsAnyKeyword(normalizedGroupCategory, InstitutionalFacilityKeywords) ||
        //        ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
        //        ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords);
        return false;
    }

    bool LooksLikeAnchorEligibleBuilding(KakaoDocument document)
    {
        if (document == null)
        {
            return false;
        }

        string normalizedPlaceName = NormalizeText(document.place_name);
        string normalizedCategory = NormalizeText(document.category_name);
        string normalizedGroupCategory = NormalizeText(document.category_group_name);

        // 현재는 삼거리/정문/후문/병원/상업 전용 판별을 사용하지 않는다.
        // if (ContainsAnyKeyword(normalizedPlaceName, NonBuildingRepresentativeKeywords) ||
        //     ContainsAnyKeyword(normalizedCategory, NonBuildingRepresentativeKeywords) ||
        //     ContainsAnyKeyword(normalizedGroupCategory, NonBuildingRepresentativeKeywords))
        // {
        //     return false;
        // }
        //
        // if (ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
        //     ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords))
        // {
        //     return false;
        // }
        return true;
    }

    string NormalizeRepresentativeName(string placeName)
    {
        string normalized = NormalizeText(placeName);
        // 현재는 기관 접두어 제거를 사용하지 않는다.
        // foreach (string prefixToken in InstitutionalPrefixTokens)
        // {
        //     normalized = normalized.Replace(NormalizeText(prefixToken), string.Empty);
        // }
        return normalized;
    }

    ClusterGroupingType GetClusterGroupingType(KakaoDocument document)
    {
        // 현재는 학교/병원/상업용 그룹 타입을 나누지 않는다.
        // string normalizedPlaceName = NormalizeText(document?.place_name);
        // string normalizedCategory = NormalizeText(document?.category_group_name);
        //
        // if (ContainsAnyKeyword(normalizedPlaceName, CommercialKeywords) || ContainsAnyKeyword(normalizedCategory, CommercialKeywords))
        // {
        //     return ClusterGroupingType.Commercial;
        // }
        //
        // if (ContainsAnyKeyword(normalizedPlaceName, InstitutionalKeywords) || ContainsAnyKeyword(normalizedCategory, InstitutionalKeywords))
        // {
        //     return ClusterGroupingType.Institutional;
        // }
        return ClusterGroupingType.Default;
    }

    bool ContainsAnyKeyword(string normalizedValue, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        foreach (string keyword in keywords)
        {
            if (normalizedValue.Contains(NormalizeText(keyword)))
            {
                return true;
            }
        }

        return false;
    }

    string GetInstitutionalBuildingToken(KakaoDocument document)
    {
        // 현재는 학교/병원/캠퍼스용 건물 토큰을 사용하지 않는다.
        // string addressToken = ExtractInstitutionalBuildingToken(GetBestAddress(document));
        // if (!string.IsNullOrWhiteSpace(addressToken))
        // {
        //     return addressToken;
        // }
        //
        // return ExtractInstitutionalBuildingToken(document?.place_name);
        return string.Empty;
    }

    string ExtractInstitutionalBuildingToken(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        string[] tokens = sourceText
            .Replace("(", " ")
            .Replace(")", " ")
            .Replace(",", " ")
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            if (IsInstitutionalBuildingToken(token))
            {
                return token.Trim();
            }
        }

        return string.Empty;
    }

    bool IsInstitutionalBuildingToken(string token)
    {
        string normalizedToken = NormalizeText(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return false;
        }

        // 현재는 학교/병원/캠퍼스용 건물 토큰 판별을 사용하지 않는다.
        // if (ContainsAnyKeyword(normalizedToken, CommercialKeywords))
        // {
        //     return false;
        // }
        //
        // foreach (string suffix in InstitutionalBuildingSuffixes)
        // {
        //     if (normalizedToken.EndsWith(NormalizeText(suffix)))
        //     {
        //         return true;
        //     }
        // }
        //
        // return false;
        return false;
    }
}
