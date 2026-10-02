using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Artti.Common;

/// <summary>ResultPanel의 사용자/선택 매장 위치를 NAVER Static Map으로 표시합니다.</summary>
public class NaverStaticMapController : MonoBehaviour
{
    private const string StaticMapUrl = "https://maps.apigw.ntruss.com/map-static/v2/raster";

    // NAVER Maps ID-KEY 인증
    // 변경점: 키는 .env의 NAVER_CLIENT_ID / NAVER_CLIENT_SECRET에서 읽는다(빌드는 Resources/api_keys.txt, ApiKeyLoader).
    //   Inspector 필드는 씬에 저장돼 공개 저장소에 올라가므로 없앴다.
    private static string naverClientId => ApiKeyLoader.Get(ApiKeyLoader.NaverClientId);
    private static string naverClientSecret => ApiKeyLoader.Get(ApiKeyLoader.NaverClientSecret);

    [Header("ResultPanel / CenterMapCard")]
    [SerializeField] private RawImage mapImage;
    [SerializeField] private GameObject mapPlaceholder;
    [SerializeField] private TMP_Text latitudeText;
    [SerializeField] private TMP_Text longitudeText;

    private Coroutine mapRequest;
    private Texture2D loadedTexture;

    public void UpdateMap(double userLatitude, double userLongitude,
                          double storeLatitude, double storeLongitude, string placeName)
    {
        Debug.Log("[NaverMap] UpdateMap 호출됨");
        Debug.Log($"[NaverMap] 사용자 위치: {userLatitude:F6}, {userLongitude:F6}");
        Debug.Log($"[NaverMap] 매장 위치: {storeLatitude:F6}, {storeLongitude:F6}");
        Debug.Log($"[NaverMap] 매장명: {placeName}");
        bool hasClientId = !string.IsNullOrWhiteSpace(naverClientId);
        bool hasClientSecret = !string.IsNullOrWhiteSpace(naverClientSecret);
        Debug.Log($"[NaverMap] Client ID 설정됨 = {hasClientId}");
        Debug.Log($"[NaverMap] Client Secret/API Key 설정됨 = {hasClientSecret}");

        ShowPlaceholder();
        ShowUserCoordinates(userLatitude, userLongitude);

        if (!IsCoordinateValid(userLatitude, userLongitude) ||
            !IsCoordinateValid(storeLatitude, storeLongitude))
        {
            Debug.LogWarning("[NaverMap] GPS 또는 매장 좌표가 유효하지 않아 요청 생략");
            return;
        }

        if (mapImage == null || !hasClientId || !hasClientSecret)
        {
            Debug.LogWarning("[NaverMap] 지도 요청 실패: MapImage 또는 NAVER 인증 정보가 없습니다.");
            return;
        }

        var (width, height) = MapSize();
        string url = BuildUrl(userLatitude, userLongitude, storeLatitude, storeLongitude,
                              placeName, width, height);
        Debug.Log("[NaverMap] 지도 요청 시작");
        mapRequest = StartCoroutine(DownloadMap(url));
    }

    // 변경점: 매장 좌표가 없을 때(카카오 검색 실패 등) 내 위치만 가운데 두고 지도를 보여 준다
    public void UpdateUserOnlyMap(double userLatitude, double userLongitude)
    {
        Debug.Log($"[NaverMap] 내 위치만 표시: {userLatitude:F6}, {userLongitude:F6}");
        ShowPlaceholder();
        ShowUserCoordinates(userLatitude, userLongitude);
        if (!IsCoordinateValid(userLatitude, userLongitude)) return;
        if (mapImage == null || string.IsNullOrWhiteSpace(naverClientId) || string.IsNullOrWhiteSpace(naverClientSecret))
        {
            Debug.LogWarning("[NaverMap] 지도 요청 실패: MapImage 또는 NAVER 인증 정보가 없습니다.");
            return;
        }

        var (width, height) = MapSize();
        string url = StaticMapUrl + $"?w={width}&h={height}&format=png&scale=2&lang=ko&level={UserOnlyLevel}" +
                     "&center=" + userLongitude.ToString("R", CultureInfo.InvariantCulture) + "," +
                     userLatitude.ToString("R", CultureInfo.InvariantCulture) +
                     "&markers=" + UnityWebRequest.EscapeURL(UserMarker(userLatitude, userLongitude));
        mapRequest = StartCoroutine(DownloadMap(url));
    }

    private const int UserOnlyLevel = 16;   // 내 위치만 보일 때 확대 단계(동네 골목 정도)

    private (int width, int height) MapSize()
    {
        Rect imageRect = mapImage.rectTransform.rect;
        int width = Mathf.Clamp(Mathf.RoundToInt(imageRect.width), 1, 1024);
        int height = Mathf.Clamp(Mathf.RoundToInt(imageRect.height), 1, 1024);
        if (width == 1 || height == 1) { width = 600; height = 420; }
        return (width, height);
    }

    private static string UserMarker(double latitude, double longitude) =>
        "type:t|size:mid|color:Blue|pos:" +
        longitude.ToString("R", CultureInfo.InvariantCulture) + " " +
        latitude.ToString("R", CultureInfo.InvariantCulture) + "|label:내 위치";

    public void ShowPlaceholder()
    {
        if (mapRequest != null)
        {
            StopCoroutine(mapRequest);
            mapRequest = null;
        }

        if (mapImage != null)
        {
            mapImage.texture = null;
            mapImage.gameObject.SetActive(false);
        }
        if (loadedTexture != null)
        {
            Destroy(loadedTexture);
            loadedTexture = null;
        }
        if (mapPlaceholder != null)
            mapPlaceholder.SetActive(true);

        if (latitudeText != null) latitudeText.text = "위도: --";
        if (longitudeText != null) longitudeText.text = "경도: --";
    }

    public void ShowLocationStatus(string status)
    {
        ShowPlaceholder();
        if (latitudeText != null) latitudeText.text = "위도: " + status;
        if (longitudeText != null) longitudeText.text = "경도: " + status;
    }

    public void ShowUserCoordinates(double latitude, double longitude)
    {
        if (!IsCoordinateValid(latitude, longitude)) return;
        if (latitudeText != null)
            latitudeText.text = $"위도: {latitude.ToString("F6", CultureInfo.InvariantCulture)}";
        if (longitudeText != null)
            longitudeText.text = $"경도: {longitude.ToString("F6", CultureInfo.InvariantCulture)}";
    }

    private void OnDestroy()
    {
        if (loadedTexture != null)
            Destroy(loadedTexture);
    }

    public static bool IsCoordinateValid(double latitude, double longitude)
    {
        return !double.IsNaN(latitude) && !double.IsInfinity(latitude) &&
               !double.IsNaN(longitude) && !double.IsInfinity(longitude) &&
               latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180 &&
               latitude != 0 && longitude != 0;
    }

    private static string BuildUrl(double userLatitude, double userLongitude,
                                   double storeLatitude, double storeLongitude, string placeName,
                                   int width, int height)
    {
        // NAVER 문서: markers만 지정하고 center/level을 생략하면 두 마커를 담도록 자동 조정됩니다.
        string userMarker = UserMarker(userLatitude, userLongitude);
        string storeMarker = "type:t|size:mid|color:Red|pos:" +
            storeLongitude.ToString("R", CultureInfo.InvariantCulture) + " " +
            storeLatitude.ToString("R", CultureInfo.InvariantCulture) + "|label:" +
            (string.IsNullOrWhiteSpace(placeName) ? "매장" : placeName);

        return StaticMapUrl + $"?w={width}&h={height}&format=png&scale=2&lang=ko" +
               "&markers=" + UnityWebRequest.EscapeURL(userMarker) +
               "&markers=" + UnityWebRequest.EscapeURL(storeMarker);
    }

    private IEnumerator DownloadMap(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            request.SetRequestHeader("x-ncp-apigw-api-key-id", naverClientId);
            request.SetRequestHeader("x-ncp-apigw-api-key", naverClientSecret);
            yield return request.SendWebRequest();

            Debug.Log($"[NaverMap] HTTP Code = {request.responseCode}");
            if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
            {
                Debug.LogWarning($"[NaverMap] Result = {request.result}");
                Debug.LogWarning($"[NaverMap] Error = {request.error}");
                Debug.LogWarning($"[NaverMap] Response = {GetSafeErrorResponse(request)}");
            }
            else
            {
                Texture2D texture = null;
                try { texture = DownloadHandlerTexture.GetContent(request); }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[NaverMap] 이미지 디코딩 실패: {exception.GetType().Name}");
                }
                if (texture == null)
                {
                    Debug.LogWarning("[NaverMap] 지도 요청 실패: 이미지 데이터가 없습니다.");
                }
                else
                {
                    loadedTexture = texture;
                    mapImage.texture = texture;
                    mapImage.gameObject.SetActive(true);
                    if (mapPlaceholder != null) mapPlaceholder.SetActive(false);
                    Debug.Log("[NaverMap] 지도 이미지 표시 완료");
                }
            }
        }
        mapRequest = null;
    }

    private string GetSafeErrorResponse(UnityWebRequest request)
    {
        string body;
        try { body = request.downloadHandler?.text ?? "(empty)"; }
        catch (System.Exception exception)
        {
            return $"(응답 본문 읽기 실패: {exception.GetType().Name})";
        }

        if (!string.IsNullOrEmpty(naverClientId))
            body = body.Replace(naverClientId, "[REDACTED]");
        if (!string.IsNullOrEmpty(naverClientSecret))
            body = body.Replace(naverClientSecret, "[REDACTED]");
        return body.Length > 2000 ? body.Substring(0, 2000) + "..." : body;
    }
}
