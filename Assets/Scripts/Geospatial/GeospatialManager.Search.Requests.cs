using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


//실제로 Kakao Local API 요청을 보내고, 응답 문서를 1차 가공해서 상위 흐름에 넘기는 파일
public partial class GeospatialManager
{
    IEnumerator FetchNearbyPlacesFromKakaoCategory(double lat, double lon, string categoryCode, string sourceLabel, int sourcePriority, Action<bool, List<KakaoDocument>> onCompleted)
    {
        List<KakaoDocument> fetchedDocuments = new List<KakaoDocument>();
        bool requestSucceeded = false;

        for (int page = 1; page <= maxSearchPages; page++)
        {
            string url = $"https://dapi.kakao.com/v2/local/search/category.json?category_group_code={categoryCode}&x={lon}&y={lat}&radius={searchRadius}&page={page}";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Authorization", "KakaoAK " + kakaoRestApiKey);
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    requestSucceeded = true;
                    KakaoResponse response = JsonUtility.FromJson<KakaoResponse>(webRequest.downloadHandler.text);
                    if (response?.documents == null || response.documents.Length == 0) break;
                    foreach (KakaoDocument doc in response.documents)
                    {
                        PrepareFetchedDocument(doc, sourceLabel, categoryCode, sourcePriority);
                        fetchedDocuments.Add(doc);
                    }
                    if (response.meta == null || response.meta.is_end) break;
                }
                else
                {
                    Debug.LogWarning($"Kakao category request failed for {categoryCode}, page {page}: {webRequest.error}");
                    break;
                }
            }
        }

        onCompleted?.Invoke(requestSucceeded, fetchedDocuments);
    }

    IEnumerator FetchNearbyPlacesFromKakaoKeyword(double lat, double lon, string query, int sourcePriority, Action<bool, List<KakaoDocument>> onCompleted)
    {
        List<KakaoDocument> fetchedDocuments = new List<KakaoDocument>();
        bool requestSucceeded = false;

        for (int page = 1; page <= maxSearchPages; page++)
        {
            string escapedQuery = UnityWebRequest.EscapeURL(query);
            string url = $"https://dapi.kakao.com/v2/local/search/keyword.json?query={escapedQuery}&x={lon}&y={lat}&radius={searchRadius}&page={page}";
            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.SetRequestHeader("Authorization", "KakaoAK " + kakaoRestApiKey);
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    requestSucceeded = true;
                    KakaoResponse response = JsonUtility.FromJson<KakaoResponse>(webRequest.downloadHandler.text);
                    if (response?.documents == null || response.documents.Length == 0) break;
                    foreach (KakaoDocument doc in response.documents)
                    {
                        PrepareFetchedDocument(doc, "keyword", query, sourcePriority);
                        fetchedDocuments.Add(doc);
                    }
                    if (response.meta == null || response.meta.is_end) break;
                }
                else
                {
                    Debug.LogWarning($"Kakao keyword request failed for '{query}', page {page}: {webRequest.error}");
                    break;
                }
            }
        }

        onCompleted?.Invoke(requestSucceeded, fetchedDocuments);
    }

    IEnumerable<string> GetCommercialCategoryCodes()
    {
        return categoryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct();
    }

    IEnumerable<string> GetCampusCategoryCodes()
    {
        return campusCategoryCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Except(GetCommercialCategoryCodes())
            .Distinct();
    }

    IEnumerable<string> GetKeywordQueries()
    {
        return keywordQueries
            .Where(query => !string.IsNullOrWhiteSpace(query))
            .Select(query => query.Trim())
            .Distinct();
    }

    void PrepareFetchedDocument(KakaoDocument document, string searchSource, string sourceQuery, int sourcePriority)
    {
        if (document == null)
        {
            return;
        }

        document.search_source = searchSource;
        document.source_query = sourceQuery;
        document.source_priority = sourcePriority;

        if (string.IsNullOrWhiteSpace(document.category_group_name) && searchSource == "keyword")
        {
            document.category_group_name = "키워드 검색";
        }
    }
}
