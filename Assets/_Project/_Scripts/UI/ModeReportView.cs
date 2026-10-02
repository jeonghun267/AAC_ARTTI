using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Report;

namespace Artti.UI
{
    // 모드별 리포트(정확도 / 대화) 공통 화면: 뒤로·다른 모드·다시 하기 버튼, 기간 탭, 날짜, 스탯 카드 4장,
    // 막대 항목, 종합 평가(레이더 + 등급). 각 리포트는 Refresh에서 값을 채우고 자기 칸(대화 기록, 어려운 발음 등)을 더한다.
    public abstract class ModeReportView : MonoBehaviour
    {
        [Header("네비게이션")]
        [SerializeField] private Button backBtn;
        [SerializeField] private Button otherModeBtn;
        [SerializeField] private Button retryBtn;

        [Header("기간 탭 (오늘 / 이번 주 / 전체)")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private RectTransform tabHighlight;
        [SerializeField] private Color tabOnColor = Color.white;
        [SerializeField] private Color tabOffColor = new Color(0.78f, 0.84f, 1f, 0.9f);
        [SerializeField] private TMP_Text dateText;

        [Header("스탯 카드 4장")]
        [SerializeField] private TMP_Text[] statValues;
        [SerializeField] private TMP_Text[] statNotes;
        [SerializeField] private GameObject[] statChips;
        [SerializeField] private Image[] statChipArrows;
        [SerializeField] private TMP_Text[] statChipTexts;
        [SerializeField] private Sprite arrowUp;
        [SerializeField] private Sprite arrowDown;

        [Header("막대 항목")]
        [SerializeField] private RectTransform[] barFills;
        [SerializeField] private float barFullWidth = 216f;
        [SerializeField] private float barMinWidth = 18f;
        [SerializeField] private TMP_Text[] barValues;

        [Header("종합 평가 (레이더 5축, 위에서 시계 방향)")]
        [SerializeField] private UIRadarChart radar;
        [SerializeField] private TMP_Text[] radarValues;
        [SerializeField] private Image gradeBadge;
        [SerializeField] private Sprite gradeBestSprite;    // '우수'가 그려진 알약
        [SerializeField] private Sprite gradePlainSprite;   // 글자를 지운 알약 + gradeText
        [SerializeField] private TMP_Text gradeText;

        protected ReportDataService Service { get; private set; }
        protected ReportPeriod Period { get; private set; } = ReportPeriod.Today;
        private CancellationTokenSource _animCts;

        protected abstract TrainingMode Mode { get; }
        protected abstract void Refresh(bool firstShow);

        protected virtual void Awake()
        {
            if (backBtn != null) backBtn.onClick.AddListener(() => SceneFader.Load("MainScene"));
            if (otherModeBtn != null)
                otherModeBtn.onClick.AddListener(() => ReportNavigation.Open(
                    Mode == TrainingMode.Conversation ? TrainingMode.Accuracy : TrainingMode.Conversation));
            if (retryBtn != null)
                retryBtn.onClick.AddListener(() =>
                {
                    TrainingModeContext.Mode = Mode;
                    SceneFader.Load("TrainingConvenienceScene");
                });
            if (tabButtons != null)
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    var period = (ReportPeriod)i;
                    if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SelectPeriod(period));
                }
            foreach (var b in new[] { backBtn, otherModeBtn, retryBtn }) UIPressScale.Attach(b);
        }

        protected virtual void Start()
        {
            if (dateText != null) dateText.text = ModeReportText.Date(DateTime.Now);
            ApplyTab(Period, false);
            InitAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid InitAsync(CancellationToken ct)
        {
            var store = AppBootstrap.Instance?.LogStore;
            string path = null;
            if (store != null)
            {
                await store.FlushAsync(ct);
                path = (store as JsonAppendLogStore)?.FilePath;
            }
            Service = new ReportDataService(path);
            Refresh(true);
        }

        private void SelectPeriod(ReportPeriod period)
        {
            if (period == Period) return;
            Period = period;
            ApplyTab(period, true);
            Refresh(false);
        }

        private void ApplyTab(ReportPeriod period, bool animate)
        {
            int idx = (int)period;
            if (tabLabels != null)
                for (int i = 0; i < tabLabels.Length; i++)
                    if (tabLabels[i] != null)
                    {
                        tabLabels[i].color = i == idx ? tabOnColor : tabOffColor;
                        tabLabels[i].fontStyle = i == idx ? FontStyles.Bold : FontStyles.Normal;
                    }
            if (tabHighlight == null || tabButtons == null || idx >= tabButtons.Length || tabButtons[idx] == null) return;
            var target = new Vector2(((RectTransform)tabButtons[idx].transform).anchoredPosition.x, tabHighlight.anchoredPosition.y);
            if (!animate) { tabHighlight.anchoredPosition = target; return; }
            var from = tabHighlight.anchoredPosition;
            UIFx.Tween(this, 0f, 0.22f, k => tabHighlight.anchoredPosition = Vector2.Lerp(from, target, UIFx.EaseOutCubic(k)),
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        // ===== 하위 화면이 부르는 칸 채우기 =====

        protected void SetStat(int i, string value, string note, int? delta, string chipUnit)
        {
            SetText(statValues, i, value);
            SetText(statNotes, i, note);
            if (statChips == null || i >= statChips.Length || statChips[i] == null) return;
            statChips[i].SetActive(delta.HasValue);
            if (!delta.HasValue) return;
            if (statChipArrows != null && i < statChipArrows.Length && statChipArrows[i] != null)
                statChipArrows[i].sprite = delta.Value >= 0 ? arrowUp : arrowDown;
            SetText(statChipTexts, i, $"{Mathf.Abs(delta.Value)}{chipUnit}");
        }

        // 막대 값(0~100, 없으면 -1) + 레이더 5축 + 종합 등급. 막대·레이더는 Animate로 차오른다
        protected void SetScores(int[] bars, int[] radarValues5, int overall, bool firstShow, Action<float> extraAnimation = null)
        {
            for (int i = 0; i < bars.Length; i++) SetText(barValues, i, bars[i] >= 0 ? $"{bars[i]}%" : "-");
            var radar01 = new float[radarValues5.Length];
            for (int i = 0; i < radarValues5.Length; i++)
            {
                radar01[i] = Mathf.Max(0, radarValues5[i]) / 100f;
                SetText(radarValues, i, radarValues5[i] >= 0 ? $"{radarValues5[i]}%" : "-");
            }
            string grade = ModeReportText.Grade(overall);
            bool best = grade == "우수";
            var sprite = best ? gradeBestSprite : gradePlainSprite;
            if (gradeBadge != null && sprite != null) gradeBadge.sprite = sprite;
            if (gradeText != null)
            {
                gradeText.text = grade;
                gradeText.gameObject.SetActive(!best);
            }
            Animate(bars, radar01, firstShow, extraAnimation);
        }

        // 막대가 차오르고 레이더가 커진다(처음엔 조금 늦게 천천히, 탭을 바꾸면 빠르게). extra(0~1)로 화면별 연출을 덧붙인다
        private void Animate(int[] bars, float[] radar01, bool firstShow, Action<float> extra)
        {
            var ct = UIFx.Restart(ref _animCts, this);
            float delay = firstShow ? 0.25f : 0f, dur = firstShow ? 0.7f : 0.35f;
            var targets = new float[bars.Length];
            for (int i = 0; i < bars.Length; i++)
                targets[i] = bars[i] > 0 ? Mathf.Max(barMinWidth, barFullWidth * bars[i] / 100f) : 0f;
            radar?.SetValues(radar01);
            UIFx.Tween(this, delay, dur, k =>
            {
                float e = UIFx.EaseOutCubic(k);
                for (int i = 0; barFills != null && i < barFills.Length && i < targets.Length; i++)
                {
                    if (barFills[i] == null) continue;
                    barFills[i].sizeDelta = new Vector2(targets[i] * e, barFills[i].sizeDelta.y);
                    barFills[i].gameObject.SetActive(targets[i] > 0f);
                }
                if (radar != null) radar.Grow = e;
                extra?.Invoke(k);
            }, ct).Forget();
        }

        protected static void SetText(TMP_Text[] texts, int i, string value)
        {
            if (texts != null && i < texts.Length && texts[i] != null) texts[i].text = value;
        }
    }
}
