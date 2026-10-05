#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Artti.AAC.Logging;
using Artti.Common.Speech;
using Artti.Report;
using Artti.Training;

namespace Artti.EditorTests.Report
{
    // 현장지원모드(대화 모드)의 두 모델 서버: 인식 문장(새 모델)은 대화에, 명료도와 그 근거 문장(기존 모델)은 상세기록에.
    //   훈련지원모드(정확도 모드)처럼 한 모델이 인식·채점하면 기록 형식은 예전과 같다.
    public class DualModelClarityRecordTests
    {
        private sealed class CaptureLogStore : ILogStore
        {
            public readonly List<AACEvent> Events = new List<AACEvent>();
            public void Log(AACEvent ev) => Events.Add(ev);
            public UniTask FlushAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask DisposeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
        }

        // 실제 서버(mode=dialogue) 응답 모양
        private const string DialogueResponse =
            "{\"recognized\":\"물 주세요.\",\"target\":\"물 주세요\",\"mode\":\"dialogue\",\"dialogue_recognized\":\"물 주세요.\"," +
            "\"clarity_recognized\":\"무 주세여.\",\"clarity_available\":true,\"cer\":0.4,\"clarity\":0.6,\"clarity_percent\":60,\"light\":\"yellow\"}";

        private CaptureLogStore _store;
        private EventLogger _logger;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _store = new CaptureLogStore();
            _logger = new EventLogger(_store, "test_profile", "convenience");
            _path = Path.Combine(Path.GetTempPath(), "artti_dual_" + Guid.NewGuid().ToString("N") + ".jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        private ReportRecordDetail BuildReport()
        {
            var lines = new List<string>();
            foreach (var ev in _store.Events) lines.Add(JsonUtility.ToJson(ev));
            File.WriteAllLines(_path, lines);
            return new ReportDataService(_path).GetRecordReport(_store.Events[0].sessionId);
        }

        private static SttResult Spoken(string text, string clarityText, int pct, string light, string target) =>
            new SttResult
            {
                text = text, clarityText = clarityText, hasClarity = true,
                clarityPercent = pct, clarityLight = light, clarityTarget = target
            };

        [Test]
        public void DialogueResponse_ClarityTextIsOldModelSentence()
        {
            Assert.That(SttServerProtocol.TryParseTranscript(DialogueResponse, out var text, out _), Is.True);
            Assert.That(text, Is.EqualTo("물 주세요."));   // NPC 대화에 쓰는 새 모델 문장
            Assert.That(SttServerProtocol.TryParseClarityText(DialogueResponse, out var clarityText), Is.True);
            Assert.That(clarityText, Is.EqualTo("무 주세여."));
        }

        [Test]
        public void SingleModelResponse_HasNoClarityText()
        {
            const string single = "{\"recognized\":\"무 주세요\",\"target\":\"물 주세요\",\"clarity_percent\":58,\"light\":\"yellow\"}";
            Assert.That(SttServerProtocol.TryParseClarityText(single, out _), Is.False);
        }

        [Test]
        public void ConversationTurn_HeardIsNewModel_ClarityHeardIsOldModel()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("select_items");
            _logger.LogCardSelected("guided_voice", "물 주세요", Spoken("물 주세요", "무 주세요", 58, "yellow", "물 주세요"));

            var step = BuildReport().steps[0];

            Assert.That(step.heardText, Is.EqualTo("물 주세요"));      // 새 모델이 알아들은 문장(대화)
            Assert.That(step.clarityHeard, Is.EqualTo("무 주세요"));   // 명료도를 계산한 기존 모델 문장
            Assert.That(step.clarityPct, Is.EqualTo(58));
            Assert.That(step.clarityTarget, Is.EqualTo("물 주세요"));
            Assert.That(step.clarityReason, Is.EqualTo("‘물’ 부분이 ‘무’로 들렸어요."));
        }

        [Test]
        public void SameModel_PayloadUnchanged_ClarityHeardIsSttText()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("select_items");
            _logger.LogCardSelected("guided_voice", "물 주세요", Spoken("무 주세요", "무 주세요", 58, "yellow", "물 주세요"));

            Assert.That(_store.Events[_store.Events.Count - 1].payloadJson,
                        Is.EqualTo("guided_voice:물 주세요|stt:무 주세요|clarity:58:yellow:물 주세요"));
            Assert.That(BuildReport().steps[0].clarityHeard, Is.EqualTo("무 주세요"));
        }

        [Test]
        public void ClarityHeardSegment_DoesNotLeakIntoUserOrHeardText()
        {
            _logger.LogScenarioEntered();
            _logger.LogObjectiveEntered("greeting");
            _logger.LogCardSelected("card_cvs_greeting", "안녕하세요", Spoken("안녕하세요", "안녕하세여", 70, "yellow", "안녕하세요"));

            var step = BuildReport().steps[0];

            Assert.That(step.userText, Is.EqualTo("안녕하세요"));
            Assert.That(step.heardText, Is.EqualTo("안녕하세요"));
            Assert.That(step.clarityHeard, Is.EqualTo("안녕하세여"));
        }
    }
}
#endif
