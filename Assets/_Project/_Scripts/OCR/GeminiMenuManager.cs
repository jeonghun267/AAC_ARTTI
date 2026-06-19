using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiMenuManager : MonoBehaviour
{
    [Header("API 설정")]
    [SerializeField] private string apiKey = "..."; // 여기에 키 입력

    [System.Serializable]
    public class DynamicListWrapper { public List<string> options; }

    // Gemini 응답 구조를 그대로 본뜬 파싱용 클래스들
    [System.Serializable] private class GeminiResponse { public Candidate[] candidates; }
    [System.Serializable] private class Candidate { public Content content; }
    [System.Serializable] private class Content { public Part[] parts; }
    [System.Serializable] private class Part { public string text; }

    public void GenerateList(string prompt, System.Action<List<string>> onComplete)
    {
        StartCoroutine(RequestGeminiList(prompt, onComplete));
    }

    private IEnumerator RequestGeminiList(string prompt, System.Action<List<string>> onComplete)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={apiKey}";

        // 역슬래시 먼저, 그다음 따옴표/줄바꿈 순서로 이스케이프
        string safePrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");

        // responseMimeType 으로 JSON 출력을 강제 → 응답이 깔끔해짐
        string jsonPayload =
            "{\"contents\":[{\"parts\":[{\"text\":\"" + safePrompt + "\"}]}]," +
            "\"generationConfig\":{\"responseMimeType\":\"application/json\"}}";

        byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(postData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            // 통신 자체가 실패한 경우 (429 포함)
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Gemini 통신 에러] 코드: {request.responseCode}\n상세: {request.downloadHandler.text}");
                onComplete?.Invoke(null);
                yield break;
            }

            string rawResponse = request.downloadHandler.text;
            Debug.Log($"[Gemini 원문 응답]\n{rawResponse}");

            List<string> result = ParseOptions(rawResponse);

            // 성공이든 실패든 콜백은 반드시 호출 → 더 이상 조용히 멈추지 않음
            onComplete?.Invoke(result);
        }
    }

    private List<string> ParseOptions(string rawResponse)
    {
        try
        {
            // 1) 전체 응답에서 모델이 생성한 텍스트만 꺼냄
            GeminiResponse resp = JsonUtility.FromJson<GeminiResponse>(rawResponse);
            if (resp == null || resp.candidates == null || resp.candidates.Length == 0)
            {
                Debug.LogError("[Gemini] candidates 없음 (안전필터 차단 등 가능). 원문 확인 필요");
                return null;
            }

            string innerText = resp.candidates[0].content.parts[0].text;

            // 2) 혹시 모를 코드블록 ```json ... ``` 제거
            innerText = innerText.Replace("```json", "").Replace("```", "").Trim();

            // 3) 꺼낸 텍스트를 {"options":[...]} 형태로 파싱
            DynamicListWrapper parsed = JsonUtility.FromJson<DynamicListWrapper>(innerText);
            return parsed != null ? parsed.options : null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Gemini] 파싱 실패: {e.Message}");
            return null;
        }
    }
}