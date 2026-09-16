using UnityEngine;
using UnityEngine.SceneManagement;
using Artti.AAC;
using Artti.Common.Speech;
using Artti.Common;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Artti.UI;

namespace Artti.Training
{
    public class TrainingSceneRoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private AACDatabase aacDatabase;
        [SerializeField] private string scenarioId;

        [Header("UI")]
        [SerializeField] private TrainingUIView uiView;

        [Header("Convenience dashboard (옵션)")]
        [SerializeField] private ConvenienceDashboardView dashboardView;

        [Header("Convenience HUD (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private ConvenienceHudView hud;

        [Header("Clerk 애니메이션 (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private ClerkView clerkView;

        [Header("카운터 물건 표시 (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private CounterDisplay counter;

        [Header("접근성 (옵션)")]
        [Tooltip("풀 모드에서 카드를 누를 때 카드 문구를 음성으로 읽어줌. 자유발화 연습 의도와 충돌하므로 기본 끔")]
        [SerializeField] private bool speakCardOnTap = false;

        private DialogueManager _dialogueManager;
        private ITtsService _ttsService;
        private ISttService _sttService;
        private GeminiDialogueService _geminiService;
        private string _systemPrompt; // system_prompts.json에서 시나리오별로 빌드 (Awake)
        // Unit 3: 풀 모드에서 룰베이스 대신 Gemini가 대화를 주도. 우선 편의점만 적용, 검증 후 약국·음식점 확대.
        private bool _useGemini;
        private EventLogger _eventLogger;
        private FallbackResponsePicker _fallbackPicker;
        private ConvenienceCardRuleBook _cardRuleBook; // 편의점 전용 카드 룰베이스 (그 외 시나리오는 null)
        private CancellationTokenSource _cts;
        private List<AACCard> _currentPool = new List<AACCard>();

        // 변경점: 직전 도구가 점원 발화를 주지 않았을 때만 고정 멘트로 메꾸기 위한 플래그(침묵 방어선).
        //   mark_objective_complete에 npc_speech를 추가했지만 LLM이 빈 값을 낼 수 있다.
        private bool _lastToolSpoke;

        // 단계 전환 턴에도 "방금 점원이 실제로 한 말"로 카드를 고르기 위한 임시 보관.
        // HandleObjectiveChanged는 이벤트 핸들러라 인자를 못 받아 필드로 넘긴다.
        private DialogueTurn _pendingTurn;

        // 대화하기(자유 대화) 모드 상태. ON인 동안 objective·스테퍼는 멈춘다.
        private bool _freeTalkActive;
        private bool _freeTalkBusy;
        private string _freeTalkSystemPrompt;
        private bool _dashboardTurnBusy;

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
            // 변경점(Unit 2): 도구 카탈로그 + 시나리오 시스템 프롬프트 로드해 주입.
            //   Resources/AAC/{dialogue_tools,system_prompts}.json (기존 _Data/AAC 원본의 런타임 복사본)
            string declsJson = null;
            var toolsAsset = Resources.Load<TextAsset>("AAC/dialogue_tools");
            if (toolsAsset != null)
            {
                // function_declarations 배열만 떼어내 서비스에 전달 (Gemini tools 스키마 형식)
                var decls = Newtonsoft.Json.Linq.JObject.Parse(toolsAsset.text)["function_declarations"];
                declsJson = decls?.ToString(Newtonsoft.Json.Formatting.None);
            }
            else
                Debug.LogWarning("[TrainingSceneRoot] Resources/AAC/dialogue_tools.json 없음 — function calling 비활성(텍스트 폴백)");

            var promptAsset = Resources.Load<TextAsset>("AAC/system_prompts");
            if (promptAsset != null)
            {
                var provider = new SystemPromptProvider(promptAsset.text);
                _systemPrompt = provider.BuildSystemPrompt(scenarioId);
                // 대화하기 모드는 도구·카드 규칙이 빠진 persona 기반 프롬프트를 따로 쓴다.
                _freeTalkSystemPrompt = BuildFreeTalkPrompt(provider.BuildPersona(scenarioId));
            }
            else
            {
                Debug.LogWarning("[TrainingSceneRoot] Resources/AAC/system_prompts.json 없음 — 페르소나 없이 호출");
                _freeTalkSystemPrompt = BuildFreeTalkPrompt(null); // 자유 대화는 최소 persona라도 있어야 한다
            }

            // 변경점(Unit 3): Gemini가 실재하는 card_id만 고르도록 시나리오 카드 목록을 시스템 프롬프트에 부착.
            //   (없으면 card_ids를 환각 → 매번 룰 풀로 폴백하게 됨)
            _systemPrompt = AppendAvailableCards(_systemPrompt);

            _geminiService = new GeminiDialogueService(geminiKey, declsJson);
            _useGemini = scenarioId == ScenarioIds.Convenience; // 편의점부터 LLM 주도 전환

            // Load fallback responses — Resources/AAC/fallback_responses.json (Editor + 빌드 동일)
            var fbAsset = Resources.Load<TextAsset>("AAC/fallback_responses");
            if (fbAsset != null)
                _fallbackPicker = new FallbackResponsePicker(fbAsset.text);
            else
                Debug.LogWarning("[TrainingSceneRoot] Resources/AAC/fallback_responses.json 없음 — 자동 응답 비활성");

            // 편의점 카드 룰베이스 로드 — Resources/AAC/convenience_card_rules.json (OCR KeywordDictionary 패턴 참고)
            // 점원 발화 키워드 매칭으로 맥락에 맞는 카드만 정렬해 보여줘 "상황과 무관한 카드" 오출력을 방지.
            if (scenarioId == ScenarioIds.Convenience)
            {
                var ruleAsset = Resources.Load<TextAsset>("AAC/convenience_card_rules");
                if (ruleAsset != null)
                    _cardRuleBook = new ConvenienceCardRuleBook(ruleAsset.text);
                else
                    Debug.LogWarning("[TrainingSceneRoot] Resources/AAC/convenience_card_rules.json 없음 — 룰베이스 비활성, objective 필터로 폴백");
            }

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
        // scenarios.json의 turnLimit과 동기화. 초과 시 앱이 세션을 부드럽게 마무리한다.
        private const int TurnLimit = 12;

        // 시나리오별 objective 순서 (scenarios.json과 동기화)
        private static readonly Dictionary<string, string[]> ObjectiveOrderByScenario = new Dictionary<string, string[]>
        {
            { "pharmacy",    new[] { "greeting", "identify_needs", "serve_meds", "payment", "farewell" } },
            // 봉투를 먼저 묻고 결제한다 — 실제 편의점 흐름이자 페르소나("Always ask about 봉투 needed
            // before payment confirmation")와 few-shot 예시가 이미 전제하던 순서.
            { "convenience", new[] { "greeting", "select_items", "extras", "checkout", "farewell" } },
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
            { "greeting", "점원에게 인사하기" }, { "select_items", "원하는 상품 고르기" }, { "checkout", "결제 방법 말하기" },
            { "extras", "봉투 여부 답하기" }, { "farewell", "감사 인사하기" },
            { "identify_needs", "증상 말하기" }, { "serve_meds", "물품 요구하기" }, { "payment", "계산하기" },
            { "menu_browse", "메뉴 보기" }, { "order", "주문하기" }, { "order_modifications", "추가 주문" }
        };

        private string _lastNpcLine;
        // 마지막 점원 발화가 fallback("다시 말씀해주세요" 등)이었는지 — 카드 탭 시 이 경우에만 TTS 중단.
        private bool _lastNpcWasFallback;
        // 비풀 모드 fallback은 ApplyToolCall→HandleToolCall→SpeakNpc 경로라 다음 SpeakNpc에 fallback 여부 전달용.
        private bool _fallbackPending;
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

        // 서브플로(분기) 진입 시 점원 안내 대사 — branchId 기준. scenarios.json subflows와 동기화.
        // 안내만 하고 같은 objective를 유지(다음 단계로 넘기지 않음).
        private static readonly Dictionary<string, Dictionary<string, string>> SubflowPromptsByScenario = new Dictionary<string, Dictionary<string, string>>
        {
            { "convenience", new Dictionary<string, string>
                {
                    { "location_subflow", "음료는 저쪽 냉장고에 있어요. 천천히 보고 오세요." }
                }
            }
        };

        private bool TryGetSubflowPrompt(string branchId, out string line)
        {
            line = null;
            return !string.IsNullOrEmpty(branchId)
                   && SubflowPromptsByScenario.TryGetValue(scenarioId, out var map)
                   && map.TryGetValue(branchId, out line);
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();

            // 씬 진입 즉시 마이크 권한 다이얼로그 (STT 첫 호출 지연 방지 + 권한 거부 조기 노출)
            // Native/Cloud 둘 다 동일한 RECORD_AUDIO 권한 사용 — CloudSttService의 정적 헬퍼 재사용
            CloudSttService.RequestMicPermissionAsync(_cts.Token).Forget();

            // scenarios.json의 첫 objective가 모든 시나리오에서 "greeting"
            // 변경점: objective 순서와 "그 단계에 카드가 있는가" 판정을 DialogueManager에 주입.
            //   진행 판정이 MonoBehaviour에서 상태 머신으로 옮겨졌다 (CLAUDE.md 아키텍처 규칙).
            ObjectiveOrderByScenario.TryGetValue(scenarioId, out var objectiveOrder);
            _dialogueManager.Initialize("greeting", objectiveOrder, ObjectiveHasCards, TurnLimit);
            _dialogueManager.OnToolCallApplied += HandleToolCall;
            _dialogueManager.OnObjectiveChanged += HandleObjectiveChanged;

            uiView.OnCardTapped += HandleCardTapped;
            uiView.OnExtraRequested += HandleExtraRequested;
            uiView.OnFreeTalkToggled += HandleFreeTalkToggled;
            if (dashboardView != null)
            {
                dashboardView.OnProductSelected += HandleDashboardProductSelected;
                dashboardView.OnQuickPhraseSelected += HandleDashboardQuickPhraseSelected;
            }

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
            hud.OnGoReport += () => SceneManager.LoadScene("ReportScene");
            hud.SetObjective(0, StepperLabel("greeting"));
        }

        private static string StepperLabel(string objectiveId) =>
            !string.IsNullOrEmpty(objectiveId) && StepperLabels.TryGetValue(objectiveId, out var l) ? l : objectiveId;

        // NPC 대사 출력 공통 경로 — 말풍선 갱신 + TTS + 재청취용 보관 + TtsPlayed 기록 (레포트 진행 흐름)
        private void SpeakNpc(string line, Artti.AAC.DialogueTool tool = Artti.AAC.DialogueTool.PresentCards, bool isFallback = false)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lastNpcWasFallback = isFallback;
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
            uiView != null
            && ObjectiveOrderByScenario.ContainsKey(scenarioId)
            && (uiView.HasPharmacyCardPool || (dashboardView != null && dashboardView.HasProducts));

        private bool TryGetObjectivePrompt(string objectiveId, out string line)
        {
            line = null;
            return ObjectivePromptsByScenario.TryGetValue(scenarioId, out var map)
                   && map.TryGetValue(objectiveId, out line);
        }

        // 변경점: 단계가 넘어가는 턴에는 _pendingTurn을 통해 실제 점원 발화와 LLM 카드가 전달된다.
        //   이전에는 항상 (null, null)이라, 단계 전환 직후 카드가 고정 멘트 기준으로만 정해졌다.
        //   그래서 "우유 꺼내 드릴까요?"라고 물어놓고 물/과자/이거 살게요가 뜨는 일이 생겼다.
        private void RefreshPool() =>
            ApplyPool(ResolveCardPool(_pendingTurn?.CardIds, _pendingTurn?.NpcSpeech));

        // 카드 풀 해석 3단계 + 채우기.
        //   1) LLM이 지정한 card_ids 중 실재하는 것
        //   2) 룰베이스 — 방금 생성된 실제 점원 발화로 키워드 매칭
        //   3) objective 태그 필터
        // 그 뒤 PoolSize에 못 미치면 objective 카드로 채운다. 이 채우기가 card_cvs_yes/no처럼
        // 룰북 어느 규칙에도 없지만 objective 태그는 달려 있는 카드를 자동으로 끌어온다.
        private List<AACCard> ResolveCardPool(string[] llmCardIds, string npcSpeech)
        {
            var pool = new List<AACCard>(PoolSize);
            if (aacDatabase == null) return pool;

            var objective = _dialogueManager.CurrentObjectiveId;

            // 1) LLM 지정 — 단, 현재 단계에 맞는 카드만 받는다.
            //    LLM이 결제 단계에서 "물 주세요"를 끼워 넣는 일이 있어 무조건 신뢰하지 않는다.
            //    풀이 4칸을 못 채우면 그냥 적게 보여준다 (빈 슬롯은 SetCardList가 숨긴다).
            //    무관한 카드로 칸을 메우면 단계만 바뀔 뿐 같은 문제가 되살아난다.
            if (llmCardIds != null)
            {
                foreach (var id in llmCardIds)
                {
                    var c = aacDatabase.GetCard(id);
                    if (c == null)
                    {
                        Debug.LogWarning($"[TrainingSceneRoot] Gemini가 고른 카드 id 미존재: {id}");
                        continue;
                    }

                    if (IsApplicable(c, objective))
                    {
                        AddCard(pool, c);
                    }
                    else
                    {
                        // 버린다. 채워 넣으면 "계산 단계에 물 주세요"가 되살아난다.
                        Debug.Log($"[TrainingSceneRoot] '{objective}' 단계와 무관한 LLM 카드 제외: {c.id}");
                    }
                }
            }

            // 2) 룰베이스 — 변경점: 고정 멘트가 아니라 실제 점원 발화로 매칭한다.
            //    이전에는 TryGetObjectivePrompt의 고정 대사로 매칭해서, 점원이 실제로 물은 것과
            //    무관한 카드가 뜨는 원인이었다.
            if (pool.Count < PoolSize && _cardRuleBook != null && _cardRuleBook.IsLoaded)
            {
                var context = !string.IsNullOrWhiteSpace(npcSpeech)
                    ? npcSpeech
                    : (TryGetObjectivePrompt(objective, out var fixedLine) ? fixedLine : null);

                var match = context != null ? _cardRuleBook.Match(context) : null;
                match ??= _cardRuleBook.ResolveByObjective(objective);

                if (match != null)
                {
                    foreach (var id in match.CardIds)
                    {
                        var c = aacDatabase.GetCard(id);
                        if (c == null)
                            Debug.LogWarning($"[TrainingSceneRoot] 룰베이스 카드 id 미존재: {id} (규칙 {match.RuleId})");
                        else if (IsApplicable(c, objective))
                            AddCard(pool, c);
                        // 서브플로 규칙(location_subflow 등)은 키워드만 보고 걸리므로 단계 검사를 한 번 더 한다.
                        // 점원이 계산 중에 "어디"라고 말했다고 "음료 어디 있어요?"가 뜨면 안 된다.
                        if (pool.Count >= PoolSize) break;
                    }
                }
            }

            // 3) objective 태그 폴백 겸 빈 칸 채우기
            if (pool.Count < PoolSize)
            {
                foreach (var c in aacDatabase.CardsForObjective(scenarioId, objective))
                {
                    AddCard(pool, c);
                    if (pool.Count >= PoolSize) break;
                }
            }

            // 최후 방어 — 풀이 비면 사용자가 아무것도 못 누른다.
            if (pool.Count == 0)
            {
                Debug.LogWarning($"[TrainingSceneRoot] {scenarioId}/{objective} 카드 풀 비어있음 — 데이터 점검 필요");
                var help = aacDatabase.GetCard("card_cvs_help");
                if (help != null) pool.Add(help);
            }

            // 안전망 — 점원이 예/아니요 질문을 했는데 LLM이 답 카드를 안 골랐을 때 앱이 채운다.
            ApplyYesNoGuarantee(pool, npcSpeech);
            return pool;
        }

        // 예/아니요로 답할 수 없는 질문을 걸러내는 의문사 목록.
        private static readonly string[] WhWords =
            { "어떤", "어느", "무엇", "무슨", "뭐", "어디", "언제", "얼마", "몇", "왜", "누구", "어떻게" };

        // 점원 발화가 예/아니요 질문인지. 물음표로 끝나되 의문사가 없으면 yes/no로 본다.
        // ("어떤 과자 찾으세요?"는 의문사가 있어 제외 — 네/아니요로는 답이 안 된다)
        private static bool IsYesNoQuestion(string npcSpeech)
        {
            if (string.IsNullOrWhiteSpace(npcSpeech)) return false;

            var trimmed = npcSpeech.TrimEnd();
            if (!trimmed.EndsWith("?")) return false;

            for (int i = 0; i < WhWords.Length; i++)
                if (trimmed.Contains(WhWords[i])) return false;
            return true;
        }

        // 네/아니요를 풀 맨 앞 두 칸으로 올린다. 방금 받은 질문의 답이 제일 먼저 보이게 하려는 것.
        private void ApplyYesNoGuarantee(List<AACCard> pool, string npcSpeech)
        {
            if (scenarioId != ScenarioIds.Convenience) return;   // 편의점 전용 카드 id
            if (!IsYesNoQuestion(npcSpeech)) return;

            var yes = aacDatabase.GetCard("card_cvs_yes");
            var no  = aacDatabase.GetCard("card_cvs_no");
            if (yes == null || no == null) return;

            // 이미 뒤쪽에 들어와 있으면 떼어내고 앞으로 다시 붙인다.
            pool.RemoveAll(c => c != null && (c.id == yes.id || c.id == no.id));
            pool.Insert(0, no);
            pool.Insert(0, yes);
            if (pool.Count > PoolSize) pool.RemoveRange(PoolSize, pool.Count - PoolSize);
        }

        // 이 카드가 현재 단계에서 쓸 만한가. applicableObjectives가 비어 있으면 어느 단계에서나 허용.
        private static bool IsApplicable(AACCard card, string objective)
        {
            if (card == null) return false;
            if (card.applicableObjectives == null || card.applicableObjectives.Length == 0) return true;
            return System.Array.IndexOf(card.applicableObjectives, objective) >= 0;
        }

        // 같은 카드가 두 경로에서 중복으로 들어오는 것을 막는다.
        private static void AddCard(List<AACCard> pool, AACCard card)
        {
            if (card == null || pool.Count >= PoolSize) return;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && pool[i].id == card.id) return;
            pool.Add(card);
        }

        private void ApplyPool(List<AACCard> pool)
        {
            _currentPool = pool ?? new List<AACCard>();
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
                // 변경점: 고정 멘트는 "직전 도구가 발화를 주지 않았을 때"만 메꾼다(침묵 방어선).
                //   이전에는 _useGemini면 무조건 건너뛰어서, npc_speech 없는 mark_objective_complete로
                //   단계가 넘어가는 턴에 점원이 아무 말도 안 했다. 사용자에게는 대답이 한 턴 밀린 것처럼 보였다.
                if ((!_useGemini || !_lastToolSpoke) && TryGetObjectivePrompt(newObjectiveId, out var npcLine))
                {
                    SpeakNpc(npcLine);
                }
                RefreshPool();
            }
        }

        private void OnDestroy()
        {
            _freeTalkActive = false;
            if (dashboardView != null)
            {
                dashboardView.OnProductSelected -= HandleDashboardProductSelected;
                dashboardView.OnQuickPhraseSelected -= HandleDashboardQuickPhraseSelected;
            }
            // 완료 없이 씬 이탈 → 중단 처리 (마지막 objective 기록). 레포트의 미완료/연습 필요 집계에 사용
            if (!_sessionCompleted)
                _eventLogger?.LogSessionAbandoned(_dialogueManager?.CurrentObjectiveId);
            AppBootstrap.Instance?.LogStore?.FlushAsync().Forget();

            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async void HandleCardTapped(AACCard card)
        {
            // 대화하기 모드 중에는 카드 입력을 받지 않는다 (풀이 잠겨 있지만 방어적으로 한 번 더).
            if (_freeTalkActive) return;

            // 변경점: 직전 점원 발화가 fallback("다시 말씀해주세요" 등)이었다면, 카드를 누르는 순간
            //         재생을 중단해 마이크 입력과 겹치지 않게 함. 일반 안내 발화는 끝까지 재생.
            if (_lastNpcWasFallback)
                _ttsService?.StopAll();

            // 풀 모드: 자유 발화 연습 (PLAN.MD 5.4.2 / 7.3.1)
            // 카드 phrase TTS는 없음. STT로 사용자 발화 수집 후 다음 objective로 진행.
            if (IsPoolMode)
            {
                uiView.HideExtraModal();

                // 접근성(옵션): 카드 누를 때 문구를 음성으로 읽어줌 (기본 꺼짐)
                if (speakCardOnTap && _ttsService != null && card.phrase != null)
                {
                    var cue = !string.IsNullOrEmpty(card.phrase.ttsText) ? card.phrase.ttsText : card.phrase.text;
                    _ttsService.SpeakAsync(cue, _cts.Token).Forget();
                }

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
                    _dialogueManager.RegisterFailure();
                    _lastToolSpoke = true;
                    SpeakNpc(PickFallback("stt_empty"), isFallback: true);
                    return;
                }

                _dialogueManager.HandleUserTurn(card, sttResultP.text);

                // 변경점(Unit 3): LLM 주도 시나리오는 매 턴 Gemini가 응답·카드·진행을 결정.
                //   카드 탭 = 발화 보조일 뿐, 단계 진행은 Gemini 도구 호출로만 (자동 진행 제거).
                if (_useGemini)
                {
                    await RunGeminiTurn(card, sttResultP.text);
                    return;
                }

                // ── 이하 기존 룰베이스 흐름 (약국·음식점은 아직 이 경로) ──
                // 서브플로 카드(위치 문의 등): 점원이 안내만 하고 같은 objective 유지 — 다음 단계로 넘기지 않음
                // (scenarios.json location_subflow: "위치 안내 후 사용자가 다시 물건을 들고 오는 가정으로 진행")
                if (TryGetSubflowPrompt(card.branchId, out var subflowLine))
                {
                    SpeakNpc(subflowLine);
                    clerkView?.PlayNod();
                    RefreshPool(); // 같은 단계 풀 다시 표시
                    return;
                }

                // 성공: 짧은 긍정 피드백 (랜덤) 후 다음 단계
                hud?.ShowPraise();

                // 점원 반응: 물건 요청 단계면 집어서 건네기 + 카운터에 물건 등장, 그 외엔 끄덕임
                var curObjective = _dialogueManager.CurrentObjectiveId;
                bool isHandOver = HandOverObjectives.Contains(curObjective);
                if (clerkView != null)
                {
                    if (isHandOver) clerkView.PlayHandOver();
                    else clerkView.PlayNod();
                }
                if (isHandOver) counter?.PlaceItem(card.id); // 집기 애니 뒤 카운터에 등장(지연은 CounterDisplay가 처리)

                // 결제(checkout)·봉투/영수증(extras) 카드를 고르면 카운터 정리 — 구매 마무리 연출
                if (curObjective == "checkout" || curObjective == "extras")
                    counter?.ClearAll();

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
                _fallbackPending = true;
                _dialogueManager.ApplyToolCall(DialogueTool.RequestClarification, line, null);
                return;
            }

            // LLM 처리 동안 "생각하는 중" 인디케이터 (응답 지연/무응답 fallback 패턴 공통)
            hud?.ShowThinking(true);
            // 변경점(Unit 2): 하드코딩 "System Prompt" → 시나리오 페르소나 주입.
            //   userPrompt에 현재 목표/시도 맥락 추가. (대화 이력·슬롯은 Unit 4에서 보강)
            var userPrompt =
                $"[active_objective: {_dialogueManager.CurrentObjectiveId}] " +
                $"[attempt: {_dialogueManager.AttemptCount}, scaffold_level: {(int)_dialogueManager.CurrentScaffoldLevel}]\n" +
                $"User selected card '{card.id}' ({card.phrase?.text}) and said: \"{sttResult.text}\"";
            var turn = await _geminiService.RequestNextTurnAsync(_systemPrompt, userPrompt, _cts.Token);
            hud?.ShowThinking(false);

            // Gemini 실패/키없음 → fallback 응답으로 대체
            if (turn == null)
            {
                var line = PickFallback("llm_call_failed");
                _fallbackPending = true;
                _dialogueManager.ApplyToolCall(DialogueTool.RequestClarification, line, null);
                return;
            }

            _dialogueManager.ApplyToolCall(turn.Tool, turn.NpcSpeech, turn.CardIds);
        }

        // ===== 대화하기 (자유 대화 모드) =====
        //
        // 카드 없이 점원과 그냥 이야기하는 모드. 버튼 하나로 켜고 끄는 토글이다.
        // 켜져 있는 동안 objective와 스테퍼는 멈춘다 — 훈련 진행 지표를 잡담으로 오염시키지 않기 위해서.
        // 대화 이력은 훈련 흐름과 같은 곳에 쌓아, 카드 화면으로 돌아왔을 때 점원이 방금 한 잡담을 기억한다.

        private void HandleFreeTalkToggled()
        {
            if (_freeTalkActive) StopFreeTalk();
            else StartFreeTalk();
        }

        private void StartFreeTalk()
        {
            if (!IsPoolMode || _freeTalkActive || _sessionCompleted) return;

            _freeTalkActive = true;
            uiView.HideExtraModal();
            uiView.SetFreeTalkActive(true);
            dashboardView?.SetInteractionEnabled(false);
            _ttsService?.StopAll();   // 안내 발화가 흐르는 중이면 끊고 바로 듣기 시작
            FreeTalkLoop(_cts.Token).Forget();
        }

        private void StopFreeTalk()
        {
            if (!_freeTalkActive) return;

            _freeTalkActive = false;
            uiView.SetFreeTalkActive(false);
            dashboardView?.SetInteractionEnabled(true);
            uiView.ShowMicIndicator(false);
            RefreshPool();   // 멈춰 있던 objective 그대로 카드 풀 복원
        }

        private async UniTaskVoid FreeTalkLoop(CancellationToken ct)
        {
            if (_freeTalkBusy) return;
            _freeTalkBusy = true;
            try
            {
                while (_freeTalkActive && !ct.IsCancellationRequested)
                {
                    uiView.ShowMicIndicator(true);
                    var stt = await _sttService.ListenOnceAsync(ct);
                    uiView.ShowMicIndicator(false);
                    if (!_freeTalkActive) break;

                    uiView.ShowSttResult(stt.text);
                    if (string.IsNullOrWhiteSpace(stt.text))
                    {
                        SpeakNpc(PickFallback("stt_empty"), isFallback: true);
                        continue;
                    }

                    hud?.SetUserUtterance(stt.text);
                    hud?.ShowThinking(true);
                    var reply = await _geminiService.RequestFreeTalkAsync(
                        _freeTalkSystemPrompt, BuildFreeTalkUserPrompt(stt.text), ct);
                    hud?.ShowThinking(false);
                    if (!_freeTalkActive) break;

                    bool failed = string.IsNullOrWhiteSpace(reply);
                    var line = failed ? PickFallback("llm_call_failed") : reply;
                    _dialogueManager.RecordTurn(stt.text, line);
                    SpeakNpc(line, DialogueTool.PresentCards, isFallback: failed);
                }
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                _freeTalkBusy = false;
                hud?.ShowThinking(false);
                uiView.ShowMicIndicator(false);
            }
        }

        // 추천 상품은 STT를 다시 거치지 않고, 사용자가 해당 상품명을 말한 한 턴으로 Gemini에 전달한다.
        private void HandleDashboardProductSelected(string productId, string productName, string utterance)
        {
            RunDashboardTurn(productId, productName, utterance, true).Forget();
        }

        // 오른쪽 대화 힌트도 AAC 보조 입력이므로 누른 문장을 그대로 Gemini에 전달한다.
        private void HandleDashboardQuickPhraseSelected(string phrase)
        {
            RunDashboardTurn("quick_phrase", "대화 힌트", phrase, false).Forget();
        }

        private async UniTaskVoid RunDashboardTurn(
            string sourceId, string sourceName, string utterance, bool isProduct)
        {
            if (_dashboardTurnBusy || _freeTalkActive || _sessionCompleted || !_useGemini
                || string.IsNullOrWhiteSpace(utterance)) return;

            _dashboardTurnBusy = true;
            dashboardView?.SetInteractionEnabled(false);
            try
            {
                _ttsService?.StopAll();
                hud?.SetUserUtterance(utterance);
                uiView.ShowSttResult(utterance);
                _eventLogger?.LogCardSelected(sourceId, utterance, utterance);
                _dialogueManager.HandleUserTurn(null, utterance);

                string sourceContext = isProduct
                    ? $"The user tapped the recommended product '{sourceName}' (product_id={sourceId}). " +
                      $"Treat it as the direct customer utterance: \"{utterance}\". Respond about that exact product."
                    : $"The user tapped a dialogue-hint button. Treat it as the direct customer utterance: \"{utterance}\".";

                await RunGeminiTurn(null, utterance, sourceContext);
            }
            finally
            {
                _dashboardTurnBusy = false;
                if (!_freeTalkActive && !_sessionCompleted)
                    dashboardView?.SetInteractionEnabled(true);
            }
        }

        // 자유 대화용 시스템 프롬프트 — 시나리오 persona만 쓰고 도구·카드 규칙은 빼야 한다.
        // shared_preamble에는 "매 턴 반드시 도구를 하나 호출하라"가 들어 있어 평문 응답과 충돌한다.
        private static string BuildFreeTalkPrompt(string persona)
        {
            var basePersona = string.IsNullOrWhiteSpace(persona)
                ? "You are 편의점 점원 (a Korean convenience store clerk)."
                : persona;

            return basePersona +
                "\n\n--- FREE TALK MODE ---\n" +
                "You are having an ordinary conversation with the customer. There are no AAC cards and no tools this turn.\n" +
                "- Reply with plain text only. Never mention tools, cards, objectives, or training.\n" +
                "- Always answer in Korean (한국어), 존댓말, short and warm.\n" +
                "- Whatever the customer brings up (weather, small talk, something unrelated to the store), receive it naturally as a friendly clerk would.\n" +
                "- Do NOT push the shopping steps forward. Only if the conversation winds down on its own may you gently mention the counter.\n" +
                "- One question at a time. Keep it to one or two sentences.";
        }

        private string BuildFreeTalkUserPrompt(string sttText) =>
            $"Conversation so far (last turns):\n{_dialogueManager.RecentHistory()}\n\n" +
            $"The customer just said: \"{sttText}\"";

        private string PickFallback(string condition)
        {
            return _fallbackPicker != null
                ? _fallbackPicker.Pick(scenarioId, condition)
                : "잠시만요, 다시 한 번 말씀해주세요.";
        }

        // LLM 주도 한 턴. Gemini가 점원 발화 + 카드 + 진행 여부를 한 번에 결정한다.
        // 변경점: DialogueTurn으로 도구 인자 전체를 받아 objective_id·slots_filled·scaffold_level·
        //         subflow_id를 모두 반영한다. 이전에는 npc_speech와 card_ids만 살아남았다.
        private async UniTask RunGeminiTurn(AACCard card, string sttText, string sourceContext = null)
        {
            // 턴 상한 / 반복 실패 — 더 끌지 않고 부드럽게 마무리한다.
            // LLM 호출 "전"에 검사한다: 응답을 말한 직후 마무리 멘트를 덧붙이면 TTS가 서로 잘린다.
            // (이전에는 ShouldForceComplete가 정의만 되고 아무 데서도 호출되지 않았다)
            if (_dialogueManager.ShouldForceComplete())
            {
                _lastToolSpoke = true;
                SpeakNpc(PickFallback("turn_limit_reached"), DialogueTool.ForceCompleteScenario);
                MarkSessionCompleted();
                return;
            }

            hud?.ShowThinking(true);
            var turn = await _geminiService.RequestNextTurnAsync(
                _systemPrompt, BuildUserPrompt(card, sttText, sourceContext), _cts.Token);
            hud?.ShowThinking(false);

            // Gemini 실패/키없음 → fallback 발화 + 룰 기반 풀로 카드 유지(대화 끊기지 않게)
            if (turn == null)
            {
                _dialogueManager.RegisterFailure();
                _lastToolSpoke = true;
                SpeakNpc(PickFallback("llm_call_failed"), isFallback: true);
                RefreshPool();
                return;
            }

            // 침묵 방어선 — 이 턴에 점원이 실제로 말했는지. HandleObjectiveChanged가 고정 멘트로 메꿀지 판단한다.
            _lastToolSpoke = turn.HasSpeech;
            _pendingTurn = turn;
            _dialogueManager.RecordTurn(sttText, turn.NpcSpeech);
            SpeakNpc(turn.NpcSpeech, turn.Tool);

            try
            {
                switch (turn.Tool)
                {
                    case DialogueTool.MarkObjectiveComplete:
                    case DialogueTool.TransitionToObjective:
                        _dialogueManager.RegisterSuccess();
                        _dialogueManager.ApplySlots(turn.SlotsFilled);
                        clerkView?.PlayNod();
                        // 단계가 실제로 바뀌면 HandleObjectiveChanged가 새 풀을 소유한다(이중 갱신 방지).
                        var before = _dialogueManager.CurrentObjectiveId;
                        AdvanceObjective(turn.ObjectiveId);
                        if (!_sessionCompleted && _dialogueManager.CurrentObjectiveId == before)
                            ApplyPool(ResolveCardPool(turn.CardIds, turn.NpcSpeech)); // 역행 거부 등으로 제자리
                        break;

                    case DialogueTool.EnterSubflow:
                        _dialogueManager.RegisterSuccess();
                        _dialogueManager.EnterSubflow(turn.SubflowId, turn.PendingTopic);
                        clerkView?.PlayNod();
                        ApplyPool(ResolveCardPool(turn.CardIds, turn.NpcSpeech));
                        break;

                    case DialogueTool.ReturnFromSubflow:
                        _dialogueManager.RegisterSuccess();
                        _dialogueManager.ApplySlots(turn.SlotsFilled);
                        _dialogueManager.ReturnFromSubflow();
                        clerkView?.PlayNod();
                        ApplyPool(ResolveCardPool(turn.CardIds, turn.NpcSpeech));
                        break;

                    case DialogueTool.RequestClarification:
                        // 못 알아들은 턴 — 힌트 강도를 올린다.
                        _dialogueManager.RegisterFailure();
                        _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
                        ApplyPool(ResolveCardPool(turn.CardIds, turn.NpcSpeech));
                        break;

                    case DialogueTool.ForceCompleteScenario:
                        MarkSessionCompleted();
                        break;

                    default:
                        // present_cards / express_understanding — 같은 단계에서 대화를 이어감.
                        _dialogueManager.RegisterSuccess();
                        _dialogueManager.ApplyScaffoldHint(turn.ScaffoldLevel);
                        clerkView?.PlayNod();
                        ApplyPool(ResolveCardPool(turn.CardIds, turn.NpcSpeech));
                        break;
                }
            }
            finally { _pendingTurn = null; }
        }

        // 시나리오의 사용 가능한 카드 목록(id: 문구)을 시스템 프롬프트 뒤에 부착.
        private string AppendAvailableCards(string basePrompt)
        {
            if (aacDatabase == null) return basePrompt;
            var cards = aacDatabase.CardsForScenario(scenarioId).ToList();
            if (cards.Count == 0) return basePrompt;

            var sb = new System.Text.StringBuilder(basePrompt ?? string.Empty);
            sb.Append("\n\n--- AVAILABLE CARDS (use only these exact card_ids) ---");
            foreach (var c in cards)
                sb.Append($"\n{c.id}: {c.phrase?.text}");
            return sb.ToString();
        }

        // Gemini userPrompt: 최근 이력 + 현재 상태 + 이번 턴 발화. 시스템 프롬프트(페르소나·규칙)는 별도 전달.
        // 변경점: 슬롯과 서브플로 상태를 실제로 넘긴다. shared_preamble이 "슬롯과 진행 상태는 별도로
        //         전달되며 authoritative"라고 선언해 놓고 정작 안 넘기고 있었다.
        private string BuildUserPrompt(AACCard card, string sttText, string sourceContext = null)
        {
            var subflow = string.IsNullOrEmpty(_dialogueManager.ActiveSubflowId)
                ? "(none)"
                : $"{_dialogueManager.ActiveSubflowId} (pending: {_dialogueManager.PendingTopic ?? "-"})";

            string turnDescription = !string.IsNullOrWhiteSpace(sourceContext)
                ? sourceContext
                : card != null
                    ? $"The user tapped card '{card.id}' ({card.phrase?.text}) and said: \"{sttText}\""
                    : $"The customer said: \"{sttText}\"";

            return
                $"Conversation so far (last turns):\n{_dialogueManager.RecentHistory()}\n\n" +
                $"State: active_objective={_dialogueManager.CurrentObjectiveId}, " +
                $"turn={_dialogueManager.TurnCount}/{TurnLimit}, " +
                $"attempt={_dialogueManager.AttemptCount}, scaffold_level={(int)_dialogueManager.CurrentScaffoldLevel}\n" +
                $"Slots: {_dialogueManager.SlotsSnapshot()}\n" +
                $"Active subflow: {subflow}\n" +
                $"This turn: {turnDescription}";
        }

        // 해당 objective에 표시할 카드가 있는지 — DialogueManager의 진행 판정에 주입되는 델리게이트.
        private bool ObjectiveHasCards(string objectiveId) =>
            aacDatabase != null && aacDatabase.CardsForObjective(scenarioId, objectiveId).Any();

        // 변경점: 진행 판정을 DialogueManager.ResolveNextObjective로 위임(하이브리드 제어).
        //   requested가 있으면 LLM 제안을 검증해 반영하고, 없으면 순서상 다음 단계로 간다.
        private void AdvanceObjective(string requested = null)
        {
            var next = _dialogueManager.ResolveNextObjective(requested);
            if (string.IsNullOrEmpty(next))
            {
                // 역행 거부는 현재 단계 유지가 정답이고, 순서 끝 도달은 세션 완료다.
                if (IsLastObjective()) MarkSessionCompleted();
                return;
            }
            _dialogueManager.SetObjective(next);
        }

        private bool IsLastObjective() =>
            ObjectiveOrderByScenario.TryGetValue(scenarioId, out var order)
            && order.Length > 0
            && System.Array.IndexOf(order, _dialogueManager.CurrentObjectiveId) == order.Length - 1;

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
            SpeakNpc("정말 잘했어요! 다음에도 함께해요.");
            hud.ShowCompletion(
                Artti.Report.ReportLabels.ScenarioName(scenarioId),
                duration,
                $"{_objectivesEntered}단계 완료");
        }


        private void HandleToolCall(DialogueTool tool, string npcText, string[] args)
        {
            bool wasFallback = _fallbackPending;
            _fallbackPending = false;
            SpeakNpc(npcText, tool, wasFallback);

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
