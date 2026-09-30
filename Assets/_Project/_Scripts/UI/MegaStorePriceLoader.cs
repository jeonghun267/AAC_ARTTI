using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class StorePriceResponse
{
    public string brand;
    public string storeName;
    public string updatedAt;
    public StoreMenuPrice[] menus;
}

[Serializable]
public sealed class StoreMenuPrice
{
    public string menuName;
    public string price;
}

[DisallowMultipleComponent]
public sealed class MegaStorePriceLoader : MonoBehaviour
{
    [SerializeField] private string priceApiBaseUrl;
    [SerializeField] private LatestMenuController latestMenuController;

    private Coroutine _loadCoroutine;

    public void LoadPricesForStore(string storeName)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(priceApiBaseUrl))
        {
            Debug.LogWarning("[StorePrice] priceApiBaseUrl이 설정되지 않았습니다.");
            return;
        }

        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
        }

        _loadCoroutine = StartCoroutine(LoadPricesCoroutine(storeName.Trim()));
    }

    private IEnumerator LoadPricesCoroutine(string storeName)
    {
        Debug.Log($"[StorePrice] 가격 조회 시작: {storeName}");

        string separator = priceApiBaseUrl.Contains("?") ? "&" : "?";
        string requestUrl = priceApiBaseUrl.Trim() + separator
            + "storeName=" + UnityWebRequest.EscapeURL(storeName);

        using (UnityWebRequest request = UnityWebRequest.Get(requestUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[StorePrice] 가격 조회 실패: {request.error}");
                _loadCoroutine = null;
                yield break;
            }

            StorePriceResponse response;
            try
            {
                response = JsonUtility.FromJson<StorePriceResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[StorePrice] 가격 조회 실패: JSON 파싱 오류 ({exception.Message})");
                _loadCoroutine = null;
                yield break;
            }

            if (response == null || response.menus == null || response.menus.Length == 0)
            {
                Debug.LogWarning("[StorePrice] 가격 조회 실패: 메뉴 가격 데이터가 없습니다.");
                _loadCoroutine = null;
                yield break;
            }

            if (latestMenuController == null)
            {
                Debug.LogWarning("[StorePrice] 가격 조회 실패: LatestMenuController 참조가 없습니다.");
                _loadCoroutine = null;
                yield break;
            }

            latestMenuController.UpdatePrices(response.menus);
            Debug.Log($"[StorePrice] 가격 조회 성공: {storeName}");
        }

        _loadCoroutine = null;
    }
}
