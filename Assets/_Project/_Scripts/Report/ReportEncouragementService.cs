using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;        // CLAUDE.md: LLM 응답 파싱은 Newtonsoft 사용
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Artti.Report
{
    // 레포트 기록(완료율/시나리오별 횟수)을 근거로 캐릭터 말풍선 응원 문구를 생성한다.
    // 말풍선 LLM은 정해진 멘트가 아니라 "이 사용자의 실제 학습 기록"을 기준으로 말해야 한다는 요구사항 반영.
    // MonoBehaviour 아님 - 순수 로직/외부 API. 키 없음/오프라인/실패 시 템플릿으로 폴백.
    public class ReportEncouragementService
    {
        private readonly string _apiKey;
        private const string ApiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        // 완료율이 이 값 이상이면 "잘함"으로 보고 톤/연출을 강하게(full_motion 환영).
        private const float HighScoreThreshold = 0.7f;

        public struct Result
        {
            public string message;     // 말풍선 + TTS 로 쓸 한국어 문구 (2줄 내외)
            public bool isHighScore;   // 성적 차등 연출용
        }

        public ReportEncouragementService(string apiKey)
        {
            _apiKey = apiKey;
        }

        // 세션 캐시: 같은 통계 상태면 Gemini 재호출 없이 재사용 (한도 절약).
        // 통계가 바뀌면(키 변경) 그때만 1회 다시 호출. 앱 재시작 시 초기화.
        static string _cacheKey;
        static Result _cache;
        static bool _hasCache;

        // overview: 한눈에 요약 통계. nickname: 활성 프로필 닉네임(없으면 비움).
        // 항상 Result를 반환 - LLM 실패 시에도 템플릿 문구로 채운다.
        public async UniTask<Result> GenerateAsync(ReportOverview overview, string nickname, CancellationToken ct = default)
        {
            bool high = overview != null && overview.CompletionRate >= HighScoreThreshold;

            string key = $"{nickname}|{(overview != null ? overview.totalAttempts : 0)}|{(overview != null ? overview.completedCount : 0)}";
            if (_hasCache && _cacheKey == key) return _cache;

            string llm = await TryGenerateLlmAsync(overview, nickname, high, ct);
            string message = !string.IsNullOrWhiteSpace(llm) ? llm.Trim() : Fallback(overview, nickname, high);

            var result = new Result { message = message, isHighScore = high };
            _cacheKey = key; _cache = result; _hasCache = true; // 성공/폴백 모두 캐시 -> 같은 상태에선 1회만 호출
            return result;
        }

        private async UniTask<string> TryGenerateLlmAsync(ReportOverview overview, string nickname, bool high, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[ReportEncouragement] API key 없음 - 템플릿 폴백");
                return null;
            }

            string stats = BuildStatsText(overview);
            string toneHint = high
                ? "이번 기록은 아주 우수합니다. 자랑스럽고 신나는 칭찬 톤으로."
                : "아직 연습 중입니다. 부담 주지 않고 따뜻하게 격려하는 톤으로.";

            string systemPrompt =
                "당신은 발달장애인 학습자를 돕는 다정한 코치입니다. " +
                "아래 학습 기록을 근거로, 캐릭터가 말풍선으로 건넬 짧은 한국어 응원 한 마디를 만드세요. " +
                "규칙: 2줄 이내, 총 40자 이내. 숫자를 그대로 나열하지 말고 따뜻하게 표현. " +
                "이모지 사용 금지. 따옴표 없이 문구만 출력. " +
                (string.IsNullOrEmpty(nickname) ? "" : $"가능하면 '{nickname}님'으로 부르세요. ") +
                toneHint;

            string userPrompt = $"학습 기록:\n{stats}";

            var body = new JObject
            {
                ["system_instruction"] = new JObject
                {
                    ["parts"] = new JArray { new JObject { ["text"] = systemPrompt } }
                },
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray { new JObject { ["text"] = userPrompt } }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.9,
                    ["maxOutputTokens"] = 80
                }
            };

            string jsonBody = body.ToString(Formatting.None);

            using (var request = new UnityWebRequest($"{ApiUrl}?key={_apiKey}", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                try
                {
                    await request.SendWebRequest().WithCancellation(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (UnityWebRequestException e)
                {
                    // 429(일일 한도 초과) 등은 템플릿 폴백으로 처리되므로 경고로만
                    Debug.LogWarning($"[ReportEncouragement] HTTP {(int)e.ResponseCode}(폴백): {e.Text}");
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ReportEncouragement] API 실패(폴백): {request.error}");
                    return null;
                }

                return ParseText(request.downloadHandler.text);
            }
        }

        private static string ParseText(string responseJson)
        {
            try
            {
                var root = JObject.Parse(responseJson);
                if (!(root["candidates"]?[0]?["content"]?["parts"] is JArray parts) || parts.Count == 0)
                {
                    Debug.LogWarning("[ReportEncouragement] 빈 응답/안전필터 - 폴백");
                    return null;
                }
                foreach (var part in parts)
                {
                    var t = part["text"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReportEncouragement] 파싱 실패: {e.Message}");
                return null;
            }
        }

        private static string BuildStatsText(ReportOverview o)
        {
            if (o == null) return "기록 없음";
            var sb = new StringBuilder();
            sb.AppendLine($"- 도전한 학습: {o.totalAttempts}회");
            sb.AppendLine($"- 완료한 학습: {o.completedCount}회");
            sb.AppendLine($"- 완료율: {Mathf.RoundToInt(o.CompletionRate * 100f)}%");
            if (o.completedByScenario != null)
                foreach (var kv in o.completedByScenario)
                    sb.AppendLine($"- {ReportLabels.ScenarioName(kv.Key)} 완료: {kv.Value}회");
            return sb.ToString();
        }

        private static string Fallback(ReportOverview o, string nickname, bool high)
        {
            string who = string.IsNullOrEmpty(nickname) ? "" : $"{nickname}님 ";
            if (o == null || o.totalAttempts == 0)
                return $"{who}오늘부터\n같이 시작해봐요!";
            if (high)
                return $"{who}정말 잘하고 있어요!\n이대로 쭉 가요!";
            return $"{who}조금씩 늘고 있어요!\n같이 연습해요!";
        }
    }
}
