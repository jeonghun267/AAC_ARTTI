using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Artti.Common.Speech
{
    // 서버 STT 요청 한 종류: POST {Path}?{Query} 에 multipart로 {FileField}=WAV + FormFields를 보낸다.
    public sealed class SttEndpoint
    {
        public string Path;
        public string FileField;
        public string Query;
        public List<KeyValuePair<string, string>> FormFields = new List<KeyValuePair<string, string>>();
        // OpenAI 호환 서버처럼 모델 id를 요구하면 그 필드 이름. 값은 ServerSttService가 서버에 물어 채운다.
        public string ModelField;
        public string ModelFallback; // 서버 목록에서 못 고르면 쓸 문서의 기본값·예시·enum
        public string Source; // "manual" | "openapi" | "candidate" — 로그와 실패 처리용
        // 변경점: 목표 문장(명료도 비교 기준)을 받는 필드. 문서에 있으면 그 이름, 값은 발화마다 ServerSttService가 채운다.
        public string TargetField;
        public bool TargetInQuery;   // 문서상 폼 필드가 아니라 쿼리 파라미터인 경우
        // 변경점: 서버 동작 방식 필드(두 모델 서버의 mode=dialogue|accuracy)와 문서의 enum 값.
        //   문서에 있을 때만 채우고, 값은 보낼 때 ServerSttService가 정한다(WithMode).
        public string ModeField;
        public List<string> ModeOptions = new List<string>();

        // 같은 엔드포인트 판별용 ("경로|파일 필드")
        public string Key => Path.TrimEnd('/') + "|" + FileField;

        public bool HasField(string name)
        {
            foreach (var f in FormFields)
                if (string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // 필드 하나를 더한 사본 (원본은 그대로)
        public SttEndpoint WithField(string name, string value)
        {
            var copy = (SttEndpoint)MemberwiseClone();
            copy.FormFields = new List<KeyValuePair<string, string>>(FormFields) { new KeyValuePair<string, string>(name, value) };
            return copy;
        }

        // 변경점: 이번 발화의 목표 문장을 담은 사본.
        //   문서에 목표 필드가 있으면 그 이름(폼 또는 쿼리)으로, 문서 없이 찾은 경로면 "target" 폼 필드로 보낸다
        //   (선언되지 않은 폼 필드는 대부분의 서버가 무시한다). 문서가 있는데 목표 필드가 없으면 보내지 않는다.
        public SttEndpoint WithTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return this;
            string field = TargetField ?? (Source == "openapi" ? null : SttServerProtocol.DefaultTargetField);
            if (field == null || HasField(field)) return this;
            if (!TargetInQuery) return WithField(field, target);

            var copy = (SttEndpoint)MemberwiseClone();
            copy.Query = SttServerProtocol.MergeQuery(Query, Uri.EscapeDataString(field) + "=" + Uri.EscapeDataString(target));
            return copy;
        }

        // 변경점: 서버 동작 방식(mode)을 담은 사본. 문서의 enum에 이 값이 있을 때만 보낸다
        //   (mode 필드가 없거나 뜻이 다른 mode인 서버에는 아무것도 더하지 않는다). 문서 기본값으로 이미 넣은 값은 바꾼다.
        public SttEndpoint WithMode(string mode)
        {
            if (string.IsNullOrEmpty(ModeField) || string.IsNullOrWhiteSpace(mode)) return this;
            if (ModeOptions == null || !ModeOptions.Contains(mode)) return this;
            var copy = (SttEndpoint)MemberwiseClone();
            copy.FormFields = new List<KeyValuePair<string, string>>(FormFields.Count + 1);
            foreach (var f in FormFields)
                if (!string.Equals(f.Key, ModeField, StringComparison.OrdinalIgnoreCase)) copy.FormFields.Add(f);
            copy.FormFields.Add(new KeyValuePair<string, string>(ModeField, mode));
            return copy;
        }

        public string Describe() =>
            $"POST {Path}{(string.IsNullOrEmpty(Query) ? "" : "?" + Query)} (file field '{FileField}', {Source})";
    }

    // STT 서버와 주고받는 형식. 서버 구현을 모르는 상태에서 붙어야 하므로
    //   1) 서버의 API 문서(FastAPI 기본 /openapi.json 등)에서 음성 업로드 엔드포인트를 찾고
    //   2) 문서가 없으면 흔한 경로 × 파일 필드 이름 후보를 차례로 시도하며
    //   3) 응답은 {"text": ...} 계열 JSON이나 평문 모두 받는다.
    // UnityEngine에 의존하지 않는 순수 로직 — 에디터 없이 테스트할 수 있게 분리했다.
    public static class SttServerProtocol
    {
        // API 문서 위치 후보. 앞에서 404면 다음을 본다 (FastAPI 기본, 접두사 붙은 FastAPI, Swagger/DRF/Spring/Flask).
        public static readonly string[] SchemaPaths =
        {
            "/openapi.json", "/api/openapi.json", "/api/v1/openapi.json", "/docs/openapi.json",
            "/swagger.json", "/v3/api-docs", "/api/schema/?format=json", "/apispec_1.json"
        };

        // 문서가 없을 때 시도할 경로. 앞쪽일수록 흔한 형태.
        //   /stt, /transcribe : FastAPI 등으로 직접 만든 서버에서 가장 흔한 이름
        //   /v1/audio/transcriptions : OpenAI 호환 서버(speaches, openedai-whisper, LocalAI 등, model 필드 필요)
        //   /inference : whisper.cpp server
        //   /asr : whisper-asr-webservice (필드 audio_file, 옵션은 쿼리스트링)
        public static readonly string[] CandidatePaths =
        {
            "/stt", "/transcribe", "/v1/audio/transcriptions", "/inference", "/asr",
            "/api/stt", "/api/transcribe", "/speech-to-text", "/recognize", "/api/v1/transcribe", "/v1/transcriptions"
        };

        // 한 경로에서 시도할 파일 필드 이름
        private static readonly string[] FileFieldNames = { "file", "audio", "audio_file", "upload_file", "voice" };

        public static IEnumerable<string> FileFieldsFor(string path)
        {
            if (TrimSlash(path) == "/asr") yield return "audio_file";
            foreach (var f in FileFieldNames)
                if (!(TrimSlash(path) == "/asr" && f == "audio_file")) yield return f;
        }

        public static SttEndpoint CandidateFor(string path, string fileField, string language, string query = null, string source = "candidate")
        {
            var e = new SttEndpoint { Path = path, FileField = fileField, Query = query, Source = source };
            string bare = TrimSlash(path);
            if (bare == "/asr")
            {
                // whisper-asr-webservice는 언어·출력 형식을 쿼리로 받는다 (직접 지정한 쿼리가 있으면 그 값이 우선)
                e.Query = MergeQuery($"language={Uri.EscapeDataString(language)}&output=json", query);
                return e;
            }

            // 선언되지 않은 폼 필드는 대부분의 서버가 무시하므로 언어 힌트는 넣어 둔다.
            if (!string.IsNullOrEmpty(language)) e.FormFields.Add(new KeyValuePair<string, string>("language", language));
            if (bare == "/v1/audio/transcriptions" || bare == "/inference")
                e.FormFields.Add(new KeyValuePair<string, string>("response_format", "json"));
            if (IsOpenAiTranscriptionPath(bare)) e.ModelField = "model";
            return e;
        }

        // OpenAI 호환 음성 인식 경로 (model 필드에 서버가 가진 모델 id를 넣어야 한다)
        public static bool IsOpenAiTranscriptionPath(string path) =>
            TrimSlash(path).EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase);

        // 쿼리 두 개를 합친다. 같은 키는 overrides 쪽 값을 쓴다.
        public static string MergeQuery(string baseQuery, string overrides)
        {
            if (string.IsNullOrEmpty(overrides)) return baseQuery;
            if (string.IsNullOrEmpty(baseQuery)) return overrides;

            var overrideKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in overrides.Split('&'))
                if (pair.Length > 0) overrideKeys.Add(pair.Split('=')[0]);

            var sb = new StringBuilder();
            foreach (var pair in baseQuery.Split('&'))
            {
                if (pair.Length == 0 || overrideKeys.Contains(pair.Split('=')[0])) continue;
                sb.Append(pair).Append('&');
            }
            return sb.Append(overrides).ToString();
        }

        private static string TrimSlash(string path) => path.Length > 1 ? path.TrimEnd('/') : path;

        // Inspector 경로 입력을 경로와 쿼리로 나눈다. "/stt?lang=ko", "stt", "v1.0/transcribe", 실수로 넣은
        // 전체 URL이나 "192.168.0.167:8000/stt" 모두 받는다. 경로가 "/"뿐이거나 없으면 path는 null(자동 탐색)이고 쿼리만 남긴다.
        public static void ParseManualPath(string input, out string path, out string query)
        {
            string p = (input ?? string.Empty).Trim();
            query = null;
            int q = p.IndexOf('?');
            if (q >= 0)
            {
                query = p.Substring(q + 1);
                if (query.Length == 0) query = null;
                p = p.Substring(0, q);
            }

            int scheme = p.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                int slash = p.IndexOf('/', scheme + 3);
                p = slash >= 0 ? p.Substring(slash) : "/";
            }
            else if (!p.StartsWith("/"))
            {
                int slash = p.IndexOf('/');
                string head = slash >= 0 ? p.Substring(0, slash) : p;
                if (LooksLikeHost(head)) p = slash >= 0 ? p.Substring(slash) : "/";
            }

            if (!p.StartsWith("/")) p = "/" + p;
            path = p == "/" ? null : p;
        }

        // "192.168.0.167", "192.168.0.167:8000", "server.local:8000", "localhost:8000" — 포트가 있거나 IPv4면 호스트로 본다.
        // "v1.0", "stt.php"처럼 점만 있는 경로 조각은 호스트가 아니다.
        private static bool LooksLikeHost(string head)
        {
            if (head.Length == 0) return false;
            int colon = head.LastIndexOf(':');
            if (colon > 0 && colon < head.Length - 1)
            {
                bool digits = true;
                for (int i = colon + 1; i < head.Length; i++) digits &= char.IsDigit(head[i]);
                if (digits) return true;
            }
            var parts = head.Split('.');
            if (parts.Length != 4) return false;
            foreach (var part in parts)
                if (!byte.TryParse(part, out _)) return false;
            return true;
        }

        // 브라우저에서 보던 문서 화면·상태 확인 주소 — 인식 경로로 쓰면 안 된다
        public static bool IsDocumentationPath(string path)
        {
            string p = TrimSlash(path).ToLowerInvariant();
            if (p == "/docs" || p == "/redoc" || p == "/health" || p.StartsWith("/swagger") || p.StartsWith("/docs/")) return true;
            foreach (var s in SchemaPaths)
                if (p == TrimSlash(s.Split('?')[0]).ToLowerInvariant()) return true;
            return false;
        }

        // ===== API 문서 탐색 (OpenAPI 3.x, Swagger 2.0) =====

        private static readonly string[] LanguageNames = { "language", "lang", "language_code", "languagecode", "lang_code", "locale" };
        // 응답 형식 필드. 단순 "format"은 업로드 음성 형식(wav 등)을 뜻하는 경우가 있어 넣지 않는다.
        private static readonly string[] FormatNames = { "response_format", "output", "output_format" };

        // 변경점: 목표 문장(사용자가 말하려던 문장) 필드. 명료도·발음 평가 서버가 인식 결과와 비교하는 기준.
        //   값은 발화마다 달라 탐색 때 고정하지 않는다(필수여도 '채울 수 없음'으로 보지 않음).
        public const string DefaultTargetField = "target";
        private static readonly string[] TargetNames =
        {
            "target", "target_text", "target_sentence", "reference", "reference_text", "expected", "expected_text", "answer"
        };

        public static bool IsTargetField(string name) =>
            !string.IsNullOrEmpty(name) && Array.IndexOf(TargetNames, name.ToLowerInvariant()) >= 0;

        // 변경점: 서버 동작 방식 필드(두 모델 서버: mode=dialogue|accuracy). 값은 발화마다 ServerSttService가 정한다.
        public const string ModeFieldName = "mode";
        public static bool IsModeField(string name) => string.Equals(name, ModeFieldName, StringComparison.OrdinalIgnoreCase);

        // 경로·operationId·summary에 이 단어가 있으면 음성 인식 엔드포인트일 가능성이 높다 (점수, 소문자 비교).
        private static readonly (string word, int score)[] PositiveWords =
        {
            ("transcri", 6), ("stt", 6), ("speech_to_text", 6), ("speech-to-text", 6), ("asr", 5),
            ("recogni", 4), ("whisper", 4), ("speech", 3), ("inference", 2), ("audio", 2), ("voice", 1),
            ("pronunci", 5), ("clarity", 5) // 변경점: 발음·명료도 평가 엔드포인트 (인식 문장 + 명료도)
        };

        // 변경점: 목표 문장 필드를 받는 엔드포인트는 인식 + 명료도 평가용일 가능성이 높아 우선한다
        //   (앱이 명료도로 상세기록 발화 정확도를 표시하므로). 이름이 중립적이어도(/analyze) 고를 수 있게 한다.
        private const int TargetFieldBonus = 3;

        // 음성 파일을 받더라도 인식이 아닌 기능(TTS 목소리 등록, 번역, 대화 에이전트 등)은 피한다.
        private static readonly (string word, int score)[] NegativeWords =
        {
            ("tts", -8), ("synthes", -8), ("clone", -8), ("speaker", -4), ("translat", -4),
            ("diariz", -4), ("upload_voice", -6), ("embedding", -4), ("detect", -4), ("chat", -3), ("sample", -3)
        };

        // OpenAPI/Swagger 문서인가 (paths 객체가 있는 JSON)
        public static bool IsApiSchema(string json)
        {
            string t = StripBom(json);
            if (t.Length == 0 || t[0] != '{') return false;
            try
            {
                return JObject.Parse(t)["paths"] is JObject;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // preferredPath: Inspector에서 경로를 지정했으면 문서에서 그 경로를 골라 필드 이름·옵션을 문서대로 맞춘다.
        // excludeKeys: 이미 거부된 엔드포인트(SttEndpoint.Key)는 고르지 않는다
        public static bool TryDiscover(string schemaJson, string language, out SttEndpoint endpoint, out string note,
            string preferredPath = null, ICollection<string> excludeKeys = null)
        {
            endpoint = null;
            note = null;
            if (string.IsNullOrWhiteSpace(schemaJson))
            {
                note = "API 문서가 비어 있음";
                return false;
            }

            try
            {
                return Discover(schemaJson, language, preferredPath, excludeKeys, out endpoint, out note);
            }
            catch (Exception e)
            {
                // 문서가 예상과 다른 모양이어도 예외를 올리지 않는다 (후보 경로 시도로 넘어가면 된다)
                endpoint = null;
                note = "API 문서 해석 실패: " + e.Message;
                return false;
            }
        }

        private static bool Discover(string schemaJson, string language, string preferredPath, ICollection<string> excludeKeys,
            out SttEndpoint endpoint, out string note)
        {
            endpoint = null;
            var root = JObject.Parse(StripBom(schemaJson));
            if (!(root["paths"] is JObject paths))
            {
                note = "API 문서에 paths 없음";
                return false;
            }

            bool swagger2 = root["swagger"] != null;
            string prefix = PathPrefix(root, swagger2);
            int bestScore = int.MinValue;
            int found = 0;
            int uploadEndpoints = 0; // 거부된 것 포함 — "인식용을 특정할 수 있는가" 판단은 전체 개수로 한다
            SttEndpoint preferred = null;
            foreach (var pathProp in paths.Properties())
            {
                if (pathProp.Name.Contains("{")) continue; // 경로 파라미터가 있는 엔드포인트는 채울 값을 모른다
                if (!(pathProp.Value is JObject pathItem)) continue;
                if (!(pathItem["post"] is JObject post)) continue;

                string fullPath = prefix + pathProp.Name;
                var candidate = swagger2
                    ? BuildFromSwagger2(root, fullPath, pathItem, post, language, out int penalty)
                    : BuildFromOperation(root, fullPath, pathItem, post, language, out penalty);
                if (candidate == null) continue;
                uploadEndpoints++;
                if (excludeKeys != null && excludeKeys.Contains(candidate.Key)) continue;

                found++;
                if (preferredPath != null && TrimSlash(fullPath) == TrimSlash(preferredPath)) preferred = candidate;

                int score = ScoreText(pathProp.Name) + ScoreText(post.Value<string>("operationId")) +
                            ScoreText(post.Value<string>("summary")) - penalty +
                            (candidate.TargetField != null ? TargetFieldBonus : 0);
                if (score > bestScore)
                {
                    bestScore = score;
                    endpoint = candidate;
                }
            }

            if (preferredPath != null)
            {
                endpoint = preferred;
                note = preferred != null ? "지정 경로를 API 문서에서 찾음" : "지정 경로가 API 문서에 없음";
                if (preferred != null) preferred.Source = "manual";
                return preferred != null;
            }

            if (endpoint == null)
            {
                note = "API 문서에 음성 파일을 받는 POST 엔드포인트 없음 (WebSocket 전용 서버일 수 있음)";
                return false;
            }

            // 이름으로 인식용이라고 판단되지 않으면 틀린 곳(TTS 목소리 등록 등)을 부를 수 있어 채택하지 않는다.
            // 업로드 엔드포인트가 하나뿐이면 이름이 중립적이어도(점수 0) 받아들인다.
            if (bestScore < 0 || (uploadEndpoints > 1 && bestScore == 0))
            {
                note = $"음성 업로드 엔드포인트 {found}개 중 인식용을 특정하지 못함 (score {bestScore}) — Stt Server Path를 지정하세요";
                endpoint = null;
                return false;
            }

            endpoint.Source = "openapi";
            note = $"API 문서에서 {found}개 중 선택 (score {bestScore})";
            return true;
        }

        // 문서의 경로가 접두사 기준일 때 (Swagger 2.0 basePath, OpenAPI servers[0].url이 "/api/v1" 같은 상대 경로)
        private static string PathPrefix(JObject root, bool swagger2)
        {
            string prefix = swagger2
                ? root.Value<string>("basePath")
                : ((root["servers"] as JArray)?.First as JObject)?.Value<string>("url");
            if (string.IsNullOrEmpty(prefix)) return string.Empty;
            if (!prefix.StartsWith("/"))
            {
                // "http://host:8000/api"처럼 절대 주소면 경로 부분만 쓴다
                if (!Uri.TryCreate(prefix, UriKind.Absolute, out var uri)) return string.Empty;
                prefix = uri.AbsolutePath;
            }
            return prefix.TrimEnd('/');
        }

        private static string StripBom(string s) => (s ?? string.Empty).Trim().TrimStart('\uFEFF').Trim();

        private static int ScoreText(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string t = text.ToLowerInvariant();
            int score = 0;
            foreach (var (word, s) in PositiveWords) if (t.Contains(word)) score += s;
            foreach (var (word, s) in NegativeWords) if (t.Contains(word)) score += s;
            return score;
        }

        // OpenAPI 3.x. FastAPI는 UploadFile을 File()로 받으면 multipart/form-data, Form()으로 받으면
        // application/x-www-form-urlencoded로 문서화한다 (speaches 등). 둘 다 본다.
        private static SttEndpoint BuildFromOperation(JObject root, string path, JObject pathItem, JObject post,
            string language, out int penalty)
        {
            penalty = 0;
            var content = Resolve(root, post["requestBody"])?["content"] as JObject;
            if (content == null) return null;

            JObject schema = null;
            foreach (var mediaType in new[] { "multipart/form-data", "application/x-www-form-urlencoded" })
            {
                schema = Resolve(root, content[mediaType]?["schema"]) as JObject;
                if (schema?["properties"] is JObject) break;
                schema = null;
            }
            if (schema == null) return null;

            var props = (JObject)schema["properties"];
            var required = new HashSet<string>(StringComparer.Ordinal);
            if (schema["required"] is JArray req)
                foreach (var r in req)
                    if (r.Type == JTokenType.String) required.Add((string)r);

            string fileField = null;
            foreach (var p in props.Properties())
            {
                if (IsBinary(root, p.Value, 0))
                {
                    fileField = p.Name;
                    break;
                }
            }
            if (fileField == null) return null;

            var endpoint = new SttEndpoint { Path = path, FileField = fileField };

            foreach (var p in props.Properties())
            {
                if (p.Name == fileField) continue;
                if (IsTargetField(p.Name))
                {
                    endpoint.TargetField = p.Name;   // 변경점: 목표 문장 필드 — 발화마다 채움
                    continue;
                }
                var ps = Resolve(root, p.Value) as JObject;
                if (IsModeField(p.Name))
                {
                    // 변경점: 동작 방식 필드와 선택지를 기억한다. 필수면 아래에서 문서 기본값을 넣고, 보낼 때 WithMode가 바꾼다.
                    endpoint.ModeField = p.Name;
                    endpoint.ModeOptions = CollectEnum(root, ps, 0);
                }
                if (NeedsServerModel(root, path, p.Name, ps, required.Contains(p.Name)))
                {
                    endpoint.ModelField = p.Name;
                    endpoint.ModelFallback = SchemaValue(root, ps);
                    continue;
                }
                AddChosenValue(root, endpoint, p.Name, ps, required.Contains(p.Name), language, ref penalty);
            }

            var query = new StringBuilder();
            foreach (var parameters in new[] { pathItem["parameters"], post["parameters"] })
            {
                if (!(parameters is JArray arr)) continue;
                foreach (var raw in arr)
                {
                    var prm = Resolve(root, raw) as JObject;
                    if (prm == null || prm.Value<string>("in") != "query") continue;
                    if (IsTargetField(prm.Value<string>("name")))
                    {
                        // 변경점: FastAPI에서 Form 없이 선언한 target: str 등은 쿼리 파라미터가 된다
                        if (endpoint.TargetField == null)
                        {
                            endpoint.TargetField = prm.Value<string>("name");
                            endpoint.TargetInQuery = true;
                        }
                        continue;
                    }
                    AppendQuery(root, query, prm, Resolve(root, prm["schema"]) as JObject, language, ref penalty);
                }
            }
            endpoint.Query = query.Length > 0 ? query.ToString() : null;
            return endpoint;
        }

        // Swagger 2.0 (drf-yasg, flasgger 등): 파일은 parameters의 {in: formData, type: file}
        private static SttEndpoint BuildFromSwagger2(JObject root, string path, JObject pathItem, JObject post,
            string language, out int penalty)
        {
            penalty = 0;
            var all = new List<JObject>();
            foreach (var parameters in new[] { pathItem["parameters"], post["parameters"] })
                if (parameters is JArray arr)
                    foreach (var raw in arr)
                        if (Resolve(root, raw) is JObject prm) all.Add(prm);

            string fileField = null;
            foreach (var prm in all)
                if (prm.Value<string>("in") == "formData" && prm.Value<string>("type") == "file")
                {
                    fileField = prm.Value<string>("name");
                    break;
                }
            if (string.IsNullOrEmpty(fileField)) return null;

            var endpoint = new SttEndpoint { Path = path, FileField = fileField };
            var query = new StringBuilder();
            foreach (var prm in all)
            {
                string name = prm.Value<string>("name");
                if (string.IsNullOrEmpty(name) || name == fileField) continue;
                string where = prm.Value<string>("in");
                if (IsTargetField(name) && (where == "formData" || where == "query") && endpoint.TargetField == null)
                {
                    endpoint.TargetField = name;   // 변경점: 목표 문장 필드 — 발화마다 채움
                    endpoint.TargetInQuery = where == "query";
                    continue;
                }
                if (where == "formData")
                {
                    bool isRequired = prm.Value<bool?>("required") ?? false;
                    if (NeedsServerModel(root, path, name, prm, isRequired))
                    {
                        endpoint.ModelField = name;
                        endpoint.ModelFallback = SchemaValue(root, prm);
                        continue;
                    }
                    AddChosenValue(root, endpoint, name, prm, isRequired, language, ref penalty);
                }
                else if (where == "query")
                {
                    AppendQuery(root, query, prm, prm, language, ref penalty);
                }
            }
            endpoint.Query = query.Length > 0 ? query.ToString() : null;
            return endpoint;
        }

        private static bool IsModelField(string name) =>
            string.Equals(name, "model", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "model_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "model_name", StringComparison.OrdinalIgnoreCase);

        // 모델 id를 서버에 물어서 채워야 하는가. OpenAI 호환 경로이거나, 필수인데 문서에 쓸 값(기본값·예시·enum)이 없을 때만.
        // 직접 만든 서버의 model: str = Form("small") 같은 필드는 다른 필드처럼 문서 값(또는 서버 기본값)을 쓴다.
        private static bool NeedsServerModel(JObject root, string path, string name, JObject schema, bool required)
        {
            if (!IsModelField(name)) return false;
            if (IsOpenAiTranscriptionPath(path)) return true;
            if (!required) return false;
            return SchemaValue(root, schema) == null;
        }

        // 문서가 알려 주는 값: 기본값 → 예시 → enum 첫 값
        private static string SchemaValue(JObject root, JObject schema)
        {
            var enumValues = CollectEnum(root, schema, 0);
            return ScalarString(schema?["default"]) ?? ScalarString(schema?["example"]) ??
                   ScalarString((schema?["examples"] as JArray)?.First) ?? (enumValues.Count > 0 ? enumValues[0] : null);
        }

        private static void AddChosenValue(JObject root, SttEndpoint endpoint, string name, JObject schema, bool required,
            string language, ref int penalty)
        {
            if (TryChooseValue(root, name, schema, required, language, out string value, out bool unfillable))
                endpoint.FormFields.Add(new KeyValuePair<string, string>(name, value));
            if (unfillable) penalty += 3;
        }

        private static void AppendQuery(JObject root, StringBuilder query, JObject prm, JObject schema, string language, ref int penalty)
        {
            string name = prm.Value<string>("name");
            if (string.IsNullOrEmpty(name)) return;
            bool isRequired = prm.Value<bool?>("required") ?? false;
            if (TryChooseValue(root, name, schema, isRequired, language, out string value, out bool unfillable))
            {
                if (query.Length > 0) query.Append('&');
                query.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
            }
            if (unfillable) penalty += 3;
        }

        // 알려진 옵션(언어, 응답 형식)은 채우고, 그 밖의 필수값은 스키마 기본값·예시·첫 enum으로 채운다.
        // 뜻을 모르는 필수 문자열에 임의 값을 넣지는 않는다 (조용히 틀린 값보다 로그에 남는 422가 낫다).
        private static bool TryChooseValue(JObject root, string name, JObject schema, bool required, string language,
            out string value, out bool unfillable)
        {
            value = null;
            unfillable = false;
            string lower = name.ToLowerInvariant();
            var enumValues = CollectEnum(root, schema, 0);
            string schemaDefault = ScalarString(schema?["default"]);

            if (Array.IndexOf(LanguageNames, lower) >= 0 && !string.IsNullOrEmpty(language))
            {
                value = ChooseLanguage(enumValues, schemaDefault, language);
                if (value != null) return true;
                if (!required || schemaDefault != null) return false; // 서버 기본값이 이미 한국어
            }

            if (Array.IndexOf(FormatNames, lower) >= 0)
            {
                // enum에 json이 있거나, enum도 기본값도 없을 때만 json을 요구한다.
                // 기본값만 있는 자유 문자열이면 서버가 json을 모를 수 있어 기본값에 맡긴다.
                if (enumValues.Contains("json") || (enumValues.Count == 0 && schemaDefault == null))
                {
                    value = "json";
                    return true;
                }
                if (!required) return false;
            }

            if (!required) return false;

            string fallback = schemaDefault ?? ScalarString(schema?["example"]) ??
                              ScalarString((schema?["examples"] as JArray)?.First) ??
                              (enumValues.Count > 0 ? enumValues[0] : null);
            if (fallback != null)
            {
                value = fallback;
                return true;
            }

            unfillable = true;
            return false;
        }

        // enum이 있으면 그 안에서 한국어를 고른다. 없으면 기본값 모양(ko-KR, Kor, korean)을 따라간다.
        // 기본값이 이미 한국어면 null(서버 기본값 사용).
        private static string ChooseLanguage(List<string> enumValues, string schemaDefault, string language)
        {
            if (enumValues.Count > 0)
            {
                foreach (var v in enumValues)
                    if (string.Equals(v, language, StringComparison.OrdinalIgnoreCase)) return v;
                foreach (var v in enumValues)
                    if (v.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase) ||
                        v.StartsWith(language + "_", StringComparison.OrdinalIgnoreCase)) return v;
                foreach (var v in enumValues)
                    if (string.Equals(v, "korean", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v, "kor", StringComparison.OrdinalIgnoreCase)) return v;
                return null;
            }

            if (string.IsNullOrEmpty(schemaDefault)) return language;
            string d = schemaDefault.Trim();
            if (d.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return null; // ko, ko-KR, kor, korean
            if (d.Length == 5 && (d[2] == '-' || d[2] == '_')) return language + d[2] + "KR";
            if (d.Length == 3) return char.IsUpper(d[0]) ? "Kor" : "kor"; // ISO 639-2 (eng, Eng)
            if (d.Length > 3 && char.IsLetter(d[0]) && !string.Equals(d, "auto", StringComparison.OrdinalIgnoreCase))
                return char.IsUpper(d[0]) ? "Korean" : "korean";
            return language;
        }

        private static List<string> CollectEnum(JObject root, JObject schema, int depth)
        {
            var list = new List<string>();
            if (schema == null || depth > 4) return list;
            if (schema["enum"] is JArray e)
                foreach (var v in e)
                {
                    var s = ScalarString(v);
                    if (s != null) list.Add(s);
                }
            foreach (var key in new[] { "anyOf", "oneOf", "allOf" })
                if (schema[key] is JArray alts)
                    foreach (var alt in alts)
                        list.AddRange(CollectEnum(root, Resolve(root, alt) as JObject, depth + 1));
            return list;
        }

        private static string ScalarString(JToken token)
        {
            if (token == null) return null;
            switch (token.Type)
            {
                case JTokenType.String: return (string)token;
                case JTokenType.Boolean: return (bool)token ? "true" : "false";
                case JTokenType.Integer:
                case JTokenType.Float: return token.ToString(Formatting.None);
                default: return null;
            }
        }

        // UploadFile은 OpenAPI 3.0에서 {"format":"binary"}, 3.1에서 {"contentMediaType": ...},
        // Optional이면 anyOf, 여러 파일이면 array.items로 나온다.
        private static bool IsBinary(JObject root, JToken token, int depth)
        {
            if (depth > 4 || !(Resolve(root, token) is JObject schema)) return false;
            if (schema.Value<string>("format") == "binary") return true;
            if (schema["contentMediaType"] != null || schema.Value<string>("contentEncoding") == "base64") return true;
            if (IsBinary(root, schema["items"], depth + 1)) return true;
            foreach (var key in new[] { "anyOf", "oneOf", "allOf" })
                if (schema[key] is JArray alts)
                    foreach (var alt in alts)
                        if (IsBinary(root, alt, depth + 1)) return true;
            return false;
        }

        // "#/components/schemas/Body_x" 형태의 $ref를 따라간다.
        private static JToken Resolve(JObject root, JToken token)
        {
            for (int guard = 0; guard < 8 && token is JObject obj && obj["$ref"] != null; guard++)
            {
                string reference = obj.Value<string>("$ref");
                if (string.IsNullOrEmpty(reference) || !reference.StartsWith("#/")) return null;
                JToken target = root;
                foreach (var part in reference.Substring(2).Split('/'))
                {
                    target = target is JObject o ? o[part.Replace("~1", "/").Replace("~0", "~")] : null;
                }
                token = target;
            }
            return token;
        }

        // OpenAI 호환 /v1/models 응답에서 음성 인식 모델 id를 고른다.
        // task 필터를 무시하고 전체 목록(LLM, TTS 포함)을 주는 서버가 있어, 항목에 음성 인식 task가 표시돼 있거나
        // 이름이 음성 인식 모델처럼 보이는 것만 받는다. 영어 전용(.en) 모델은 한국어를 못 하므로 뒤로 미룬다.
        private static readonly string[] AsrModelWords = { "whisper", "asr", "stt", "parakeet", "canary", "sensevoice", "transcri" };

        public static bool TryPickModel(string modelsJson, out string modelId)
        {
            modelId = null;
            if (string.IsNullOrWhiteSpace(modelsJson)) return false;
            JToken root;
            try
            {
                root = JToken.Parse(StripBom(modelsJson));
            }
            catch (JsonException)
            {
                return false;
            }

            var data = root is JObject o ? o["data"] as JArray : root as JArray;
            if (data == null) return false;

            string englishOnly = null;
            foreach (var item in data)
            {
                string id;
                bool asrTask = false;
                if (item is JObject m)
                {
                    id = m.Value<JToken>("id")?.Type == JTokenType.String ? m.Value<string>("id") : null;
                    string task = m["task"]?.Type == JTokenType.String ? m.Value<string>("task") : null;
                    asrTask = task != null && task.IndexOf("speech-recognition", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                else
                {
                    id = item.Type == JTokenType.String ? (string)item : null;
                }
                if (string.IsNullOrEmpty(id) || !(asrTask || LooksLikeAsrModel(id))) continue;

                // .en 모델과 distil-whisper 공식 체크포인트는 영어 전용
                if (id.EndsWith(".en", StringComparison.OrdinalIgnoreCase) ||
                    id.IndexOf("distil", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    englishOnly = englishOnly ?? id;
                    continue;
                }
                modelId = id;
                return true;
            }
            modelId = englishOnly;
            return modelId != null;
        }

        private static bool LooksLikeAsrModel(string id)
        {
            foreach (var w in AsrModelWords)
                if (id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ===== 요청 본문 =====

        // 16bit PCM mono WAV (44바이트 헤더). 대부분의 STT 서버가 받는 가장 무난한 형식.
        public static byte[] EncodeWav(float[] samples, int sampleRate)
        {
            int dataBytes = samples.Length * 2;
            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                  // fmt chunk size
                w.Write((short)1);            // PCM
                w.Write((short)1);            // mono
                w.Write(sampleRate);
                w.Write(sampleRate * 2);      // byte rate
                w.Write((short)2);            // block align
                w.Write((short)16);           // bits per sample
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                {
                    float s = samples[i];
                    if (s > 1f) s = 1f;
                    else if (s < -1f) s = -1f;
                    w.Write((short)(s * 32767f));
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static string NewBoundary() => "----ArttiStt" + Guid.NewGuid().ToString("N");

        public static string MultipartContentType(string boundary) => "multipart/form-data; boundary=" + boundary;

        public static byte[] BuildMultipart(string boundary, SttEndpoint endpoint, byte[] wav, string fileName = "speech.wav")
        {
            using (var ms = new MemoryStream(wav.Length + 512))
            {
                foreach (var field in endpoint.FormFields)
                {
                    WriteAscii(ms, "--" + boundary + "\r\n");
                    WriteUtf8(ms, $"Content-Disposition: form-data; name=\"{field.Key}\"\r\n\r\n");
                    WriteUtf8(ms, field.Value ?? string.Empty);
                    WriteAscii(ms, "\r\n");
                }

                WriteAscii(ms, "--" + boundary + "\r\n");
                WriteUtf8(ms, $"Content-Disposition: form-data; name=\"{endpoint.FileField}\"; filename=\"{fileName}\"\r\n");
                WriteAscii(ms, "Content-Type: audio/wav\r\n\r\n");
                ms.Write(wav, 0, wav.Length);
                WriteAscii(ms, "\r\n--" + boundary + "--\r\n");
                return ms.ToArray();
            }
        }

        private static void WriteAscii(Stream s, string text)
        {
            var b = Encoding.ASCII.GetBytes(text);
            s.Write(b, 0, b.Length);
        }

        private static void WriteUtf8(Stream s, string text)
        {
            var b = Encoding.UTF8.GetBytes(text);
            s.Write(b, 0, b.Length);
        }

        // ===== 응답 =====

        // 인식 문장을 담는 흔한 키. 앞쪽 우선. 값이 객체/배열이면 한 단계 더 들어간다.
        private static readonly string[] TextKeys =
        {
            // 변경점: "recognized" — 명료도 서버(/analyze)의 인식 문장 키 ({"recognized":..., "clarity_percent":...})
            "text", "DisplayText", "transcript", "transcription", "recognized_text", "recognizedText", "recognized", "sentence",
            "utterance", "result", "results", "alternatives", "utterances", "data", "output", "prediction", "stt"
        };

        // 이 키 아래의 문자열이 상태값이면 문장이 아니다 ({"result":"success","data":{"text":...}})
        private static readonly string[] StatusKeys = { "result", "data", "output", "status" };
        private static readonly HashSet<string> StatusWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "success", "succeeded", "ok", "completed", "done", "true", "false", "fail", "failed", "failure", "error"
        };

        // 서버가 정상 응답했는지(true)와 문장(text, 빈 문자열이면 인식된 말 없음)을 돌려준다.
        // {"detail": ...} 같은 오류 JSON, 음성·HTML·바이너리 응답처럼 문장이 아니면 false.
        public static bool TryParseTranscript(string body, out string text, out float confidence, string contentType = null)
        {
            text = null;
            confidence = 0f;
            if (body == null) return false;

            if (!string.IsNullOrEmpty(contentType))
            {
                string ct = contentType.ToLowerInvariant();
                if (ct.StartsWith("audio/") || ct.StartsWith("video/") || ct.StartsWith("image/") ||
                    ct.StartsWith("application/octet-stream")) return false;
            }

            string trimmed = body.Trim().TrimStart('﻿').Trim();
            if (trimmed.Length == 0)
            {
                text = string.Empty;
                return true;
            }

            char c = trimmed[0];
            if (c == '{' || c == '[' || c == '"' || trimmed == "null")
            {
                JToken token;
                try
                {
                    token = JToken.Parse(trimmed);
                }
                catch (JsonException)
                {
                    return false; // 잘린 JSON을 문장으로 쓰지 않는다
                }

                if (token.Type == JTokenType.Null)
                {
                    text = string.Empty;
                    return true;
                }
                if (!TryExtract(token, 0, out text)) return false;
                confidence = ReadConfidence(token);
                text = text.Trim();
                return true;
            }

            if (LooksLikeMarkupOrBinary(trimmed)) return false;

            // 평문 응답 (PlainTextResponse, Flask/Django 문자열 응답, whisper-asr-webservice output=txt 등)
            text = trimmed;
            return true;
        }

        private static bool LooksLikeMarkupOrBinary(string s)
        {
            if (s[0] == '<') return true;
            if (s.StartsWith("RIFF") || s.StartsWith("ID3") || s.StartsWith("OggS") || s.StartsWith("fLaC")) return true;
            foreach (char ch in s)
                if (ch == '\0' || ch == '�' || (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')) return true;
            return false;
        }

        private static bool TryExtract(JToken token, int depth, out string text)
        {
            text = null;
            if (token == null || depth > 5) return false;

            switch (token.Type)
            {
                case JTokenType.String:
                    text = (string)token;
                    return true;

                case JTokenType.Null:
                    return false;

                case JTokenType.Object:
                {
                    var obj = (JObject)token;
                    foreach (var key in TextKeys)
                    {
                        var v = obj.GetValue(key, StringComparison.OrdinalIgnoreCase);
                        if (v == null) continue;

                        // {"text": null}, {"segments": []}, {"results": []} — 서버가 "인식된 말 없음"으로 답한 것.
                        // data/result/output 같은 범용 키의 빈 배열은 오류 응답({"code":400,"data":[]})일 수 있어 제외한다.
                        if ((v.Type == JTokenType.Null && string.Equals(key, "text", StringComparison.OrdinalIgnoreCase)) ||
                            (v.Type == JTokenType.Array && !v.HasValues && IsSpeechKey(key)))
                        {
                            text = string.Empty;
                            return true;
                        }
                        if (v.Type == JTokenType.String && IsStatusKey(key) && StatusWords.Contains(((string)v).Trim()))
                            continue;
                        if (TryExtract(v, depth + 1, out text)) return true;
                    }

                    // 문장 조각 배열: Whisper segments [{"text"}], RTZR utterances [{"msg"}]
                    var segments = obj.GetValue("segments", StringComparison.OrdinalIgnoreCase);
                    if (segments != null && segments.Type == JTokenType.Array && !segments.HasValues)
                    {
                        text = string.Empty;
                        return true;
                    }
                    if (segments != null && TryExtract(segments, depth + 1, out text)) return true;
                    var msg = obj.GetValue("msg", StringComparison.OrdinalIgnoreCase);
                    return depth > 0 && msg != null && msg.Type == JTokenType.String && TryExtract(msg, depth + 1, out text);
                }

                case JTokenType.Array:
                {
                    var parts = new List<string>();
                    bool any = false;
                    foreach (var item in (JArray)token)
                    {
                        if (!TryExtract(item, depth + 1, out var part)) continue;
                        any = true;
                        if (!string.IsNullOrWhiteSpace(part)) parts.Add(part.Trim());
                    }
                    if (!any) return false;
                    text = string.Join(" ", parts);
                    return true;
                }

                default:
                    return false;
            }
        }

        private static readonly string[] SpeechKeys =
        {
            "text", "transcript", "transcription", "results", "alternatives", "utterances", "segments"
        };

        private static bool IsSpeechKey(string key)
        {
            foreach (var k in SpeechKeys)
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsStatusKey(string key)
        {
            foreach (var k in StatusKeys)
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // 변경점: 서버가 목표 문장 대비 명료도(발화 정확도)를 주면 읽는다. 일반 STT 서버 응답이면 false.
        //   percent: clarity_percent(0..100) 우선, 없으면 clarity(0..1이면 ×100, 그보다 크면 이미 %)
        //   light: 신호등 판정(green/yellow/red), target: 서버가 비교에 쓴 목표 문장(에코)
        //   최상위에 없으면 result/data 객체 한 단계 안에서 찾는다({"result": {...}}).
        public static bool TryParseClarity(string body, out int percent, out string light, out string target)
        {
            percent = -1;
            light = null;
            target = null;
            if (string.IsNullOrWhiteSpace(body)) return false;
            string trimmed = body.Trim().TrimStart('﻿').Trim();
            if (trimmed.Length == 0 || trimmed[0] != '{') return false;

            JObject obj;
            try
            {
                obj = JObject.Parse(trimmed);
            }
            catch (JsonException)
            {
                return false;
            }

            if (!TryReadClarity(obj, out percent, out light, out target))
            {
                foreach (var key in new[] { "result", "data" })
                    if (obj.GetValue(key, StringComparison.OrdinalIgnoreCase) is JObject inner &&
                        TryReadClarity(inner, out percent, out light, out target))
                        return true;
                return false;
            }
            return true;
        }

        // 변경점: 명료도를 계산한 모델이 들은 문장(두 모델 서버의 clarity_recognized). 대화용 인식(recognized)과 다른 모델의 결과다.
        //   없으면 false(명료도와 인식 문장이 같은 모델인 서버). 빈 문자열은 '그 모델이 아무것도 못 들음'이라 그대로 돌려준다.
        public static bool TryParseClarityText(string body, out string text)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(body)) return false;
            string trimmed = body.Trim().TrimStart('\uFEFF').Trim();
            if (trimmed.Length == 0 || trimmed[0] != '{') return false;

            JObject obj;
            try
            {
                obj = JObject.Parse(trimmed);
            }
            catch (JsonException)
            {
                return false;
            }

            var token = obj.GetValue("clarity_recognized", StringComparison.OrdinalIgnoreCase);
            if (token == null || token.Type != JTokenType.String) return false;
            text = ((string)token).Trim();
            return true;
        }

        private static bool TryReadClarity(JObject obj, out int percent, out string light, out string target)
        {
            percent = -1;
            light = null;
            target = null;

            var pct = obj.GetValue("clarity_percent", StringComparison.OrdinalIgnoreCase) ??
                      obj.GetValue("clarityPercent", StringComparison.OrdinalIgnoreCase);
            var clarity = obj.GetValue("clarity", StringComparison.OrdinalIgnoreCase);
            if (IsNumber(pct))
            {
                percent = (int)Math.Round((double)pct);
            }
            else if (IsNumber(clarity))
            {
                double c = (double)clarity;
                percent = (int)Math.Round(c <= 1.0 ? c * 100.0 : c);
            }
            else
            {
                return false;
            }
            percent = Math.Max(0, Math.Min(100, percent));

            var l = obj.GetValue("light", StringComparison.OrdinalIgnoreCase);
            if (l != null && l.Type == JTokenType.String)
            {
                var s = ((string)l).Trim().ToLowerInvariant();
                if (s.Length > 0) light = s;
            }
            var t = obj.GetValue("target", StringComparison.OrdinalIgnoreCase);
            if (t != null && t.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)t)) target = ((string)t).Trim();
            return true;
        }

        private static bool IsNumber(JToken v) => v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer);

        private static float ReadConfidence(JToken token)
        {
            if (!(token is JObject obj)) return 0f;
            foreach (var key in new[] { "confidence", "score" })
            {
                var v = obj.GetValue(key, StringComparison.OrdinalIgnoreCase);
                if (v != null && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer))
                    return (float)v;
            }
            return 0f;
        }

        // 무음·잡음 구간에서 Whisper 계열이 지어내는 대표 문장(방송 자막 학습 흔적).
        // 편의점 대화에서 사용자가 할 리 없는 말만 둔다. 비교는 글자·숫자만 남기고 영문은 대문자로 맞춘 뒤 한다.
        private static readonly HashSet<string> HallucinationPhrases = new HashSet<string>(StringComparer.Ordinal)
        {
            "시청해주셔서감사합니다", "시청해주셔서고맙습니다", "구독과좋아요부탁드립니다", "구독과좋아요",
            "구독좋아요알림설정부탁드립니다", "다음영상에서만나요", "MBC뉴스이덕영입니다"
        };

        private static readonly string[] HallucinationPrefixes = { "자막제공", "한글자막", "자막편집" };

        public static bool IsLikelyHallucination(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            string s = sb.ToString();

            if (HallucinationPhrases.Contains(s)) return true;
            if (s.StartsWith("MBC뉴스", StringComparison.Ordinal) && s.EndsWith("입니다", StringComparison.Ordinal)) return true;
            foreach (var prefix in HallucinationPrefixes)
                if (s.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
