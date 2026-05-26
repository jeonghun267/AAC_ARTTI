using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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

        // V5: present_cards 도구 호출 진입점.
        // PLAN.md 5.5.2 / 5.5.4. 단계 4 현재 — tools 정의 전송까지 구현, 응답 파싱은 mock.
        // 실패 또는 API 키 없음 시 null 리턴.
        public async UniTask<PresentCardsArgs> RequestNextTurnV5Async(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogWarning("[Gemini] API key missing — caller should use fallback");
                return null;
            }

            var requestBody = new GeminiRequestV5
            {
                system_instruction = new GeminiContent { parts = new[] { new GeminiPart { text = systemPrompt ?? string.Empty } } },
                contents = new[] { new GeminiContent { parts = new[] { new GeminiPart { text = userPrompt ?? string.Empty } } } },
                tools = new[] { BuildPresentCardsTool() }
            };

            string bodyJson = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            Debug.Log($"[GeminiDialogueService] V5 request body: {bodyJson}");

            using (var request = new UnityWebRequest($"{ApiUrl}?key={_apiKey}", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
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

                // 단계 5에서 functionCall 파싱 구현. 현재는 mock.
                return new PresentCardsArgs
                {
                    main_card_ids = new[] { "card_01", "card_02" },
                    fallback_card_id = "card_03",
                    npc_speech = "네, 알겠습니다.",
                    scaffold_level = 0
                };
            }
        }

        // present_cards 도구 스키마 빌드. PLAN.md 5.5.2 / v5 변경 요약 기준.
        static GeminiTool BuildPresentCardsTool()
        {
            return new GeminiTool
            {
                functionDeclarations = new[]
                {
                    new GeminiFunctionDeclaration
                    {
                        name = "present_cards",
                        description = "Display AAC cards and NPC dialogue to the user for the current turn. Called most frequently.",
                        parameters = new GeminiSchema
                        {
                            type = "object",
                            properties = new GeminiSchemaProperties
                            {
                                main_card_ids = new GeminiSchema
                                {
                                    type = "array",
                                    description = "Array of exactly 2 AAC card IDs that directly satisfy the current objective.",
                                    items = new GeminiSchema { type = "string" }
                                },
                                fallback_card_id = new GeminiSchema
                                {
                                    type = "string",
                                    description = "One additional AAC card ID used when the user's intent does not match either of the main 2 cards."
                                },
                                npc_speech = new GeminiSchema
                                {
                                    type = "string",
                                    description = "NPC dialogue line to display to the user. MUST be in Korean (한국어)."
                                },
                                scaffold_level = new GeminiSchema
                                {
                                    type = "integer",
                                    description = "Help-strengthening level. 0=normal, 1=mild hint, 2=strong hint highlighting the correct card."
                                }
                            },
                            required = new[] { "main_card_ids", "fallback_card_id", "npc_speech", "scaffold_level" }
                        }
                    }
                }
            };
        }

        [Serializable] class GeminiRequest { public GeminiContent system_instruction; public GeminiContent[] contents; }
        [Serializable] class GeminiContent { public GeminiPart[] parts; }
        [Serializable] class GeminiPart    { public string text; }

        // V5 직렬화 전용 타입 (Newtonsoft.Json, NullValueHandling.Ignore 전제)
        class GeminiRequestV5
        {
            public GeminiContent system_instruction;
            public GeminiContent[] contents;
            public GeminiTool[] tools;
        }
        class GeminiTool { public GeminiFunctionDeclaration[] functionDeclarations; }
        class GeminiFunctionDeclaration { public string name; public string description; public GeminiSchema parameters; }
        class GeminiSchema
        {
            public string type;
            public string description;
            public GeminiSchemaProperties properties;
            public string[] required;
            public GeminiSchema items;
        }
        class GeminiSchemaProperties
        {
            public GeminiSchema main_card_ids;
            public GeminiSchema fallback_card_id;
            public GeminiSchema npc_speech;
            public GeminiSchema scaffold_level;
        }
    }
}
