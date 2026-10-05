#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Artti.Common.Speech;

namespace Artti.EditorTests.Speech
{
    // 두 모델 서버(Artti_ASR_dual_epoch2)의 동작 방식(mode) 필드: 문서에서 찾기, 보낼 때만 넣기, 모르는 서버에는 넣지 않기.
    //   훈련모드는 mode=accuracy(기존 명료도 모델만), 현장모드는 mode=dialogue(새 모델 인식 + 기존 모델 명료도)를 보낸다.
    public class SttServerModeTests
    {
        // 실제 서버와 같은 FastAPI 문서 모양: audio(파일) + target(선택) + mode(선택, enum dialogue/accuracy, 기본 dialogue)
        private const string DualSchema = @"{
  ""openapi"": ""3.1.0"",
  ""info"": { ""title"": ""dual"", ""version"": ""0.1"" },
  ""paths"": {
    ""/analyze"": { ""post"": {
      ""summary"": ""Analyze"", ""operationId"": ""analyze_analyze_post"",
      ""requestBody"": { ""required"": true, ""content"": { ""multipart/form-data"": {
        ""schema"": { ""$ref"": ""#/components/schemas/Body_analyze_analyze_post"" } } } },
      ""responses"": { ""200"": { ""description"": ""ok"" } } } }
  },
  ""components"": { ""schemas"": { ""Body_analyze_analyze_post"": {
    ""type"": ""object"", ""required"": [""audio""],
    ""properties"": {
      ""audio"": { ""type"": ""string"", ""format"": ""binary"", ""contentMediaType"": ""application/octet-stream"", ""title"": ""Audio"" },
      ""target"": { ""type"": ""string"", ""maxLength"": 500, ""title"": ""Target"", ""default"": """" },
      ""mode"": { ""type"": ""string"", ""enum"": [""dialogue"", ""accuracy""], ""title"": ""Mode"", ""default"": ""dialogue"" } } } } }
}";

        // 기존 한 모델 서버(8000): mode 필드 없음
        private const string SingleSchema = @"{
  ""openapi"": ""3.1.0"",
  ""info"": { ""title"": ""clarity"", ""version"": ""0.1"" },
  ""paths"": {
    ""/analyze"": { ""post"": {
      ""requestBody"": { ""required"": true, ""content"": { ""multipart/form-data"": {
        ""schema"": { ""$ref"": ""#/components/schemas/Body"" } } } },
      ""responses"": { ""200"": { ""description"": ""ok"" } } } }
  },
  ""components"": { ""schemas"": { ""Body"": {
    ""type"": ""object"", ""required"": [""audio"", ""target""],
    ""properties"": {
      ""audio"": { ""type"": ""string"", ""format"": ""binary"" },
      ""target"": { ""type"": ""string"" } } } } }
}";

        private static SttEndpoint Discover(string schema)
        {
            Assert.That(SttServerProtocol.TryDiscover(schema, "ko", out var endpoint, out var note), Is.True, note);
            return endpoint;
        }

        private static string FieldValue(SttEndpoint endpoint, string name)
        {
            string value = null;
            int count = 0;
            foreach (var f in endpoint.FormFields)
                if (f.Key == name) { value = f.Value; count++; }
            Assert.That(count, Is.LessThanOrEqualTo(1), $"{name} 필드가 두 번 들어감");
            return value;
        }

        [Test]
        public void DualServer_ModeFieldAndOptionsFound_NotFixedAtDiscovery()
        {
            var endpoint = Discover(DualSchema);

            Assert.That(endpoint.Path, Is.EqualTo("/analyze"));
            Assert.That(endpoint.TargetField, Is.EqualTo("target"));
            Assert.That(endpoint.ModeField, Is.EqualTo("mode"));
            Assert.That(endpoint.ModeOptions, Is.EquivalentTo(new[] { "dialogue", "accuracy" }));
            Assert.That(endpoint.HasField("mode"), Is.False);   // 선택 필드라 탐색 때 서버 기본값을 박아 두지 않음
        }

        [TestCase("accuracy")]   // 훈련모드: 기존 명료도 모델만
        [TestCase("dialogue")]   // 현장모드: 새 모델 인식 + 기존 모델 명료도
        public void DualServer_WithMode_SendsThatMode(string mode)
        {
            var sending = Discover(DualSchema).WithTarget("물 주세요").WithMode(mode);

            Assert.That(FieldValue(sending, "mode"), Is.EqualTo(mode));
            Assert.That(FieldValue(sending, "target"), Is.EqualTo("물 주세요"));
        }

        [Test]
        public void WithMode_DoesNotChangeDiscoveredEndpoint()
        {
            var endpoint = Discover(DualSchema);
            endpoint.WithMode("accuracy");

            Assert.That(endpoint.HasField("mode"), Is.False);
        }

        [Test]
        public void SingleServer_WithoutModeField_SendsNothingExtra()
        {
            var endpoint = Discover(SingleSchema);

            Assert.That(endpoint.ModeField, Is.Null);
            Assert.That(endpoint.WithMode("accuracy"), Is.SameAs(endpoint));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("translate")]   // 문서 선택지에 없는 값
        public void DualServer_UnknownOrEmptyMode_SendsNothing(string mode)
        {
            var endpoint = Discover(DualSchema);

            Assert.That(endpoint.WithMode(mode), Is.SameAs(endpoint));
        }

        [Test]
        public void OtherMeaningOfMode_IsNotTouched()
        {
            // 다른 서버의 mode(transcribe/translate)에는 dialogue/accuracy를 넣지 않는다
            var schema = DualSchema.Replace(@"[""dialogue"", ""accuracy""]", @"[""transcribe"", ""translate""]")
                                   .Replace(@"""default"": ""dialogue""", @"""default"": ""transcribe""");
            var endpoint = Discover(schema);

            Assert.That(endpoint.WithMode("accuracy"), Is.SameAs(endpoint));
        }

        [Test]
        public void RequiredMode_DefaultReplacedNotDuplicated()
        {
            var schema = DualSchema.Replace(@"""required"": [""audio""]", @"""required"": [""audio"", ""mode""]");
            var endpoint = Discover(schema);
            Assert.That(FieldValue(endpoint, "mode"), Is.EqualTo("dialogue"));   // 필수라 문서 기본값을 채워 둠

            Assert.That(FieldValue(endpoint.WithMode("accuracy"), "mode"), Is.EqualTo("accuracy"));
        }
    }
}
#endif
