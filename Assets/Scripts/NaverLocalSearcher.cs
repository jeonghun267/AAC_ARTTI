using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 네이버 지역 검색 API 통신 및 데이터 파싱을 담당하는 클래스
/// </summary>
public class NaverLocalSearcher : MonoBehaviour
{
    // [중요] 발급받으신 Client ID와 Secret을 여기에 다시 붙여넣어 주세요!
    private const string CLIENT_ID = "G4fleNtA6Zpq0RWRa2jQ";
    private const string CLIENT_SECRET = "0vLoh6wed3";

    private const string SEARCH_URL = "https://openapi.naver.com/v1/search/local.json?display=1&query=";

    /// <summary>
    /// 검색어를 네이버 API로 보내고 결과를 콜백으로 반환하는 코루틴
    /// </summary>
    public IEnumerator SearchPlaceCoroutine(string keyword, Action<string> onComplete, Action<string> onError)
    {
        string encodedKeyword = UnityWebRequest.EscapeURL(keyword);
        string requestUrl = SEARCH_URL + encodedKeyword;

        using (UnityWebRequest request = UnityWebRequest.Get(requestUrl))
        {
            request.SetRequestHeader("X-Naver-Client-Id", CLIENT_ID);
            request.SetRequestHeader("X-Naver-Client-Secret", CLIENT_SECRET);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("[NaverAPI] 통신 성공: " + jsonResponse);
                onComplete?.Invoke(jsonResponse);
            }
            else
            {
                Debug.LogError("[NaverAPI] 통신 실패: " + request.error);
                onError?.Invoke(request.error);
            }
        }
    }

    /// <summary>
    /// 네이버 JSON 응답을 분석하여 카테고리 문자열만 추출
    /// </summary>
    public string ParseCategoryFromJson(string jsonResponse)
    {
        try
        {
            NaverLocalResult result = JsonUtility.FromJson<NaverLocalResult>(jsonResponse);

            if (result != null && result.total > 0 && result.items.Length > 0)
            {
                return result.items[0].category;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[NaverAPI] JSON 파싱 에러: " + e.Message);
        }

        return null;
    }

    /// <summary>
    /// 네이버의 복잡한 카테고리를 앱의 4개 분류(편의점, 약국, 카페, 음식점)로 번역
    /// </summary>
    public string MapToAppCategory(string naverCategory)
    {
        if (string.IsNullOrEmpty(naverCategory)) return null;

        if (naverCategory.Contains("편의점")) return "편의점";
        if (naverCategory.Contains("약국")) return "약국";
        if (naverCategory.Contains("카페") || naverCategory.Contains("커피") || naverCategory.Contains("다방")) return "카페";
        if (naverCategory.Contains("음식점") || naverCategory.Contains("한식") || naverCategory.Contains("중식") || naverCategory.Contains("양식")) return "음식점";

        return null;
    }
}

// =========================================================
// 네이버 API JSON 규격에 맞춘 데이터 모델
// =========================================================
[System.Serializable]
public class NaverLocalResult
{
    public int total;
    public NaverLocalItem[] items;
}

[System.Serializable]
public class NaverLocalItem
{
    public string title;
    public string category;
}