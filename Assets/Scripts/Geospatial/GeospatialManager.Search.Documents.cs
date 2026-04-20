using System;
using System.Collections.Generic;

public partial class GeospatialManager
{
    void MergeFetchedPlaces(Dictionary<string, KakaoDocument> mergedPlaces, List<KakaoDocument> fetchedDocuments)
    {
        foreach (KakaoDocument document in fetchedDocuments)
        {
            string dedupeKey = GetDocumentDedupeKey(document);
            if (mergedPlaces.TryGetValue(dedupeKey, out KakaoDocument existingDocument))
            {
                if (ShouldReplaceDocument(existingDocument, document))
                {
                    mergedPlaces[dedupeKey] = document;
                }
                continue;
            }

            mergedPlaces[dedupeKey] = document;
        }
    }

    bool ShouldReplaceDocument(KakaoDocument existingDocument, KakaoDocument candidateDocument)
    {
        int existingScore = GetDocumentQualityScore(existingDocument);
        int candidateScore = GetDocumentQualityScore(candidateDocument);
        return candidateScore > existingScore;
    }

    int GetDocumentQualityScore(KakaoDocument document)
    {
        int score = document?.source_priority ?? 0;
        if (!string.IsNullOrWhiteSpace(document?.road_address_name)) score += 20;
        if (!string.IsNullOrWhiteSpace(document?.phone)) score += 5;
        if (!string.IsNullOrWhiteSpace(document?.category_group_name) && document.category_group_name != "키워드 검색") score += 10;
        return score;
    }

    string GetDocumentDedupeKey(KakaoDocument document)
    {
        string normalizedName = NormalizeText(document?.place_name);
        string normalizedAddress = NormalizeText(GetBestAddress(document));
        string normalizedCoords = $"{NormalizeCoordinate(document?.y)}|{NormalizeCoordinate(document?.x)}";

        if (!string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return $"{normalizedName}|{normalizedAddress}";
        }

        return $"{normalizedName}|{normalizedCoords}";
    }

    string GetBestAddress(KakaoDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document?.road_address_name))
        {
            return document.road_address_name;
        }

        return document?.address_name ?? string.Empty;
    }

    string NormalizeText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }

    string NormalizeCoordinate(string value)
    {
        if (!double.TryParse(value, out double parsed))
        {
            return "0";
        }

        return Math.Round(parsed, 5).ToString("F5");
    }
}
