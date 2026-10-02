using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using Artti.Common;

/// <summary>
/// 카카오 로컬 API - 좌표 기반 주변 장소 카테고리 검색
/// OcrTestController와 같은 오브젝트에 추가하고 인스펙터에서 연결하세요.
/// </summary>
public class KakaoLocalSearcher : MonoBehaviour
{
    private const int BrandRadiusMeters = 1000;   // 브랜드 카페는 먼저 이 반경 안에서 찾는다
    // 변경점: 키를 코드에서 제거. .env의 KAKAO_REST_API_KEY를 읽는다 (빌드 시 Resources/api_keys.txt로 복사됨)
    private static string RestApiKey => ApiKeyLoader.Get(ApiKeyLoader.KakaoRestApi);
    private const string BASE_URL = "https://dapi.kakao.com/v2/local/search/category.json";
    private const string KEYWORD_URL = "https://dapi.kakao.com/v2/local/search/keyword.json";

    // 검색할 카테고리 코드 목록 (앱의 4개 분류에 맞춤)
    // CE7: 카페, PM9: 약국, FD6: 음식점, CS2: 편의점
    private readonly string[] categoryGroups = { "CE7", "PM9", "FD6", "CS2" };

    public KakaoDocument SelectedPlace { get; private set; }

    // 변경점: 새 사진을 인식하기 전에 이전 사진의 지점을 지운다(GPS가 없어 검색을 건너뛸 때 이전 지점이 남지 않게)
    public void ClearSelection() => SelectedPlace = null;

    public static bool IsMegaCoffeeOcr(string ocrText, string matchedKeyword)
    {
        if (LatestMenuController.IsMegaCoffee(matchedKeyword)) return true;
        if (string.IsNullOrWhiteSpace(ocrText)) return false;

        string normalized = Regex.Replace(ocrText.ToUpperInvariant(), @"\s+", "");
        return normalized.Contains("메가MGC커피") || normalized.Contains("메가커피") ||
               normalized.Contains("MEGAMGCCOFFEE") || normalized.Contains("MEGACOFFEE") ||
               Regex.IsMatch(ocrText, @"(?<![A-Z])MEGA(?![A-Z])", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(ocrText, @"(?<![가-힣])메가(?![가-힣])");
    }

    public static string GetRecognizedCoffeeBrand(string ocrText, string matchedKeyword)
    {
        if (IsMegaCoffeeOcr(ocrText, matchedKeyword)) return LatestMenuController.MegaCoffeeBrand;
        if (LatestMenuController.IsTenPercentCoffee(matchedKeyword) ||
            LatestMenuController.IsTenPercentCoffee(ocrText))
            return LatestMenuController.TenPercentCoffeeBrand;
        return null;
    }

    /// <summary>
    /// 좌표 기반으로 반경 1000m 내 장소를 검색하고 KeywordMatch 형태로 반환합니다.
    /// </summary>
    /// <param name="lat">위도</param>
    /// <param name="lon">경도</param>
    /// <param name="onSuccess">검색 성공 시 KeywordMatch 반환</param>
    /// <param name="onFail">검색 실패 시 호출</param>
    public IEnumerator SearchByCoords(double lat, double lon,
                                      Action<KeywordMatch> onSuccess,
                                      Action onFail,
                                      string recognizedBrand = null)
    {
        SelectedPlace = null;
        bool hasRecognizedBrand = recognizedBrand == LatestMenuController.MegaCoffeeBrand ||
                                  recognizedBrand == LatestMenuController.TenPercentCoffeeBrand;
        Debug.Log(hasRecognizedBrand ? $"[PlaceSearch] OCR 브랜드 = {recognizedBrand}" :
                                      "[PlaceSearch] 브랜드 없음 - 기존 주변 검색 사용");

        // 변경점: 키가 없으면 요청하지 않고 실패 처리 (401 반복 방지)
        string apiKey = RestApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[KakaoAPI] .env에 KAKAO_REST_API_KEY가 없어 좌표 검색을 건너뜁니다.");
            onFail?.Invoke();
            yield break;
        }

        if (hasRecognizedBrand)
        {
            yield return StartCoroutine(SearchBrandCoffee(lat, lon, apiKey, recognizedBrand, onSuccess, onFail));
            yield break;
        }

        KeywordMatch bestMatch = null;
        float bestDistance = float.MaxValue;

        foreach (var code in categoryGroups)
        {
            // 좌표(x=경도, y=위도), 반경 1000m, 거리순 정렬
            string url = $"{BASE_URL}?category_group_code={code}" +
                         $"&x={lon}&y={lat}&radius=1000&sort=distance";

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
                    SelectedPlace = nearest;
                    bestMatch = new KeywordMatch(
                        appCategory,
                        GetDescription(appCategory),
                        GetImageName(appCategory),
                        nearest.place_name
                    );
                    Debug.Log($"[KakaoAPI] 후보 발견: {nearest.place_name} | {appCategory} | {dist}m");
                }
            }
        }

        if (bestMatch != null)
        {
            Debug.Log($"[KakaoAPI] 최종 선택: {bestMatch.Category} ({bestDistance}m)");
            Debug.Log($"[KakaoLocal] SelectedPlace = {SelectedPlace.place_name}");
            Debug.Log($"[KakaoLocal] x = {SelectedPlace.x}");
            Debug.Log($"[KakaoLocal] y = {SelectedPlace.y}");
            onSuccess?.Invoke(bestMatch);
        }
        else
        {
            Debug.Log("[KakaoAPI] 반경 1000m 내 해당하는 장소 없음.");
            onFail?.Invoke();
        }
    }

    private IEnumerator SearchBrandCoffee(double lat, double lon, string apiKey, string recognizedBrand,
                                          Action<KeywordMatch> onSuccess, Action onFail)
    {
        Debug.Log("[PlaceSearch] 브랜드 우선 검색 실행");
        string searchKeyword = recognizedBrand == LatestMenuController.TenPercentCoffeeBrand
            ? "텐퍼센트커피" : "메가MGC커피";

        KakaoDocument nearest = null;
        yield return StartCoroutine(RequestNearestBrand(lat, lon, apiKey, searchKeyword, recognizedBrand,
                                                        BrandRadiusMeters, found => nearest = found));

        // 변경점: 1km 안에 없으면 거리 제한 없이 가장 가까운 지점을 쓴다.
        //   매장 앞이 아닌 곳(시연장 등)에서 간판 사진을 찍어도 결과 화면 지도에 내 위치와 지점이 나오게.
        if (nearest == null)
        {
            Debug.LogWarning($"[PlaceSearch] 1km 내 {recognizedBrand} 카페 후보 없음 - 거리 제한 없이 가장 가까운 지점 검색");
            yield return StartCoroutine(RequestNearestBrand(lat, lon, apiKey, searchKeyword, recognizedBrand,
                                                            0, found => nearest = found));
        }

        if (nearest == null)
        {
            Debug.LogWarning($"[PlaceSearch] {recognizedBrand} 카페 후보 없음 - OCR 결과 사용");
            onFail?.Invoke();
            yield break;
        }

        SelectedPlace = nearest;
        Debug.Log($"[PlaceSearch] 최종 선택 = {nearest.place_name}, distance = {nearest.distance}m");
        Debug.Log($"[KakaoLocal] SelectedPlace = {nearest.place_name}");
        Debug.Log($"[KakaoLocal] x = {nearest.x}");
        Debug.Log($"[KakaoLocal] y = {nearest.y}");
        onSuccess?.Invoke(new KeywordMatch("카페", GetDescription("카페"),
                                          GetImageName("카페"), nearest.place_name));
    }

    // 변경점: 브랜드 카페 키워드 검색 한 번. radius가 0이면 거리 제한 없이(카카오가 가까운 순으로 정렬) 찾는다.
    private IEnumerator RequestNearestBrand(double lat, double lon, string apiKey, string searchKeyword,
                                            string recognizedBrand, int radius, Action<KakaoDocument> onFound)
    {
        string url = KEYWORD_URL + "?query=" + UnityWebRequest.EscapeURL(searchKeyword) +
                     "&category_group_code=CE7" +
                     "&x=" + lon.ToString("R", CultureInfo.InvariantCulture) +
                     "&y=" + lat.ToString("R", CultureInfo.InvariantCulture) +
                     (radius > 0 ? "&radius=" + radius : string.Empty) +
                     "&sort=distance&size=15";

        using var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", $"KakaoAK {apiKey}");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[PlaceSearch] 브랜드 검색 실패: HTTP {request.responseCode}, {request.error}");
            yield break;
        }

        var response = JsonUtility.FromJson<KakaoResponse>(request.downloadHandler.text);
        KakaoDocument nearest = null;
        float nearestDistance = float.MaxValue;
        if (response?.documents != null)
        {
            foreach (var candidate in response.documents)
            {
                if (candidate == null || candidate.category_group_code != "CE7" ||
                    string.IsNullOrWhiteSpace(candidate.place_name)) continue;

                string name = candidate.place_name.ToUpperInvariant();
                bool matchesBrand = recognizedBrand == LatestMenuController.TenPercentCoffeeBrand
                    ? LatestMenuController.IsTenPercentCoffee(candidate.place_name)
                    : name.Contains("메가MGC") || name.Contains("메가커피") || name.Contains("MEGA");
                if (!matchesBrand) continue;

                if (!float.TryParse(candidate.distance, NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out float distance) ||
                    float.IsNaN(distance) || distance < 0f || (radius > 0 && distance > radius)) continue;

                Debug.Log($"[PlaceSearch] 후보 = {candidate.place_name}, distance = {candidate.distance}");
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
        }
        onFound?.Invoke(nearest);
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
    public string category_name;        // 상세 카테고리명
    public string x;                    // 경도
    public string y;                    // 위도
    public string category_group_code;  // 카테고리 코드 (예: "CE7")
    public string category_group_name;  // 카테고리명 (예: "카페")
    public string distance;             // 거리 (미터 단위, 예: "23")
}
