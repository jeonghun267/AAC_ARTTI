using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.Common.Speech;
using Artti.Report;

namespace Artti.UI
{
    // 정확도 모드 리포트(정확도 시안, REPORT 조각 재사용). 계산은 AccuracyReportBuilder, 공통 칸은 ModeReportView.
    // 이 화면만의 칸: 어려웠던 발음 목록(줄마다 그 단어 듣기), 모범 발음 듣기(내가 말한 문장 전부를 차례로 읽어 주며 파형이 차오른다).
    public class AccuracyReportView : ModeReportView
    {
        [Serializable]
        public class DifficultRow
        {
            public GameObject root;
            public TMP_Text syllable;
            public TMP_Text word;
            public TMP_Text romanization;
            public TMP_Text tag;
            public TMP_Text description;
            public Button play;
            public Image playIcon;
        }

        [Header("어려웠던 발음 목록")]
        [SerializeField] private TMP_Text difficultTotal;
        [SerializeField] private DifficultRow[] difficultRows;
        [SerializeField] private TMP_Text difficultEmpty;

        // 변경점(사용자 결정, 2026-10-02): 모범 발음 듣기 = 그 기간에 내가 말한 문장 전부를 정확도 모드 점원 목소리로 차례로 읽는다.
        //   파형 아래 안내 글자에 지금 읽는 문장을 보여 주고, 밝은 파형은 글자 수 비율로 차오른다(문장 경계에 점선)
        [Header("모범 발음 듣기")]
        [SerializeField] private Button modelPlayBtn;
        [SerializeField] private TMP_Text modelTime;
        [SerializeField] private TMP_Text modelCaption;
        [SerializeField] private RectTransform waveProgress;   // 밝은 파형을 가리는 틀(RectMask2D). 폭 = 전체 x 진행
        [SerializeField] private float waveFullWidth = 1030f;
        [SerializeField] private RectTransform[] sentenceSeparators;   // 문장 경계 점선. 문장이 칸보다 많으면 모두 숨긴다
        [SerializeField] private float waveLeft = 480f;       // 점선 x 기준(부모 기준 파형 왼쪽)
        [SerializeField] private AudioSource audioSource;
        // 재생 중 표시: 누른 버튼의 아이콘이 멈춤(■)으로 바뀌고 천천히 커졌다 작아진다(번쩍임 없음). 다시 누르면 멈춘다
        [SerializeField] private Image modelPlayIcon;
        [SerializeField] private Sprite playSprite;
        [SerializeField] private Sprite stopSprite;

        const float PulseSeconds = 0.9f, PulseAmount = 0.08f;

        private List<string> _sentences = new List<string>();
        private int[] _sentenceStart = new int[0];   // 앞 문장들 글자 수 합(진행 계산)
        private int _totalSyllables;
        private ITtsService _tts;
        private CancellationTokenSource _playCts;
        private bool _playing;
        private int _playIndex;
        private int _shownSecond = -1;
        private float _playStart;
        private int _speakingRow = -1;   // 어려웠던 발음 줄 ▶로 듣는 중인 줄
        private List<DifficultSound> _difficult = new List<DifficultSound>();
        private Image _pulsing;          // 지금 재생 중 표시를 하는 아이콘

        protected override TrainingMode Mode => TrainingMode.Accuracy;

        protected override void Awake()
        {
            base.Awake();
            if (modelPlayBtn != null) modelPlayBtn.onClick.AddListener(ToggleModel);
            UIPressScale.Attach(modelPlayBtn);
            for (int i = 0; difficultRows != null && i < difficultRows.Length; i++)
            {
                var row = difficultRows[i];
                if (row?.play == null) continue;
                int index = i;
                row.play.onClick.AddListener(() => ToggleRow(index));
                UIPressScale.Attach(row.play);
            }
        }

        private void OnDestroy()
        {
            UIFx.Cancel(ref _playCts);
            _tts?.StopAll();
        }

        protected override void Refresh(bool firstShow)
        {
            var data = AccuracyReportBuilder.Build(Service, Period, DateTime.Now);
            var cur = data.current;
            var prev = data.previous;
            bool any = cur.sessions > 0;

            SetStat(0, any ? ModeReportText.Value(ModeReportText.Minutes(cur.practiceSeconds), "분") : "-",
                AccuracyReportLabels.PracticeNote(data), AccuracyReportLabels.PracticeDelta(data), "%");
            SetStat(1, ModeReportText.Value(cur.pronunciation, "%"), AccuracyReportLabels.PronunciationNote(data),
                AccuracyReportLabels.CountDelta(cur.pronunciation, prev, p => p.pronunciation), "%");
            SetStat(2, any ? ModeReportText.Value(cur.repeats, "회") : "-", AccuracyReportLabels.RepeatNote(data),
                any ? AccuracyReportLabels.CountDelta(cur.repeats, prev, p => p.repeats) : null, "회");
            SetStat(3, any ? ModeReportText.Value(cur.difficult.Count, "개") : "-", AccuracyReportLabels.DifficultNote(data),
                any ? AccuracyReportLabels.CountDelta(cur.difficult.Count, prev, p => p.difficult.Count) : null, "개");

            ShowDifficult(cur.difficult);
            StopModel();
            SetSentences(data.modelSentences);
            if (modelPlayBtn != null) modelPlayBtn.interactable = _sentences.Count > 0;
            SetProgress(_sentences.Count > 0 ? 1f : 0f);

            // 막대: 발음 정확도 / 발화 속도 / 억양 안정성 / 또렷함 / 띄어읽기 (= 레이더 순서)
            SetScores(cur.Radar, cur.Radar, cur.Overall, firstShow);
        }

        private void ShowDifficult(List<DifficultSound> list)
        {
            _difficult = list;
            if (difficultTotal != null) difficultTotal.text = $"총 {list.Count}개";
            for (int i = 0; difficultRows != null && i < difficultRows.Length; i++)
            {
                var row = difficultRows[i];
                bool has = i < list.Count;
                if (row.root != null) row.root.SetActive(has);
                if (!has) continue;
                var d = list[i];
                if (row.syllable != null) row.syllable.text = d.word.Length > 0 ? d.word.Substring(0, 1) : d.syllable;
                if (row.word != null) row.word.text = d.word;
                if (row.romanization != null) row.romanization.text = $"/{d.romanization}/";
                if (row.tag != null) row.tag.text = d.tag;
                if (row.description != null) row.description.text = d.description;
            }
            if (difficultEmpty != null) difficultEmpty.gameObject.SetActive(list.Count == 0);
        }

        // 문장 목록을 받아 글자 수 누계·문장 경계 점선·안내 글자·길이를 맞춘다
        private void SetSentences(List<string> sentences)
        {
            _sentences = sentences ?? new List<string>();
            _sentenceStart = new int[_sentences.Count];
            _totalSyllables = 0;
            for (int i = 0; i < _sentences.Count; i++)
            {
                _sentenceStart[i] = _totalSyllables;
                _totalSyllables += Mathf.Max(1, AccuracyReportLabels.Syllables(_sentences[i]));
            }
            int boundaries = Mathf.Max(0, _sentences.Count - 1);
            bool showSeparators = sentenceSeparators != null && boundaries <= sentenceSeparators.Length;
            for (int i = 0; sentenceSeparators != null && i < sentenceSeparators.Length; i++)
            {
                var sep = sentenceSeparators[i];
                if (sep == null) continue;
                bool has = showSeparators && i < boundaries;
                sep.gameObject.SetActive(has);
                if (has)
                    sep.anchoredPosition = new Vector2(waveLeft + waveFullWidth * _sentenceStart[i + 1] / _totalSyllables, sep.anchoredPosition.y);
            }
            ShowIdle();
        }

        private void ShowIdle()
        {
            if (modelCaption != null) modelCaption.text = AccuracyReportLabels.ModelIdle(_sentences.Count);
            if (modelTime != null) modelTime.text = AccuracyReportLabels.ModelLength(_sentences);
        }

        // ===== 듣기 =====

        private void ToggleModel()
        {
            if (_playing) { StopModel(); SetProgress(1f); return; }
            if (_sentences.Count == 0) return;
            StopModel();   // 줄 ▶로 듣는 중이었으면 끊는다
            SetPlayingLook(modelPlayIcon);
            PlayModelAsync(UIFx.Restart(ref _playCts, this)).Forget();
        }

        // 문장마다: 음성을 받아 재생을 시작하고(SpeakAsync는 시작하면 바로 돌아온다), 소리가 끝날 때까지 기다린 뒤 다음 문장
        private async UniTaskVoid PlayModelAsync(CancellationToken ct)
        {
            _tts ??= BuildTts();
            _playing = true;
            _playStart = Time.unscaledTime;
            _shownSecond = -1;
            try
            {
                for (_playIndex = 0; _playIndex < _sentences.Count; _playIndex++)
                {
                    if (modelCaption != null)
                        modelCaption.text = AccuracyReportLabels.ModelPlaying(_playIndex, _sentences.Count, _sentences[_playIndex]);
                    await SpeakAndWaitAsync(_sentences[_playIndex], ct);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.45f), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogWarning($"[AccuracyReport] 모범 발음 재생 실패: {e.Message}"); }
            if (ct.IsCancellationRequested) return;
            _playing = false;
            ClearPlayingLook();
            SetProgress(1f);
            ShowIdle();
        }

        // 어려웠던 발음 줄 ▶ = 비교 듣기(사용자 결정, 2026-10-02): "이렇게 들렸어요. (들린 문장)" -> "바르게 말하면, (바른 문장)".
        //   소리에 맞춰 그 줄 설명 자리에 '이렇게 들렸어요 → 아과'(다르게 들린 글자 분홍) -> '바르게는 → 사과'(맞는 글자 하늘색)를 보여 주고,
        //   다 읽으면 잠시 뒤 원래 설명으로 돌아온다. 읽는 중에 다시 누르면 멈춘다
        const float CompareGapSeconds = 0.6f, CompareHoldSeconds = 1.5f;

        private void ToggleRow(int index)
        {
            if (_speakingRow == index) { StopModel(); return; }
            if (index >= _difficult.Count) return;
            StopModel();
            SetProgress(1f);
            _tts ??= BuildTts();
            _speakingRow = index;
            SetPlayingLook(difficultRows[index].playIcon);
            SpeakCompareAsync(index, _difficult[index], UIFx.Restart(ref _playCts, this)).Forget();
        }

        private async UniTaskVoid SpeakCompareAsync(int index, DifficultSound d, CancellationToken ct)
        {
            var description = difficultRows[index].description;
            try
            {
                if (!string.IsNullOrWhiteSpace(d.heard))
                {
                    if (description != null) description.text = AccuracyReportLabels.HeardLine(d);
                    await SpeakAndWaitAsync(AccuracyReportLabels.HeardSpeech(d), ct);
                    await UniTask.Delay(TimeSpan.FromSeconds(CompareGapSeconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
                }
                if (description != null) description.text = AccuracyReportLabels.RightLine(d);
                await SpeakAndWaitAsync(AccuracyReportLabels.RightSpeech(d), ct);
                await UniTask.Delay(TimeSpan.FromSeconds(CompareHoldSeconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException) { return; }   // 멈춤은 StopModel이 되돌린다
            catch (Exception e) { Debug.LogWarning($"[AccuracyReport] 비교 듣기 실패: {e.Message}"); }
            if (ct.IsCancellationRequested) return;
            EndRow();
        }

        // 음성을 받아 재생을 시작하고(SpeakAsync는 시작하면 바로 돌아온다) 소리가 끝날 때까지 기다린다. 음성을 못 받으면 바로 돌아온다
        private async UniTask SpeakAndWaitAsync(string text, CancellationToken ct)
        {
            if (audioSource != null) audioSource.clip = null;
            await _tts.SpeakAsync(text, ct);
            if (audioSource == null || audioSource.clip == null) return;
            float started = Time.unscaledTime;
            await UniTask.WaitUntil(() => audioSource == null || (!audioSource.isPlaying && Time.unscaledTime - started > 0.1f),
                PlayerLoopTiming.Update, ct);
        }

        // 줄 비교 듣기를 끝낸다: 설명을 되돌리고 재생 중 표시를 끈다
        private void EndRow()
        {
            if (_speakingRow >= 0 && _speakingRow < _difficult.Count && difficultRows[_speakingRow].description != null)
                difficultRows[_speakingRow].description.text = _difficult[_speakingRow].description;
            _speakingRow = -1;
            ClearPlayingLook();
        }

        private void StopModel()
        {
            UIFx.Cancel(ref _playCts);
            _tts?.StopAll();
            _playing = false;
            EndRow();
            ShowIdle();
        }

        private void SetPlayingLook(Image icon)
        {
            ClearPlayingLook();
            _pulsing = icon;
            if (_pulsing != null && stopSprite != null) _pulsing.sprite = stopSprite;
        }

        private void ClearPlayingLook()
        {
            if (_pulsing == null) return;
            if (playSprite != null) _pulsing.sprite = playSprite;
            _pulsing.rectTransform.localScale = Vector3.one;
            _pulsing = null;
        }

        // 재생 중: (앞 문장 글자 수 + 지금 문장 글자 수 x 재생 위치) / 전체 글자 수 만큼 밝은 파형이 차오르고, 지난 시간(초)을 보여 준다
        private void Update()
        {
            if (_pulsing != null)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / PulseSeconds);
                _pulsing.rectTransform.localScale = Vector3.one * (1f + PulseAmount * k);
            }
            if (!_playing || _totalSyllables == 0 || _playIndex >= _sentences.Count) return;
            float part = 0f;
            if (audioSource != null && audioSource.isPlaying && audioSource.clip != null && audioSource.clip.length > 0f)
                part = Mathf.Clamp01(audioSource.time / audioSource.clip.length);
            int len = Mathf.Max(1, AccuracyReportLabels.Syllables(_sentences[_playIndex]));
            SetProgress(Mathf.Clamp01((_sentenceStart[_playIndex] + len * part) / _totalSyllables));
            int sec = Mathf.FloorToInt(Time.unscaledTime - _playStart);
            if (sec != _shownSecond && modelTime != null)
            {
                _shownSecond = sec;
                modelTime.text = $"{sec / 60}:{sec % 60:00}";
            }
        }

        private void SetProgress(float k)
        {
            if (waveProgress != null) waveProgress.sizeDelta = new Vector2(waveFullWidth * k, waveProgress.sizeDelta.y);
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
            var tts = new CloudTtsService(key, audioSource);
            ProfileVoice.Apply(tts);   // 변경점: 모범 발음도 프로필 목소리(여성/남성)·빠르기(예전: 항상 정확도 모드 점원 여성 목소리)
            return tts;
        }
    }
}
