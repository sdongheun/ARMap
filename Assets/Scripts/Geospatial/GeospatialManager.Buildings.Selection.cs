using System.Collections.Generic;
using System.Linq;

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
