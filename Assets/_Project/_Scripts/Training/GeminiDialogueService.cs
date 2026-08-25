using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;       // CLAUDE.md: LLM function calling 응답 파싱은 Newtonsoft 사용
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Artti.AAC;

namespace Artti.Training
{
    public class GeminiDialogueService
    {
        private readonly string _apiKey;
        // dialogue_tools.json의 "function_declarations" 배열 원문(JSON 문자열). 호출부에서 TextAsset으로 읽어 주입.
        private readonly string _functionDeclarationsJson;
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        // functionDeclarationsJson을 비우면 function calling 없이 일반 텍스트 응답으로 폴백.
        // 자유 대화(도구 흐름)를 쓰려면 반드시 dialogue_tools.json의 function_declarations 배열을 주입할 것.
        public GeminiDialogueService(string apiKey, string functionDeclarationsJson = null)
        {
            _apiKey = apiKey;
            _functionDeclarationsJson = functionDeclarationsJson;
        }

        // 훈련 흐름 한 턴 — 도구 호출을 강제해 DialogueTurn으로 파싱.
        // 실패/키없음 시 null 리턴 — caller가 FallbackResponsePicker로 대체
        // 변경점: 반환 타입을 (tool, npcText, args) 튜플 → DialogueTurn으로 교체.
        //         objective_id / slots_filled / scaffold_level / subflow_id가 더 이상 버려지지 않는다.
        public async UniTask<DialogueTurn> RequestNextTurnAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default)
        {
            var body = BuildBody(systemPrompt, userPrompt);

            // 변경점: 도구 카탈로그 주입 + mode ANY로 매 턴 반드시 하나의 함수 호출 강제.
            // (system_prompts.json shared_preamble: "Every turn you MUST call exactly one tool.")
            if (!string.IsNullOrEmpty(_functionDeclarationsJson))
            {
                JArray decls = null;
                try { decls = JArray.Parse(_functionDeclarationsJson); }
                catch (Exception e) { Debug.LogError($"[Gemini] function_declarations 파싱 실패: {e.Message}"); }

                if (decls != null)
                {
                    body["tools"] = new JArray { new JObject { ["function_declarations"] = decls } };
                    body["tool_config"] = new JObject
                    {
                        ["function_calling_config"] = new JObject { ["mode"] = "ANY" }
                    };
                }
            }

            var raw = await SendAsync(body, ct);
            return raw == null ? null : ParseResponse(raw);
        }

        // 변경점(대화하기 모드): 도구 없이 평문 응답만 받는 자유 대화 경로.
        //   tools/tool_config를 아예 싣지 않아 Gemini가 함수 호출 대신 그냥 말하게 된다.
        //   실패/키없음 시 null.
        public async UniTask<string> RequestFreeTalkAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default)
        {
            var raw = await SendAsync(BuildBody(systemPrompt, userPrompt), ct);
            if (raw == null) return null;

            try
            {
                var parts = JObject.Parse(raw)["candidates"]?[0]?["content"]?["parts"] as JArray;
                var text = parts?.Select(p => p["text"]?.ToString()).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.LogWarning($"[Gemini] 자유 대화 응답 비어있음 (안전필터 가능)\n{raw}");
                    return null;
                }
                return text.Trim();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gemini] 자유 대화 파싱 실패: {e.Message}\n{raw}");
                return null;
            }
        }

        // system_instruction + contents 공통 골격.
        private static JObject BuildBody(string systemPrompt, string userPrompt) => new JObject
        {
            ["system_instruction"] = new JObject
            {
                ["parts"] = new JArray { new JObject { ["text"] = systemPrompt ?? string.Empty } }
            },
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray { new JObject { ["text"] = userPrompt ?? string.Empty } }
                }
            }
        };

        // HTTP 왕복 공통부. 성공 시 응답 본문 원문, 실패 시 null.
        private async UniTask<string> SendAsync(JObject body, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[Gemini] API key missing — caller should use fallback");
                return null;
            }

            using (var request = new UnityWebRequest($"{ApiUrl}?key={_apiKey}", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));
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

                return request.downloadHandler.text;
            }
        }

        // candidates[0].content.parts[] 에서 functionCall을 찾아 DialogueTurn으로 변환.
        // functionCall이 없으면 text 파트를 PresentCards로 폴백. 파싱 불가 시 null(→ caller fallback).
        private DialogueTurn ParseResponse(string responseJson)
        {
            try
            {
                var root = JObject.Parse(responseJson);
                if (!(root["candidates"]?[0]?["content"]?["parts"] is JArray parts) || parts.Count == 0)
                {
                    Debug.LogWarning($"[Gemini] candidates/parts 없음 (안전필터 또는 빈 응답)\n{responseJson}");
                    return null;
                }

                foreach (var part in parts)
                {
                    var fc = part["functionCall"];
                    if (fc == null) continue;

                    string name = fc["name"]?.ToString();
                    var args = fc["args"] as JObject ?? new JObject();

                    if (!TryMapTool(name, out var tool))
                    {
                        Debug.LogWarning($"[Gemini] 미지원 도구 이름: '{name}'");
                        continue;
                    }

                    // 변경점: 도구 인자를 전부 실어 보낸다. 이전에는 npc_speech와 card_ids만 살아남았다.
                    return new DialogueTurn
                    {
                        Tool          = tool,
                        NpcSpeech     = args["npc_speech"]?.ToString() ?? string.Empty,
                        CardIds       = ExtractCardIds(args),
                        ObjectiveId   = args["objective_id"]?.ToString(),
                        SubflowId     = args["subflow_id"]?.ToString(),
                        PendingTopic  = args["pending_topic"]?.ToString(),
                        Reason        = args["reason"]?.ToString(),
                        ScaffoldLevel = ExtractScaffoldLevel(args),
                        SlotsFilled   = ExtractSlots(args)
                    };
                }

                // function call이 없을 때(mode ANY면 드묾) 텍스트라도 살려서 발화로 사용.
                string text = parts.Select(p => p["text"]?.ToString()).FirstOrDefault(t => !string.IsNullOrEmpty(t));
                if (!string.IsNullOrEmpty(text))
                    return new DialogueTurn
                    {
                        Tool      = DialogueTool.PresentCards,
                        NpcSpeech = text,
                        CardIds   = Array.Empty<string>()
                    };

                Debug.LogWarning($"[Gemini] functionCall/text 모두 없음\n{responseJson}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gemini] 응답 파싱 실패: {e.Message}\n{responseJson}");
                return null;
            }
        }

        // present_cards는 card_ids, enter_subflow/request_clarification은 suggested_cards를 사용.
        private static string[] ExtractCardIds(JObject args)
        {
            var arr = (args["card_ids"] ?? args["suggested_cards"]) as JArray;
            if (arr == null) return Array.Empty<string>();
            return arr.Select(t => t.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }

        // 모델이 정수 대신 문자열("1")로 낼 수 있어 파싱 실패를 null로 흡수한다.
        private static int? ExtractScaffoldLevel(JObject args)
        {
            var token = args["scaffold_level"];
            if (token == null) return null;
            if (token.Type == JTokenType.Integer) return (int)token;
            return int.TryParse(token.ToString(), out var v) ? v : (int?)null;
        }

        // slots_filled의 값은 string[]·bool·string이 섞여 온다. 프롬프트 되먹임 용도이므로 문자열로 평탄화.
        private static Dictionary<string, string> ExtractSlots(JObject args)
        {
            if (!(args["slots_filled"] is JObject slots) || !slots.HasValues) return null;

            var result = new Dictionary<string, string>();
            foreach (var prop in slots.Properties())
            {
                if (prop.Value == null || prop.Value.Type == JTokenType.Null) continue;
                var value = prop.Value is JArray arr
                    ? string.Join(", ", arr.Select(t => t.ToString()))
                    : prop.Value.ToString();
                if (!string.IsNullOrWhiteSpace(value)) result[prop.Name] = value;
            }
            return result.Count > 0 ? result : null;
        }

        // dialogue_tools.json의 snake_case 함수명 → DialogueTool enum 매핑.
        private static bool TryMapTool(string name, out DialogueTool tool)
        {
            switch (name)
            {
                case "present_cards":           tool = DialogueTool.PresentCards;          return true;
                case "mark_objective_complete": tool = DialogueTool.MarkObjectiveComplete; return true;
                case "transition_to_objective": tool = DialogueTool.TransitionToObjective; return true;
                case "enter_subflow":           tool = DialogueTool.EnterSubflow;           return true;
                case "return_from_subflow":     tool = DialogueTool.ReturnFromSubflow;      return true;
                case "request_clarification":   tool = DialogueTool.RequestClarification;   return true;
                case "express_understanding":   tool = DialogueTool.ExpressUnderstanding;   return true;
                case "force_complete_scenario": tool = DialogueTool.ForceCompleteScenario;  return true;
                default:                        tool = DialogueTool.PresentCards;           return false;
            }
        }
    }
}
