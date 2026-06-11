using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiMenuManager : MonoBehaviour
{
    [Header("API 설정")]
    [SerializeField] private string apiKey = "여기에_발급받은_GEMINI_API_KEY_입력";

    [Header("UI 연결")]
    [SerializeField] private GameObject menuCardPrefab; // 1번에서 만든 카드 프리팹
    [SerializeField] private Transform contentParent;   // 1번에서 만든 Scroll View의 Content

    // 유니티 내장 JsonUtility를 사용하기 위한 데이터 구조체
    [System.Serializable]
    public class MenuDataWrapper
    {
        public List<string> menuList;
    }

    // ★ 이 함수를 호출하면 실행됩니다! (예: 카카오맵 검색 완료 시 혹은 주문 탭을 열 때)
    // 테스트용으로 사용하시려면 외부에서 GeminiMenuManager.GetCafeMenus("메가커피") 처럼 호출하세요.
    public void GetCafeMenus(string brandName)
    {
        StartCoroutine(RequestGeminiAPI(brandName));
    }

    private IEnumerator RequestGeminiAPI(string brandName)
    {
        // 이전 카드들이 남아있다면 청소
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

        // Gemini가 딱 유니티 데이터 형식(JSON)으로만 답하도록 엄격하게 프롬프트 작성
        string prompt = $"너는 카페 키오스크 데이터를 제공하는 AI야. 현재 매장은 '{brandName}'이야. " +
                        "이 브랜드의 가장 인기 있는 대표 메뉴 5개를 찾아서 반드시 다음 JSON 형식으로만 답변해줘. " +
                        "다른 설명이나 인사말, 주석은 절대 포함하지 마. " +
                        "형식: { \"menuList\": [\"메뉴1\", \"메뉴2\", \"메뉴3\"] }";

        // JSON 데이터 바디 구성
        string jsonPayload = "{\"contents\":[{\"parts\":[{\"text\":\"" + prompt + "\"}]}]}";
        byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(postData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawResponse = request.downloadHandler.text;

                // Gemini의 복잡한 응답에서 우리가 요청한 순수 JSON 문자열만 추출하는 가공 과정
                string cleanJson = ParseGeminiResponse(rawResponse);

                try
                {
                    // JSON 문자열을 C# 리스트 데이터로 변환
                    MenuDataWrapper parsedData = JsonUtility.FromJson<MenuDataWrapper>(cleanJson);

                    // 받아온 메뉴 개수만큼 카드 생성하여 화면에 배치!
                    foreach (string menuName in parsedData.menuList)
                    {
                        GameObject newCard = Instantiate(menuCardPrefab, contentParent);
                        MenuCard cardScript = newCard.GetComponent<MenuCard>();
                        if (cardScript != null)
                        {
                            cardScript.SetupMenu(menuName);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("JSON 파싱 실패. 제미나이가 형식을 지키지 않았을 수 있습니다: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Gemini API 통신 실패: " + request.error);
            }
        }
    }

    // Gemini 응답 구조에서 실제 텍스트 내부 내용만 꺼내는 헬퍼 함수
    private string ParseGeminiResponse(string rawJson)
    {
        // 에디터 환경 등에서 유연하게 텍스트 내용만 자르기 위한 단순 문자열 처리
        int startIndex = rawJson.IndexOf("{\\n  \\\"menuList\\\"");
        if (startIndex == -1) startIndex = rawJson.IndexOf("{\"menuList\"");

        if (startIndex != -1)
        {
            int endIndex = rawJson.IndexOf("}", startIndex);
            string result = rawJson.Substring(startIndex, endIndex - startIndex + 1);
            // 역슬래시나 줄바꿈 기호 복원
            result = result.Replace("\\n", "").Replace("\\\"", "\"");
            return result;
        }
        return rawJson;
    }
}