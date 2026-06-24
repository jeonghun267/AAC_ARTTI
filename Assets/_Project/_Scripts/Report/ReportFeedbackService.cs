using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;        // CLAUDE.md: LLM 응답 파싱은 Newtonsoft 사용
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Artti.Report
{
    // 세션 1개의 상세 기록을 근거로 AI 리드백(메인 문구/말풍선/잘한점/연습할점/다음목표)을
    // 실제 Gemini 판단으로 생성한다. MonoBehaviour 아님 - 순수 로직/외부 API.
    // 키 없음/오프라인/실패 시 null 반환 -> 호출측이 규칙 기반 폴백 사용.
    public class ReportFeedbackService
    {
        private readonly string _apiKey;
        private const string ApiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        public class Feedback
        {
            public string main;
            public string bubble;
            public List<string> good = new List<string>();
            public List<string> improve = new List<string>();
            public List<string> next = new List<string>();
        }

        public ReportFeedbackService(string apiKey)
        {
            _apiKey = apiKey;
        }

        // sessionId 기준 세션 캐시: 같은 세션 상세를 다시 열면 Gemini 재호출 없이 재사용 (한도 절약).
        // 세션 기록은 불변이라 sessionId만으로 충분. 앱 재시작 시 초기화.
        static readonly System.Collections.Generic.Dictionary<string, Feedback> _cache
            = new System.Collections.Generic.Dictionary<string, Feedback>();

        public async UniTask<Feedback> GenerateAsync(ReportRecordDetail report, string nickname, CancellationToken ct = default)
        {
            string sid = report != null ? report.sessionId : null;
            if (!string.IsNullOrEmpty(sid) && _cache.TryGetValue(sid, out var cached)) return cached;

            var fb = await DoGenerateAsync(report, nickname, ct);
            if (!string.IsNullOrEmpty(sid)) _cache[sid] = fb; // 성공/폴백(null) 모두 캐시 -> 같은 세션 1회만 호출
            return fb;
        }

        private async UniTask<Feedback> DoGenerateAsync(ReportRecordDetail report, string nickname, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[ReportFeedback] API key 없음 - 폴백");
                return null;
            }
            if (report == null) return null;

            string systemPrompt =
                "당신은 발달장애인 학습자를 돕는 다정한 한국어 코치입니다. " +
                "아래 '대화 연습 세션 기록'을 분석해, 학습자에게 줄 피드백을 JSON으로만 출력하세요. " +
                "필드: main(한 문장 격려, 40자 이내), bubble(캐릭터 말풍선용 짧은 응원 1~2줄 20자 이내, 끝에 이모지 1개 허용), " +
                "good(잘한 점 2개, 각 12자 이내 짧은 구), improve(연습하면 좋은 점 1~2개, 각 14자 이내), next(다음 목표 1개, 18자 이내). " +
                "실제 기록(목표 달성/재시도/발화 내용)을 근거로 구체적으로. 숫자 나열 금지. " +
                (string.IsNullOrEmpty(nickname) ? "" : $"가능하면 '{nickname}님'을 자연스럽게 포함. ");

            string userPrompt = "대화 연습 세션 기록:\n" + BuildStatsText(report);

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
                    ["temperature"] = 0.8,
                    ["maxOutputTokens"] = 1024,           // 잘림 방지 (JSON 끊김 오류 해결)
                    ["responseMimeType"] = "application/json",
                    ["thinkingConfig"] = new JObject { ["thinkingBudget"] = 0 } // 2.5-flash 사고 토큰이 출력 예산 먹는 것 방지
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
                    // 429(일일 한도 초과) 등은 규칙 기반 폴백으로 처리되므로 경고로만
                    Debug.LogWarning($"[ReportFeedback] HTTP {(int)e.ResponseCode}(폴백): {e.Text}");
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[ReportFeedback] API 실패(폴백): {request.error}");
                    return null;
                }

                return Parse(request.downloadHandler.text);
            }
        }

        private static Feedback Parse(string responseJson)
        {
            try
            {
                var root = JObject.Parse(responseJson);
                var text = root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.LogWarning("[ReportFeedback] 빈 응답/안전필터 - 폴백");
                    return null;
                }

                var obj = JObject.Parse(text); // responseMimeType=application/json 이므로 text 자체가 JSON
                var fb = new Feedback
                {
                    main = obj["main"]?.ToString(),
                    bubble = obj["bubble"]?.ToString(),
                };
                AddAll(fb.good, obj["good"]);
                AddAll(fb.improve, obj["improve"]);
                AddAll(fb.next, obj["next"]);

                if (string.IsNullOrWhiteSpace(fb.main) && fb.good.Count == 0) return null;
                return fb;
            }
            catch (Exception e)
            {
                // 잘림/형식 오류 시 규칙 기반으로 폴백되므로 경고로만 (치명적 아님)
                Debug.LogWarning($"[ReportFeedback] 파싱 실패(규칙 기반 폴백): {e.Message}");
                return null;
            }
        }

        private static void AddAll(List<string> dst, JToken arr)
        {
            if (!(arr is JArray ja)) return;
            foreach (var t in ja)
            {
                var s = t?.ToString();
                if (!string.IsNullOrWhiteSpace(s)) dst.Add(s.Trim());
            }
        }

        private static string BuildStatsText(ReportRecordDetail r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"- 시나리오: {r.scenarioName}");
            sb.AppendLine($"- 완료 여부: {(r.completed ? "완료" : "미완료")}");
            sb.AppendLine($"- 걸린 시간: {r.durationMin}분");
            sb.AppendLine($"- 단계 결과: 완전 성공 {r.fullSuccess} / 부분 성공 {r.partialSuccess} / 도움 필요 {r.needHelp}");
            sb.AppendLine("- 단계별 기록:");
            foreach (var s in r.steps)
            {
                string utter = string.IsNullOrWhiteSpace(s.userText) ? "(발화 없음)" : s.userText;
                sb.AppendLine($"  {s.index}. {s.objectiveName} | 사용자: \"{utter}\" | 재시도 {s.retryCount}회 | 평가 {s.ratingLabel}");
            }
            return sb.ToString();
        }
    }
}
