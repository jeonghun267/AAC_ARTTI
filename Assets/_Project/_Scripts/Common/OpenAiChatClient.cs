using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;        // CLAUDE.md: LLM 응답 파싱은 Newtonsoft 사용
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Artti.Common
{
    // OpenAI Chat Completions 최소 클라이언트(JSON 응답). MonoBehaviour 아님 - 외부 API 연동만.
    // 변경점(2026-10-02): .env의 GEMINI_API_KEY가 유출 키로 막혀서 사용자가 OpenAI 키를 줬다. 모델은 .env OPENAI_MODEL(비우면 DefaultModel)
    public class OpenAiChatClient
    {
        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
        // 낮은 temperature와 JSON 모드를 함께 쓸 수 있는 빠르고 싼 모델(같은 대화면 비슷한 점수)
        public const string DefaultModel = "gpt-4.1-mini";
        private const int TimeoutSeconds = 20;

        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiChatClient(string apiKey, string model = null)
        {
            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        }

        public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
        public string Model => _model;

        public static OpenAiChatClient FromEnv() =>
            new OpenAiChatClient(ApiKeyLoader.Get(ApiKeyLoader.OpenAiApi), ApiKeyLoader.Get(ApiKeyLoader.OpenAiModel));

        // system/user 프롬프트를 보내 JSON 문자열(메시지 내용)을 받는다. 키 없음·실패면 null. 취소는 그대로 던진다.
        //   JSON 모드는 프롬프트에 'JSON'이라는 말이 있어야 한다
        public async UniTask<string> CompleteJsonAsync(string system, string user, float temperature, int maxTokens, CancellationToken ct = default)
        {
            if (!IsAvailable) return null;
            var body = new JObject
            {
                ["model"] = _model,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = system },
                    new JObject { ["role"] = "user", ["content"] = user },
                },
                ["temperature"] = temperature,
                ["max_tokens"] = maxTokens,
                ["response_format"] = new JObject { ["type"] = "json_object" },
            };
            using (var request = new UnityWebRequest(ApiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
                request.timeout = TimeoutSeconds;
                try { await request.SendWebRequest().WithCancellation(ct); }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e)
                {
                    Debug.LogWarning($"[OpenAI] HTTP {(int)e.ResponseCode}: {Trim(e.Text)}");
                    return null;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[OpenAI] 실패: {request.error}");
                    return null;
                }
                return ParseContent(request.downloadHandler.text);
            }
        }

        // {"choices":[{"message":{"content":"..."}}]} -> content
        public static string ParseContent(string responseJson)
        {
            try
            {
                var content = JObject.Parse(responseJson)["choices"]?[0]?["message"]?["content"]?.ToString();
                return string.IsNullOrWhiteSpace(content) ? null : content;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenAI] 응답 파싱 실패: {e.Message}");
                return null;
            }
        }

        static string Trim(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) : s);
    }
}
