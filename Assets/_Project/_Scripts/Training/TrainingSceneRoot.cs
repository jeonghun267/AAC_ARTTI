using UnityEngine;
using UnityEngine.SceneManagement;
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

        [Header("Convenience HUD (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private ConvenienceHudView hud;

        [Header("Clerk 애니메이션 (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private ClerkView clerkView;

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

            // .env: GEMINI_API_KEY + GOOGLE_TTS_API_KEY 사용. STT는 OS 네이티브(키 불필요).
            // Android 단말이 ko-KR TTS 언어 데이터를 지원 안 해서 TTS만 Cloud로 유지.
            var geminiKey = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            var ttsKey    = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);

            _ttsService = new CloudTtsService(ttsKey, GetComponent<AudioSource>());

#if UNITY_ANDROID && !UNITY_EDITOR
            _sttService = new AndroidNativeSttService();
#else
            // Editor: STT는 더미 텍스트로 풀 모드 흐름만 검증
            _sttService = new EditorMockSttService();
#endif
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
                if (profile == null)
                    Debug.LogWarning("[TrainingSceneRoot] 활성 프로필 없음 — 이 세션은 레포트에 기록되지 않습니다");
            }
            else
            {
                Debug.LogWarning("[TrainingSceneRoot] AppBootstrap 없음 — 프로필 선택을 거치지 않아 이 세션은 레포트에 기록되지 않습니다. SplashScene부터 Play 하세요");
            }
        }

        private const int PoolSize = 4;

        // 시나리오별 objective 순서 (scenarios.json과 동기화)
        private static readonly Dictionary<string, string[]> ObjectiveOrderByScenario = new Dictionary<string, string[]>
        {
            { "pharmacy",    new[] { "greeting", "identify_needs", "serve_meds", "payment", "farewell" } },
            { "convenience", new[] { "greeting", "select_items", "checkout", "extras", "farewell" } },
            { "restaurant",  new[] { "greeting", "menu_browse", "order", "order_modifications", "payment", "farewell" } }
        };

        // 점원이 "물건을 건네는" 동작(HandOver)을 하는 objective. 그 외 성공은 끄덕임(Nod).
        private static readonly HashSet<string> HandOverObjectives = new HashSet<string>
        {
            "select_items", // 편의점 — 콜라 등 물건 건네기
            "serve_meds",   // 약국 — 약 건네기
            "order"         // 음식점 — 주문 받기
        };

        // 진행 표시(스테퍼)용 objective 한국어 라벨 — 시안 기준 5단계 명칭
        private static readonly Dictionary<string, string> StepperLabels = new Dictionary<string, string>
        {
            { "greeting", "인사하기" }, { "select_items", "물건 찾기" }, { "checkout", "계산하기" },
            { "extras", "후속 처리" }, { "farewell", "작별" },
            { "identify_needs", "증상 말하기" }, { "serve_meds", "물품 요구하기" }, { "payment", "계산하기" },
            { "menu_browse", "메뉴 보기" }, { "order", "주문하기" }, { "order_modifications", "추가 주문" }
        };

        private string _lastNpcLine;
        private int _objectivesEntered;
        private System.DateTimeOffset _sessionStartUtc;

        // v1 풀 모드: 각 objective 진입 시 NPC가 말하는 대사 (STT만 사용, Gemini 미사용 흐름)
        private static readonly Dictionary<string, Dictionary<string, string>> ObjectivePromptsByScenario = new Dictionary<string, Dictionary<string, string>>
        {
            { "pharmacy", new Dictionary<string, string>
                {
                    { "greeting",       "안녕하세요! 약사예요." },
                    { "identify_needs", "어디가 아파서 오셨어요?" },
                    { "serve_meds",     "이 약을 드릴게요." },
                    { "payment",        "결제는 어떻게 하시겠어요?" },
                    { "farewell",       "안녕히 가세요!" }
                }
            },
            { "convenience", new Dictionary<string, string>
                {
                    { "greeting",     "어서 오세요!" },
                    { "select_items", "찾으시는 물건 있으세요?" },
                    { "checkout",     "결제는 어떻게 하시겠어요?" },
                    { "extras",       "봉투 필요하세요?" },
                    { "farewell",     "안녕히 가세요!" }
                }
            },
            { "restaurant", new Dictionary<string, string>
                {
                    { "greeting",            "어서 오세요! 몇 분이세요?" },
                    { "menu_browse",         "메뉴 보여드릴게요. 천천히 보세요." },
                    { "order",               "주문하시겠어요?" },
                    { "order_modifications", "추가로 더 필요한 거 있으세요?" },
                    { "payment",             "결제는 어떻게 하시겠어요?" },
                    { "farewell",            "맛있게 드세요! 안녕히 가세요." }
                }
            }
        };

        private void Start()
        {
            _cts = new CancellationTokenSource();

            // 씬 진입 즉시 마이크 권한 다이얼로그 (STT 첫 호출 지연 방지 + 권한 거부 조기 노출)
            // Native/Cloud 둘 다 동일한 RECORD_AUDIO 권한 사용 — CloudSttService의 정적 헬퍼 재사용
            CloudSttService.RequestMicPermissionAsync(_cts.Token).Forget();

            // scenarios.json의 첫 objective가 모든 시나리오에서 "greeting"
            _dialogueManager.Initialize("greeting");
            _dialogueManager.OnToolCallApplied += HandleToolCall;
            _dialogueManager.OnObjectiveChanged += HandleObjectiveChanged;

            uiView.OnCardTapped += HandleCardTapped;
            uiView.OnExtraRequested += HandleExtraRequested;

            _eventLogger?.LogScenarioEntered();
            _eventLogger?.LogObjectiveEntered("greeting");
            _sessionStartUtc = System.DateTimeOffset.UtcNow;
            _objectivesEntered = 1;

            WireHud();

            // 풀 모드: 첫 NPC 대사 + TTS (Initialize는 이벤트 안 쏘므로 수동 호출)
            if (IsPoolMode && TryGetObjectivePrompt("greeting", out var greetingLine))
            {
                SpeakNpc(greetingLine);
                clerkView?.PlayGreeting();
            }

            ShowInitialCards();
        }

        // ===== Convenience HUD 연동 (hud 미와이어링 씬에서는 전부 무동작) =====

        private void WireHud()
        {
            if (hud == null) return;
            // 일시정지 종료 — 중단 기록은 OnDestroy의 SessionAbandoned 경로가 처리
            hud.OnExitRequested += () => SceneManager.LoadScene("TrainingHubScene");
            hud.OnReplayRequested += ReplayNpcLine;
            hud.OnRetrySession += () => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            hud.OnGoHub += () => SceneManager.LoadScene("TrainingHubScene");
            hud.OnGoHome += () => SceneManager.LoadScene("MainScene");
            hud.SetObjective(0, StepperLabel("greeting"));
        }

        private static string StepperLabel(string objectiveId) =>
            !string.IsNullOrEmpty(objectiveId) && StepperLabels.TryGetValue(objectiveId, out var l) ? l : objectiveId;

        // NPC 대사 출력 공통 경로 — 말풍선 갱신 + TTS + 재청취용 보관 + TtsPlayed 기록 (레포트 진행 흐름)
        private void SpeakNpc(string line, Artti.AAC.DialogueTool tool = Artti.AAC.DialogueTool.PresentCards)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lastNpcLine = line;
            uiView.SetNPCDialogue(line);
            if (_ttsService != null)
                _ttsService.SpeakAsync(line, _cts.Token).Forget();
            _eventLogger?.LogNpcTurn(line, tool);
        }

        private void ReplayNpcLine()
        {
            if (!string.IsNullOrEmpty(_lastNpcLine) && _ttsService != null)
                _ttsService.SpeakAsync(_lastNpcLine, _cts.Token).Forget();
        }

        private void ShowInitialCards()
        {
            if (aacDatabase == null) return;

            if (IsPoolMode)
            {
                RefreshPool();
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

        // 풀 모드: View에 카드 풀 슬롯이 와이어링되었고 시나리오의 objective 순서가 정의되어 있을 때
        private bool IsPoolMode =>
            uiView != null && uiView.HasPharmacyCardPool && ObjectiveOrderByScenario.ContainsKey(scenarioId);

        private bool TryGetObjectivePrompt(string objectiveId, out string line)
        {
            line = null;
            return ObjectivePromptsByScenario.TryGetValue(scenarioId, out var map)
                   && map.TryGetValue(objectiveId, out line);
        }

        private void RefreshPool()
        {
            if (aacDatabase == null) return;
            var objective = _dialogueManager.CurrentObjectiveId;
            _currentPool = aacDatabase.CardsForObjective(scenarioId, objective)
                                      .Take(PoolSize)
                                      .ToList();
            if (_currentPool.Count == 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] {scenarioId}/{objective} objective 카드 없음 — 데이터 점검 필요");
            }
            uiView.SetCardList(_currentPool);
        }

        // "기타" 버튼 → 현재 풀에 포함되지 않은 같은 시나리오 카드 전체를 모달로 표시
        private void HandleExtraRequested()
        {
            if (!IsPoolMode || aacDatabase == null) return;
            var displayed = new HashSet<string>(_currentPool.Where(c => c != null).Select(c => c.id));
            var others = aacDatabase.CardsForScenario(scenarioId)
                                    .Where(c => !displayed.Contains(c.id))
                                    .ToList();
            uiView.ShowExtraModal(others);
        }

        private void HandleObjectiveChanged(string newObjectiveId)
        {
            _eventLogger?.LogObjectiveEntered(newObjectiveId);
            _objectivesEntered++;

            // 진행 표시는 objective를 따라감 (turn 아님)
            if (hud != null && ObjectiveOrderByScenario.TryGetValue(scenarioId, out var order))
            {
                int idx = System.Array.IndexOf(order, newObjectiveId);
                if (idx >= 0) hud.SetObjective(idx, StepperLabel(newObjectiveId));
            }

            if (IsPoolMode)
            {
                if (TryGetObjectivePrompt(newObjectiveId, out var npcLine))
                {
                    SpeakNpc(npcLine);
                }
                RefreshPool();
            }
        }

        private void OnDestroy()
        {
            // 완료 없이 씬 이탈 → 중단 처리 (마지막 objective 기록). 레포트의 미완료/연습 필요 집계에 사용
            if (!_sessionCompleted)
                _eventLogger?.LogSessionAbandoned(_dialogueManager?.CurrentObjectiveId);
            AppBootstrap.Instance?.LogStore?.FlushAsync().Forget();

            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async void HandleCardTapped(AACCard card)
        {
            // 풀 모드: 자유 발화 연습 (PLAN.MD 5.4.2 / 7.3.1)
            // 카드 phrase TTS는 없음. STT로 사용자 발화 수집 후 다음 objective로 진행.
            if (IsPoolMode)
            {
                uiView.HideExtraModal();

                // 시안 3.png: 선택 카드 강조 + 나머지 터치 비활성·흐림, 사용자 발화 버블 표시
                hud?.SetUserUtterance(card.phrase?.text);
                uiView.SetPoolLocked(card);

                uiView.ShowMicIndicator(true);
                var sttResultP = await _sttService.ListenOnceAsync(_cts.Token);
                uiView.ShowSttResult(sttResultP.text);
                uiView.UnlockPool();
                _eventLogger?.LogCardSelected(card.id, card.phrase?.text, sttResultP.text);

                // 빈 결과 시 fallback 전용 대사 후 같은 objective 유지 — 2차 시도(scaffold_level=1)는 같은 카드 유지 (PLAN.MD 7.3.1)
                if (string.IsNullOrWhiteSpace(sttResultP.text))
                {
                    _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
                    SpeakNpc(PickFallback("stt_empty"));
                    return;
                }

                // 성공: 짧은 긍정 피드백 (랜덤) 후 다음 단계
                hud?.ShowPraise();
                // 점원 반응: 물건 요청 단계면 건네기, 그 외엔 끄덕임
                if (clerkView != null)
                {
                    if (HandOverObjectives.Contains(_dialogueManager.CurrentObjectiveId))
                        clerkView.PlayHandOver();
                    else
                        clerkView.PlayNod();
                }
                AdvanceObjective();
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
                _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
                var line = PickFallback("stt_empty");
                _dialogueManager.ApplyToolCall(DialogueTool.RequestClarification, line, null);
                return;
            }

            // LLM 처리 동안 "생각하는 중" 인디케이터 (응답 지연/무응답 fallback 패턴 공통)
            hud?.ShowThinking(true);
            var result = await _geminiService.RequestNextTurnAsync("System Prompt", $"{card.phrase?.text} {sttResult.text}", _cts.Token);
            hud?.ShowThinking(false);

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
        private void AdvanceObjective()
        {
            if (!ObjectiveOrderByScenario.TryGetValue(scenarioId, out var order))
            {
                Debug.LogWarning($"[TrainingSceneRoot] '{scenarioId}' 시나리오 objective 순서 미정의");
                return;
            }
            var current = _dialogueManager.CurrentObjectiveId;
            int idx = System.Array.IndexOf(order, current);
            if (idx < 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] 알 수 없는 {scenarioId} objective: {current}");
                return;
            }
            for (int i = idx + 1; i < order.Length; i++)
            {
                var next = order[i];
                var hasCards = aacDatabase != null && aacDatabase.CardsForObjective(scenarioId, next).Any();
                if (hasCards)
                {
                    _dialogueManager.SetObjective(next);
                    return;
                }
                Debug.Log($"[TrainingSceneRoot] '{next}' objective 카드 없음 — 건너뜀");
            }
            Debug.Log($"[TrainingSceneRoot] {scenarioId} 시나리오 마지막 objective 도달");
            MarkSessionCompleted();
        }

        private bool _sessionCompleted;

        // 세션 완료 기록 + 즉시 flush — 레포트가 파일에서 바로 읽을 수 있게
        private void MarkSessionCompleted()
        {
            if (_sessionCompleted) return;
            _sessionCompleted = true;
            _eventLogger?.LogSessionEnded("completed");
            AppBootstrap.Instance?.LogStore?.FlushAsync().Forget();
            ShowCompletionScreen();
        }

        // 완료 화면 — 평가성 요소 없이 시나리오/걸린 시간/진행 단계 수만 (시안 스펙)
        private void ShowCompletionScreen()
        {
            if (hud == null) return;
            var elapsed = System.DateTimeOffset.UtcNow - _sessionStartUtc;
            string duration = elapsed.TotalSeconds < 60 ? "1분 이내" : $"{Mathf.RoundToInt((float)elapsed.TotalMinutes)}분";
            hud.ShowCompletion(
                Artti.Report.ReportLabels.ScenarioName(scenarioId),
                duration,
                $"{_objectivesEntered}단계 완료");
        }


        private void HandleToolCall(DialogueTool tool, string npcText, string[] args)
        {
            SpeakNpc(npcText, tool);

            // Gemini 흐름의 시나리오 종료 — 풀 모드의 마지막 objective 도달과 동일하게 완료 처리
            if (tool == DialogueTool.ForceCompleteScenario)
                MarkSessionCompleted();

            // 풀 모드: 카드 갱신은 OnObjectiveChanged에서 처리. Gemini의 args 무시
            if (IsPoolMode) return;

            if (args != null && args.Length >= 2)
            {
                var c1 = aacDatabase.GetCard(args[0]);
                var c2 = aacDatabase.GetCard(args[1]);
                uiView.SetCards(c1, c2);
            }
        }
    }
}
