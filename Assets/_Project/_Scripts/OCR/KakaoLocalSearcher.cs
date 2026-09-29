using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Artti.Common;

/// <summary>
/// 카카오 로컬 API - 좌표 기반 주변 장소 카테고리 검색
/// OcrTestController와 같은 오브젝트에 추가하고 인스펙터에서 연결하세요.
/// </summary>
public class KakaoLocalSearcher : MonoBehaviour
{
    // 변경점: 키를 코드에서 제거. .env의 KAKAO_REST_API_KEY를 읽는다 (빌드 시 Resources/api_keys.txt로 복사됨)
    private static string RestApiKey => ApiKeyLoader.Get(ApiKeyLoader.KakaoRestApi);
    private const string BASE_URL = "https://dapi.kakao.com/v2/local/search/category.json";

    // 검색할 카테고리 코드 목록 (앱의 4개 분류에 맞춤)
    // CE7: 카페, PM9: 약국, FD6: 음식점, CS2: 편의점
    private readonly string[] categoryGroups = { "CE7", "PM9", "FD6", "CS2" };

    /// <summary>
    /// 좌표 기반으로 반경 50m 내 장소를 검색하고 KeywordMatch 형태로 반환합니다.
    /// </summary>
    /// <param name="lat">위도</param>
    /// <param name="lon">경도</param>
    /// <param name="onSuccess">검색 성공 시 KeywordMatch 반환</param>
    /// <param name="onFail">검색 실패 시 호출</param>
    public IEnumerator SearchByCoords(double lat, double lon,
                                      Action<KeywordMatch> onSuccess,
                                      Action onFail)
    {
        // 변경점: 키가 없으면 요청하지 않고 실패 처리 (401 반복 방지)
        string apiKey = RestApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[KakaoAPI] .env에 KAKAO_REST_API_KEY가 없어 좌표 검색을 건너뜁니다.");
            onFail?.Invoke();
            yield break;
        }

        KeywordMatch bestMatch = null;
        float bestDistance = float.MaxValue;

        foreach (var code in categoryGroups)
        {
            // 좌표(x=경도, y=위도), 반경 50m, 거리순 정렬
            string url = $"{BASE_URL}?category_group_code={code}" +
                         $"&x={lon}&y={lat}&radius=50&sort=distance";

            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"KakaoAK {apiKey}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[KakaoAPI] {code} 검색 실패: {request.error}");
                continue;
            }

            Debug.Log($"[KakaoAPI] {code} 응답: {request.downloadHandler.text}");

            var response = JsonUtility.FromJson<KakaoResponse>(request.downloadHandler.text);
            if (response?.documents == null || response.documents.Length == 0) continue;

            // 가장 가까운 장소 선택
            var nearest = response.documents[0];
            float dist = float.TryParse(nearest.distance, out float d) ? d : float.MaxValue;

            if (dist < bestDistance)
            {
                bestDistance = dist;
                string appCategory = MapToAppCategory(nearest.category_group_code);

                if (appCategory != null)
                {
                    bestMatch = new KeywordMatch(
                        appCategory,
                        GetDescription(appCategory),
                        GetImageName(appCategory),
                        "좌표검색"
                    );
                    Debug.Log($"[KakaoAPI] 후보 발견: {nearest.place_name} | {appCategory} | {dist}m");
                }
            }
        }

        if (bestMatch != null)
        {
            Debug.Log($"[KakaoAPI] 최종 선택: {bestMatch.Category} ({bestDistance}m)");
            onSuccess?.Invoke(bestMatch);
        }
        else
        {
            Debug.Log("[KakaoAPI] 반경 50m 내 해당하는 장소 없음.");
            onFail?.Invoke();
        }
    }

    /// <summary>
    /// 카카오 카테고리 코드 → 앱 카테고리명 변환
    /// </summary>
    private string MapToAppCategory(string code) => code switch
    {
        "CE7" => "카페",
        "PM9" => "약국",
        "FD6" => "음식점",
        "CS2" => "편의점",
        _ => null
    };

    /// <summary>
    /// 카테고리별 설명 텍스트
    /// CategoryInfo.cs에 설명이 있다면 그쪽 값으로 교체하세요.
    /// </summary>
    private string GetDescription(string category) => category switch
    {
        "카페" => "커피와 음료를 파는 곳입니다.",
        "약국" => "약을 구매할 수 있는 곳입니다.",
        "음식점" => "음식을 먹을 수 있는 곳입니다.",
        "편의점" => "생활용품을 살 수 있는 곳입니다.",
        _ => ""
    };

    /// <summary>
    /// 카테고리별 AAC 이미지 이름 (Resources/AAC/ 폴더 기준)
    /// CategoryInfo.cs의 ImageName 값과 동일하게 맞춰주세요.
    /// </summary>
    private string GetImageName(string category) => category switch
    {
        "카페" => "cafe",
        "약국" => "pharmacy",
        "음식점" => "restaurant",
        "편의점" => "convenience",
        _ => ""
    };
}

// =========================================================
// 카카오 API JSON 데이터 모델
// =========================================================
[System.Serializable]
public class KakaoResponse
{
    public KakaoDocument[] documents;
}

[System.Serializable]
public class KakaoDocument
{
    public string place_name;           // 장소명 (예: "메가MGC커피 강남점")
    public string category_group_code;  // 카테고리 코드 (예: "CE7")
    public string category_group_name;  // 카테고리명 (예: "카페")
    public string distance;             // 거리 (미터 단위, 예: "23")
}