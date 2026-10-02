using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;        // CLAUDE.md: LLM 응답 파싱은 Newtonsoft 사용
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Artti.Common;

namespace Artti.Report
{
    // 대화 한 번에 대한 AI 평가(기록에 SessionEvaluated 이벤트로 저장. 작은 페이로드라 JsonUtility로 직렬화)
    [Serializable]
    public class ConversationEvaluation
    {
        public int appropriateness = -1;   // 응답 적절성 0~100: 점원 질문·상황에 맞는 대답이었는지
        public int naturalness = -1;       // 대화 자연스러움 0~100: 실제로 한 말이 편의점에서 자연스러운 표현인지
        public string praise;              // 잘한 점 한 문장
        public string tip;                 // 다음에 해 볼 점 한 문장

        public bool IsValid => appropriateness >= 0 && naturalness >= 0;
    }

    // 대화 모드 한 번이 끝나면 AI가 대화 전체를 읽고 한 번만 채점한다(사용자 결정, 2026-10-02).
    //   Gemini(.env GEMINI_API_KEY)가 먼저, 실패하면 OpenAI(.env OPENAI_API_KEY가 있을 때).
    // MonoBehaviour 아님 - 외부 API 연동. 키 없음/오프라인/실패/이상한 응답이면 null(리포트는 기존 계산으로 보여 준다).
    public class ConversationEvaluationService
    {
        // 변경점: 모델·주소는 GeminiConfig에서
        private const int TimeoutSeconds = 20;
        private const int MaxPraiseLength = 40;

        // 발달장애 사용자 대상이라 너그럽게: 뜻이 통하면 높게, 인식 오류로 보이는 글자 틀림은 감점하지 않는다
        private const string SystemPrompt =
            "당신은 발달장애인이 편의점 대화를 연습하는 앱의 너그러운 평가자입니다. " +
            "아래는 점원과 사용자가 나눈 대화 기록입니다. 사용자의 말만 평가하세요.\n" +
            "- appropriateness(응답 적절성, 0~100 정수): 사용자의 말이 바로 앞 점원의 질문이나 상황에 맞는 대답이었는지. 짧아도 뜻이 통하면 높게.\n" +
            "- naturalness(대화 자연스러움, 0~100 정수): 사용자가 실제로 한 말이 편의점에서 자연스러운 표현과 말 흐름인지. " +
            "음성 인식 오류로 보이는 글자 틀림은 감점하지 말고 표현과 흐름만 보세요. 카드로 고른 말은 상황에 맞게 골랐는지로 보세요.\n" +
            "기준: 대체로 통했으면 70 이상, 아주 잘했으면 90 이상. 엄하게 매기지 마세요.\n" +
            // 변경점: 엇나간 대화에서 praise에 아쉬운 점을 쓴 적이 있어 칭찬만 쓰게, tip은 대화 속 장면을 들게
            "- praise: 잘한 점 한 문장(존댓말, 25자 이내, 대화 내용을 들어 구체적으로). 반드시 칭찬만 쓰고 아쉬운 점은 쓰지 마세요. " +
            "잘 맞은 대답이 없으면 끝까지 대화에 참여한 점이나 또렷하게 말한 점을 칭찬하세요.\n" +
            "- tip: 다음에 해 볼 점 한 문장(존댓말, 25자 이내, 부드럽게). 대화 속 한 장면을 들어 그때 할 수 있는 말을 알려 주세요.\n" +
            "이모지 금지. 반드시 JSON만 출력: {\"appropriateness\":0,\"naturalness\":0,\"praise\":\"\",\"tip\":\"\"}";

        private readonly OpenAiChatClient _openAi;
        private readonly string _apiKey;   // Gemini

        public ConversationEvaluationService(OpenAiChatClient openAi, string geminiKey)
        {
            _openAi = openAi;
            _apiKey = geminiKey;
        }

        public static ConversationEvaluationService FromEnv() =>
            new ConversationEvaluationService(OpenAiChatClient.FromEnv(), ApiKeyLoader.Get(ApiKeyLoader.GeminiApi));

        // 마지막 평가를 해 준 곳("Gemini" / "OpenAI" / null). 확인 메뉴 로그용
        public string LastProvider { get; private set; }

        // 변경점: 새 Gemini 키(2026-10-02)가 들어와 기획서대로 Gemini가 먼저, 실패하면 OpenAI
        public async UniTask<ConversationEvaluation> EvaluateAsync(IList<ConversationLine> lines, CancellationToken ct = default)
        {
            LastProvider = null;
            if (lines == null || !HasUserLine(lines)) return null;
            var result = await EvaluateWithGeminiAsync(lines, ct);
            if (result != null) { LastProvider = "Gemini"; return result; }
            if (_openAi == null || !_openAi.IsAvailable) return null;
            var content = await _openAi.CompleteJsonAsync(SystemPrompt, BuildTranscript(lines), 0.2f, 300, ct);
            result = content != null ? ParseResult(content) : null;
            if (result != null) LastProvider = "OpenAI";
            return result;
        }

        private async UniTask<ConversationEvaluation> EvaluateWithGeminiAsync(IList<ConversationLine> lines, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[ConversationEvaluation] API key 없음 - 평가 건너뜀");
                return null;
            }

            var body = new JObject
            {
                ["system_instruction"] = new JObject { ["parts"] = new JArray { new JObject { ["text"] = SystemPrompt } } },
                ["contents"] = new JArray
                {
                    new JObject { ["role"] = "user", ["parts"] = new JArray { new JObject { ["text"] = BuildTranscript(lines) } } }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.2,   // 같은 대화면 비슷한 점수가 나오게
                    ["maxOutputTokens"] = 300,
                    ["responseMimeType"] = "application/json",
                    // 변경점: 스키마가 없으면 [{...}] 배열로 답할 때가 있어 객체 하나로 고정
                    ["responseSchema"] = new JObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new JObject
                        {
                            ["appropriateness"] = new JObject { ["type"] = "INTEGER" },
                            ["naturalness"] = new JObject { ["type"] = "INTEGER" },
                            ["praise"] = new JObject { ["type"] = "STRING" },
                            ["tip"] = new JObject { ["type"] = "STRING" },
                        },
                        ["required"] = new JArray { "appropriateness", "naturalness", "praise", "tip" }
                    },
                    ["thinkingConfig"] = GeminiConfig.MinimalThinking()
                }
            };

            using (var request = new UnityWebRequest($"{GeminiConfig.GenerateUrl}?key={_apiKey}", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;
                try { await request.SendWebRequest().WithCancellation(ct); }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e)
                {
                    Debug.LogWarning($"[ConversationEvaluation] HTTP {(int)e.ResponseCode}(기존 계산 사용): {e.Text}");
                    return null;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ConversationEvaluation] API 실패(기존 계산 사용): {request.error}");
                    return null;
                }
                return Parse(request.downloadHandler.text);
            }
        }

        // 점원: ... / 나(목소리, 들린 말: ...): ... / 나(카드): ...
        public static string BuildTranscript(IList<ConversationLine> lines)
        {
            var sb = new StringBuilder("대화 기록:\n");
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l.text)) continue;
                if (!l.isUser) sb.Append("점원: ");
                else if (!l.spoken) sb.Append("나(카드로 고름): ");
                else if (!string.IsNullOrWhiteSpace(l.heard) && l.heard.Trim() != l.text.Trim())
                    sb.Append($"나(목소리, 실제로 들린 말: {l.heard.Trim()}): ");
                else sb.Append("나(목소리): ");
                sb.AppendLine(l.text.Trim());
            }
            return sb.ToString();
        }

        // Gemini 응답 본문 -> 평가. 점수는 0~100으로 자르고, 점수가 없으면 null
        public static ConversationEvaluation Parse(string responseJson)
        {
            try
            {
                var root = JObject.Parse(responseJson);
                var text = (root["candidates"]?[0]?["content"]?["parts"] as JArray)?[0]?["text"]?.ToString();
                return string.IsNullOrWhiteSpace(text) ? null : ParseResult(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConversationEvaluation] 파싱 실패(기존 계산 사용): {e.Message}");
                return null;
            }
        }

        public static ConversationEvaluation ParseResult(string json)
        {
            try
            {
                // 변경점: ```json 울타리, [{...}] 배열로 와도 첫 객체를 쓴다
                json = json.Trim();
                if (json.StartsWith("```"))
                {
                    int start = json.IndexOfAny(new[] { '{', '[' });
                    int end = Math.Max(json.LastIndexOf('}'), json.LastIndexOf(']'));
                    if (start < 0 || end <= start) return null;
                    json = json.Substring(start, end - start + 1);
                }
                var token = JToken.Parse(json);
                var obj = token as JObject ?? (token as JArray)?.First as JObject;
                if (obj == null) return null;
                var a = obj["appropriateness"];
                var n = obj["naturalness"];
                if (a == null || n == null) return null;
                var e = new ConversationEvaluation
                {
                    appropriateness = Mathf.Clamp(Mathf.RoundToInt(a.Value<float>()), 0, 100),
                    naturalness = Mathf.Clamp(Mathf.RoundToInt(n.Value<float>()), 0, 100),
                    praise = Short(obj["praise"]?.ToString()),
                    tip = Short(obj["tip"]?.ToString()),
                };
                return e;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConversationEvaluation] 결과 형식이 다름(기존 계산 사용): {ex.Message}");
                return null;
            }
        }

        static string Short(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().Replace('\n', ' ');
            // 변경점: 중간에 자르면 문장이 끊겨 보여, 한 줄 칸(글자 줄여 약 40자)을 넘으면 버리고 기존 문구를 쓴다
            return s.Length > MaxPraiseLength ? null : s;
        }

        static bool HasUserLine(IList<ConversationLine> lines)
        {
            foreach (var l in lines)
                if (l.isUser && !string.IsNullOrWhiteSpace(l.text)) return true;
            return false;
        }
    }
}
