using System.Collections;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// GPS 좌표 수집 및 관리
/// </summary>
public class GPSManager : MonoBehaviour
{
    public static double Latitude { get; private set; }
    public static double Longitude { get; private set; }
    public static bool IsReady { get; private set; }
    public static string StatusMessage { get; private set; } = "GPS 초기화 중...";

    private void Start()
    {
        StartCoroutine(RequestPermissionAndStartGPS());
    }

    private IEnumerator RequestPermissionAndStartGPS()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            StatusMessage = "GPS 권한 요청 중...";
            Debug.Log("[GPS] 위치 권한 요청 중...");

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += (permissionName) =>
            {
                Debug.Log("[GPS] 위치 권한 허용됨!");
            };
            callbacks.PermissionDenied += (permissionName) =>
            {
                StatusMessage = "GPS 권한 거부됨";
                Debug.LogWarning("[GPS] 위치 권한 거부됨.");
            };

            Permission.RequestUserPermission(Permission.FineLocation, callbacks);

            float waited = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && waited < 10f)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            StatusMessage = "⚠️ GPS 권한 없음";
            Debug.LogWarning("[GPS] 위치 권한이 없어 GPS를 시작할 수 없습니다.");
            yield break;
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            StatusMessage = "⚠️ 위치 서비스 꺼져있음";
            Debug.LogWarning("[GPS] 기기의 위치 서비스가 꺼져있습니다.");
            yield break;
        }

        Input.location.Start(10f, 5f);
        StatusMessage = "GPS 시작 중...";
        Debug.Log("[GPS] 위치 서비스 시작 중...");

        int timeout = 15;
        while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
        {
            yield return new WaitForSeconds(1f);
            timeout--;
            StatusMessage = $"GPS 초기화 중... ({16 - timeout}/15초)";
            Debug.Log($"[GPS] 초기화 대기 중... ({16 - timeout}/15초)");
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            StatusMessage = $"⚠️ GPS 시작 실패 ({Input.location.status})";
            Debug.LogError($"[GPS] GPS 시작 실패. 상태: {Input.location.status}");
            yield break;
        }

        IsReady = true;
        Latitude = Input.location.lastData.latitude;
        Longitude = Input.location.lastData.longitude;
        StatusMessage = $"✅ GPS 준비완료";
        Debug.Log($"[GPS] 준비완료 → 위도: {Latitude:F6}, 경도: {Longitude:F6}");
    }

    /// <summary>
    /// 사진 촬영 직전에 호출하여 최신 좌표로 갱신합니다.
    /// </summary>
    public static void UpdateCoords()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[GPS] GPS가 아직 준비되지 않았습니다.");
            return;
        }

        Latitude = Input.location.lastData.latitude;
        Longitude = Input.location.lastData.longitude;
        Debug.Log($"[GPS] 좌표 갱신 → 위도: {Latitude:F6}, 경도: {Longitude:F6}");
    }
}