using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Artti.AAC;

namespace Artti.Training
{
    public class GeminiDialogueService
    {
        private string _apiKey;
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public GeminiDialogueService(string apiKey)
        {
            _apiKey = apiKey;
        }

        // 실패/키없음 시 null 리턴 — caller가 FallbackResponsePicker로 대체
        public async UniTask<(DialogueTool tool, string npcText, string[] args)?> RequestNextTurnAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[Gemini] API key missing — caller should use fallback");
                return null;
            }

            // PLAN.MD 5.2.2 / 5.5.4 - Gemini function calling implementation
            // JsonUtility는 익명 타입 직렬화 불가 → [Serializable] 클래스 사용
            var requestBody = new GeminiRequest
            {
                system_instruction = new GeminiContent { parts = new[] { new GeminiPart { text = systemPrompt ?? string.Empty } } },
                contents = new[] { new GeminiContent { parts = new[] { new GeminiPart { text = userPrompt ?? string.Empty } } } }
            };

            string jsonBody = JsonUtility.ToJson(requestBody);

            using (var request = new UnityWebRequest($"{ApiUrl}?key={_apiKey}", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await request.SendWebRequest().WithCancellation(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e)
                {
                    Debug.LogError($"[Gemini] HTTP {(int)e.ResponseCode}: {e.Text}");
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Gemini] API Error: {request.error}\n{request.downloadHandler.text}");
                    return null;
                }

                // TODO: 응답 파싱 (function calling, candidates[0].content.parts[].text 등) - 현재는 mock
                return (DialogueTool.PresentCards, "네, 알겠습니다. 여기 카드들이에요.", new[] { "card_01", "card_02" });
            }
        }

        [Serializable] class GeminiRequest { public GeminiContent system_instruction; public GeminiContent[] contents; }
        [Serializable] class GeminiContent { public GeminiPart[] parts; }
        [Serializable] class GeminiPart    { public string text; }
    }
}
