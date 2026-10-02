#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Report;

namespace Artti.EditorTests.Report
{
    // 정확도 모드 리포트: 어려웠던 발음 풀이(초성·모음·받침·빠짐), 로마자, 대신 쓰는 지표(억양 안정성·또렷함·띄어읽기)
    public class AccuracyReportTests
    {
        private readonly List<AACEvent> _events = new List<AACEvent>();
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            _path = Path.Combine(Path.GetTempPath(), "artti_acc_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void Analyze_InitialDropped_IsWeak()
        {
            var d = AccuracyReportBuilder.Analyze("사과 주세요", "아과 주세요");

            Assert.That(d.word, Is.EqualTo("사과"));
            Assert.That(d.syllable, Is.EqualTo("사"));
            Assert.That(d.tag, Is.EqualTo("ㅅ 발음 약함"));
            Assert.That(d.description, Is.EqualTo("'사'의 초성 ㅅ이 약하게 발음되었어요."));
            Assert.That(d.romanization, Is.EqualTo("sa-gwa"));
        }

        [Test]
        public void Analyze_OtherInitial_IsUnclear()
        {
            var d = AccuracyReportBuilder.Analyze("초콜릿 주세요", "토콜릿 주세요");

            Assert.That(d.tag, Is.EqualTo("ㅊ 발음 불명확"));
            Assert.That(d.description, Is.EqualTo("'초'의 ㅊ 발음이 부정확했어요."));
            Assert.That(d.romanization, Is.EqualTo("cho-kol-lit"));
        }

        [Test]
        public void Analyze_FinalConsonant_And_Missing()
        {
            var final = AccuracyReportBuilder.Analyze("학교 가요", "하교 가요");
            Assert.That(final.word, Is.EqualTo("학교"));
            Assert.That(final.tag, Is.EqualTo("받침 처리 불안정"));
            Assert.That(final.description, Is.EqualTo("'학'의 받침 ㄱ이 약하게 들렸어요."));
            Assert.That(final.romanization, Is.EqualTo("hak-gyo"));

            var missing = AccuracyReportBuilder.Analyze("학교 가요", "교 가요");
            Assert.That(missing.tag, Is.EqualTo("소리 빠짐"));
            Assert.That(missing.syllable, Is.EqualTo("학"));

            Assert.That(AccuracyReportBuilder.Analyze("물 주세요", "물 주세요"), Is.Null);
        }

        // 비교 듣기: 들린 단어·바른 단어와 다른 글자 자리, 화면 글자(색), 읽을 문장
        [Test]
        public void Analyze_KeepsHeardAndRightForCompare()
        {
            var d = AccuracyReportBuilder.Analyze("사과 주세요", "아과 주세요");
            Assert.That(d.target, Is.EqualTo("사과 주세요"));
            Assert.That(d.heard, Is.EqualTo("아과 주세요"));
            Assert.That(d.wordIndex, Is.EqualTo(0));
            Assert.That(d.heardWord, Is.EqualTo("아과"));
            Assert.That(d.heardWordIndex, Is.EqualTo(0));
            Assert.That(AccuracyReportLabels.HeardLine(d), Is.EqualTo("이렇게 들렸어요  →  <color=#FF7FA8>아</color>과"));
            Assert.That(AccuracyReportLabels.RightLine(d), Is.EqualTo("바르게는  →  <color=#6FE8FF>사</color>과"));
            Assert.That(AccuracyReportLabels.HeardSpeech(d), Is.EqualTo("이렇게 들렸어요. 아과 주세요"));
            Assert.That(AccuracyReportLabels.RightSpeech(d), Is.EqualTo("바르게 말하면, 사과 주세요"));

            var second = AccuracyReportBuilder.Analyze("물 주세요 초콜릿 하나요", "물 주세요 토콜릿 하나요");
            Assert.That(second.word, Is.EqualTo("초콜릿"));
            Assert.That(second.heardWord, Is.EqualTo("토콜릿"));
            Assert.That(second.wordIndex, Is.EqualTo(0));

            var missing = AccuracyReportBuilder.Analyze("학교 가요", "교 가요");
            Assert.That(missing.heardWord, Is.EqualTo("교"), "소리가 빠지면 같은 자리 단어");
            Assert.That(missing.heardWordIndex, Is.EqualTo(-1));
            Assert.That(AccuracyReportLabels.HeardLine(missing), Is.EqualTo("이렇게 들렸어요  →  교"));
            Assert.That(AccuracyReportLabels.Mark("학교", 5, "FFFFFF"), Is.EqualTo("학교"));
        }

        [TestCase("물 주세요", "물 주세요", 100)]
        [TestCase("물 주세요", "물주세요", 0)]
        [TestCase("감사합니다", "감사합니다", -1)]
        public void PhrasingScore_ComparesSpacing(string target, string heard, int expected)
        {
            Assert.That(AccuracyReportBuilder.PhrasingScore(target, heard), Is.EqualTo(expected));
        }

        private static long Ms(DateTime local) => new DateTimeOffset(local).ToUnixTimeMilliseconds();

        private void Ev(AACEventType type, long ms, string objective = null, string payload = null)
        {
            _events.Add(new AACEvent
            {
                profileId = "p", sessionId = "acc1", eventType = type.ToString(), scenarioId = "convenience",
                objectiveId = objective, timestampUnixMs = ms, payloadJson = payload,
            });
        }

        [Test]
        public void Measure_UsesClarityLightsSpacingAndRetries()
        {
            var now = DateTime.Now;
            long t0 = Ms(now.Date.AddHours(9));
            Ev(AACEventType.ScenarioEntered, t0, payload: "mode:accuracy");
            Ev(AACEventType.ObjectiveEntered, t0, "select_items");
            Ev(AACEventType.TtsPlayed, t0 + 1000, payload: "Speak:찾으시는 물건 있으세요?");
            Ev(AACEventType.CardSelected, t0 + 5000, payload: "guided_voice:사과 주세요|stt:사과 주세요|clarity:90:green:사과 주세요");
            Ev(AACEventType.ObjectiveEntered, t0 + 6000, "checkout");
            Ev(AACEventType.TtsPlayed, t0 + 7000, payload: "Speak:어디 가세요?");
            Ev(AACEventType.CardSelected, t0 + 11000, payload: "guided_voice:학교 가요|stt:하교 가요|clarity:50:red:학교 가요");
            Ev(AACEventType.StepRetryAttempt, t0 + 11500, "checkout");
            Ev(AACEventType.SessionEnded, t0 + 120000, payload: "completed");
            var lines = new List<string>();
            foreach (var ev in _events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);

            var d = AccuracyReportBuilder.Build(new ReportDataService(_path), ReportPeriod.Today, now);
            var m = d.current;

            Assert.That(m.sessions, Is.EqualTo(1));
            Assert.That(m.practiceSeconds, Is.EqualTo(120));
            Assert.That(m.repeats, Is.EqualTo(1));
            Assert.That(m.pronunciation, Is.EqualTo(70));
            Assert.That(m.stability, Is.EqualTo(60), "표준편차 20 -> 100 - 40");
            Assert.That(m.clearness, Is.EqualTo(65), "초록 100, 빨강 30");
            Assert.That(m.phrasing, Is.EqualTo(100));
            Assert.That(m.speed, Is.EqualTo(100), "두 번 다 4초 만에 대답");
            Assert.That(m.difficult.Count, Is.EqualTo(1));
            Assert.That(m.difficult[0].word, Is.EqualTo("학교"));
            Assert.That(d.modelSentences, Is.EqualTo(new[] { "사과 주세요", "학교 가요" }), "내가 말한 문장 전부, 오래된 것부터");
            Assert.That(AccuracyReportLabels.ModelLength(d.modelSentences), Is.EqualTo("0:04"), "9글자 x 0.3 + 2문장 x 0.6");
        }

        [Test]
        public void SameSentenceTwice_IsReadOnce()
        {
            var now = DateTime.Now;
            long t0 = Ms(now.Date.AddHours(9));
            Ev(AACEventType.ScenarioEntered, t0, payload: "mode:accuracy");
            Ev(AACEventType.ObjectiveEntered, t0, "greeting");
            Ev(AACEventType.CardSelected, t0 + 3000, payload: "guided_voice:안녕하세요 반가워요|stt:안녕하세요 반가워요|clarity:95:green:안녕하세요 반가워요");
            Ev(AACEventType.CardSelected, t0 + 9000, payload: "guided_voice:안녕하세요 반가워요|stt:안녕하세요 반가워요|clarity:93:green:안녕하세요 반가워요");
            Ev(AACEventType.SessionEnded, t0 + 30000, payload: "completed");
            var lines = new List<string>();
            foreach (var ev in _events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);

            var d = AccuracyReportBuilder.Build(new ReportDataService(_path), ReportPeriod.All, now);

            Assert.That(d.current.difficult, Is.Empty);
            Assert.That(d.modelSentences, Is.EqualTo(new[] { "안녕하세요 반가워요" }));
            Assert.That(AccuracyReportLabels.DifficultNote(d), Is.EqualTo("어려운 발음이 없어요!"));
        }
    }
}
#endif
