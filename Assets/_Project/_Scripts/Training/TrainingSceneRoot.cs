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

        // 변경점: 화면 둘러보기(온보딩). 사용자마다 처음 한 번 자동으로 보여 주고, 도움말 버튼으로 다시 본다.
        //   처음 둘러보기가 끝나야 훈련(점원 첫 인사, 기록, 걸린 시간)이 시작된다.
        [Header("화면 둘러보기 (옵션 — 미연결 시 바로 훈련 시작)")]
        [SerializeField] private TrainingOnboardingView onboarding;

        [Header("Clerk 애니메이션 (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private ClerkView clerkView;

        // 변경점: 모드별 점원 배역 (대화 모드 = 남자 점원). 연결되어 있으면 Awake에서 clerkView·npcSpeechSource를 바꿔 끼운다.
        [Header("모드별 점원 배역 (옵션 — 미연결 시 clerkView 그대로)")]
        [SerializeField] private ClerkCast clerkCast;

        [Header("NPC 음성 (옵션 — 미연결 시 기존 TTS 소스 사용)")]
        [SerializeField] private AudioSource npcSpeechSource;
        public AudioSource NpcSpeechSource => npcSpeechSource != null ? npcSpeechSource : GetComponent<AudioSource>();

        [Header("카운터 물건 표시 (옵션 — 미와이어링 시 무동작)")]
        [SerializeField] private CounterDisplay counter;

        [Header("접근성 (옵션)")]
        [Tooltip("카드(대시보드 상품·대화 힌트 포함)를 누를 때 문장을 음성으로 읽어줌. 활성 프로필이 있으면 프로필 설정값으로 덮어쓴다 — 이 값은 프로필 없이 씬을 바로 실행할 때만 쓰인다")]
        [SerializeField] private bool speakCardOnTap = false;

        // 변경점: 서버PC에서 돌리는 음성 인식 서버. 지정하면 사용자 발화를 이 서버로 인식하고,
        //         서버에 닿지 않으면 기존 기기 음성 인식(Android 네이티브 / 에디터 더미)으로 넘어간다.
        [Header("STT 서버 (옵션 — 비우면 기기 음성 인식)")]
        [Tooltip("서버PC의 음성 인식 서버 주소. 예: http://192.168.0.167:8000 — 비우면 .env의 STT_SERVER_URL을 쓴다")]
        [SerializeField] private string sttServerUrl;
        [Tooltip("인식 요청 경로. 비우면 서버의 API 문서(/openapi.json 등)나 흔한 경로에서 자동으로 찾는다 (예: /stt, /transcribe)")]
        [SerializeField] private string sttServerPath;
        // 변경점: 두 모델 서버(Artti_ASR_dual_epoch2)에 보낼 동작 방식(훈련모드 안의 두 모드만).
        //   정확도 모드 = accuracy: 인식 문장과 명료도 모두 기존 명료도 학습 모델.
        //   대화 모드 = dialogue: 같은 녹음을 새 모델(어눌한 말 인식 → NPC 대화)과 기존 모델(목표 문장 대비 명료도)에 각각 보낸다.
        //   서버를 어느 실행 파일로 켜도 모드별로 같게 동작한다. 서버 문서의 mode 선택지에 값이 있을 때만 보내므로
        //   기존 한 모델 서버(8000)에는 아무것도 더하지 않는다. 비우면 보내지 않는다(서버 실행 설정을 따름).
        [Tooltip("정확도 모드에서 두 모델 서버에 보낼 mode. accuracy = 기존 명료도 모델만")]
        [SerializeField] private string sttServerModeAccuracy = "accuracy";
        [Tooltip("대화 모드에서 두 모델 서버에 보낼 mode. dialogue = 새 모델 인식(NPC 대화) + 기존 모델 명료도")]
        [SerializeField] private string sttServerModeConversation = "dialogue";

        // 변경점: 정확도 모드 명료도 관문 (ClarityGatePolicy). 대화 모드에는 적용하지 않는다.
        [Header("정확도 모드 명료도 기준")]
        [Tooltip("목표 문장을 말했을 때 서버 명료도가 이 값(%)보다 낮으면 같은 단계에서 다시 말하게 한다")]
        [SerializeField, Range(0, 100)] private int accuracyClarityMinPercent = 60;
        [Tooltip("같은 단계에서 이만큼 다시 말해도 기준 미달이면, 그다음 시도는 통과시킨다(막히지 않게)")]
        [SerializeField, Range(0, 5)] private int accuracyClarityMaxRetries = 2;
        private ClarityGatePolicy _clarityGate;

        // 변경점(사용자 결정): 현장지원모드(대화 모드)는 어눌한 말은 알아듣고 넘어가되, 뭉개진 말은 한 번 더 기회를 준다.
        //   뭉개짐 = 명료도 모델(기존 모델)이 들은 말이 해석된 문장과 이 값(0~100, ClarityEstimator)보다 덜 같을 때.
        //   모의 녹음 270개 기준 40: 약간 어눌함 90/90 통과, 중간 78/90 통과, 심함 49/90 통과. 뭉개 말한 실기기 4건(0~33)은 되묻기.
        [Header("현장지원모드 되묻기 기준")]
        [Tooltip("명료도 모델이 들은 말이 해석된 문장과 이 값(%)보다 덜 같으면 추천 문장을 한 번 더 말해 달라고 한다(두 번째는 넘어감)")]
        [SerializeField, Range(0, 100)] private int conversationRetryClarityPercent = 40;
        // 변경점: 두 모델(새 대화 모델·명료도 모델)이 같은 녹음을 이 값 이상 같게 들었으면 또렷한 말로 본다.
        //   모의 녹음 270개 기준 90: 약간 어눌함 84/90 합의, 뭉개진 말(목표 대비 40 미만) 53개 중 1개만 합의.
        //   쓰임: "아니요"처럼 추천 문장과 다른 말로 또렷하게 답하면 뭉개짐으로 보지 않는다.
        //         AI가 일상 대화로 판단했는데 두 모델이 다르게 들었으면 뭉개진 말로 보고 되묻는다(예전에는 그 단계에서 계속 맴돌았다)
        [Tooltip("두 모델이 들은 문장이 이 값(%) 이상 같으면 또렷한 말로 본다")]
        [SerializeField, Range(0, 100)] private int conversationModelAgreePercent = 90;

        private DialogueManager _dialogueManager;
        private ITtsService _ttsService;
        private ITtsService _npcTtsService;
        private ISttService _sttService;
        private FailoverSttService _sttFailover; // 서버 STT 미사용이면 null
        private AudioSource _cardSpeechSource;
        private int _speechRequestsInFlight; // TTS 요청을 보냈지만 아직 재생이 시작되지 않은 발화 수
        private CancellationTokenSource _speechCts; // 요청 중인 TTS까지 StopSpeech에서 끊기 위한 토큰
        private int _listeningCount; // 마이크로 사용자 발화를 듣는 중인 호출 수 (겹친 호출이 서로의 표시를 지우지 않게 센다)
        private GeminiDialogueService _geminiService;
        private string _systemPrompt; // system_prompts.json에서 시나리오별로 빌드 (Awake)
        // Unit 3: 풀 모드에서 룰베이스 대신 Gemini가 대화를 주도 (편의점).
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

        // 허브에서 고른 훈련 모드. 대화 모드는 어눌한 발화도 화면 문장으로 해석해 대화를 이어간다.
        private TrainingMode _mode;
        private bool IsConversationMode => _mode == TrainingMode.Conversation;

        private void Awake()
        {
            _dialogueManager = new DialogueManager();
            _mode = TrainingModeContext.Mode;
            _clarityGate = new ClarityGatePolicy(accuracyClarityMinPercent, accuracyClarityMaxRetries);

            // .env: GEMINI_API_KEY + GOOGLE_TTS_API_KEY 사용. STT는 OS 네이티브(키 불필요).
            // Android 단말이 ko-KR TTS 언어 데이터를 지원 안 해서 TTS만 Cloud로 유지.
            var geminiKey = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            var ttsKey    = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);

            // 변경점: 모드에 맞는 점원만 켜고, 그 점원의 뷰·음성 채널로 NPC 발화와 스피커 표시를 연결한다.
            if (clerkCast != null)
            {
                var actor = clerkCast.Activate(_mode);
                if (actor.view != null) clerkView = actor.view;
                if (actor.speech != null) npcSpeechSource = actor.speech;
                hud?.SetSpeakerSource(npcSpeechSource);
            }

            // 변경점: 카드 읽기와 점원 목소리는 항상 다른 채널. 점원 채널이 없거나 카드와 같으면
            //   기존 소스는 점원이 쓰고 카드 전용 소스를 새로 만든다(예전에는 같은 채널을 써 카드도 점원 목소리였다)
            _cardSpeechSource = GetComponent<AudioSource>();
            if (npcSpeechSource == null || npcSpeechSource == _cardSpeechSource)
            {
                var shared = _cardSpeechSource;
                if (npcSpeechSource == null) npcSpeechSource = shared != null ? shared : gameObject.AddComponent<AudioSource>();
                _cardSpeechSource = gameObject.AddComponent<AudioSource>();
                _cardSpeechSource.playOnAwake = false;
                _cardSpeechSource.spatialBlend = 0f;
                if (shared != null)
                {
                    _cardSpeechSource.volume = shared.volume;
                    _cardSpeechSource.outputAudioMixerGroup = shared.outputAudioMixerGroup;
                }
            }
            _ttsService = new CloudTtsService(ttsKey, _cardSpeechSource);
            _npcTtsService = new CloudTtsService(ttsKey, npcSpeechSource);
            // 변경점: 점원 목소리 = 화면에 나오는 점원의 성별(여자 점원 = 여성 / 남자 점원 = 남성, 이름은 TrainingModeContext).
            //   남자 점원이 연결되지 않아 여자 점원이 나오면 여성 목소리(예전: 모드만 보고 남성 목소리가 될 수 있었다)
            bool maleClerk = clerkCast != null ? clerkCast.UsesMaleClerk(_mode) : _mode == TrainingMode.Conversation;
            _npcTtsService.SetVoice((maleClerk ? ReadingVoice.Male : ReadingVoice.Female).ToVoiceName());
            Debug.Log($"[TrainingSceneRoot] 점원 목소리 {(maleClerk ? "남성" : "여성")}, 카드 읽기 목소리 {ProfileVoice.Voice.DisplayName()}");

            // 변경점: 활성 프로필의 카드 읽어주기·말 빠르기(점원·카드 음성 공통)를 적용한다.
            //   프로필 없이 씬을 바로 실행하면(에디터) Inspector 값과 보통 빠르기를 그대로 쓴다.
            var speechPrefs = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            if (speechPrefs != null)
            {
                speakCardOnTap = speechPrefs.speakCardOnTap;
                // 변경점: 카드 읽어주는 목소리 = 프로필 남녀 목소리(카드 채널은 항상 점원과 따로, ProfileVoice와 같은 기준)
                _ttsService.SetVoice(ProfileVoice.Voice.ToVoiceName());
                float rate = speechPrefs.speechRate.ToMultiplier();
                _ttsService.SetSpeakingRate(rate);
                _npcTtsService.SetSpeakingRate(rate);
            }

            ISttService deviceStt;
#if UNITY_ANDROID && !UNITY_EDITOR
            deviceStt = new AndroidNativeSttService();
#else
            // Editor: STT는 더미 텍스트로 풀 모드 흐름만 검증
            deviceStt = new EditorMockSttService();
#endif
            // 변경점: STT 서버 주소가 있으면 서버 인식을 먼저 쓰고, 쓸 수 없으면 기기 인식으로 넘긴다.
            //   Inspector 값이 우선, 비어 있으면 .env(STT_SERVER_URL). 둘 다 없으면 기존과 동일.
            bool fromInspector = !string.IsNullOrWhiteSpace(sttServerUrl);
            var serverUrl = fromInspector ? sttServerUrl : ApiKeyLoader.Get(ApiKeyLoader.SttServerUrl);
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                Debug.Log($"[TrainingSceneRoot] STT 서버 {serverUrl} ({(fromInspector ? "Inspector" : ".env STT_SERVER_URL")})");
                var serverStt = new ServerSttService(
                    serverUrl,
                    sttServerPath,
                    ApiKeyLoader.Get(ApiKeyLoader.SttServerApiKey),
                    ApiKeyLoader.Get(ApiKeyLoader.SttServerModel),
                    serverMode: IsConversationMode ? sttServerModeConversation : sttServerModeAccuracy);
                _sttFailover = new FailoverSttService(serverStt, deviceStt);
                _sttService = _sttFailover;
            }
            else
            {
                _sttService = deviceStt;
            }
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
            // 봉투를 먼저 묻고 결제한다 — 실제 편의점 흐름이자 페르소나("Always ask about 봉투 needed
            // before payment confirmation")와 few-shot 예시가 이미 전제하던 순서.
            { "convenience", new[] { "greeting", "select_items", "extras", "checkout", "farewell" } }
        };

        // 점원이 "물건을 건네는" 동작(HandOver)을 하는 objective. 그 외 성공은 끄덕임(Nod).
        private static readonly HashSet<string> HandOverObjectives = new HashSet<string>
        {
            "select_items" // 편의점 — 콜라 등 물건 건네기
        };

        // 진행 표시(스테퍼)용 objective 한국어 라벨 — 시안 기준 5단계 명칭
        private static readonly Dictionary<string, string> StepperLabels = new Dictionary<string, string>
        {
            { "greeting", "점원에게 인사하기" }, { "select_items", "원하는 상품 고르기" }, { "checkout", "결제 방법 말하기" },
            { "extras", "봉투 여부 답하기" }, { "farewell", "감사 인사하기" }
        };

        private string _lastNpcLine;
        // 마지막 점원 발화가 fallback("다시 말씀해주세요" 등)이었는지 — 카드 탭 시 이 경우에만 TTS 중단.
        private bool _lastNpcWasFallback;
        // 비풀 모드 fallback은 ApplyToolCall→HandleToolCall→SpeakNpc 경로라 다음 SpeakNpc에 fallback 여부 전달용.
        private bool _fallbackPending;
        private System.DateTimeOffset _sessionStartUtc;

        // v1 풀 모드: 각 objective 진입 시 NPC가 말하는 대사 (STT만 사용, Gemini 미사용 흐름)
        private static readonly Dictionary<string, Dictionary<string, string>> ObjectivePromptsByScenario = new Dictionary<string, Dictionary<string, string>>
        {
            { "convenience", new Dictionary<string, string>
                {
                    { "greeting",     "어서 오세요!" },
                    { "select_items", "찾으시는 물건 있으세요?" },
                    { "checkout",     "결제는 어떻게 하시겠어요?" },
                    { "extras",       "봉투 필요하세요?" },
                    { "farewell",     "안녕히 가세요!" }
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
            // 변경점: STT 서버 연결 확인·엔드포인트 탐색을 미리 해 둔다 (첫 발화 지연 방지)
            _sttFailover?.WarmUpAsync(_cts.Token).Forget();

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
                dashboardView.OnTypedUtterance += HandleTypedUtterance;
            }

            WireHud();
            UpdateDashboardDialogueContext();
            ShowInitialCards();

            // 변경점: 처음 들어온 사용자는 환영 창에서 화면 둘러보기 / 바로 시작을 고르고, 창이나 둘러보기가 닫히면 훈련을 시작한다.
            if (onboarding != null)
            {
                onboarding.SetMode(IsConversationMode);
                onboarding.OnStepShown += HandleOnboardingStep;
                onboarding.OnClosed += HandleOnboardingClosed;
            }
            if (onboarding != null && ShouldAutoShowOnboarding()) onboarding.ShowWelcome();
            else BeginSession();
        }

        // 훈련 시작: 기록(시작·첫 단계·걸린 시간 기준) + 점원 첫 인사. 둘러보기가 끝난 뒤 한 번만.
        private bool _sessionBegun;
        private static bool _onboardingShownWithoutProfile;   // 프로필 없이 바로 실행(에디터)할 때는 실행 중 한 번만

        private void BeginSession()
        {
            if (_sessionBegun) return;
            _sessionBegun = true;
            _eventLogger?.LogScenarioEntered(_mode);
            _eventLogger?.LogObjectiveEntered("greeting");
            _sessionStartUtc = System.DateTimeOffset.UtcNow;

            // 풀 모드: 첫 NPC 대사 + TTS (Initialize는 이벤트 안 쏘므로 수동 호출)
            if (IsPoolMode && TryGetObjectivePrompt("greeting", out var greetingLine))
            {
                SpeakNpc(greetingLine);
                clerkView?.PlayGreeting();
            }
        }

        private static bool ShouldAutoShowOnboarding()
        {
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            if (profile != null) return !profile.seenTrainingOnboarding;
            return !_onboardingShownWithoutProfile;
        }

        // 환영 창·둘러보기 문장은 카드 음성으로 읽어 준다(앞 문장·점원 말은 끊는다)
        private void HandleOnboardingStep(string text)
        {
            StopSpeech();
            if (_ttsService != null && !string.IsNullOrWhiteSpace(text))
                TrackSpeech(_ttsService.SpeakAsync(text.Replace('\n', ' '), SpeechToken)).Forget();
        }

        private void HandleOnboardingClosed()
        {
            StopSpeech();
            var manager = AppBootstrap.Instance?.ProfileManager;
            var profile = manager?.ActiveProfile;
            if (profile != null && !profile.seenTrainingOnboarding)
            {
                profile.seenTrainingOnboarding = true;
                manager.Save();
            }
            _onboardingShownWithoutProfile = true;
            BeginSession();   // 처음 둘러보기였으면 여기서 훈련 시작, 도움말로 다시 본 경우는 무동작
        }

        // ===== Convenience HUD 연동 (hud 미와이어링 씬에서는 전부 무동작) =====

        private void WireHud()
        {
            if (hud == null) return;
            // 일시정지 종료 — 중단 기록은 OnDestroy의 SessionAbandoned 경로가 처리
            // 변경점: 씬 전환은 페이드(SceneFader)로
            hud.OnExitRequested += () => SceneFader.Load("TrainingHubScene");
            hud.OnReplayRequested += ReplayNpcLine;
            hud.OnRetrySession += () => SceneFader.Load(SceneManager.GetActiveScene().name);
            hud.OnGoHub += () => SceneFader.Load("TrainingHubScene");
            hud.OnGoHome += () => SceneFader.Load("MainScene");
            // 변경점: 완료 화면 '리포트보기'는 방금 연습한 모드의 리포트로
            hud.OnGoReport += () => ReportNavigation.Open(_mode);
            hud.OnGoFieldMode += () => SceneFader.Load("ARFieldScene");
            hud.OnQuitApp += AppQuit.QuitApp;
            hud.SetObjective(0, StepperLabel("greeting"));
        }

        private static string StepperLabel(string objectiveId) =>
            !string.IsNullOrEmpty(objectiveId) && StepperLabels.TryGetValue(objectiveId, out var l) ? l : objectiveId;

        private void UpdateDashboardDialogueContext()
        {
            if (dashboardView == null || _dialogueManager == null) return;
            dashboardView.SetDialogueContext(
                _dialogueManager.CurrentObjectiveId,
                !string.IsNullOrEmpty(_dialogueManager.LastSlotListItem("items_requested")));
        }

        // NPC 대사 출력 공통 경로 — 말풍선 갱신 + TTS + 재청취용 보관 + TtsPlayed 기록 (레포트 진행 흐름)
        private void SpeakNpc(string line, Artti.AAC.DialogueTool tool = Artti.AAC.DialogueTool.PresentCards, bool isFallback = false)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lastNpcWasFallback = isFallback;
            _lastNpcLine = line;
            uiView.SetNPCDialogue(line);
            if (_npcTtsService != null)
                TrackSpeech(_npcTtsService.SpeakAsync(line, SpeechToken)).Forget();
            _eventLogger?.LogNpcTurn(line, tool);
        }

        // 변경점: "생각하는 중"(LLM 대기) 표시는 HUD(옛 시안)와 점원 말풍선(대시보드) 양쪽에 보낸다.
        private void ShowThinking(bool on)
        {
            hud?.ShowThinking(on);
            uiView.ShowNpcThinking(on);
        }

        private void ReplayNpcLine()
        {
            // 변경점: 듣는 중에는 다시 듣기를 막는다. 스피커로 나온 점원 목소리가 사용자 발화로 녹음되기 때문이다.
            if (_listeningCount > 0) return;
            if (!string.IsNullOrEmpty(_lastNpcLine) && _npcTtsService != null)
                TrackSpeech(_npcTtsService.SpeakAsync(_lastNpcLine, SpeechToken)).Forget();
        }

        // 변경점: TTS 요청용 토큰. StopSpeech가 취소하면 아직 도착하지 않은 음성도 재생되지 않는다.
        private CancellationToken SpeechToken
        {
            get
            {
                if (_cts == null || _cts.IsCancellationRequested) return new CancellationToken(true); // 씬 종료 뒤 늦게 온 호출
                return (_speechCts ??= CancellationTokenSource.CreateLinkedTokenSource(_cts.Token)).Token;
            }
        }

        // 변경점: TTS는 요청(네트워크)이 끝나야 재생이 시작돼 그 사이 AudioSource.isPlaying이 false다.
        //   "아직 말할 게 남았는가"를 알기 위해 요청 중인 발화 수를 센다 (WaitSpeechIdleAsync에서 사용).
        private async UniTaskVoid TrackSpeech(UniTask speak)
        {
            _speechRequestsInFlight++;
            try { await speak; }
            finally { _speechRequestsInFlight--; }
        }

        // 사용자 발화 한 번 듣기 — 마이크 표시 + STT.
        // 변경점: 서버 STT는 Unity 마이크로 직접 녹음하므로 점원(또는 카드) 음성이 스피커로 나오는 동안 녹음을
        //   시작하면 점원 목소리까지 인식된다. 서버로 들을 때는 음성이 끝난 뒤 마이크를 연다.
        //   stillWanted가 false가 되면(대화하기 종료 등) 기다린 뒤 녹음하지 않는다.
        private async UniTask<SttResult> ListenUserAsync(CancellationToken ct, System.Func<bool> stillWanted = null)
        {
            // 준비(서버 확인·점원 음성 대기)부터 듣기가 끝날 때까지 다시 듣기를 막는다
            _listeningCount++;
            try
            {
                var stt = _sttService;
                if (_sttFailover != null)
                {
                    try
                    {
                        stt = await _sttFailover.PrepareTurnAsync(WaitSpeechIdleAsync, ct);
                    }
                    catch (System.OperationCanceledException)
                    {
                        return new SttResult { text = "", isEmpty = true };
                    }
                    if (stillWanted != null && !stillWanted())
                        return new SttResult { text = "", isEmpty = true };
                }

                uiView.ShowMicIndicator(true);
                return await stt.ListenOnceAsync(ct);
            }
            finally
            {
                _listeningCount--;
            }
        }

        private const float SpeechIdleWaitMaxSeconds = 15f;
        // AudioSource가 멈춘 뒤에도 기기 출력 버퍼에 남은 소리가 스피커로 나오는 시간 (Android 약 150~250ms)
        private const int SpeechOutputDrainMs = 250;

        private async UniTask WaitSpeechIdleAsync(CancellationToken ct)
        {
            var npcSource = NpcSpeechSource;
            float deadline = Time.unscaledTime + SpeechIdleWaitMaxSeconds;
            bool waited = false;
            while (Time.unscaledTime < deadline &&
                   (_speechRequestsInFlight > 0
                    || (npcSource != null && npcSource.isPlaying)
                    || (_cardSpeechSource != null && _cardSpeechSource.isPlaying)))
            {
                waited = true;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (waited) await UniTask.Delay(SpeechOutputDrainMs, DelayType.Realtime, PlayerLoopTiming.Update, ct);
        }

        // 변경점: 카드 음성으로 문장을 읽고 스피커 출력이 끝날 때까지 기다린다.
        //   CloudTtsService.SpeakAsync는 재생을 시작하면 바로 돌아오므로, 재생 종료는 WaitSpeechIdleAsync로 기다린다.
        private async UniTask SpeakCardAsync(string text, CancellationToken ct)
        {
            if (_ttsService == null || string.IsNullOrWhiteSpace(text)) return;
            TrackSpeech(_ttsService.SpeakAsync(text, ct)).Forget();
            await WaitSpeechIdleAsync(ct);
        }

        private void StopSpeech()
        {
            // 변경점: 재생 중인 음성뿐 아니라 요청 중인 TTS도 취소한다.
            //   AudioSource.Stop만 하면 요청이 도착한 뒤 재생이 시작돼, 멈춘 줄 알았던 점원이 말을 이어 간다.
            if (_speechCts != null)
            {
                _speechCts.Cancel();
                _speechCts.Dispose();
                _speechCts = null;
            }
            _ttsService?.StopAll();
            if (!object.ReferenceEquals(_npcTtsService, _ttsService))
                _npcTtsService?.StopAll();
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
            && (uiView.HasCardPool || (dashboardView != null && dashboardView.HasProducts));

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
            UpdateDashboardDialogueContext();
        }

        private void OnDestroy()
        {
            _freeTalkActive = false;
            if (dashboardView != null)
            {
                dashboardView.OnProductSelected -= HandleDashboardProductSelected;
                dashboardView.OnQuickPhraseSelected -= HandleDashboardQuickPhraseSelected;
                dashboardView.OnTypedUtterance -= HandleTypedUtterance;
            }
            if (onboarding != null)
            {
                onboarding.OnStepShown -= HandleOnboardingStep;
                onboarding.OnClosed -= HandleOnboardingClosed;
            }
            // 완료 없이 씬 이탈 → 중단 처리 (마지막 objective 기록). 레포트의 미완료/연습 필요 집계에 사용
            //   변경점: 둘러보기 중에 나가 훈련을 시작하지 않았으면 기록하지 않는다.
            if (_sessionBegun && !_sessionCompleted)
                _eventLogger?.LogSessionAbandoned(_dialogueManager?.CurrentObjectiveId);
            AppBootstrap.Instance?.LogStore?.FlushAsync().Forget();

            _cts?.Cancel();
            StopSpeech();
            _cts?.Dispose();
        }

        private async void HandleCardTapped(AACCard card)
        {
            // 대화하기 모드 중에는 카드 입력을 받지 않는다 (풀이 잠겨 있지만 방어적으로 한 번 더).
            if (_freeTalkActive) return;

            // 변경점: 직전 점원 발화가 fallback("다시 말씀해주세요" 등)이었다면, 카드를 누르는 순간
            //         재생을 중단해 마이크 입력과 겹치지 않게 함. 일반 안내 발화는 끝까지 재생.
            if (_lastNpcWasFallback)
                StopSpeech();

            // 풀 모드: 자유 발화 연습 (PLAN.MD 5.4.2 / 7.3.1)
            // 카드 phrase TTS는 없음. STT로 사용자 발화 수집 후 다음 objective로 진행.
            if (IsPoolMode)
            {
                uiView.HideExtraModal();

                // 접근성(옵션): 카드 누를 때 문구를 음성으로 읽어줌 (기본 꺼짐)
                if (speakCardOnTap && _ttsService != null && card.phrase != null)
                {
                    var cue = !string.IsNullOrEmpty(card.phrase.ttsText) ? card.phrase.ttsText : card.phrase.text;
                    TrackSpeech(_ttsService.SpeakAsync(cue, SpeechToken)).Forget();
                }

                // 시안 3.png: 선택 카드 강조 + 나머지 터치 비활성·흐림, 사용자 발화 버블 표시
                hud?.SetUserUtterance(card.phrase?.text);
                uiView.SetPoolLocked(card);

                // 변경점: 명료도를 계산하는 STT(서버 STT)에는 탭한 카드 문구를 목표 문장으로 넘긴다.
                (_sttService as ITargetedSttService)?.SetTarget(card.phrase?.text);
                var sttResultP = await ListenUserAsync(_cts.Token);
                uiView.ShowSttResult(sttResultP.text);
                uiView.UnlockPool();
                // 변경점: 정확도 모드에서 서버 명료도가 없으면(기기 STT) 리포트용 추정치를 채워 기록한다. 관문은 아래에서 서버 값으로만
                _eventLogger?.LogCardSelected(card.id, card.phrase?.text,
                    IsConversationMode ? sttResultP : ClarityEstimator.FillIfMissing(sttResultP, card.phrase?.text));

                // 빈 결과 시 fallback 전용 대사 후 같은 objective 유지 — 2차 시도(scaffold_level=1)는 같은 카드 유지 (PLAN.MD 7.3.1)
                // 변경점: 대화(현장지원) 모드는 한 번만 다시 묻고, 또 못 들으면 누른 카드 문장으로 진행한다
                bool cardMoveOn = IsConversationMode && !string.IsNullOrWhiteSpace(card.phrase?.text)
                                  && _unclearRetries >= UnclearRetryLimit;
                if (string.IsNullOrWhiteSpace(sttResultP.text) && !cardMoveOn)
                {
                    if (IsConversationMode)
                    {
                        // 시나리오 실패 횟수(4회면 강제 종료)에 넣지 않고 되묻기 횟수로 센다. 발화는 위에서 이미 기록됨
                        AskConversationRetry(sttResultP, card.phrase?.text, "카드 말하기 못 들음", logUtterance: false, askChoice: false);   // 변경점: askChoice
                        return;
                    }
                    _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
                    _lastToolSpoke = true;
                    _dialogueManager.RegisterFailure();
                    SpeakNpc(PickFallback("stt_empty"), isFallback: true);
                    return;
                }
                // 변경점: 카드 문장을 뭉개서 말했으면 한 번 더 기회(두 번째는 카드 문장으로 진행)
                if (IsConversationMode && !cardMoveOn && IsGarbled(sttResultP.clarityText, card.phrase?.text))
                {
                    AskConversationRetry(sttResultP, card.phrase?.text, "카드 말하기 뭉개짐", logUtterance: false, askChoice: false);   // 변경점: askChoice
                    return;
                }
                if (IsConversationMode) _unclearRetries = 0;   // 진행하는 턴이면 되묻기 횟수를 비운다

                // 변경점: 정확도 모드 명료도 관문 (발화는 위 LogCardSelected에서 이미 기록됨)
                if (BlockLowClarity(card.id, sttResultP, logUtterance: false)) return;

                // 대화 모드: 어눌하게 말해도 탭한 카드 문장을 말한 것으로 보고 진행한다.
                //   인식 원문·명료도는 위 LogCardSelected에서 이미 기록됐다.
                var turnText = IsConversationMode && !string.IsNullOrWhiteSpace(card.phrase?.text)
                    ? card.phrase.text
                    : sttResultP.text;

                _dialogueManager.HandleUserTurn(card, turnText);

                // 변경점(Unit 3): LLM 주도 시나리오는 매 턴 Gemini가 응답·카드·진행을 결정.
                //   카드 탭 = 발화 보조일 뿐, 단계 진행은 Gemini 도구 호출로만 (자동 진행 제거).
                if (_useGemini)
                {
                    await RunGeminiTurn(card, turnText);
                    return;
                }

                // ── 이하 기존 룰베이스 흐름 (Gemini 미사용 시) ──
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
                TrackSpeech(_ttsService.SpeakAsync(spokenText, SpeechToken)).Forget();
            }

            var sttResult = await ListenUserAsync(_cts.Token);
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
            ShowThinking(true);
            // 변경점(Unit 2): 하드코딩 "System Prompt" → 시나리오 페르소나 주입.
            //   userPrompt에 현재 목표/시도 맥락 추가. (대화 이력·슬롯은 Unit 4에서 보강)
            var userPrompt =
                $"[active_objective: {_dialogueManager.CurrentObjectiveId}] " +
                $"[attempt: {_dialogueManager.AttemptCount}, scaffold_level: {(int)_dialogueManager.CurrentScaffoldLevel}]\n" +
                $"User selected card '{card.id}' ({card.phrase?.text}) and said: \"{sttResult.text}\"";
            var turn = await _geminiService.RequestNextTurnAsync(_systemPrompt, userPrompt, _cts.Token);
            ShowThinking(false);

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
            // 편의점 대시보드의 큰 음성 버튼은 현재 단계의 말하기 연습용이다.
            // 한 번 누를 때 한 문장을 듣고, 인식된 실제 발화로 대화/단계를 진행한다.
            if (scenarioId == ScenarioIds.Convenience && dashboardView != null)
            {
                RunGuidedVoiceTurn().Forget();
                return;
            }

            if (_freeTalkActive) StopFreeTalk();
            else StartFreeTalk();
        }

        private async UniTaskVoid RunGuidedVoiceTurn()
        {
            if (_dashboardTurnBusy || _freeTalkActive || _sessionCompleted) return;

            _dashboardTurnBusy = true;
            dashboardView?.SetInteractionEnabled(false);
            string recognized = null;
            string utterance = null; // 대화에 넘길 문장. 정확도 모드 = 인식 원문, 대화 모드 = 후보 문장으로 해석한 결과
            bool smallTalk = false;  // 변경점: 대화 모드에서 화면 문장이 아닌 일상 대화로 판단됨(utterance = 인식 원문)
            SttResult spoken = default;
            try
            {
                uiView.HideExtraModal();
                StopSpeech();
                // 변경점: 명료도 기준 문장 = 대시보드에서 강조된 추천 문장.
                string clarityTarget = dashboardView != null ? dashboardView.RecommendedPhrase : null;
                (_sttService as ITargetedSttService)?.SetTarget(clarityTarget);
                var stt = await ListenUserAsync(_cts.Token);
                recognized = stt.text;
                uiView.ShowSttResult(recognized);

                // 변경점: 추천 문장이 아닌 다른 정답(다른 힌트·다른 상품)을 말했으면 목표 대비 명료도는 의미가 없으므로 버린다.
                //   대화 모드는 뭉개진 말이 대부분 다른 후보에 더 가까워 보여 다 버려졌으므로, 해석 뒤 ConversationClarity로 정한다
                spoken = stt;
                if (!IsConversationMode && spoken.hasClarity && dashboardView != null
                    && !ConvenienceQuickPhrasePolicy.IsAttemptAtTarget(recognized, clarityTarget, dashboardView.GetSpeechCandidates()))
                    spoken.hasClarity = false;

                // 변경점: 정확도 모드에서 서버 명료도가 없으면(기기 STT) 목표 문장을 말하려던 발화에 리포트용 추정치를 채운다.
                //   다시 말하기 관문은 서버 명료도(gateInput)로만 막는다(기존 동작)
                var gateInput = spoken;
                if (!IsConversationMode && !spoken.hasClarity && dashboardView != null
                    && ConvenienceQuickPhrasePolicy.IsAttemptAtTarget(recognized, clarityTarget, dashboardView.GetSpeechCandidates()))
                    spoken = ClarityEstimator.FillIfMissing(spoken, clarityTarget);

                bool heardNothing = string.IsNullOrWhiteSpace(recognized);
                _lastAnswerWasDecline = false;   // 변경점: 이번 턴 해석 전에는 거절 답이 아니다(이전 턴 값이 남지 않게)
                // 변경점: 대화(현장지원) 모드는 아무것도 못 들은 것도 '다시 말해 줄래요' 횟수에 넣는다.
                //   한 번 다시 묻고, 또 못 들으면 다음 대화로 넘어간다. 아무 소리도 못 들어 고를 단서가 없을 때만 추천 문장을 쓴다
                //   (변경점, 사용자 결정 2026-10-06: 들린 말이 있으면 추천 문장이 아니라 가장 가까운 선택지로 넘어간다 - ClosestCandidate)
                if (heardNothing && IsConversationMode && dashboardView != null
                    && _unclearRetries >= UnclearRetryLimit && !string.IsNullOrWhiteSpace(clarityTarget))
                {
                    utterance = ConvenienceQuickPhrasePolicy.ConversationFallback(recognized, clarityTarget);
                    Debug.Log($"[TrainingSceneRoot] 대화 모드: 다시 말해도 못 들음 -> 추천 문장 '{utterance}'로 진행");
                }
                else if (heardNothing)
                {
                    if (IsConversationMode && dashboardView != null)
                    {
                        // 대화 모드: 시나리오 실패 횟수(RegisterFailure, 4회면 강제 종료)에 넣지 않고 되묻기 횟수로 센다
                        AskConversationRetry(spoken, clarityTarget, "못 들음");
                        return;
                    }
                    _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
                    _dialogueManager.RegisterFailure();
                    SpeakNpc("괜찮아요. 왼쪽 문장을 보고 다시 말해볼까요?", isFallback: true);   // 변경점: 문장 버튼은 왼쪽에 있다
                    return;
                }
                else
                {
                    // 변경점: 정확도 모드는 목표 문장을 또렷하게 말해야 다음으로 넘어간다(명료도 관문).
                    if (BlockLowClarity("guided_voice", gateInput, logUtterance: true)) return;

                    // 대화 모드: 어눌하게 말해도 화면 후보 문장으로 해석해 진행한다 (규칙 -> 애매하면 AI -> 추천 문장).
                    //   인식 원문과 명료도는 spoken에 그대로 남아 레포트에 기록된다. 정확도 모드는 기존 흐름 그대로.
                    if (IsConversationMode && dashboardView != null)
                    {
                        // 변경점: 명료도 모델이 들은 문장도 함께 넘긴다(ResolveConversationUtteranceAsync 안에서 추측에 합친다)
                        var resolved = await ResolveConversationUtteranceAsync(recognized, stt.alternatives, stt.clarityText, clarityTarget, _cts.Token);
                        utterance = resolved.phrase;
                        smallTalk = resolved.smallTalk;
                    }
                    else utterance = recognized;

                    // 변경점: AI가 일상 대화로 판단했어도 두 모델이 서로 다르게 들었으면 뭉개진 말이다(봉투 질문에서 "예요요요"가 일상 대화로 맴돎).
                    //   한 번 되묻고, 또 뭉개지면 넘어간다. 추천 문장이 아니라 들린 소리에 가장 가까운 선택지로(변경점, 사용자 결정 2026-10-06).
                    //   명료도 모델 문장이 없으면(기기 음성 인식) 예전처럼 일상 대화
                    if (IsConversationMode && dashboardView != null && smallTalk && stt.clarityText != null
                        && !ModelsAgree(recognized, stt.clarityText))
                    {
                        if (_unclearRetries < UnclearRetryLimit)
                        {
                            Debug.Log($"[TrainingSceneRoot] 대화 모드 뭉개짐(일상 대화로 판단됨): 새 모델 '{recognized}' / 명료도 모델 '{stt.clarityText}'");
                            AskConversationRetry(spoken, clarityTarget, "뭉개진 발화");
                            return;
                        }
                        utterance = ClosestCandidate(recognized, stt.alternatives, stt.clarityText, clarityTarget);
                        smallTalk = false;
                        Debug.Log($"[TrainingSceneRoot] 대화 모드: 다시 말해도 뭉개짐 -> 가장 가까운 선택지 '{utterance}'로 진행");
                    }

                    // 변경점(사용자 결정): 뭉개진 말은 새 모델·AI가 그럴듯한 문장으로 맞춰도 바로 넘기지 않는다.
                    //   명료도 모델이 들은 말이 해석된 문장과 많이 다르면 한 번 더 기회를 주고, 두 번째에는 해석된 문장으로 진행한다
                    //   변경점: 두 모델이 거의 같게 들었으면 다른 말로 또렷하게 답한 것("아니요" -> 봉투 필요 없어요)이므로 되묻지 않는다
                    if (IsConversationMode && dashboardView != null && utterance != null && !smallTalk
                        && !_lastAnswerWasDecline   // 변경점: "아니요/없어요"로 알아들었으면 뭉개짐으로 되묻지 않는다(해석 문장과 소리가 달라 늘 뭉개짐으로 보임)
                        && _unclearRetries < UnclearRetryLimit && IsGarbled(stt.clarityText, utterance)
                        && !ModelsAgree(recognized, stt.clarityText))
                    {
                        Debug.Log($"[TrainingSceneRoot] 대화 모드 뭉개짐: 해석 '{utterance}' / 명료도 모델 '{stt.clarityText}'");
                        AskConversationRetry(spoken, clarityTarget, "뭉개진 발화");
                        return;
                    }
                }

                // 변경점: 못 알아들었으면 한 번 다시 말해 달라고 한다. 인식 원문과 재시도는 기록하고 이번 턴은 진행하지 않는다
                //   (DialogueManager.RegisterFailure는 부르지 않는다: 실패 4회면 시나리오가 강제 종료된다)
                if (utterance == null)
                {
                    AskConversationRetry(spoken, clarityTarget, "해석 실패");
                    return;
                }
                // 변경점: 거절 답은 일상 대화처럼 명료도를 기록하지 않는다(해석된 '계산할게요'와 '없어요'의 문장 차이는 발음이 아니다)
                if (IsConversationMode) spoken = ConversationClarity(spoken, utterance, clarityTarget, smallTalk || _lastAnswerWasDecline);
            }
            finally
            {
                _dashboardTurnBusy = false;
                if (!_sessionCompleted) dashboardView?.SetInteractionEnabled(true);
            }

            // 변경점: 일상 대화는 점원이 자연스럽게 받아 주고 지금 단계로 부드럽게 돌아온다(단계 진행·장바구니 변화 없음)
            if (smallTalk)
            {
                RunDashboardTurn("guided_voice", "음성으로 말하기", utterance, false, null, spoken, smallTalk: true,
                    logSourceId: Artti.Report.ReportDataService.FreeSpeechSource).Forget();
                return;
            }

            bool isWater = IsWaterRequest(utterance);
            // 변경점: "과자 주세요"처럼 추천 상품 요청이면 그 상품을 누른 것과 똑같이 처리한다(장바구니에 그 상품).
            //   예전에는 대화 힌트처럼 AI에게만 넘겨 과자가 장바구니에 안 들어갈 수 있었다. "물 주세요"는 기존 정책(ResolveWater) 그대로
            if (!isWater && dashboardView != null && dashboardView.TryFindProduct(utterance, out var productId, out var productName))
            {
                // 변경점: 목소리로 한 상품 요청은 음성 턴으로 기록한다(상품 id로 기록하면 리포트가 버튼을 누른 것으로 본다)
                RunDashboardTurn(productId, productName, utterance, true,
                    IsConvenienceItemSelectionStep() ? productName : null, spoken, logSourceId: "guided_voice").Forget();
                return;
            }
            bool captureWater = IsConvenienceItemSelectionStep() && isWater;
            RunDashboardTurn(
                "guided_voice",
                "음성으로 말하기",
                utterance,
                false,
                captureWater ? "물" : null,
                spoken).Forget();
        }

        private static bool IsWaterRequest(string text) =>
            string.Equals(text?.Trim().TrimEnd('?', '!', '.', '…'), "물 주세요", System.StringComparison.Ordinal);

        // 정확도 모드 명료도 관문. 기준 미달이면 점원이 다시 말해 달라고 하고 true를 돌려준다(이번 턴은 진행하지 않음).
        //   명료도가 없으면(다른 후보 문장을 말함, 명료도를 주지 않는 기기 STT) 막지 않는다.
        //   기준 미달 시도도 레포트에 남도록 발화(명료도 포함)와 재시도를 기록한다.
        //   DialogueManager.RegisterFailure는 부르지 않는다: 실패 4회면 시나리오가 강제 종료되기 때문(횟수는 관문이 따로 센다).
        private bool BlockLowClarity(string sourceId, SttResult spoken, bool logUtterance)
        {
            if (IsConversationMode || _clarityGate == null) return false;
            var verdict = _clarityGate.Evaluate(_dialogueManager.CurrentObjectiveId, spoken.hasClarity, spoken.clarityPercent);
            if (verdict == ClarityVerdict.PassAfterRetries)
                Debug.Log($"[TrainingSceneRoot] 명료도 {spoken.clarityPercent}% 기준 미달이지만 {_clarityGate.Failures}번 다시 말해서 통과");
            if (verdict != ClarityVerdict.Retry) return false;

            Debug.Log($"[TrainingSceneRoot] 명료도 {spoken.clarityPercent}% < {_clarityGate.MinPercent}% -> 다시 말하기 ({_clarityGate.Failures}/{_clarityGate.MaxRetries})");
            if (logUtterance)
            {
                _eventLogger?.LogCardSelected(sourceId, spoken.text, spoken);
                hud?.SetUserUtterance(spoken.text);
            }
            _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
            _lastToolSpoke = true;
            SpeakNpc(_clarityGate.RetryLine(), isFallback: true);
            return true;
        }

        // 대화 모드 문장 해석: 1) 규칙(틀린 글자 수)으로 확실하면 바로 결정
        //   2) 애매하면 Gemini가 뜻으로 판단 (최대 ConversationJudgeTimeoutSeconds초)
        //   3) Gemini가 '알아들었지만 화면 문장이 아님(일상 대화)'이라고 하면 점원이 그 말을 받아 주고 지금 단계에 머문다
        //   4) Gemini도 모르거나 늦으면 한 번은 다시 말해 달라고 하고(null), 두 번째에도 모르면 들린 소리에 가장 가까운 선택지로 넘어간다
        //      (변경점, 사용자 결정 2026-10-06: 2026-10-02에는 두 번째에 고정된 추천 문장으로 넘겼으나, 결제에서 "현금"이 "카드"가 됐다)
        private const float ConversationJudgeTimeoutSeconds = 3f;
        private const int UnclearRetryLimit = 1;
        private const string UnclearRetryLine = "잘 못 들었어요. 한 번만 더 말해 줄래요?";
        // 변경점(사용자 결정 2026-10-06): 되묻기는 추천 문장을 지목하지 않는다. 사용자가 무슨 말을 할지 모르므로
        //   단계의 질문을 그대로 다시 묻는다(점원이 처음 물을 때 ConvenienceQuickPhrasePolicy와 같은 선택지).
        //   한 번 되물은 뒤에도 못 알아들으면 되묻지 않고 넘어간다(들린 소리에 가장 가까운 선택지, ClosestCandidate).

        private static string StepRetryLine(string objectiveId)
        {
            switch (objectiveId)
            {
                case "checkout": return "카드와 현금 중 무엇으로 하시겠어요?";
                case "extras": return "봉투가 필요하세요?";
                case "select_items": return "더 필요한 물건이 있으세요?";
                case "greeting": return "어떤 물건이 필요하세요?";
                default: return UnclearRetryLine;
            }
        }
        private string _lastRetryLine;   // 마지막으로 한 되묻기 문장(AI 판단 때 원래 질문을 맥락으로 쓰려고)

        // 변경점: 현장지원모드 되묻기(못 들음·뜻 모름·뭉개짐 공통).
        //   시나리오 실패 횟수(RegisterFailure, 4회면 강제 종료)에는 넣지 않는다. 알아들은 턴이 진행되면 횟수는 0
        //   변경점(사용자 결정 2026-10-06): 음성 되묻기는 추천 문장을 지목하지 않고 단계의 질문을 다시 묻는다(askChoice, StepRetryLine).
        //   카드를 눌러 그 문장을 따라 읽는 경우(askChoice=false)는 읽을 문장이 정해져 있으므로 그대로 그 문장을 말해 달라고 한다
        private void AskConversationRetry(SttResult spoken, string target, string reason, bool logUtterance = true, bool askChoice = true)
        {
            if (logUtterance && !string.IsNullOrWhiteSpace(spoken.text))
            {
                // 못 알아들은 말은 자유 발화로 기록한다(리포트 '전체 보기'에서 모범 발음 없이 내 발음만)
                _eventLogger?.LogCardSelected(Artti.Report.ReportDataService.FreeSpeechSource, spoken.text, spoken);
                hud?.SetUserUtterance(spoken.text);
            }
            _eventLogger?.LogStepRetryAttempt(_dialogueManager.CurrentObjectiveId);
            _unclearRetries++;
            if (_lastNpcLine != _lastRetryLine) _clerkLineBeforeRetry = _lastNpcLine;   // 다시 말했을 때 AI가 원래 질문을 맥락으로 쓰게
            _lastRetryLine = askChoice ? StepRetryLine(_dialogueManager.CurrentObjectiveId) : RetryLineFor(target);
            _lastToolSpoke = true;
            Debug.Log($"[TrainingSceneRoot] 대화 모드 {reason} -> 다시 말해 달라고 함 ({_unclearRetries}/{UnclearRetryLimit}): '{_lastRetryLine}'");
            SpeakNpc(_lastRetryLine, isFallback: true);
        }

        // "물 주세요" -> "물 주세요라고 한 번 더 말씀해 주시겠어요?" (추천 문장이 없으면 기본 되묻기)
        private static string RetryLineFor(string target)
        {
            var phrase = target?.Trim().TrimEnd('?', '!', '.', '…');
            return string.IsNullOrEmpty(phrase) ? UnclearRetryLine : $"{phrase}라고 한 번 더 말씀해 주시겠어요?";
        }

        // 변경점: 뭉개짐 판단 - 명료도 모델(기존 모델)이 들은 말이 해석된 문장과 얼마나 같은지(ClarityEstimator, 0~100).
        //   명료도 모델 문장이 없으면(기기 음성 인식·서버 없음) 판단하지 않는다. 명료도 모델이 아무것도 못 들었으면 뭉개짐.
        private bool IsGarbled(string clarityText, string phrase)
        {
            if (clarityText == null || string.IsNullOrWhiteSpace(phrase)) return false;
            if (string.IsNullOrWhiteSpace(clarityText)) return true;
            return ClarityEstimator.TryEstimate(phrase, clarityText, out int percent) && percent < conversationRetryClarityPercent;
        }

        // 변경점: 현장지원모드 명료도 기록 기준 = 해석된 문장(사용자가 말하려던 문장).
        //   예전에는 뭉개진 인식 문장이 추천 문장보다 다른 후보에 가까워 보이면 명료도를 버려서, 뭉개 말한 턴은 거의 기록이 없었고
        //   완료 화면 '정확도'가 "-"였다(2026-10-05 태블릿 세션 음성 9턴 중 0턴 기록).
        //   추천 문장으로 해석됨 -> 서버 점수 그대로. 다른 후보로 해석됨 -> 명료도 모델 문장을 그 후보와 비교한 추정치(ClarityEstimator).
        //   일상 대화는 목표 문장이 없으므로 기록하지 않는다. 되묻기 턴은 추천 문장을 다시 말해 달라고 했으므로 서버 점수 그대로(호출 측).
        //   "아니요" -> 봉투 필요 없어요처럼 다른 말로 또렷하게 답했으면(두 모델 합의) 문장 차이만 재므로 기록하지 않는다(정확도 모드와 같은 원칙)
        private SttResult ConversationClarity(SttResult spoken, string phrase, string target, bool smallTalk)
        {
            if (!spoken.hasClarity) return spoken;
            if (smallTalk || string.IsNullOrWhiteSpace(phrase)
                || (ModelsAgree(spoken.text, spoken.clarityText) && IsGarbled(spoken.clarityText, phrase)))
            {
                spoken.hasClarity = false;
                return spoken;
            }
            if (ClarityEstimator.TryEstimate(target, phrase, out int same) && same == 100) return spoken;

            int percent;
            if (spoken.clarityText == null) { spoken.hasClarity = false; return spoken; }
            if (string.IsNullOrWhiteSpace(spoken.clarityText)) percent = 0;   // 명료도 모델이 아무것도 못 들음
            else if (!ClarityEstimator.TryEstimate(phrase, spoken.clarityText, out percent)) { spoken.hasClarity = false; return spoken; }
            spoken.clarityPercent = percent;
            spoken.clarityLight = percent >= ClarityEstimator.GreenFrom ? "green" : percent >= ClarityEstimator.YellowFrom ? "yellow" : "red";
            spoken.clarityTarget = phrase;
            return spoken;
        }

        // 변경점: 두 모델이 같은 녹음을 거의 같게 들었는가(또렷한 말). 어느 한쪽이 비었으면 false
        private bool ModelsAgree(string recognized, string clarityText) =>
            ClarityEstimator.TryEstimate(recognized, clarityText, out int percent) && percent >= conversationModelAgreePercent;
        private int _unclearRetries;   // 못 알아들어 다시 말해 달라고 한 횟수. 알아들은 턴이 진행되면 0
        private string _clerkLineBeforeRetry;   // 다시 말해 달라고 하기 전 점원이 한 말(AI 판단 맥락)

        // 반환: phrase = 진행할 문장(null이면 다시 말해 달라고 한다), smallTalk = 화면 문장이 아닌 일상 대화(phrase = 인식 원문)
        private async UniTask<(string phrase, bool smallTalk)> ResolveConversationUtteranceAsync(
            string recognized, string[] alternatives, string clarityText, string target, CancellationToken ct)
        {
            var candidates = dashboardView.GetSpeechCandidates().ToList();
            // 변경점: 명료도 모델이 들은 문장도 인식기의 추측으로 본다. 새 모델이 "현금으로 할게요"를 '형님은 왜 그래요?'로 들어도
            //   명료도 모델은 '형금으로 알 해요?'로 들어 후보와 가까운 경우가 있다(2026-10-06 실기기). 같은 문장·빈 문장은 넣지 않는다.
            if (!string.IsNullOrWhiteSpace(clarityText) && clarityText != recognized
                && (alternatives == null || System.Array.IndexOf(alternatives, clarityText) < 0))
            {
                int old = alternatives != null ? alternatives.Length : 0;
                var merged = new string[old + 1];
                if (old > 0) System.Array.Copy(alternatives, merged, old);
                merged[old] = clarityText;
                alternatives = merged;
            }
            // 변경점: 인식기의 다른 추측들도 함께 본다(맨 앞 = 가장 그럴듯한 것)
            var heardOptions = new List<string> { recognized };
            if (alternatives != null) heardOptions.AddRange(alternatives);
            // 변경점(2026-10-06): "아니요", "없어요" 같은 거절은 화면 문장에 없어도 단계 질문의 답으로 본다(아래 DeclineAnswerPhrase)
            var declinePhrase = DeclineAnswerPhrase(heardOptions);
            _lastAnswerWasDecline = declinePhrase != null;
            if (declinePhrase != null)
            {
                Debug.Log($"[TrainingSceneRoot] 대화 모드 해석(거절 답): '{string.Join("' / '", heardOptions)}' -> '{declinePhrase}'");
                return (declinePhrase, false);
            }
            if (ConvenienceQuickPhrasePolicy.TryMatchCandidate(heardOptions, target, candidates, out var match))
            {
                Debug.Log($"[TrainingSceneRoot] 대화 모드 해석(규칙): '{string.Join("' / '", heardOptions)}' -> '{match}'");
                return (match, false);
            }
            // 변경점: AI에게는 점원이 방금 한 말(맥락)을 함께 준다. 다시 물어본 뒤라면 그 전의 원래 질문
            var clerkLine = _lastNpcLine == _lastRetryLine ? _clerkLineBeforeRetry : _lastNpcLine;

            if (_geminiService != null && candidates.Count > 0)
            {
                // 변경점: 타이머도 함께 해제한다. 예전에는 판단이 3초 전에 끝나 CTS가 해제된 뒤 타이머가 Cancel을 불러
                //   매 턴 ObjectDisposedException이 로그에 찍혔다
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                using (timeout.CancelAfterSlim(System.TimeSpan.FromSeconds(ConversationJudgeTimeoutSeconds), DelayType.Realtime))
                {
                    ShowThinking(true);
                    try
                    {
                        int index = await _geminiService.RequestPhraseMatchAsync(recognized, candidates, timeout.Token, clerkLine, alternatives);
                        if (index >= 0)
                        {
                            Debug.Log($"[TrainingSceneRoot] 대화 모드 해석(AI): '{recognized}' -> '{candidates[index]}'");
                            return (candidates[index], false);
                        }
                        // 변경점: 알아들었지만 화면 문장이 아니면(일상 대화) 되묻지 않고 점원이 그 말을 받아 준다
                        if (index == GeminiDialogueService.PhraseNone)
                        {
                            Debug.Log($"[TrainingSceneRoot] 대화 모드 해석(AI): '{recognized}' -> 일상 대화(점원이 받아 주고 지금 단계 유지)");
                            return (recognized, true);
                        }
                    }
                    catch (System.OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning("[TrainingSceneRoot] 대화 모드 AI 판단 시간 초과");
                    }
                    finally
                    {
                        ShowThinking(false);
                    }
                }
            }

            if (_unclearRetries < UnclearRetryLimit)
            {
                Debug.Log($"[TrainingSceneRoot] 대화 모드 해석 실패: '{recognized}'");
                return (null, false);   // 되묻기(횟수·맥락 기록)는 호출 측 AskConversationRetry
            }
            // 변경점(사용자 결정 2026-10-06): 두 번째에도 못 골랐으면 고정된 추천 문장이 아니라 들린 소리에 가장 가까운 선택지로 넘어간다
            var closest = ClosestCandidate(recognized, alternatives, null, target);
            Debug.Log($"[TrainingSceneRoot] 대화 모드 해석(가장 가까운 선택지): '{recognized}' -> '{closest}'");
            return (closest, false);
        }

        // 변경점(2026-10-06): 점원의 질문이 "더 필요한 물건 있으세요?"(물건 고르기, 담은 물건이 있을 때)나 "봉투 필요하세요?"(봉투)일 때
        //   "아니요", "없어요", "됐어요" 같은 거절은 화면에 '아니요' 문장이 없어도 각각 '계산할게요' / '봉투 필요 없어요'로 본다.
        //   어눌한 말도 잡도록 새 모델·다른 추측·명료도 모델이 들은 것 중 하나라도 거절이면 인정한다(2026-10-06 실기기: 없어요 -> 일상 대화로 되풀이).
        //   해당 단계가 아니거나 그 문장이 후보에 없으면 null
        private bool _lastAnswerWasDecline;   // 직전 해석이 거절 답("아니요")으로 정해졌나: 되묻기·명료도 기록 판단에서 쓴다
        private string DeclineAnswerPhrase(IList<string> heardOptions)
        {
            if (dashboardView == null || _dialogueManager == null) return null;
            string phrase;
            switch (_dialogueManager.CurrentObjectiveId)
            {
                case "select_items":
                    if (string.IsNullOrEmpty(_dialogueManager.LastSlotListItem("items_requested"))) return null;
                    phrase = "계산할게요";
                    break;
                case "extras":
                    phrase = "봉투 필요 없어요";
                    break;
                default:
                    return null;
            }
            if (!dashboardView.GetSpeechCandidates().Contains(phrase)) return null;
            foreach (var heard in heardOptions)
                if (ConvenienceQuickPhrasePolicy.IsDeclineAnswer(heard)) return phrase;
            return null;
        }

        // 변경점: 기준(임계값)을 넘어도 들린 소리(새 모델 + 다른 추측 + 명료도 모델)에 가장 가까운 화면 문장. 하나도 못 고르면 추천 문장
        private string ClosestCandidate(string recognized, string[] alternatives, string clarityText, string target)
        {
            var heard = new List<string> { recognized };
            if (alternatives != null) heard.AddRange(alternatives);
            if (!string.IsNullOrWhiteSpace(clarityText) && !heard.Contains(clarityText)) heard.Add(clarityText);
            if (dashboardView != null
                && ConvenienceQuickPhrasePolicy.TryMatchCandidate(heard, target, dashboardView.GetSpeechCandidates().ToList(), out var closest,
                                                                  ignoreThreshold: true))
                return closest;
            return ConvenienceQuickPhrasePolicy.ConversationFallback(recognized, target);
        }

        private void StartFreeTalk()
        {
            if (!IsPoolMode || _freeTalkActive || _sessionCompleted) return;

            _freeTalkActive = true;
            uiView.HideExtraModal();
            uiView.SetFreeTalkActive(true);
            dashboardView?.SetInteractionEnabled(false);
            StopSpeech();   // 안내 발화가 흐르는 중이면 끊고 바로 듣기 시작
            FreeTalkLoop(_cts.Token).Forget();
        }

        private void StopFreeTalk()
        {
            if (!_freeTalkActive) return;

            _freeTalkActive = false;
            _sttService?.Cancel(); // 변경점: 듣는 중이던 녹음도 멈춘다 (서버 STT는 끄지 않으면 발화 끝까지 녹음해 올린다)
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
                    var stt = await ListenUserAsync(ct, () => _freeTalkActive);
                    uiView.ShowMicIndicator(false);
                    if (!_freeTalkActive) break;

                    uiView.ShowSttResult(stt.text);
                    if (string.IsNullOrWhiteSpace(stt.text))
                    {
                        SpeakNpc(PickFallback("stt_empty"), isFallback: true);
                        continue;
                    }

                    hud?.SetUserUtterance(stt.text);
                    ShowThinking(true);
                    var reply = await _geminiService.RequestFreeTalkAsync(
                        _freeTalkSystemPrompt, BuildFreeTalkUserPrompt(stt.text), ct);
                    ShowThinking(false);
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
                ShowThinking(false);
                uiView.ShowMicIndicator(false);
            }
        }

        // 추천 상품은 STT를 다시 거치지 않고, 사용자가 해당 상품명을 말한 한 턴으로 Gemini에 전달한다.
        private void HandleDashboardProductSelected(string productId, string productName, string utterance)
        {
            // 상품은 선택 목록에 추가하되, 사용자가 더 고를 수 있도록 select_items를 유지한다.
            bool captureItem = IsConvenienceItemSelectionStep();
            RunDashboardTurn(
                productId,
                productName,
                utterance,
                true,
                captureItem ? productName : null).Forget();
        }

        // 오른쪽 대화 힌트도 AAC 보조 입력이므로 누른 문장을 그대로 Gemini에 전달한다.
        private void HandleDashboardQuickPhraseSelected(string phrase)
        {
            // "물 주세요"는 상품 하나를 고른 것이지 쇼핑을 끝낸 것은 아니다.
            bool captureItem = IsConvenienceItemSelectionStep()
                               && string.Equals(phrase?.Trim(), "물 주세요", System.StringComparison.Ordinal);
            RunDashboardTurn(
                "quick_phrase",
                "대화 힌트",
                phrase,
                false,
                captureItem ? "물" : null).Forget();
        }

        // 변경점(TrainingCon 시안): 키보드로 쓴 문장도 대화 힌트를 누른 것과 같은 한 턴으로 보낸다.
        private void HandleTypedUtterance(string text)
        {
            // 변경점: 키보드로 "과자 주세요"를 써도 그 상품을 누른 것과 똑같이(음성과 같은 규칙)
            if (!IsWaterRequest(text) && dashboardView != null && dashboardView.TryFindProduct(text, out var productId, out var productName))
            {
                RunDashboardTurn(productId, productName, text, true,
                    IsConvenienceItemSelectionStep() ? productName : null).Forget();
                return;
            }
            bool captureItem = IsConvenienceItemSelectionStep() && IsWaterRequest(text);
            RunDashboardTurn(
                "keyboard_input",
                "키보드 입력",
                text,
                false,
                captureItem ? "물" : null).Forget();
        }

        private bool IsConvenienceItemSelectionStep() =>
            scenarioId == ScenarioIds.Convenience
            && (_dialogueManager.CurrentObjectiveId == "greeting"
                || _dialogueManager.CurrentObjectiveId == "select_items");

        private async UniTaskVoid RunDashboardTurn(
            string sourceId,
            string sourceName,
            string utterance,
            bool isProduct,
            string capturedItemName = null,
            SttResult? spoken = null,
            bool smallTalk = false,
            string logSourceId = null)   // 변경점: 기록에 남길 출처 id(없으면 sourceId). 음성 턴 구분용
        {
            if (_dashboardTurnBusy || _freeTalkActive || _sessionCompleted || !_useGemini
                || string.IsNullOrWhiteSpace(utterance)) return;

            // 변경점: 알아들은 턴(음성·버튼·키보드)이 진행되면 '다시 말해 줄래요' 횟수를 비운다.
            //   일상 대화는 단계가 그대로이므로 비우지 않는다(비우면 같은 질문에서 되묻기가 끝없이 반복됐다)
            if (!smallTalk) _unclearRetries = 0;
            _dashboardTurnBusy = true;
            dashboardView?.SetInteractionEnabled(false);
            try
            {
                StopSpeech();
                hud?.SetUserUtterance(utterance);
                uiView.ShowSttResult(utterance);

                // 변경점: 카드 읽어주기가 켜져 있으면 누른 문장을 앱이 대신 소리 내 읽고, 다 읽은 뒤 점원이 대답한다.
                //   음성 턴(spoken 있음)은 사용자가 이미 말했으므로 읽지 않는다.
                //   읽는 중 취소(씬 종료 등)되면 기록·진행 전에 턴을 끝낸다.
                if (speakCardOnTap && spoken == null)
                    await SpeakCardAsync(utterance, SpeechToken);

                // 변경점: 음성 턴이면 STT 결과(명료도 포함)를, 탭 턴이면 문장만 기록한다.
                _eventLogger?.LogCardSelected(logSourceId ?? sourceId, utterance, spoken ?? new SttResult { text = utterance });
                _dialogueManager.HandleUserTurn(null, utterance);

                // 다섯 개 고정 대화 힌트는 현재 단계와 장바구니 상태를 함께 보고 판정한다.
                // 같은 문장도 단계에 따라 유지/전환/마무리 결과가 달라진다.
                if (!isProduct && !smallTalk)
                {
                    var lastItem = _dialogueManager.LastSlotListItem("items_requested");
                    if (ConvenienceQuickPhrasePolicy.TryResolve(
                            utterance,
                            _dialogueManager.CurrentObjectiveId,
                            lastItem,
                            !string.IsNullOrEmpty(lastItem),
                            out var decision))
                    {
                        if (!string.IsNullOrWhiteSpace(decision.ItemToAppend))
                            _dialogueManager.AppendSlotListItem("items_requested", decision.ItemToAppend);

                        await RunGeminiTurn(
                            null,
                            utterance,
                            resolvedTurn: new DialogueTurn
                            {
                                Tool = decision.Tool,
                                ObjectiveId = decision.ObjectiveId,
                                NpcSpeech = decision.NpcSpeech,
                                CardIds = decision.CardIds
                            });
                        return;
                    }
                }

                string sourceContext = isProduct
                    ? $"The user tapped the recommended product '{sourceName}' (product_id={sourceId}). " +
                      $"Treat it as the direct customer utterance: \"{utterance}\". Respond about that exact product."
                    : $"The user tapped a dialogue-hint button. Treat it as the direct customer utterance: \"{utterance}\".";
                // 변경점: 일상 대화(화면 문장이 아님). 어눌한 음성 인식이라 너그럽게 뜻을 짐작해 짧게 받아 주고, 지금 단계 질문으로 부드럽게 돌아온다
                if (smallTalk)
                    sourceContext =
                        $"The customer said this by voice: \"{utterance}\". It is not one of the hint phrases; it is everyday small talk or another topic. " +
                        "It comes from speech recognition of a person with a developmental disability, so it may be garbled: guess the meaning kindly. " +
                        "Reply warmly in one short sentence to what they said, then gently ask the question for the current step again. " +
                        "Do not complete the objective, do not add items, and use present_cards to stay on the current step.";

                if (!string.IsNullOrWhiteSpace(capturedItemName))
                {
                    sourceContext +=
                        " Add this item to items_requested, but do not finish shopping yet." +
                        " Ask whether the customer needs any additional items and stay on select_items.";
                }

                await RunGeminiTurn(
                    null,
                    utterance,
                    sourceContext,
                    capturedItemName,
                    stayOnStep: smallTalk);
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
        private async UniTask RunGeminiTurn(
            AACCard card,
            string sttText,
            string sourceContext = null,
            string capturedItemName = null,
            DialogueTurn resolvedTurn = null,
            bool stayOnStep = false)
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

            DialogueTurn turn;
            if (resolvedTurn != null)
            {
                turn = resolvedTurn;
            }
            else if (!string.IsNullOrWhiteSpace(capturedItemName))
            {
                // 대시보드의 상품/문장 버튼은 발화 내용이 확정된 입력이다. 이 경우 Gemini가
                // 쇼핑을 조기 완료하지 않도록 상품을 누적하고 추가 상품 여부를 직접 묻는다.
                bool enteringItemSelection = _dialogueManager.CurrentObjectiveId == "greeting";
                _dialogueManager.AppendSlotListItem("items_requested", capturedItemName);
                turn = new DialogueTurn
                {
                    Tool = enteringItemSelection
                        ? DialogueTool.MarkObjectiveComplete
                        : DialogueTool.PresentCards,
                    ObjectiveId = enteringItemSelection ? "greeting" : null,
                    NpcSpeech = $"네, {capturedItemName.Trim()} 준비해 드릴게요.\n‘계산할게요’라고 직접 말해볼까요?\n어려우면 왼쪽 버튼을 눌러도 돼요.",
                    CardIds = new[] { "card_cvs_yes", "card_cvs_no" },
                };
            }
            else
            {
                ShowThinking(true);
                turn = await _geminiService.RequestNextTurnAsync(
                    _systemPrompt, BuildUserPrompt(card, sttText, sourceContext), _cts.Token);
                ShowThinking(false);
            }

            // Gemini 실패/키없음 → fallback 발화 + 룰 기반 풀로 카드 유지(대화 끊기지 않게)
            if (turn == null)
            {
                _dialogueManager.RegisterFailure();
                _lastToolSpoke = true;
                SpeakNpc(PickFallback("llm_call_failed"), isFallback: true);
                RefreshPool();
                return;
            }

            // 변경점: 일상 대화 턴은 점원이 받아 주기만 하고 단계·장바구니는 그대로(AI가 단계를 넘기려 해도 지금 단계에 머문다)
            if (stayOnStep && (turn.Tool == DialogueTool.MarkObjectiveComplete || turn.Tool == DialogueTool.TransitionToObjective
                               || turn.Tool == DialogueTool.ForceCompleteScenario))
            {
                turn.Tool = DialogueTool.PresentCards;
                turn.ObjectiveId = null;
                turn.SlotsFilled = null;
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
        // 변경점: flush는 완료 화면(ShowCompletionScreenAsync)이 기다린 뒤 같은 기록에서 점수를 계산한다
        private void MarkSessionCompleted()
        {
            if (_sessionCompleted) return;
            _sessionCompleted = true;
            _eventLogger?.LogSessionEnded("completed");
            ShowCompletionScreenAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // 변경점(ending 시안): 완료 화면 — 점수·별·정확도·잘한 표현·최고 기록을 레포트와 같은 기록(JSONL)에서 계산한다.
        //   기록이 없으면(프로필 없이 바로 실행) 점수 칸은 빈 값으로 보인다.
        // 변경점(사용자 요청 2026-10-03): 점원 말이 다 끝난 뒤에 완료 화면을 띄운다.
        //   예전에는 마지막 인사가 나오는 중에 "정말 잘했어요"가 끼어들어 인사를 끊고, 완료 화면도 바로 덮였다.
        //   순서: 기록 저장(flush)과 점수 계산 -> 점원의 마지막 말이 끝날 때까지 기다림 -> "정말 잘했어요" -> 끝나면 완료 화면
        private async UniTaskVoid ShowCompletionScreenAsync(CancellationToken ct)
        {
            var elapsed = System.DateTimeOffset.UtcNow - _sessionStartUtc;

            Artti.Report.ReportDataService service = null;
            var store = AppBootstrap.Instance?.LogStore;
            if (store != null)
            {
                await store.FlushAsync(ct);
                var path = (store as Artti.AAC.Logging.JsonAppendLogStore)?.FilePath;
                if (_eventLogger != null && !string.IsNullOrEmpty(path)) service = new Artti.Report.ReportDataService(path);
            }
            if (ct.IsCancellationRequested || hud == null) return;

            // 변경점: 대화 모드는 Gemini가 대화 전체를 한 번 평가해 기록에 남긴다(리포트의 자연스러움·응답 적절성·AI 코멘트).
            //   완료 화면에서 바로 나가도 끝까지 하도록 앱 수명(AppBootstrap) 토큰을 쓴다. 실패하면 리포트는 기존 계산을 쓴다
            if (IsConversationMode && service != null && _eventLogger != null && AppBootstrap.Instance != null)
                EvaluateConversationAsync(Artti.Report.ConversationReportBuilder.Transcript(service, _eventLogger.SessionId), _eventLogger,
                    AppBootstrap.Instance.GetCancellationTokenOnDestroy()).Forget();

            var result = Artti.Report.TrainingResultCalculator.Build(service, _eventLogger?.SessionId,
                accuracyClarityMinPercent, _mode, Artti.Report.ReportLabels.ScenarioName(scenarioId),
                Mathf.RoundToInt((float)elapsed.TotalSeconds));

            try
            {
                await WaitSpeechIdleAsync(ct);   // 점원의 마지막 말(인사)
                SpeakNpc("정말 잘했어요! 다음에도 함께해요.");
                await WaitSpeechIdleAsync(ct);
            }
            catch (System.OperationCanceledException) { return; }
            if (ct.IsCancellationRequested || hud == null) return;
            hud.ShowCompletion(result);
        }


        // 이 씬이 사라진 뒤에도 돌 수 있게 인스턴스 필드를 쓰지 않는다
        private static async UniTaskVoid EvaluateConversationAsync(Artti.Report.ConversationTranscript transcript, EventLogger logger,
            CancellationToken ct)
        {
            try
            {
                var service = Artti.Report.ConversationEvaluationService.FromEnv();   // OpenAI 키가 있으면 OpenAI
                var evaluation = await service.EvaluateAsync(transcript.lines, ct);
                if (evaluation == null) return;
                logger.LogSessionEvaluated(evaluation);
                var store = AppBootstrap.Instance?.LogStore;
                if (store != null) await store.FlushAsync(ct);
                Debug.Log($"[TrainingSceneRoot] 대화 AI 평가 저장: 적절성 {evaluation.appropriateness}, 자연스러움 {evaluation.naturalness}");
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[TrainingSceneRoot] 대화 AI 평가 실패(리포트는 기존 계산): {e.Message}"); }
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
