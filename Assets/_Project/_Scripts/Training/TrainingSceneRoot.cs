using UnityEngine;
using Artti.AAC;
using Artti.Common.Speech;
using Artti.Common;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Artti.Training
{
    public class TrainingSceneRoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private AACDatabase aacDatabase;
        [SerializeField] private string scenarioId;

        [Header("UI")]
        [SerializeField] private TrainingUIView uiView;

        private DialogueManager _dialogueManager;
        private ITtsService _ttsService;
        private ISttService _sttService;
        private GeminiDialogueService _geminiService;
        private EventLogger _eventLogger;
        private FallbackResponsePicker _fallbackPicker;
        private CancellationTokenSource _cts;
        private List<AACCard> _currentPool = new List<AACCard>();

        private void Awake()
        {
            _dialogueManager = new DialogueManager();

            // API 키는 .env (또는 StreamingAssets/api_keys.env) 에서 로드
            var ttsKey    = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            var sttKey    = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleSttApi, ApiKeyLoader.GeminiApi);
            var geminiKey = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);

            _ttsService = new CloudTtsService(ttsKey, GetComponent<AudioSource>());
            _sttService = new CloudSttService(sttKey);
            _geminiService = new GeminiDialogueService(geminiKey);

            // Load fallback responses — Resources/AAC/fallback_responses.json (Editor + 빌드 동일)
            var fbAsset = Resources.Load<TextAsset>("AAC/fallback_responses");
            if (fbAsset != null)
                _fallbackPicker = new FallbackResponsePicker(fbAsset.text);
            else
                Debug.LogWarning("[TrainingSceneRoot] Resources/AAC/fallback_responses.json 없음 — 자동 응답 비활성");

            // Setup Logger
            if (AppBootstrap.Instance != null)
            {
                var profile = AppBootstrap.Instance.ProfileManager.ActiveProfile;
                _eventLogger = new EventLogger(AppBootstrap.Instance.LogStore, profile?.id, scenarioId);
            }
        }

        private const int PharmacyPoolSize = 4;

        // 약국 objective 순서 (scenarios.json과 동기화)
        private static readonly string[] PharmacyObjectiveOrder =
        {
            "greeting", "identify_needs", "serve_meds", "payment", "farewell"
        };

        // 약국 v1: 각 objective 진입 시 NPC가 말하는 대사 (STT/Gemini 미사용 흐름)
        private static readonly Dictionary<string, string> PharmacyObjectivePrompts = new Dictionary<string, string>
        {
            { "greeting",       "안녕하세요! 약사예요." },
            { "identify_needs", "어디가 아파서 오셨어요?" },
            { "serve_meds",     "이 약을 드릴게요." },
            { "payment",        "결제는 어떻게 하시겠어요?" },
            { "farewell",       "안녕히 가세요!" }
        };

        private void Start()
        {
            _cts = new CancellationTokenSource();

            // 씬 진입 즉시 마이크 권한 다이얼로그 (STT 첫 호출 지연 방지 + 권한 거부 조기 노출)
            CloudSttService.RequestMicPermissionAsync(_cts.Token).Forget();

            // scenarios.json의 첫 objective가 모든 시나리오에서 "greeting"
            _dialogueManager.Initialize("greeting");
            _dialogueManager.OnToolCallApplied += HandleToolCall;
            _dialogueManager.OnObjectiveChanged += HandleObjectiveChanged;

            uiView.OnCardTapped += HandleCardTapped;
            uiView.OnExtraRequested += HandleExtraRequested;

            _eventLogger?.LogObjectiveEntered("greeting");

            // 약국: 첫 NPC 대사 + TTS (Initialize는 이벤트 안 쏘므로 수동 호출)
            if (IsPharmacyPoolMode && PharmacyObjectivePrompts.TryGetValue("greeting", out var greetingLine))
            {
                uiView.SetNPCDialogue(greetingLine);
                if (_ttsService != null)
                    _ttsService.SpeakAsync(greetingLine, _cts.Token).Forget();
            }

            ShowInitialCards();
        }

        private void ShowInitialCards()
        {
            if (aacDatabase == null) return;

            if (IsPharmacyPoolMode)
            {
                RefreshPharmacyPool();
                return;
            }

            var cards = aacDatabase.CardsForScenario(scenarioId).Take(2).ToList();
            if (cards.Count == 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] {scenarioId} 시나리오 카드 없음");
                return;
            }
            uiView.SetCards(cards.Count > 0 ? cards[0] : null, cards.Count > 1 ? cards[1] : null);
        }

        // 약국 풀 모드: 시나리오==pharmacy && View에 pharmacyCardSlots가 와이어링됨
        private bool IsPharmacyPoolMode => scenarioId == "pharmacy" && uiView != null && uiView.HasPharmacyCardPool;

        private void RefreshPharmacyPool()
        {
            if (aacDatabase == null) return;
            var objective = _dialogueManager.CurrentObjectiveId;
            _currentPool = aacDatabase.CardsForObjective(scenarioId, objective)
                                      .Take(PharmacyPoolSize)
                                      .ToList();
            if (_currentPool.Count == 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] {scenarioId}/{objective} objective 카드 없음 — 데이터 점검 필요");
            }
            uiView.SetCardList(_currentPool);
        }

        // "기타" 버튼 → 현재 풀에 포함되지 않은 약국 카드 전체를 모달로 표시
        private void HandleExtraRequested()
        {
            if (!IsPharmacyPoolMode || aacDatabase == null) return;
            var displayed = new HashSet<string>(_currentPool.Where(c => c != null).Select(c => c.id));
            var others = aacDatabase.CardsForScenario(scenarioId)
                                    .Where(c => !displayed.Contains(c.id))
                                    .ToList();
            uiView.ShowExtraModal(others);
        }

        private void HandleObjectiveChanged(string newObjectiveId)
        {
            _eventLogger?.LogObjectiveEntered(newObjectiveId);
            if (IsPharmacyPoolMode)
            {
                if (PharmacyObjectivePrompts.TryGetValue(newObjectiveId, out var npcLine))
                {
                    uiView.SetNPCDialogue(npcLine);
                    if (_ttsService != null)
                        _ttsService.SpeakAsync(npcLine, _cts.Token).Forget();
                }
                RefreshPharmacyPool();
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async void HandleCardTapped(AACCard card)
        {
            // 약국 풀 모드: 자유 발화 연습 (PLAN.MD 5.4.2 / 7.3.1)
            // 카드 phrase TTS는 없음. STT로 사용자 발화 수집 후 다음 objective로 진행.
            if (IsPharmacyPoolMode)
            {
                uiView.HideExtraModal();

                uiView.ShowMicIndicator(true);
                var sttResultP = await _sttService.ListenOnceAsync(_cts.Token);
                uiView.ShowSttResult(sttResultP.text);
                _eventLogger?.LogCardSelected(card.id, card.phrase?.text, sttResultP.text);

                // 빈 결과 시 안내 후 같은 objective 유지 (PLAN.MD 7.3.1)
                if (string.IsNullOrWhiteSpace(sttResultP.text))
                {
                    var line = PickFallback("stt_empty");
                    uiView.SetNPCDialogue(line);
                    if (_ttsService != null)
                        _ttsService.SpeakAsync(line, _cts.Token).Forget();
                    return;
                }

                AdvancePharmacyObjective();
                return;
            }

            _eventLogger?.LogCardSelected(card.id, card.phrase?.text);

            // 그 외 시나리오: 카드 TTS + STT → Gemini 흐름 (기존)
            if (_ttsService != null && card.phrase != null)
            {
                var spokenText = !string.IsNullOrEmpty(card.phrase.ttsText) ? card.phrase.ttsText : card.phrase.text;
                _ttsService.SpeakAsync(spokenText, _cts.Token).Forget();
            }

            uiView.ShowMicIndicator(true);
            var sttResult = await _sttService.ListenOnceAsync(_cts.Token);
            uiView.ShowSttResult(sttResult.text);

            _dialogueManager.HandleUserTurn(card, sttResult.text);

            // STT 빈 결과면 Gemini 호출 없이 fallback 응답으로 안내
            if (string.IsNullOrWhiteSpace(sttResult.text))
            {
                var line = PickFallback("stt_empty");
                _dialogueManager.ApplyToolCall(DialogueTool.RequestClarification, line, null);
                return;
            }

            var result = await _geminiService.RequestNextTurnAsync("System Prompt", $"{card.phrase?.text} {sttResult.text}", _cts.Token);

            // Gemini 실패/키없음 → fallback 응답으로 대체
            if (!result.HasValue)
            {
                var line = PickFallback("llm_call_failed");
                _dialogueManager.ApplyToolCall(DialogueTool.RequestClarification, line, null);
                return;
            }

            _dialogueManager.ApplyToolCall(result.Value.tool, result.Value.npcText, result.Value.args);
        }

        private string PickFallback(string condition)
        {
            return _fallbackPicker != null
                ? _fallbackPicker.Pick(scenarioId, condition)
                : "잠시만요, 다시 한 번 말씀해주세요.";
        }

        // 다음 objective(카드 풀이 비어있지 않은 것)로 이동. 끝까지 가면 무동작.
        private void AdvancePharmacyObjective()
        {
            var current = _dialogueManager.CurrentObjectiveId;
            int idx = System.Array.IndexOf(PharmacyObjectiveOrder, current);
            if (idx < 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] 알 수 없는 약국 objective: {current}");
                return;
            }
            for (int i = idx + 1; i < PharmacyObjectiveOrder.Length; i++)
            {
                var next = PharmacyObjectiveOrder[i];
                var hasCards = aacDatabase != null && aacDatabase.CardsForObjective(scenarioId, next).Any();
                if (hasCards)
                {
                    _dialogueManager.SetObjective(next);
                    return;
                }
                Debug.Log($"[TrainingSceneRoot] '{next}' objective 카드 없음 — 건너뜀");
            }
            Debug.Log("[TrainingSceneRoot] 약국 시나리오 마지막 objective 도달");
        }


        private void HandleToolCall(DialogueTool tool, string npcText, string[] args)
        {
            uiView.SetNPCDialogue(npcText);
            _ttsService.SpeakAsync(npcText, _cts.Token).Forget();
            _eventLogger?.LogNpcTurn(npcText, tool);

            // 약국 풀 모드: 카드 갱신은 OnObjectiveChanged에서 처리. Gemini의 args 무시
            if (IsPharmacyPoolMode) return;

            if (args != null && args.Length >= 2)
            {
                var c1 = aacDatabase.GetCard(args[0]);
                var c2 = aacDatabase.GetCard(args[1]);
                uiView.SetCards(c1, c2);
            }
        }
    }
}
