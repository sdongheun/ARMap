using System;

public partial class GeospatialManager
{
    string GetRepresentativeBuildingClusterKey(KakaoDocument document)
    {
        string buildingToken = NormalizeText(GetInstitutionalBuildingToken(document));
        if (!string.IsNullOrWhiteSpace(buildingToken))
        {
            return buildingToken;
        }

        return NormalizeRepresentativeName(document?.place_name);
    }

    bool LooksLikeInstitutionalFacility(KakaoDocument document)
    {
        string normalizedPlaceName = NormalizeText(document?.place_name);
        string normalizedCategory = NormalizeText(document?.category_name);
        string normalizedGroupCategory = NormalizeText(document?.category_group_name);

        return ContainsAnyKeyword(normalizedPlaceName, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedCategory, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedGroupCategory, InstitutionalFacilityKeywords) ||
               ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
               ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords);
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

        if (ContainsAnyKeyword(normalizedPlaceName, NonBuildingRepresentativeKeywords) ||
            ContainsAnyKeyword(normalizedCategory, NonBuildingRepresentativeKeywords) ||
            ContainsAnyKeyword(normalizedGroupCategory, NonBuildingRepresentativeKeywords))
        {
            return false;
        }

        if (ContainsAnyKeyword(normalizedCategory, CommercialKeywords) ||
            ContainsAnyKeyword(normalizedGroupCategory, CommercialKeywords))
        {
            return false;
        }

        return true;
    }

    string NormalizeRepresentativeName(string placeName)
    {
        string normalized = NormalizeText(placeName);
        foreach (string prefixToken in InstitutionalPrefixTokens)
        {
            normalized = normalized.Replace(NormalizeText(prefixToken), string.Empty);
        }

        return normalized;
    }

    ClusterGroupingType GetClusterGroupingType(KakaoDocument document)
    {
        string normalizedPlaceName = NormalizeText(document?.place_name);
        string normalizedCategory = NormalizeText(document?.category_group_name);

        if (ContainsAnyKeyword(normalizedPlaceName, CommercialKeywords) || ContainsAnyKeyword(normalizedCategory, CommercialKeywords))
        {
            return ClusterGroupingType.Commercial;
        }

        if (ContainsAnyKeyword(normalizedPlaceName, InstitutionalKeywords) || ContainsAnyKeyword(normalizedCategory, InstitutionalKeywords))
        {
            return ClusterGroupingType.Institutional;
        }

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
        string addressToken = ExtractInstitutionalBuildingToken(GetBestAddress(document));
        if (!string.IsNullOrWhiteSpace(addressToken))
        {
            return addressToken;
        }

        return ExtractInstitutionalBuildingToken(document?.place_name);
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

        if (ContainsAnyKeyword(normalizedToken, CommercialKeywords))
        {
            return false;
        }

        foreach (string suffix in InstitutionalBuildingSuffixes)
        {
            if (normalizedToken.EndsWith(NormalizeText(suffix)))
            {
                return true;
            }
        }

        return false;
    }
}
