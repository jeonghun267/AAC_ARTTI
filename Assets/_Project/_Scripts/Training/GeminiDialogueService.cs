using System;
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

            // ── 요청 본문 구성 (Newtonsoft) ──
            // 변경점: 기존 JsonUtility + [Serializable] 방식은 tools/tool_config의 중첩 동적 스키마를
            //        직렬화하지 못해 function calling이 불가능했음. JObject로 교체.
            var body = new JObject
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

            string jsonBody = body.ToString(Formatting.None);

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

                // 변경점: mock 제거 → 실제 function calling 응답 파싱.
                return ParseResponse(request.downloadHandler.text);
            }
        }

        // candidates[0].content.parts[] 에서 functionCall을 찾아 (tool, npc_speech, card_ids)로 변환.
        // functionCall이 없으면 text 파트를 PresentCards로 폴백. 파싱 불가 시 null(→ caller fallback).
        private (DialogueTool tool, string npcText, string[] args)? ParseResponse(string responseJson)
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
                    var argsObj = fc["args"] as JObject ?? new JObject();

                    if (!TryMapTool(name, out var tool))
                    {
                        Debug.LogWarning($"[Gemini] 미지원 도구 이름: '{name}'");
                        continue;
                    }

                    string npc = argsObj["npc_speech"]?.ToString() ?? string.Empty;
                    string[] cards = ExtractCardIds(argsObj);
                    return (tool, npc, cards);
                    // NOTE(Unit 4): objective_id / slots_filled / scaffold_level / subflow_id 등은
                    //               현재 반환 튜플에 담기지 않아 DialogueManager가 못 읽음. 반환 타입 확장 예정.
                }

                // function call이 없을 때(mode ANY면 드묾) 텍스트라도 살려서 발화로 사용.
                string text = parts.Select(p => p["text"]?.ToString()).FirstOrDefault(t => !string.IsNullOrEmpty(t));
                if (!string.IsNullOrEmpty(text))
                    return (DialogueTool.PresentCards, text, Array.Empty<string>());

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
