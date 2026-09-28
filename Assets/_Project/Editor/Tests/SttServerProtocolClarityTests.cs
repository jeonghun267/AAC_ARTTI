#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Artti.Common.Speech;

namespace Artti.EditorTests.Speech
{
    // 명료도(발화 정확도) 서버 지원: 목표 문장 필드 탐색, 목표 문장 전송, 응답의 인식 문장·명료도 파싱.
    public class SttServerProtocolClarityTests
    {
        // FastAPI 기본 문서 모양: POST /analyze, multipart { audio: 파일, target: 문자열(필수) }
        private const string AnalyzeSchema = @"{
  ""openapi"": ""3.1.0"",
  ""info"": { ""title"": ""clarity"", ""version"": ""0.1"" },
  ""paths"": {
    ""/analyze"": { ""post"": {
      ""summary"": ""Analyze"", ""operationId"": ""analyze_analyze_post"",
      ""requestBody"": { ""required"": true, ""content"": { ""multipart/form-data"": {
        ""schema"": { ""$ref"": ""#/components/schemas/Body_analyze_analyze_post"" } } } },
      ""responses"": { ""200"": { ""description"": ""ok"" } } } }
  },
  ""components"": { ""schemas"": { ""Body_analyze_analyze_post"": {
    ""type"": ""object"", ""required"": [""audio"", ""target""],
    ""properties"": {
      ""audio"": { ""type"": ""string"", ""format"": ""binary"", ""title"": ""Audio"" },
      ""target"": { ""type"": ""string"", ""title"": ""Target"" } } } } }
}";

        private const string AnalyzeResponse =
            "{\"recognized\":\"무 주세요\",\"target\":\"물 주세요\",\"clarity\":0.58,\"clarity_percent\":58,\"light\":\"yellow\"}";

        [Test]
        public void Discover_AnalyzeWithRequiredTarget_IsChosen_TargetFilledPerRequest()
        {
            Assert.That(SttServerProtocol.TryDiscover(AnalyzeSchema, "ko", out var endpoint, out var note), Is.True, note);

            Assert.That(endpoint.Path, Is.EqualTo("/analyze"));
            Assert.That(endpoint.FileField, Is.EqualTo("audio"));
            Assert.That(endpoint.TargetField, Is.EqualTo("target"));
            Assert.That(endpoint.HasField("target"), Is.False);   // 탐색 때 고정하지 않음

            var sending = endpoint.WithTarget("물 주세요");
            Assert.That(sending.HasField("target"), Is.True);
            Assert.That(endpoint.HasField("target"), Is.False);   // 원본은 그대로
        }

        [Test]
        public void Discover_PrefersEndpointWithTargetField()
        {
            var schema = AnalyzeSchema.Replace("\"paths\": {", @"""paths"": {
    ""/upload"": { ""post"": {
      ""requestBody"": { ""content"": { ""multipart/form-data"": { ""schema"": {
        ""type"": ""object"", ""properties"": { ""file"": { ""type"": ""string"", ""format"": ""binary"" } } } } } },
      ""responses"": { ""200"": { ""description"": ""ok"" } } } },");

            Assert.That(SttServerProtocol.TryDiscover(schema, "ko", out var endpoint, out var note), Is.True, note);
            Assert.That(endpoint.Path, Is.EqualTo("/analyze"));
        }

        [Test]
        public void WithTarget_CandidateEndpoint_SendsTargetFormField()
        {
            var e = SttServerProtocol.CandidateFor("/stt", "file", "ko");
            Assert.That(e.WithTarget("물 주세요").HasField(SttServerProtocol.DefaultTargetField), Is.True);
        }

        [Test]
        public void WithTarget_DocumentedEndpointWithoutTarget_SendsNothingExtra()
        {
            var e = SttServerProtocol.CandidateFor("/transcribe", "file", "ko", source: "openapi");
            Assert.That(e.WithTarget("물 주세요").HasField(SttServerProtocol.DefaultTargetField), Is.False);
        }

        [Test]
        public void WithTarget_QueryTarget_GoesToQuery()
        {
            var e = SttServerProtocol.CandidateFor("/analyze", "audio", "ko", source: "openapi");
            e.TargetField = "target";
            e.TargetInQuery = true;

            var sending = e.WithTarget("물 주세요");
            Assert.That(sending.Query, Does.Contain("target=" + System.Uri.EscapeDataString("물 주세요")));
            Assert.That(sending.HasField("target"), Is.False);
        }

        [Test]
        public void WithTarget_NoTarget_Unchanged()
        {
            var e = SttServerProtocol.CandidateFor("/stt", "file", "ko");
            Assert.That(ReferenceEquals(e.WithTarget(null), e), Is.True);
        }

        [Test]
        public void AnalyzeResponse_TranscriptIsRecognizedSentence()
        {
            Assert.That(SttServerProtocol.TryParseTranscript(AnalyzeResponse, out var text, out _), Is.True);
            Assert.That(text, Is.EqualTo("무 주세요"));
        }

        [Test]
        public void AnalyzeResponse_ClarityPercentLightAndTarget()
        {
            Assert.That(SttServerProtocol.TryParseClarity(AnalyzeResponse, out var pct, out var light, out var target), Is.True);
            Assert.That(pct, Is.EqualTo(58));
            Assert.That(light, Is.EqualTo("yellow"));
            Assert.That(target, Is.EqualTo("물 주세요"));
        }

        [TestCase("{\"text\":\"물 주세요\",\"clarity\":0.64}", 64)]
        [TestCase("{\"text\":\"물 주세요\",\"clarity\":72.0}", 72)]
        [TestCase("{\"text\":\"물 주세요\",\"clarity_percent\":120}", 100)]
        [TestCase("{\"result\":{\"recognized\":\"물 주세요\",\"clarity_percent\":81}}", 81)]
        public void ClarityVariants(string body, int expected)
        {
            Assert.That(SttServerProtocol.TryParseClarity(body, out var pct, out _, out _), Is.True);
            Assert.That(pct, Is.EqualTo(expected));
        }

        [TestCase("{\"text\":\"물 주세요\"}")]
        [TestCase("{\"text\":\"물 주세요\",\"confidence\":0.9}")]
        [TestCase("물 주세요")]
        [TestCase("")]
        [TestCase("{\"clarity\":\"high\"}")]
        public void PlainSttResponses_HaveNoClarity(string body)
        {
            Assert.That(SttServerProtocol.TryParseClarity(body, out var pct, out var light, out var target), Is.False);
            Assert.That(pct, Is.EqualTo(-1));
            Assert.That(light, Is.Null);
            Assert.That(target, Is.Null);
        }
    }
}
#endif
