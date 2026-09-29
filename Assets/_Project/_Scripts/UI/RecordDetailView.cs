using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Common.Speech;
using Artti.Report;

namespace Artti.UI
{
    // 세션 1개의 상세 리포트 화면(hh.png). 진입 시 RecordDetailContext.SessionId 세션을 읽어 표시.
    // 데이터 가공은 ReportDataService, 이 클래스는 표시/입력/네비게이션만 담당.
    public class RecordDetailView : MonoBehaviour
    {
        [Header("헤더")]
        [SerializeField] private Button backBtn;
        [SerializeField] private Button shareBtn;

        [Header("전체 성공률")]
        [SerializeField] private Image donutFill;
        [SerializeField] private TMP_Text donutPercentText;
        [SerializeField] private TMP_Text fullSuccessText;
        [SerializeField] private TMP_Text partialSuccessText;
        [SerializeField] private TMP_Text needHelpText;
        [SerializeField] private TMP_Text clarityAvgText;   // 변경점: 발화 정확도 평균 (서버 STT 명료도). 누르면 이유 팝업

        [Header("스탯 카드")]
        [SerializeField] private TMP_Text scenarioNameText;
        [SerializeField] private Button scenarioDetailBtn; // 상세 보기 (stub)
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text durationText;

        [Header("AI 리드백")]
        [SerializeField] private TMP_Text feedbackMainText;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private TMP_Text[] goodPointTexts;
        [SerializeField] private TMP_Text[] improvePointTexts;
        [SerializeField] private TMP_Text[] nextGoalTexts;

        [Header("연습 상세 기록")]
        [SerializeField] private RecordStepCard[] stepCards;

        [Header("하단")]
        [SerializeField] private Button prevBtn;
        [SerializeField] private Button retryBtn;
        [SerializeField] private Button nextBtn;

        [Header("TTS")]
        [SerializeField] private AudioSource audioSource;

        private ReportDataService _service;
        private ReportRecordDetail _report;
        private ITtsService _tts;
        private CancellationToken _ct;

        private void Start()
        {
            _ct = this.GetCancellationTokenOnDestroy();

            if (backBtn != null) backBtn.onClick.AddListener(GoReport);
            if (prevBtn != null) prevBtn.onClick.AddListener(GoReport);
            if (shareBtn != null) shareBtn.onClick.AddListener(HandleShare);
            if (scenarioDetailBtn != null) scenarioDetailBtn.gameObject.SetActive(false); // 상세보기 제거 - 시나리오명만 표시
            if (retryBtn != null) retryBtn.onClick.AddListener(HandleRetry);
            if (nextBtn != null) nextBtn.onClick.AddListener(HandleNext);

            RenameHeader();        // 제목 "레포트" -> "상세 기록"
            SetupDecor();          // 배경 + 메달 반짝임 + 카드 탭 펀치
            EnsureClarityUi();     // 변경점: 발화 정확도 표시(빌더 재실행 전에도) + 이유 팝업
            PlayIntro().Forget();  // 섹션 순차 진입 애니
            LoadAsync().Forget();
        }

        private void RenameHeader()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var t = FindDeep(canvas.transform, "Title")?.GetComponent<TMP_Text>();
            if (t != null) t.text = "상세 기록";
        }

        // ===== 연출 셋업 (런타임, 씬/빌더 수정 없이) =====

        private void SetupDecor()
        {
            // 1) 연한 파스텔 그라데이션 + 미세 입자 배경 (캔버스 맨 뒤)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var bg = new GameObject("Backdrop", typeof(RectTransform));
                bg.transform.SetParent(canvas.transform, false);
                bg.transform.SetSiblingIndex(0); // 맨 뒤
                bg.AddComponent<UIBackdrop>();
            }

            // 2) 타임라인 트랙(연결선) 데이터 파티클
            if (canvas != null)
            {
                var connector = FindDeep(canvas.transform, "Connector");
                if (connector != null && connector.GetComponent<UITrackParticles>() == null)
                    connector.gameObject.AddComponent<UITrackParticles>();
            }

            // 3) 메달 반짝임 + 카드 탭 펀치
            if (stepCards != null)
            {
                foreach (var card in stepCards)
                {
                    if (card == null) continue;
                    var medal = card.transform.Find("Medal")?.GetComponent<Graphic>();
                    if (medal != null && medal.GetComponent<UISparkle>() == null)
                        medal.gameObject.AddComponent<UISparkle>();

                    // 카드 전체 탭 -> 펀치 + 라이트블루 플래시
                    if (card.GetComponent<UICardTap>() == null)
                    {
                        var tap = card.gameObject.AddComponent<UICardTap>();
                        tap.flashTarget = card.transform.Find("Bg")?.GetComponent<Graphic>();
                    }
                }
            }
        }

        // 제목 -> 상단 요약/스탯 -> AI -> 타임라인 순서로 살짝 떠오르며 페이드 인
        private async UniTaskVoid PlayIntro()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.transform;

            string[][] groups =
            {
                new[] { "Title", "Subtitle" },
                new[] { "DonutCard", "ScenarioCard", "StatusCard", "DateCard", "TimeCard" },
                new[] { "AiTitle", "AiPanel" },
                new[] { "TimelineTitle", "TimelinePanel" },
                new[] { "PrevBtn", "RetryBtn", "NextBtn" },
            };

            float delay = 0f;
            foreach (var group in groups)
            {
                foreach (var n in group)
                {
                    var rt = FindDeep(root, n);
                    if (rt != null) UIFx.SlideFadeIn(rt, delay, 0.45f, 44f, _ct).Forget();
                }
                delay += 0.12f;
            }
            await UniTask.CompletedTask;
        }

        private static RectTransform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private async UniTaskVoid LoadAsync()
        {
            var store = AppBootstrap.Instance?.LogStore;
            string path = null;
            if (store != null)
            {
                await store.FlushAsync(_ct);
                path = (store as JsonAppendLogStore)?.FilePath;
            }
            _service = new ReportDataService(path);

            string sessionId = RecordDetailContext.SessionId;
            if (string.IsNullOrEmpty(sessionId))
            {
                var sessions = _service.GetSessions();
                if (sessions.Count > 0) sessionId = sessions[0].sessionId;
            }
            if (!string.IsNullOrEmpty(sessionId)) _report = _service.GetRecordReport(sessionId);

            // 데이터 없으면(프로필 미선택/로그 없음) 빈 상태로 — 가짜 값 표시 안 함
            if (_report == null)
            {
                ShowEmpty();
                return;
            }

            Bind(_report);                       // 규칙 기반 즉시 표시
            GenerateFeedbackAsync(_report).Forget(); // Gemini 응답 오면 AI 피드백 교체
        }

        // 학습 기록이 없을 때: 모든 값 비우고 카드/항목 숨김
        private void ShowEmpty()
        {
            if (donutFill != null) donutFill.fillAmount = 0f;
            if (donutPercentText != null) donutPercentText.text = "0<size=50%>%</size>";
            if (fullSuccessText != null) fullSuccessText.text = "0개";
            if (partialSuccessText != null) partialSuccessText.text = "0개";
            if (needHelpText != null) needHelpText.text = "0개";
            if (clarityAvgText != null) clarityAvgText.text = "";
            if (scenarioNameText != null) scenarioNameText.text = "-";
            if (statusText != null) statusText.text = "-";
            if (dateText != null) dateText.text = "-";
            if (durationText != null) durationText.text = "-";
            if (feedbackMainText != null) feedbackMainText.text = "아직 학습 기록이 없어요.";
            if (bubbleText != null) bubbleText.text = "";
            HideAll(goodPointTexts);
            HideAll(improvePointTexts);
            HideAll(nextGoalTexts);
            if (stepCards != null)
                foreach (var c in stepCards) if (c != null) c.gameObject.SetActive(false);
        }

        // 변경점: 명료도 색 — 서버 신호등(green/yellow/red)을 그대로 쓴다.
        //   신호등이 없으면 중립색. 앱에서 임의 구간(예: 80/50)을 만들면 서버 판정과 어긋날 수 있다.
        private static readonly Color32 ClarityGreen   = new Color32(30, 158, 85, 255);
        private static readonly Color32 ClarityYellow  = new Color32(235, 140, 52, 255);
        private static readonly Color32 ClarityRed     = new Color32(224, 64, 64, 255);
        private static readonly Color32 ClarityNeutral = new Color32(33, 41, 60, 255);
        private const float ClarityBadgeMargin = 58f;   // 빌더의 Clarity 배지 폭(56) + 간격

        private static Color ClarityColor(string light)
        {
            switch (light)
            {
                case "green":  return ClarityGreen;
                case "yellow": return ClarityYellow;
                case "red":    return ClarityRed;
                default:       return ClarityNeutral;
            }
        }

        // ===== 발화 정확도 (변경점) =====
        // 서버 STT가 채점한 발화 정확도 %와 그 이유(목표 문장/들린 문장 비교)를 보여 준다.
        // 씬을 빌더로 다시 만들기 전에도 보이도록, 연결이 비어 있으면 런타임에 만든다(배치는 RecordDetailSceneBuilder와 동일).
        // 평균 문구나 단계 카드를 누르면 단계별 %·목표 문장·들린 문장·이유 팝업을 연다.
        private GameObject _clarityPopup;
        private TMP_Text _clarityPopupText;

        private void EnsureClarityUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var font = donutPercentText != null ? donutPercentText.font : (feedbackMainText != null ? feedbackMainText.font : null);

            if (clarityAvgText == null)
            {
                var donut = FindDeep(canvas.transform, "DonutCard");
                if (donut != null)
                {
                    clarityAvgText = CreateRuntimeText("ClarityAvg", donut, font, 22, TextAlignmentOptions.Right);
                    PlaceTopLeft(clarityAvgText.rectTransform, 270, -28, 226, 34);
                }
            }
            if (clarityAvgText != null)
            {
                clarityAvgText.enableAutoSizing = true;
                clarityAvgText.fontSizeMin = 14;
                clarityAvgText.fontSizeMax = 22;
                clarityAvgText.raycastTarget = true;
                AddClick(clarityAvgText.gameObject, ShowClarityDetail);
            }

            if (stepCards != null)
            {
                foreach (var card in stepCards)
                {
                    if (card == null) continue;
                    if (card.clarityText == null)
                    {
                        var box = card.transform.Find("UserBox");
                        if (box != null)
                        {
                            card.clarityText = CreateRuntimeText("Clarity", box, font, 18, TextAlignmentOptions.Right);
                            PlaceTopLeft(card.clarityText.rectTransform, 252 - 12 - 56, -10, 56, 32);
                        }
                    }
                    // 카드 루트에는 UICardTap이 넣은 투명 Image가 있어 클릭을 받는다(스피커 버튼은 자식이라 따로 동작)
                    AddClick(card.gameObject, ShowClarityDetail);
                }
            }

            BuildClarityPopup(canvas.transform, font);
        }

        private static void AddClick(GameObject go, UnityEngine.Events.UnityAction action)
        {
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(action);
        }

        private static TMP_Text CreateRuntimeText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = ClarityNeutral;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        private static void PlaceTopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private void BuildClarityPopup(Transform canvasRoot, TMP_FontAsset font)
        {
            if (_clarityPopup != null) return;

            // 반투명 전체 화면 — 어디를 눌러도 닫힘
            _clarityPopup = new GameObject("ClarityPopup", typeof(RectTransform));
            var root = (RectTransform)_clarityPopup.transform;
            root.SetParent(canvasRoot, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            _clarityPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            AddClick(_clarityPopup, () => _clarityPopup.SetActive(false));

            var panel = new GameObject("Panel", typeof(RectTransform));
            var pr = (RectTransform)panel.transform;
            pr.SetParent(root, false);
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1200f, 820f);
            panel.AddComponent<Image>().color = Color.white;

            _clarityPopupText = CreateRuntimeText("Text", pr, font, 26, TextAlignmentOptions.TopLeft);
            _clarityPopupText.fontStyle = FontStyles.Normal;
            _clarityPopupText.textWrappingMode = TextWrappingModes.Normal;
            _clarityPopupText.enableAutoSizing = true;
            _clarityPopupText.fontSizeMin = 16;
            _clarityPopupText.fontSizeMax = 26;
            var tr = _clarityPopupText.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(56f, 48f); tr.offsetMax = new Vector2(-56f, -48f);

            _clarityPopup.SetActive(false);
        }

        private void ShowClarityDetail()
        {
            if (_clarityPopup == null || _clarityPopupText == null) return;
            _clarityPopupText.text = BuildClarityDetailText(_report);
            _clarityPopup.transform.SetAsLastSibling();
            _clarityPopup.SetActive(true);
        }

        private static string BuildClarityDetailText(ReportRecordDetail r)
        {
            var sb = new System.Text.StringBuilder();
            if (r == null)
            {
                sb.Append("아직 학습 기록이 없어요.");
                return sb.ToString();
            }

            int measured = 0, spokenUnscored = 0;
            foreach (var s in r.steps)
            {
                if (s.clarityPct >= 0) measured++;
                else if (s.userSpoken) spokenUnscored++;
            }

            sb.Append("<size=125%><b>발화 정확도 ");
            sb.Append(r.avgClarityPct >= 0 ? $"{r.avgClarityPct}%" : "기록 없음");
            sb.Append("</b></size>\n");
            sb.Append("<color=#6E7687>서버 STT가 목표 문장과 비교해 채점한 값이에요.");
            if (r.avgClarityPct >= 0) sb.Append(" 평균은 음성으로 말한 모든 시도 기준이에요.");
            if (r.mode == TrainingMode.Conversation)
                sb.Append(" 대화 모드라 발음이 달라도 가장 가까운 문장으로 대화를 이어갔어요.");
            sb.Append("</color>\n\n");

            if (measured == 0)
            {
                sb.Append("이 연습에는 서버가 채점한 음성 발화가 없어요.\n");
                if (spokenUnscored > 0)
                    sb.Append("<color=#6E7687>음성으로 말한 단계는 있지만 서버 채점 결과가 오지 않았어요. " +
                              "서버 STT 연결(서버 주소 설정, 서버와 같은 네트워크인지)을 확인해 주세요.</color>\n\n");
                else
                    sb.Append("<color=#6E7687>대화 힌트·상품 버튼으로만 진행해서 채점할 음성이 없어요. " +
                              "음성 버튼을 눌러 직접 말하면 채점돼요.</color>\n\n");
            }

            foreach (var s in r.steps)
            {
                sb.Append($"<b>{s.index:D2} {Escape(s.objectiveName)}</b>  ");
                if (s.clarityPct < 0)
                {
                    sb.Append(s.userSpoken
                        ? "<color=#6E7687>채점 없음 — 음성으로 말했지만 서버 채점 결과가 없어요(서버 STT 미연결·접속 실패)</color>\n\n"
                        : "<color=#6E7687>채점 없음 — 버튼으로 고른 문장이라 채점 대상이 아니에요</color>\n\n");
                    continue;
                }
                sb.Append($"<b><color=#{ColorUtility.ToHtmlStringRGB(ClarityColor(s.clarityLight))}>{s.clarityPct}%</color></b>");
                var light = LightLabel(s.clarityLight);
                if (light != null) sb.Append($"  <color=#6E7687>({light})</color>");
                sb.Append('\n');
                if (!string.IsNullOrEmpty(s.clarityTarget)) sb.Append($"   목표 문장: {Escape(s.clarityTarget)}\n");
                if (!string.IsNullOrEmpty(s.clarityHeard)) sb.Append($"   들린 문장: {Escape(s.clarityHeard)}\n");
                // 대화 모드: 들린 문장과 실제로 대화에 쓴(해석된) 문장이 다르면 함께 보여 준다
                if (r.mode == TrainingMode.Conversation && !string.IsNullOrEmpty(s.heardText) && s.heardText != s.userText)
                    sb.Append($"   대화에 쓴 문장: {Escape(s.userText)}\n");
                if (!string.IsNullOrEmpty(s.clarityReason)) sb.Append($"   이유: {Escape(s.clarityReason)}\n");
                sb.Append('\n');
            }

            sb.Append("<size=80%><color=#6E7687>화면을 누르면 닫혀요</color></size>");
            return sb.ToString();
        }

        private static string LightLabel(string light)
        {
            switch (light)
            {
                case "green":  return "서버 판정 초록";
                case "yellow": return "서버 판정 노랑";
                case "red":    return "서버 판정 빨강";
                default:       return null;
            }
        }

        // 사용자/서버 문장이 TMP 리치 텍스트 태그로 해석되지 않도록
        private static string Escape(string s) => string.IsNullOrEmpty(s) ? s : "<noparse>" + s + "</noparse>";

        private static void HideAll(TMP_Text[] arr)
        {
            if (arr == null) return;
            foreach (var t in arr) if (t != null) t.gameObject.SetActive(false);
        }

        // AI 리드백 요약을 실제 Gemini 판단으로 생성해 교체 (실패 시 규칙 기반 유지).
        private async UniTaskVoid GenerateFeedbackAsync(ReportRecordDetail r)
        {
            var key = ApiKeyLoader.Get(ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) return;
            string nick = AppBootstrap.Instance?.ProfileManager?.ActiveProfile?.nickname;
            try
            {
                var fb = await new ReportFeedbackService(key).GenerateAsync(r, nick, _ct);
                if (fb == null) return;
                if (feedbackMainText != null && !string.IsNullOrWhiteSpace(fb.main)) UIFx.Typewriter(feedbackMainText, fb.main, 38f, _ct).Forget();
                if (bubbleText != null && !string.IsNullOrWhiteSpace(fb.bubble)) bubbleText.text = fb.bubble;
                if (fb.good != null && fb.good.Count > 0) FillList(goodPointTexts, fb.good);
                if (fb.improve != null && fb.improve.Count > 0) FillList(improvePointTexts, fb.improve);
                if (fb.next != null && fb.next.Count > 0) FillList(nextGoalTexts, fb.next);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[RecordDetail] AI 피드백 실패: {e.Message}"); }
        }

        private void Bind(ReportRecordDetail r)
        {
            // 전체 성공률 (0% -> 실제% 카운트업 + 도넛 채움 동시)
            float rate = r.successRate;
            UIFx.CountUp(0f, rate, 1.2f, v =>
            {
                if (donutFill != null) donutFill.fillAmount = v;
                if (donutPercentText != null) donutPercentText.text = $"{Mathf.RoundToInt(v * 100f)}<size=50%>%</size>";
            }, _ct).Forget();
            if (fullSuccessText != null) fullSuccessText.text = $"{r.fullSuccess}개";
            if (partialSuccessText != null) partialSuccessText.text = $"{r.partialSuccess}개";
            if (needHelpText != null) needHelpText.text = $"{r.needHelp}개";
            // 변경점: 서버가 채점한 음성 발화가 없던 세션도 "기록 없음"으로 보여 준다(누르면 이유 팝업).
            if (clarityAvgText != null)
                clarityAvgText.text = r.avgClarityPct >= 0 ? $"발화 정확도 {r.avgClarityPct}% ›" : "발화 정확도 기록 없음 ›";

            // 스탯 카드
            if (scenarioNameText != null) scenarioNameText.text = $"{r.scenarioName} · {TrainingModeContext.DisplayName(r.mode)}";
            if (statusText != null) statusText.text = r.completed ? "완료" : "미완료";
            if (dateText != null)
            {
                var dt = System.DateTimeOffset.FromUnixTimeMilliseconds(r.dateMs).ToLocalTime();
                dateText.text = $"{dt:yyyy.MM.dd} ({Weekday(dt.DayOfWeek)})";
            }
            if (durationText != null) durationText.text = $"{r.durationMin}분";

            // AI 리드백 (메인 문구 타자기 효과)
            if (feedbackMainText != null) UIFx.Typewriter(feedbackMainText, r.feedbackMain, 38f, _ct).Forget();
            if (bubbleText != null) bubbleText.text = r.feedbackBubble;
            FillList(goodPointTexts, r.goodPoints);
            FillList(improvePointTexts, r.improvePoints);
            FillList(nextGoalTexts, r.nextGoals);

            // 타임라인
            if (stepCards != null)
            {
                for (int i = 0; i < stepCards.Length; i++)
                {
                    var card = stepCards[i];
                    if (card == null) continue;
                    if (i >= r.steps.Count) { card.gameObject.SetActive(false); continue; }

                    var s = r.steps[i];
                    card.gameObject.SetActive(true);
                    if (card.numberText != null) card.numberText.text = $"{s.index:D2}";
                    if (card.objectiveText != null) card.objectiveText.text = s.objectiveName;
                    if (card.userText != null) card.userText.text = s.userText ?? "";
                    if (card.timeText != null) card.timeText.text = s.time;
                    if (card.ratingText != null) card.ratingText.text = s.ratingLabel;
                    if (card.clarityText != null)
                    {
                        bool hasClarity = s.clarityPct >= 0;
                        card.clarityText.text = hasClarity ? $"{s.clarityPct}%" : "";
                        card.clarityText.color = ClarityColor(s.clarityLight);
                        // 배지가 있을 때만 발화 문장이 배지와 겹치지 않도록 오른쪽 여백 확보
                        if (card.userText != null)
                            card.userText.margin = hasClarity ? new Vector4(0f, 0f, ClarityBadgeMargin, 0f) : Vector4.zero;
                    }
                    if (card.speakerButton != null)
                    {
                        var line = !string.IsNullOrEmpty(s.npcText) ? s.npcText : s.userText;
                        card.speakerButton.onClick.RemoveAllListeners();
                        card.speakerButton.onClick.AddListener(() => Speak(line));
                    }
                }
            }
        }

        private static void FillList(TMP_Text[] slots, System.Collections.Generic.List<string> values)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (values != null && i < values.Count)
                {
                    slots[i].gameObject.SetActive(true);
                    slots[i].text = $"· {values[i]}";
                }
                else slots[i].gameObject.SetActive(false);
            }
        }

        private static string Weekday(System.DayOfWeek d)
        {
            switch (d)
            {
                case System.DayOfWeek.Monday: return "월";
                case System.DayOfWeek.Tuesday: return "화";
                case System.DayOfWeek.Wednesday: return "수";
                case System.DayOfWeek.Thursday: return "목";
                case System.DayOfWeek.Friday: return "금";
                case System.DayOfWeek.Saturday: return "토";
                default: return "일";
            }
        }

        // ===== 네비게이션 =====

        private void GoReport() => SceneManager.LoadScene("ReportScene");

        private void HandleRetry()
        {
            if (_report == null) { GoReport(); return; }
            SceneManager.LoadScene(TrainingConvenienceScene);
        }

        // 시나리오가 편의점 하나뿐이라 "다음 시나리오"도 편의점 훈련으로 연결
        private void HandleNext() => SceneManager.LoadScene(TrainingConvenienceScene);

        private void HandleShare()
        {
            // TODO: 리포트 공유 (이미지/링크). 현재는 미구현.
            Debug.Log("[RecordDetail] 공유 기능 미구현");
        }

        private const string TrainingConvenienceScene = "TrainingConvenienceScene";

        // ===== TTS =====

        private void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            SpeakAsync(text).Forget();
        }

        private async UniTaskVoid SpeakAsync(string text)
        {
            try
            {
                _tts ??= BuildTts();
                if (_tts != null) await _tts.SpeakAsync(text, _ct);
            }
            catch (System.OperationCanceledException) { }
            catch (System.Exception e) { Debug.LogWarning($"[RecordDetail] TTS 실패: {e.Message}"); }
        }

        private ITtsService BuildTts()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            var key = ApiKeyLoader.GetOrFallback(ApiKeyLoader.GoogleTtsApi, ApiKeyLoader.GeminiApi);
            if (string.IsNullOrEmpty(key)) return new NoopTtsService();
            return new CloudTtsService(key, audioSource);
        }
    }
}
