using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class GeminiMenuGenerator : MonoBehaviour
{
    [Header("API 설정")]
    public string apiKey = "여기에_복사한_API_KEY를_넣으세요";

    [Header("UI 연결")]
    public Transform contentArea;      // 카드가 생성될 부모 (Scroll View의 Content)
    public GameObject menuCardPrefab;  // 아까 폴더에 저장한 프리팹

    // JSON 파싱을 위한 데이터 구조 클래스들
    [System.Serializable]
    private class GeminiRequest { public List<Content> contents; }
    [System.Serializable]
    private class Content { public List<Part> parts; }
    [System.Serializable]
    private class Part { public string text; }

    [System.Serializable]
    private class GeminiResponse { public List<Candidate> candidates; }
    [System.Serializable]
    private class Candidate { public Content content; }

    [System.Serializable]
    private class MenuData { public string[] menus; } // 최종 메뉴 리스트

    // 이 함수를 실행하면 메뉴를 불러옵니다.
    public void RequestMenuToGemini(string cafeName)
    {
        StartCoroutine(SendRequest(cafeName));
    }

    private IEnumerator SendRequest(string cafeName)
    {
        // 1. 기존에 생성된 카드가 있다면 전부 삭제 (초기화)
        foreach (Transform child in contentArea)
        {
            Destroy(child.gameObject);
        }

        // 2. Gemini에게 보낼 프롬프트 작성 (JSON 형식으로만 답하도록 강제)
        string prompt = $"너는 AAC 소통 앱의 AI야. 사용자가 지금 '{cafeName}'에 있어. " +
                        "이 카페의 가장 대중적이고 인기 있는 메뉴 5개를 골라서, " +
                        "반드시 아래 JSON 형식으로만 대답해. 다른 말은 절대 하지마.\n" +
                        "{\"menus\": [\"메뉴1\", \"메뉴2\", \"메뉴3\", \"메뉴4\", \"메뉴5\"]}";

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

        // 3. 요청 데이터 세팅
        GeminiRequest requestData = new GeminiRequest
        {
            contents = new List<Content> { new Content { parts = new List<Part> { new Part { text = prompt } } } }
        };
        string jsonData = JsonUtility.ToJson(requestData);

        // 4. 통신 시작
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 5. 응답받은 데이터 처리
                GeminiResponse response = JsonUtility.FromJson<GeminiResponse>(request.downloadHandler.text);
                string rawJsonText = response.candidates[0].content.parts[0].text;

                // 마크다운(```json) 찌꺼기 제거
                rawJsonText = rawJsonText.Replace("```json", "").Replace("```", "").Trim();

                // 6. JSON을 배열로 변환하고 화면에 카드 생성!
                MenuData finalData = JsonUtility.FromJson<MenuData>(rawJsonText);

                foreach (string menuName in finalData.menus)
                {
                    // 프리팹 복사해서 Content 아래에 붙이기
                    GameObject newCard = Instantiate(menuCardPrefab, contentArea);

                    // 카드 안의 텍스트 찾아서 메뉴 이름 넣기
                    TextMeshProUGUI cardText = newCard.GetComponentInChildren<TextMeshProUGUI>();
                    if (cardText != null)
                    {
                        cardText.text = menuName + " 주문하고 싶어요.";
                    }
                }
            }
            else
            {
                Debug.LogError("Gemini API 통신 에러: " + request.error);
            }
        }
    }
}