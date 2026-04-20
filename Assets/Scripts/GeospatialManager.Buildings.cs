using System.Collections.Generic;
using System.Linq;

public partial class GeospatialManager
{
    BuildingData ProcessClusterIntoBuildingData(List<KakaoDocument> cluster)
    {
        BuildingData newBuilding = new BuildingData();
        KakaoDocument detailRepresentative = SelectRepresentativeDocument(cluster);
        string institutionalBuildingToken = SelectInstitutionalBuildingToken(cluster);
        bool useInstitutionalBuildingName = !string.IsNullOrWhiteSpace(institutionalBuildingToken);
        KakaoDocument nameRepresentative = SelectBuildingNameDocument(cluster, detailRepresentative, institutionalBuildingToken);
        KakaoDocument anchorRepresentative = SelectAnchorRepresentativeDocument(cluster, nameRepresentative);

        newBuilding.buildingName = ResolveBuildingDisplayName(cluster, nameRepresentative, institutionalBuildingToken, useInstitutionalBuildingName);
        newBuilding.latitude = double.Parse(anchorRepresentative.y);
        newBuilding.longitude = double.Parse(anchorRepresentative.x);
        newBuilding.altitude = 0;
        newBuilding.fetchedAddress = !string.IsNullOrWhiteSpace(GetBestAddress(nameRepresentative))
            ? GetBestAddress(nameRepresentative)
            : GetBestAddress(detailRepresentative);
        newBuilding.description = GetBuildingDescription(cluster, newBuilding.buildingName, nameRepresentative, detailRepresentative, useInstitutionalBuildingName);

        newBuilding.phoneNumber = detailRepresentative.phone;
        newBuilding.placeUrl = detailRepresentative.place_url;
        newBuilding.facilities = BuildFacilityList(cluster, newBuilding.buildingName, institutionalBuildingToken);

        return newBuilding;
    }

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

    string ResolveBuildingDisplayName(List<KakaoDocument> cluster, KakaoDocument nameRepresentative, string buildingToken, bool useInstitutionalBuildingName)
    {
        if (!useInstitutionalBuildingName)
        {
            return nameRepresentative.place_name;
        }

        KakaoDocument representativeBuildingDocument = cluster
            .Where(IsRepresentativeBuildingCandidate)
            .OrderByDescending(GetRepresentativeBuildingCandidateScore)
            .ThenByDescending(GetDocumentQualityScore)
            .FirstOrDefault(document => NormalizeText(GetRepresentativeBuildingClusterKey(document)) == NormalizeText(buildingToken));

        if (!string.IsNullOrWhiteSpace(representativeBuildingDocument?.place_name))
        {
            return representativeBuildingDocument.place_name;
        }

        return !string.IsNullOrWhiteSpace(nameRepresentative?.place_name)
            ? nameRepresentative.place_name
            : buildingToken;
    }

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
        return cluster
            .Where(IsRepresentativeBuildingCandidate)
            .Select(document => GetRepresentativeBuildingClusterKey(document))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .GroupBy(token => NormalizeText(token))
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(token => token.Length))
            .Select(group => group.First())
            .FirstOrDefault();
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

        string extractedToken = NormalizeText(GetInstitutionalBuildingToken(document));
        if (string.IsNullOrWhiteSpace(extractedToken) || extractedToken != normalizedBuildingToken)
        {
            return false;
        }

        string reducedName = normalizedPlaceName.Replace(normalizedBuildingToken, string.Empty);
        foreach (string prefixToken in InstitutionalPrefixTokens)
        {
            reducedName = reducedName.Replace(NormalizeText(prefixToken), string.Empty);
        }

        return string.IsNullOrWhiteSpace(reducedName);
    }
}
