using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Report;

namespace Artti.UI
{
    // 대화 모드 리포트(REPORT 시안). 계산은 ConversationReportBuilder, 공통 칸은 ModeReportView.
    // 이 화면만의 칸: 대화 기록(최근 대화 세션) + 전체 보기, AI 코멘트, 이번 주 변화 그래프.
    public class ConversationReportView : ModeReportView
    {
        [Serializable]
        public class MessageRow
        {
            public GameObject npcRoot;
            public RectTransform npcBubble;
            public TMP_Text npcText;
            public TMP_Text npcTime;
            public GameObject userRoot;
            public RectTransform userBubble;
            public TMP_Text userText;
            public TMP_Text userTime;
        }

        [Header("대화 기록")]
        [SerializeField] private Button viewAllBtn;
        [SerializeField] private Image[] timelineDots;
        [SerializeField] private TMP_Text[] timelineNumbers;
        [SerializeField] private Color userDotColor = new Color32(52, 232, 250, 255);
        [SerializeField] private Color npcDotColor = new Color32(66, 132, 255, 255);
        [SerializeField] private Color emptyDotColor = new Color(0.55f, 0.65f, 0.9f, 0.25f);
        [SerializeField] private MessageRow[] rows;
        [SerializeField] private float bubblePadding = 36f;
        [SerializeField] private float bubbleMinWidth = 90f;
        [SerializeField] private float bubbleMaxWidth = 250f;
        [SerializeField] private TMP_Text historyEmpty;

        [Header("AI 코멘트 / 이번 주 변화")]
        [SerializeField] private TMP_Text[] comments;
        [SerializeField] private UILineChart weekChart;
        [SerializeField] private GameObject weekChip;
        [SerializeField] private TMP_Text weekChipText;

        // 변경점: '전체 보기' = 이 화면 위에 뜨는 창. 그 대화의 모든 말을 말풍선으로 스크롤해서 본다.
        //   줄 원본(점원 / 나) 아래 Bubble, Bubble/Text, Time이 있다. 줄은 처음 열 때 만들고 다음부터 다시 쓴다
        [Header("전체 대화 기록 창 (전체 보기)")]
        [SerializeField] private GameObject historyOverlay;
        [SerializeField] private Button historyCloseBtn;
        [SerializeField] private Button historyDimBtn;
        [SerializeField] private TMP_Text historySubtitle;
        [SerializeField] private ScrollRect historyScroll;
        [SerializeField] private RectTransform historyContent;
        [SerializeField] private RectTransform npcRowTemplate;
        [SerializeField] private RectTransform userRowTemplate;
        [SerializeField] private TMP_Text historyOverlayEmpty;
        [SerializeField] private float npcBubbleX = 46f;      // 점원 말풍선 왼쪽(NPC 동그라미 오른쪽)
        [SerializeField] private float userLeftMargin = 70f;  // 내 말풍선이 넘지 않는 왼쪽 여백(오른쪽에 붙어 보이게)
        [SerializeField] private float timeWidth = 76f;       // 시각 칸 폭
        [SerializeField] private float rowGap = 14f;
        [SerializeField] private float rowMinHeight = 40f;

        private sealed class HistoryRow
        {
            public RectTransform root, bubble;
            public TMP_Text text, time;
        }

        private readonly List<HistoryRow> _npcRows = new List<HistoryRow>();
        private readonly List<HistoryRow> _userRows = new List<HistoryRow>();
        private string _latestSessionId;

        // 변경점: 대화 기록 점 연출 — 화면을 열거나 탭을 바꾸면 점이 왼쪽부터 하나씩 톡 나타나고(한 번),
        //   그 뒤에는 가장 최근 말 점만 천천히 숨 쉬듯 커졌다 작아진다(번쩍임 없음)
        const float DotPopDelay = 0.25f, DotPopStep = 0.06f, DotPopSeconds = 0.3f;
        const float DotBreathSeconds = 1.8f, DotBreathAmount = 0.18f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private System.Threading.CancellationTokenSource _dotCts;
        private RectTransform _breathDot;
        private float _breathFrom;

        protected override TrainingMode Mode => TrainingMode.Conversation;

        protected override void Awake()
        {
            base.Awake();
            if (viewAllBtn != null) viewAllBtn.onClick.AddListener(OpenHistory);
            if (historyCloseBtn != null) historyCloseBtn.onClick.AddListener(() => UIPopupMotion.Close(historyOverlay));
            if (historyDimBtn != null) historyDimBtn.onClick.AddListener(() => UIPopupMotion.Close(historyOverlay));
            UIPressScale.Attach(viewAllBtn);
            UIPressScale.Attach(historyCloseBtn);
            if (historyOverlay != null) historyOverlay.SetActive(false);
            // 변경점: AI 코멘트는 길이가 들쭉날쭉해 한 줄 칸을 넘으면 글자를 줄여 끝까지 보이게(그래도 넘치면 빌더의 말줄임)
            for (int i = 0; comments != null && i < comments.Length; i++)
            {
                if (comments[i] == null) continue;
                comments[i].fontSizeMax = comments[i].fontSize;
                comments[i].fontSizeMin = CommentMinFontSize;
                comments[i].enableAutoSizing = true;
            }
        }

        private const float CommentMinFontSize = 12.5f;

        protected override void Refresh(bool firstShow)
        {
            var data = ConversationReportBuilder.Build(Service, Period, DateTime.Now);
            var cur = data.current;
            var prev = data.previous;
            _latestSessionId = data.latestSessionId;

            bool any = cur.sessions > 0;
            SetStat(0, any ? ModeReportText.Value(ModeReportText.Minutes(cur.talkSeconds), "분") : "-",
                ConversationReportLabels.TalkNote(data), ConversationReportLabels.TalkDelta(data), "%");
            SetStat(1, ModeReportText.Value(cur.naturalness, "%"), ConversationReportLabels.NaturalnessNote(data),
                ModeReportText.Delta(cur.naturalness, prev?.naturalness ?? -1), "%");
            SetStat(2, ModeReportText.Value(cur.appropriateness, "%"), ConversationReportLabels.AppropriatenessNote(data),
                ModeReportText.Delta(cur.appropriateness, prev?.appropriateness ?? -1), "%");
            SetStat(3, any ? ModeReportText.Value(cur.completed, "개") : "-", ConversationReportLabels.CompletedNote(data),
                ConversationReportLabels.CompletedDelta(data), "개");

            ShowHistory(data);
            var lines = ConversationReportLabels.Comments(cur, data.latestEvaluation);   // 변경점: AI 평가가 있으면 잘한 점·다음에 해 볼 점
            for (int i = 0; comments != null && i < comments.Length; i++) SetText(comments, i, i < lines.Length ? lines[i] : "");

            var week = new float[data.week.Length];
            for (int i = 0; i < week.Length; i++) week[i] = data.week[i] >= 0 ? data.week[i] / 100f : -1f;
            weekChart?.SetValues(week);
            var weekDelta = ConversationReportLabels.WeekDelta(data.week);
            if (weekChip != null) weekChip.SetActive(weekDelta.HasValue);
            if (weekChipText != null && weekDelta.HasValue) weekChipText.text = $"{(weekDelta.Value >= 0 ? "+" : "")}{weekDelta.Value}%";

            // 막대: 자연스러움 / 응답 속도 / 질문 이해 / 대화 지속력. 그래프는 왼쪽부터 그려진다
            SetScores(new[] { cur.naturalness, cur.speed, cur.understanding, cur.persistence }, cur.Radar, cur.Overall, firstShow,
                k => { if (weekChart != null) weekChart.Reveal = k; });
        }

        private void ShowHistory(ConversationReportData data)
        {
            var lines = data.lines;
            if (_breathDot != null) _breathDot.localScale = Vector3.one;
            _breathDot = null;
            var ct = UIFx.Restart(ref _dotCts, this);
            int shown = 0;
            for (int i = 0; timelineDots != null && i < timelineDots.Length; i++)
            {
                bool has = i < lines.Count;
                if (timelineDots[i] != null)
                {
                    timelineDots[i].color = !has ? emptyDotColor : lines[i].isUser ? userDotColor : npcDotColor;
                    var rt = timelineDots[i].rectTransform;
                    if (has)
                    {
                        UIFx.PopAroundIn(rt, rt.anchoredPosition, Center, DotPopDelay + i * DotPopStep, DotPopSeconds, 0.2f, ct).Forget();
                        _breathDot = rt;
                        shown = i + 1;
                    }
                    else
                    {
                        rt.localScale = Vector3.one;
                        UIFx.Group(rt).alpha = 1f;
                    }
                }
                if (timelineNumbers != null && i < timelineNumbers.Length && timelineNumbers[i] != null)
                    timelineNumbers[i].alpha = has ? 1f : 0.35f;
            }
            _breathFrom = Time.unscaledTime + DotPopDelay + shown * DotPopStep + DotPopSeconds;   // 나타나기가 끝난 뒤부터 숨쉬기

            int rowCount = rows != null ? rows.Length : 0;
            int start = Mathf.Max(0, lines.Count - rowCount);   // 말풍선은 마지막 턴들
            for (int i = 0; i < rowCount; i++)
            {
                var row = rows[i];
                int li = start + i;
                bool has = li < lines.Count;
                bool user = has && lines[li].isUser;
                if (row.npcRoot != null) row.npcRoot.SetActive(has && !user);
                if (row.userRoot != null) row.userRoot.SetActive(user);
                if (!has) continue;
                var line = lines[li];
                if (user) FillBubble(row.userBubble, row.userText, row.userTime, line);
                else FillBubble(row.npcBubble, row.npcText, row.npcTime, line);
            }
            if (historyEmpty != null) historyEmpty.gameObject.SetActive(lines.Count == 0);
        }

        private void Update()
        {
            if (_breathDot == null) return;
            float t = Time.unscaledTime - _breathFrom;
            if (t < 0f) return;
            float k = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / DotBreathSeconds);
            _breathDot.localScale = Vector3.one * (1f + DotBreathAmount * k);
        }

        // 말풍선 폭을 글자에 맞춘다(최소~최대). 시각은 말풍선 오른쪽에 붙어 있어 같이 움직인다
        private void FillBubble(RectTransform bubble, TMP_Text text, TMP_Text time, ConversationLine line)
        {
            if (text != null) text.text = line.text;
            if (time != null) time.text = ModeReportText.Time(line.timeMs);
            if (bubble == null || text == null) return;
            float w = text.GetPreferredValues(line.text).x + bubblePadding;
            bubble.sizeDelta = new Vector2(Mathf.Clamp(w, bubbleMinWidth, bubbleMaxWidth), bubble.sizeDelta.y);
        }

        // ===== 전체 대화 기록 창 =====

        // 대화 기록 판과 같은 대화(이 기간의 가장 최근 대화)의 모든 말. 기록이 없으면 빈 창 안내
        private void OpenHistory()
        {
            FillHistory(ConversationReportBuilder.Transcript(Service, _latestSessionId));
            UIPopupMotion.Open(historyOverlay);
        }

        private void FillHistory(ConversationTranscript t)
        {
            if (historySubtitle != null) historySubtitle.text = ConversationReportLabels.TranscriptSubtitle(t);
            if (historyOverlayEmpty != null) historyOverlayEmpty.gameObject.SetActive(t.lines.Count == 0);
            if (historyContent == null) return;
            float width = historyContent.rect.width;
            float y = 0f;
            int npc = 0, user = 0;
            foreach (var line in t.lines)
            {
                var row = line.isUser ? Row(_userRows, userRowTemplate, user++) : Row(_npcRows, npcRowTemplate, npc++);
                if (row == null) continue;
                float h = LayoutRow(row, line, width);
                row.root.anchoredPosition = new Vector2(0f, -y);
                y += h + rowGap;
            }
            for (int i = npc; i < _npcRows.Count; i++) _npcRows[i].root.gameObject.SetActive(false);
            for (int i = user; i < _userRows.Count; i++) _userRows[i].root.gameObject.SetActive(false);
            historyContent.sizeDelta = new Vector2(historyContent.sizeDelta.x, Mathf.Max(0f, y - rowGap));
            if (historyScroll != null) historyScroll.verticalNormalizedPosition = 1f;   // 대화 처음부터
        }

        private HistoryRow Row(List<HistoryRow> pool, RectTransform template, int index)
        {
            if (template == null) return null;
            while (pool.Count <= index)
            {
                var root = (RectTransform)Instantiate(template.gameObject, historyContent).transform;
                pool.Add(new HistoryRow
                {
                    root = root,
                    bubble = (RectTransform)root.Find("Bubble"),
                    text = root.Find("Bubble/Text")?.GetComponent<TMP_Text>(),
                    time = root.Find("Time")?.GetComponent<TMP_Text>(),
                });
            }
            return pool[index];
        }

        // 말풍선 폭은 글자에 맞추고(최대 폭을 넘으면 줄바꿈), 높이는 줄 수만큼. 여백은 원본의 글자 칸 여백을 그대로 쓴다.
        //   점원: 말풍선 왼쪽 고정, 시각은 말풍선 오른쪽 아래 / 나: 말풍선 오른쪽 고정, 시각은 오른쪽 끝 칸
        private float LayoutRow(HistoryRow row, ConversationLine line, float width)
        {
            row.root.gameObject.SetActive(true);
            if (row.bubble == null || row.text == null) return rowMinHeight;
            bool user = line.isUser;
            var tr = row.text.rectTransform;
            float padL = tr.offsetMin.x, padB = tr.offsetMin.y, padR = -tr.offsetMax.x, padT = -tr.offsetMax.y;
            float maxBubble = user ? width - timeWidth - 8f - userLeftMargin : width - npcBubbleX - timeWidth - 10f;
            float maxText = Mathf.Max(40f, maxBubble - padL - padR);
            // 변경점: 목소리로 한 내 말은 모범 발음 + 내 발음 두 줄(ConversationReportLabels.HistoryText)
            var shown = ConversationReportLabels.HistoryText(line);
            row.text.text = shown;
            var pref = row.text.GetPreferredValues(shown, maxText, 0f);
            float w = Mathf.Min(pref.x, maxText) + padL + padR;
            float h = Mathf.Max(rowMinHeight, pref.y + padT + padB);
            row.bubble.sizeDelta = new Vector2(w, h);
            row.bubble.anchoredPosition = new Vector2(user ? width - timeWidth - 8f : npcBubbleX, 0f);
            if (row.time != null)
            {
                row.time.text = ModeReportText.Time(line.timeMs);
                row.time.rectTransform.anchoredPosition = new Vector2(user ? width - timeWidth + 2f : npcBubbleX + w + 10f, -h);
            }
            row.root.sizeDelta = new Vector2(row.root.sizeDelta.x, h);
            return h;
        }
    }
}
