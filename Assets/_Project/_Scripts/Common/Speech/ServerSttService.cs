using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Artti.Common.Speech
{
    // 같은 네트워크의 서버PC에서 돌아가는 STT 서버(Whisper 등)로 인식한다.
    // 흐름: 마이크로 한 발화 녹음(MicrophoneUtteranceRecorder) → WAV → multipart POST → 응답 문장.
    // 서버 형식은 SttServerProtocol이 API 문서 또는 흔한 경로 × 필드 후보로 맞춘다.
    // 상태:
    //   Online  — 서버가 HTTP로 응답함
    //   Offline — 연결 실패·서버 오류(5xx). RetryOfflineAfterSeconds 뒤 다시 확인한다 (그동안 기기 음성 인식)
    //   disabled — 설정 문제(HTTP 차단, 인증 필요, 맞는 경로 없음, WebSocket 전용). 다시 해도 같으므로 이번 실행 동안 끈다
    public sealed class ServerSttService : ISttService
    {
        public enum ServerState { Unknown, Online, Offline }

        private const int ProbeTimeoutSeconds = 3;
        private const int RequestTimeoutSeconds = 30;
        private const float RetryOfflineAfterSeconds = 30f;
        // 같은 엔드포인트에서 400류가 연달아 이만큼 나면 엔드포인트를 다시 찾는다.
        // 잡음만 녹음됐을 때 400("말이 없음")으로 답하는 서버가 있어 한두 번으로는 판단하지 않는다.
        private const int BadRequestLimit = 3;
        // 성공 없이 서버 오류로 쉬기를 이만큼 반복하면 서버 쪽 고장으로 보고 끈다 (쉴 때마다 발화 하나를 잃기 때문).
        // 게이트웨이 오류(502/503/504)는 모델을 불러오는 중일 수 있어 더 기다린다.
        private const int ServerErrorLimit = 3;
        private const int GatewayErrorLimit = 6;
        // 경로는 있는데(404가 아닌 응답) 형식을 못 맞춘 탐색을 이만큼 반복하면 끈다
        private const int FailedSweepLimit = 3;
        // 이보다 빨리 끊긴 연결은 네트워크가 아니라 서버가 요청을 거절한 것(잘못된 필드에 연결을 닫는 서버 등)으로 본다
        private const float FastFailSeconds = 3f;

        private readonly string _baseUrl;
        private readonly string _language;
        private readonly string _apiKey;
        private readonly string _configuredModel;
        private readonly string _manualPath;
        private readonly string _manualQuery;
        private readonly bool _manualFromUrl; // 경로를 주소 칸에서 가져왔다 — 문서에 없으면 강제하지 않는다
        private readonly MicrophoneUtteranceRecorder _recorder = new MicrophoneUtteranceRecorder();

        private ServerState _state = ServerState.Unknown;
        private float _offlineSince;
        private bool _disabled;
        private bool _probing;
        private bool _micFailed;
        private SttEndpoint _endpoint;
        private int _endpointFailures;  // 성공 없이 엔드포인트를 잃은 횟수 — 다시 찾은 것도 실패하면 끈다
        private int _failedSweeps;
        private bool _endpointProducedText; // 지금 엔드포인트가 한 번이라도 문장을 돌려줬는가
        private int _emptyStreak;
        private const int EmptyLimit = 3;   // 문장을 한 번도 준 적 없는 엔드포인트가 연달아 빈 결과만 주면 다시 찾는다
        private int _badRequestStreak;
        private int _serverErrorStreak;
        private int _gatewayErrorStreak;
        private readonly HashSet<string> _rejectedEndpoints = new HashSet<string>(); // "경로|필드" — 다시 고르지 않는다
        private string _modelId;
        private bool _modelListUnavailable; // /v1/models에서 음성 인식 모델을 못 찾음 — 보낼 때마다 다시 묻지 않는다
        private bool _missingModelLogged;
        private CancellationTokenSource _activeCts;

        public ServerState State => _state;
        public string BaseUrl => _baseUrl;

        // Unknown(아직 확인 전)도 true — 첫 호출에서 확인한다.
        public bool IsAvailable => !_disabled && _state != ServerState.Offline;

        // 이 기기에서 Unity 마이크로 녹음할 수 있는가 (에디터에 마이크가 없거나 입력이 막혀 있으면 false)
        public bool CanRecord => !_micFailed && MicrophoneUtteranceRecorder.HasDevice;

        // model: OpenAI 호환 서버의 모델 id를 직접 지정할 때 (비우면 서버의 /v1/models에서 고른다)
        public ServerSttService(string baseUrl, string endpointPath = null, string apiKey = null, string model = null,
            string language = "ko")
        {
            _language = language;
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _configuredModel = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
            _modelId = _configuredModel;
            if (!string.IsNullOrWhiteSpace(endpointPath))
                SttServerProtocol.ParseManualPath(endpointPath, out _manualPath, out _manualQuery);

            if (!TryNormalizeBaseUrl(baseUrl, out _baseUrl, out string pathInUrl))
            {
                _baseUrl = (baseUrl ?? string.Empty).Trim();
                Disable("서버 주소 형식이 잘못됨 — 예: http://192.168.0.167:8000");
                return;
            }

            // 주소 칸에 인식 경로까지 넣은 경우(http://ip:8000/stt): 경로 칸이 비어 있으면 그 경로를 쓴다.
            // 브라우저에서 복사한 문서 화면 주소(/docs, /openapi.json 등)는 인식 경로가 아니므로 무시한다.
            if (pathInUrl != null)
            {
                SttServerProtocol.ParseManualPath(pathInUrl, out string urlPath, out string urlQuery);
                if (urlPath != null && SttServerProtocol.IsDocumentationPath(urlPath))
                {
                    Debug.Log($"[ServerSTT] 주소의 {urlPath}는 문서 화면 경로라 무시하고 자동 탐색");
                }
                else if (_manualPath == null && _manualQuery == null)
                {
                    _manualPath = urlPath;
                    _manualQuery = urlQuery;
                    _manualFromUrl = urlPath != null;
                }
                else
                {
                    Debug.LogWarning($"[ServerSTT] 주소 칸의 경로({pathInUrl})는 무시 — Stt Server Path 값을 사용");
                }
            }

            Debug.Log($"[ServerSTT] 서버 {_baseUrl}" + (_manualPath != null ? $", 경로 {_manualPath}" : ", 경로 자동 탐색"));
        }

        // "192.168.0.167:8000", "http://192.168.0.167:8000/", ".env 값 # 주석" 모두 받는다. 형식이 틀리면 false.
        private static bool TryNormalizeBaseUrl(string url, out string baseUrl, out string pathInUrl)
        {
            baseUrl = null;
            pathInUrl = null;
            string u = (url ?? string.Empty).Trim();
            int space = u.IndexOfAny(new[] { ' ', '\t' }); // 값 뒤에 붙은 주석 등
            if (space >= 0) u = u.Substring(0, space);
            u = u.Trim('"', '\'');
            if (u.Length == 0) return false;
            if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // ftp:// 등 다른 방식, "http:/..." "http//..." "https//..." 같은 오타
                if (u.Contains("://") || u.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                    u.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
                    u.StartsWith("http/", StringComparison.OrdinalIgnoreCase) ||
                    u.StartsWith("https/", StringComparison.OrdinalIgnoreCase))
                    return false;
                u = "http://" + u;
            }

            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host))
                return false;

            baseUrl = uri.GetLeftPart(UriPartial.Authority);
            if (uri.PathAndQuery.Length > 1) pathInUrl = uri.PathAndQuery;
            return true;
        }

        // 서버 연결 확인 + 엔드포인트 결정. 씬 시작 때 미리 불러 두면 첫 발화가 지연되지 않는다.
        public async UniTask<bool> EnsureReadyAsync(CancellationToken ct)
        {
            if (_disabled) return false;
            if (_state == ServerState.Online) return true;
            if (_state == ServerState.Offline && !RetryDue) return false;

            if (_probing)
            {
                // 다른 호출(씬 시작 시 워밍업 등)이 확인 중이면 그 결과를 기다린다
                await UniTask.WaitUntil(() => !_probing, cancellationToken: ct);
                return IsAvailable && _state == ServerState.Online;
            }

            _probing = true;
            try
            {
                await ProbeAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // 예상 못 한 오류로 대화가 멈추지 않게 서버 STT만 끈다
                Disable("연결 확인 중 오류: " + e.Message);
            }
            finally
            {
                _probing = false;
            }
            return IsAvailable && _state == ServerState.Online;
        }

        // 서버가 끊긴 상태에서 재확인 시간이 됐으면 뒤에서 확인만 걸어 둔다 (이번 발화는 기다리지 않는다).
        public void RefreshInBackground(CancellationToken ct)
        {
            if (_disabled || _probing || _state != ServerState.Offline || !RetryDue) return;
            EnsureReadyAsync(ct).Forget();
        }

        private bool RetryDue => Time.realtimeSinceStartup - _offlineSince >= RetryOfflineAfterSeconds;

        // 서버에 닿는지 확인하고, API 문서가 있으면 엔드포인트를 정한다. HTTP 응답이 하나라도 오면(404 포함) 서버는 살아 있다.
        private async UniTask ProbeAsync(CancellationToken ct)
        {
            bool reachable = false;
            foreach (var schemaPath in SttServerProtocol.SchemaPaths)
            {
                using (var req = UnityWebRequest.Get(_baseUrl + schemaPath))
                {
                    req.timeout = ProbeTimeoutSeconds;
                    ApplyAuth(req);
                    var outcome = await SendAsync(req, ct);

                    if (outcome == SendOutcome.Blocked)
                    {
                        Disable("http:// 요청이 차단됨 — Player Settings > Other Settings > 'Allow downloads over HTTP'를 Always allowed로");
                        return;
                    }
                    if (outcome == SendOutcome.TransportError)
                    {
                        if (!reachable) MarkOffline($"연결 실패 ({req.error})");
                        break;
                    }

                    reachable = true;
                    if (IsWebSocketOnly(req))
                    {
                        Disable("WebSocket 전용 STT 서버로 보임 — HTTP 파일 업로드를 받는 모드가 필요합니다 (예: WhisperLive는 --enable_rest)");
                        return;
                    }

                    long code = req.responseCode;
                    if (code == 502 || code == 503 || code == 504)
                    {
                        // 프록시 뒤 서버가 죽었거나 모델을 불러오는 중 — 잠시 뒤 다시 확인한다
                        MarkOffline($"서버가 응답하지 않음 HTTP {code}");
                        return;
                    }
                    // 이 위치엔 문서가 없다 — 다음 위치. POST만 받는 서버(GET에 501)나 문서 경로에서 500을 내는 서버도 여기로 온다.
                    if (code == 404 || code == 405 || code >= 500) continue;
                    if (outcome == SendOutcome.Success)
                    {
                        string doc = req.downloadHandler.text;
                        if (!SttServerProtocol.IsApiSchema(doc)) continue; // 200이지만 API 문서가 아님(SPA 등) — 다음 위치

                        bool ok = SttServerProtocol.TryDiscover(doc, _language, out var found, out var note, _manualPath, _rejectedEndpoints);
                        if (!ok && _manualFromUrl)
                        {
                            // 주소 칸에서 온 경로가 문서에 없으면 문서가 가리키는 인식 엔드포인트를 쓴다
                            ok = SttServerProtocol.TryDiscover(doc, _language, out found, out note, null, _rejectedEndpoints);
                        }
                        if (ok)
                        {
                            found.Query = SttServerProtocol.MergeQuery(found.Query, _manualQuery);
                            // 이미 쓰고 있는(확인된) 엔드포인트는 바꾸지 않고, 한 번 거부된 엔드포인트는 다시 고르지 않는다
                            if (_endpoint == null && !IsRejected(found)) LockEndpoint(found, producedText: false);
                        }
                        Debug.Log($"[ServerSTT] {schemaPath}: {note}");
                    }
                    break;
                }
            }

            if (!reachable) return;

            _state = ServerState.Online;
            if (_endpoint == null)
                Debug.Log("[ServerSTT] API 문서로 엔드포인트를 정하지 못함 — 첫 인식 때 " +
                          (_manualPath != null ? $"{_manualPath}에 필드 이름을 바꿔 가며" : "흔한 경로를 차례로") + " 시도");
            Debug.Log($"[ServerSTT] 연결됨 {_baseUrl}" + (_endpoint != null ? " → " + _endpoint.Describe() : ""));
        }

        private static bool IsWebSocketOnly(UnityWebRequest req)
        {
            if (req.responseCode == 426) return true;
            string upgrade = req.GetResponseHeader("Upgrade");
            return !string.IsNullOrEmpty(upgrade) && upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default)
        {
            // 겹친 호출은 이전 녹음을 끊는다. 이전 호출의 CTS는 그쪽 finally가 정리한다.
            _activeCts?.Cancel();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _activeCts = cts;
            var token = cts.Token;

            try
            {
                if (!await EnsureReadyAsync(token)) return Empty;

                if (!await CloudSttService.RequestMicPermissionAsync(token))
                {
                    Debug.LogWarning("[ServerSTT] 마이크 권한 거부");
                    return Empty;
                }

                var (status, samples) = await _recorder.RecordAsync(token);
                if (status == RecordStatus.MicUnavailable)
                {
                    // 이 기기에서는 녹음이 안 된다 — 다음 발화부터 기기 음성 인식을 쓰게 한다
                    _micFailed = true;
                    Debug.LogWarning("[ServerSTT] 마이크로 녹음할 수 없음 — 이후 기기 음성 인식 사용");
                    return Empty;
                }
                if (status == RecordStatus.NoSpeech)
                {
                    Debug.Log("[ServerSTT] 발화 감지 안 됨 — 서버 호출 생략");
                    return Empty;
                }

                var wav = SttServerProtocol.EncodeWav(samples, _recorder.SampleRate);
                return await TranscribeAsync(wav, samples.Length / (float)_recorder.SampleRate, token);
            }
            catch (OperationCanceledException)
            {
                return Empty;
            }
            catch (Exception e)
            {
                Disable("인식 중 오류: " + e.Message);
                return Empty;
            }
            finally
            {
                if (_activeCts == cts) _activeCts = null;
                cts.Dispose();
            }
        }

        public void Cancel()
        {
            try { _activeCts?.Cancel(); } catch (ObjectDisposedException) { }
        }

        private async UniTask<SttResult> TranscribeAsync(byte[] wav, float seconds, CancellationToken ct)
        {
            if (_endpoint == null) return await ProbeCandidatesAsync(wav, seconds, ct);

            var r = await PostAsync(_endpoint, wav, seconds, ct);
            switch (r.Outcome)
            {
                case SendOutcome.Success:
                    if (!r.Result.isEmpty)
                    {
                        _endpointProducedText = true;
                        _emptyStreak = 0;
                        Succeeded();
                    }
                    else if (!_endpointProducedText && ++_emptyStreak >= EmptyLimit)
                    {
                        // 문장을 한 번도 준 적 없는 엔드포인트가 빈 결과만 준다 — 오류를 200으로 감싸 주는 엉뚱한 곳일 수 있다
                        EndpointFailed($"빈 결과 {_emptyStreak}회 연속", reject: false);
                    }
                    break;
                case SendOutcome.TransportError:
                    // 서버 확인은 됐는데 인식 요청만 실패 — 반복되면 서버 쪽 문제(요청마다 죽는 서버, 늘 시간 초과)다
                    ServerFailed(r.TimedOutAfterUpload ? "서버 응답이 너무 늦음(시간 초과)" : "인식 요청 중 연결 끊김", 0);
                    break;
                case SendOutcome.Blocked:
                    Disable("http:// 요청이 차단됨");
                    break;
                case SendOutcome.ServerError:
                    ServerFailed($"서버 오류 HTTP {r.Code}", r.Code);
                    break;
                case SendOutcome.AuthRequired:
                    Disable(AuthHint(r.Code));
                    break;
                case SendOutcome.NotFound:
                case SendOutcome.Redirect:
                    EndpointFailed($"HTTP {r.Code}", reject: true);
                    break;
                case SendOutcome.BadRequest:
                    if (++_badRequestStreak >= BadRequestLimit)
                        EndpointFailed($"HTTP {r.Code} {_badRequestStreak}회 연속", reject: false);
                    break;
            }
            return r.Result;
        }

        private void LockEndpoint(SttEndpoint endpoint, bool producedText)
        {
            _endpoint = endpoint;
            _endpointProducedText = producedText;
            _emptyStreak = 0;
            _badRequestStreak = 0;
        }

        private void Succeeded()
        {
            _badRequestStreak = 0;
            _serverErrorStreak = 0;
            _gatewayErrorStreak = 0;
            _endpointFailures = 0;
            _failedSweeps = 0;
        }

        // 확정된 엔드포인트가 계속 거부할 때: 다음 발화 전에 API 문서부터 다시 읽어 엔드포인트를 다시 찾는다.
        // 그 사이 한 번도 성공하지 못한 채 또 잃으면 끈다.
        // reject: 경로가 없다는 뚜렷한 답(404/405/리다이렉트)일 때만 이 엔드포인트를 다시 고르지 않는다.
        private void EndpointFailed(string reason, bool reject)
        {
            _badRequestStreak = 0;
            if (reject) _rejectedEndpoints.Add(_endpoint.Key);
            _modelId = _configuredModel; // 서버 목록에서 고른 모델이 원인일 수 있어 다시 고른다
            _modelListUnavailable = false;

            if (++_endpointFailures >= 2)
            {
                Disable($"{_endpoint.Describe()} 거부됨({reason}) — TrainingSceneRoot의 Stt Server Path를 확인하세요");
                return;
            }
            Debug.LogWarning($"[ServerSTT] {_endpoint.Describe()} 거부됨({reason}) — 다음 발화에서 엔드포인트를 다시 찾음");
            _endpoint = null;
            _state = ServerState.Unknown; // 다음 발화 전에 API 문서를 다시 읽는다
        }

        // 서버 오류로 쉬기. 성공 없이 반복되면 서버 쪽 고장(모델 로드 실패 등)으로 보고 끈다.
        private void ServerFailed(string reason, long code)
        {
            bool gateway = code == 502 || code == 503 || code == 504;
            int streak = gateway ? ++_gatewayErrorStreak : ++_serverErrorStreak;
            int limit = gateway ? GatewayErrorLimit : ServerErrorLimit;
            if (streak >= limit)
            {
                Disable($"{reason} — {limit}번 연속 실패, 서버PC의 STT 서버 로그를 확인하세요");
                return;
            }
            MarkOffline(reason);
        }

        private bool IsRejected(SttEndpoint e) => _rejectedEndpoints.Contains(e.Key);

        // Django(DEBUG)가 끝에 '/' 없는 POST에 내는 오류 문구. 설정 목록에 늘 나오는 "APPEND_SLASH"만으로는 판단하지 않는다.
        private static bool IsDjangoSlashError(string body) =>
            body != null && body.IndexOf("end in a slash", StringComparison.OrdinalIgnoreCase) >= 0;

        // 엔드포인트 미확정: 경로마다 파일 필드 이름을 바꿔 가며 시도한다.
        //   404/405 → 이 경로는 없음, 다음 경로
        //   400/415/422/5xx → 경로는 있으나 형식이 다를 수 있음, 같은 경로에 다음 필드 이름
        //   Django(APPEND_SLASH) 오류면 끝에 '/'를 붙여 다시
        private async UniTask<SttResult> ProbeCandidatesAsync(byte[] wav, float seconds, CancellationToken ct)
        {
            string[] paths;
            if (_manualPath == null) paths = SttServerProtocol.CandidatePaths;
            else if (!_manualFromUrl) paths = new[] { _manualPath };
            else
            {
                // 주소 칸에서 온 경로는 확정이 아니므로 먼저 시도하고, 안 되면 흔한 경로도 본다
                var list = new List<string> { _manualPath };
                foreach (var p in SttServerProtocol.CandidatePaths)
                    if (!string.Equals(p.TrimEnd('/'), _manualPath.TrimEnd('/'), StringComparison.Ordinal)) list.Add(p);
                paths = list.ToArray();
            }
            string source = _manualPath != null && !_manualFromUrl ? "manual" : "candidate";
            bool sawServerError = false;
            bool onlyGatewayErrors = true; // 본 5xx가 전부 502/503/504였나 (모델 로딩 중인 서버는 더 기다린다)
            long lastServerErrorCode = 0;
            bool pathExists = false;
            int pathsFailingWith5xx = 0;

            foreach (var basePath in paths)
            {
                string path = basePath;
                bool slashTried = path.EndsWith("/");
                bool onSlashRetry = false;
                bool redirectFollowed = false;
                bool firstRequestOnPath = true;
                foreach (var field in SttServerProtocol.FileFieldsFor(basePath))
                {
                    var last = SendOutcome.Success;
                    while (true)
                    {
                        var candidate = SttServerProtocol.CandidateFor(path, field, _language, _manualQuery, source);
                        var r = await PostAsync(candidate, wav, seconds, ct);
                        last = r.Outcome;
                        switch (r.Outcome)
                        {
                            case SendOutcome.Success:
                                if (r.Result.isEmpty)
                                {
                                    // 200인데 빈 결과 — 기침·잡음이었을 수도, 오류를 200으로 감싸 주는 엉뚱한 곳일 수도 있다.
                                    // 확정하지 않고 다음 발화에서 다시 찾는다 (반복되면 아래 FailedSweepLimit에 걸린다).
                                    pathExists = true;
                                    if (++_failedSweeps >= FailedSweepLimit)
                                    {
                                        Disable("인식 결과가 계속 비어 있어 인식 경로를 확정하지 못함 — Stt Server Path를 확인하세요");
                                        return Empty;
                                    }
                                    Debug.LogWarning($"[ServerSTT] {candidate.Describe()} 빈 결과 — 확정하지 않고 다음 발화에서 다시 찾음");
                                    return Empty;
                                }
                                LockEndpoint(candidate, producedText: true);
                                Succeeded();
                                Debug.Log($"[ServerSTT] 엔드포인트 확정 → {candidate.Describe()}");
                                return r.Result;
                            case SendOutcome.TransportError:
                                if (r.ElapsedSeconds >= FastFailSeconds)
                                {
                                    ServerFailed("인식 요청 중 연결 끊김", 0);
                                    return Empty;
                                }
                                // 곧바로 끊겼다 — 서버가 이 형식을 거절한 것으로 보고 다음 후보로
                                sawServerError = true;
                                onlyGatewayErrors = false;
                                break;
                            case SendOutcome.Blocked:
                                Disable("http:// 요청이 차단됨");
                                return Empty;
                            case SendOutcome.AuthRequired:
                                Disable(AuthHint(r.Code));
                                return Empty;
                        }
                        if (r.Outcome != SendOutcome.NotFound && r.Outcome != SendOutcome.Redirect &&
                            r.Outcome != SendOutcome.TransportError)
                            pathExists = true;

                        // '/'를 붙여 봤는데 없는 경로면 원래 경로로 돌아가 다음 필드 이름을 시도한다
                        if (onSlashRetry && r.Outcome == SendOutcome.NotFound)
                        {
                            onSlashRetry = false;
                            path = basePath;
                            last = SendOutcome.BadRequest;
                            break;
                        }

                        // Django: 끝에 '/'가 없는 POST는 DEBUG면 500("doesn't end in a slash"), 아니면 '/' 붙은 주소로 리다이렉트
                        bool wantsSlash = r.Outcome == SendOutcome.Redirect ? r.RedirectsToSlash : IsDjangoSlashError(r.Body);
                        if (!slashTried && wantsSlash)
                        {
                            slashTried = true;
                            onSlashRetry = true;
                            path += "/";
                            continue;
                        }

                        // 307/308은 POST를 유지한 채 다른 경로로 옮겨 가라는 뜻 — 한 번 따라간다
                        if (r.Outcome == SendOutcome.Redirect && r.RedirectPath != null && !redirectFollowed)
                        {
                            redirectFollowed = true;
                            path = r.RedirectPath;
                            continue;
                        }

                        if (r.Outcome == SendOutcome.ServerError)
                        {
                            sawServerError = true;
                            lastServerErrorCode = r.Code;
                            if (r.Code != 502 && r.Code != 503 && r.Code != 504) onlyGatewayErrors = false;
                            // 서로 다른 경로의 첫 요청이 둘 다 5xx면 경로 문제가 아니라 서버(또는 프록시)가 고장난 것
                            if (firstRequestOnPath && ++pathsFailingWith5xx >= 2)
                            {
                                ServerFailed($"서버 오류 HTTP {r.Code}", r.Code);
                                return Empty;
                            }
                        }
                        break;
                    }
                    firstRequestOnPath = false;

                    // 경로 자체가 없으면(다른 곳으로 리다이렉트 포함) 필드 이름을 바꿔 봐야 소용없다
                    if (last == SendOutcome.NotFound || last == SendOutcome.Redirect) break;
                }
            }

            if (sawServerError)
            {
                // 5xx나 즉시 끊김이 섞여 있었다면 서버 쪽 문제일 수 있다 — 바로 끄지 않고 잠시 뒤 다시 찾는다
                ServerFailed("인식 경로를 찾는 중 서버 오류", onlyGatewayErrors ? lastServerErrorCode : 0);
                return Empty;
            }

            if (pathExists && ++_failedSweeps < FailedSweepLimit)
            {
                // 경로는 있는데 이번 녹음으로는 확정하지 못했다(기침·잡음이라 "말 없음"으로 답했을 수 있다) — 다음 발화에서 다시
                Debug.LogWarning($"[ServerSTT] 인식 경로를 확정하지 못함 ({_failedSweeps}/{FailedSweepLimit}) — 다음 발화에서 다시 찾음");
                return Empty;
            }

            Disable(_manualPath != null && !_manualFromUrl
                ? $"{_manualPath}가 음성 파일을 받지 않음 — Stt Server Path를 확인하세요"
                : "알려진 STT 경로가 모두 맞지 않음 — TrainingSceneRoot의 Stt Server Path에 인식 경로를 직접 지정하세요");
            return Empty;
        }

        private struct PostResult
        {
            public SendOutcome Outcome;
            public long Code;
            public string Body;
            public bool TimedOutAfterUpload;
            public bool RedirectsToSlash;
            public string RedirectPath; // 307/308의 이동 경로 (같은 서버일 때만)
            public double ElapsedSeconds;
            public SttResult Result;
        }

        private async UniTask<PostResult> PostAsync(SttEndpoint endpoint, byte[] wav, float seconds, CancellationToken ct)
        {
            // 모델 값은 보낼 때마다 정한다 (엔드포인트 객체에 박아 두면 거부 뒤 다시 고른 모델이 반영되지 않는다)
            SttEndpoint sending = endpoint;
            if (endpoint.ModelField != null && !endpoint.HasField(endpoint.ModelField))
            {
                string model = await ResolveModelAsync(endpoint, ct);
                if (model != null)
                {
                    sending = endpoint.WithField(endpoint.ModelField, model);
                }
            }

            string url = _baseUrl + endpoint.Path + (string.IsNullOrEmpty(endpoint.Query) ? "" : "?" + endpoint.Query);
            string boundary = SttServerProtocol.NewBoundary();
            byte[] body = SttServerProtocol.BuildMultipart(boundary, sending, wav);

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(body) { contentType = SttServerProtocol.MultipartContentType(boundary) };
                req.downloadHandler = new DownloadHandlerBuffer();
                // 리다이렉트를 따라가면 POST가 GET으로 바뀌어 엉뚱한 405가 된다. 직접 판단한다.
                req.redirectLimit = 0;
                req.SetRequestHeader("Accept", "application/json, text/plain, */*");
                ApplyAuth(req);
                req.timeout = RequestTimeoutSeconds;

                var watch = Stopwatch.StartNew();
                var outcome = await SendAsync(req, ct);
                watch.Stop();

                var result = new PostResult
                {
                    Outcome = outcome, Code = req.responseCode, Result = Empty, ElapsedSeconds = watch.Elapsed.TotalSeconds
                };
                if (outcome == SendOutcome.Blocked) return result;

                string responseText = req.downloadHandler?.text;
                result.Body = responseText;

                long status = req.responseCode;
                // redirectLimit=0이면 리다이렉트가 오류("Redirect limit exceeded")로 끝날 수 있어 오류 문구도 본다
                bool redirectError = req.error != null && req.error.IndexOf("redirect", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((status >= 300 && status < 400) || redirectError)
                {
                    string location = req.GetResponseHeader("Location");
                    result.Outcome = SendOutcome.Redirect;
                    // 이동할 곳을 모르면 가장 흔한 경우(Django의 '/' 붙이기)로 본다
                    result.RedirectsToSlash = string.IsNullOrEmpty(location)
                        ? !endpoint.Path.EndsWith("/")
                        : RedirectsToSlash(endpoint.Path, location);
                    if ((status == 307 || status == 308) && !result.RedirectsToSlash)
                        result.RedirectPath = SameServerPath(location);
                    Debug.LogWarning($"[ServerSTT] {url} HTTP {status} → {location ?? req.error}");
                    return result;
                }
                if (outcome == SendOutcome.TransportError)
                {
                    result.TimedOutAfterUpload = req.uploadProgress >= 1f && watch.Elapsed.TotalSeconds >= RequestTimeoutSeconds - 1;
                    Debug.LogWarning($"[ServerSTT] {url} 연결 실패: {req.error}");
                    return result;
                }

                if (outcome != SendOutcome.Success)
                {
                    result.Outcome = Classify(req.responseCode);
                    Debug.LogWarning($"[ServerSTT] {url} HTTP {req.responseCode}: {Truncate(responseText)}");
                    return result;
                }

                if (!SttServerProtocol.TryParseTranscript(responseText, out var text, out var confidence,
                        req.GetResponseHeader("Content-Type")))
                {
                    // 200인데 문장이 아니다 — 이 경로가 인식용이 아닐 수 있다
                    Debug.LogWarning($"[ServerSTT] {url} 응답에서 문장을 찾지 못함: {Truncate(responseText)}");
                    result.Outcome = SendOutcome.BadRequest;
                    return result;
                }

                if (SttServerProtocol.IsLikelyHallucination(text))
                {
                    Debug.Log($"[ServerSTT] 무음 환각으로 보이는 결과 무시: '{text}'");
                    text = string.Empty;
                }

                Debug.Log($"[ServerSTT] '{text}' (음성 {seconds:F1}s, 업로드 {wav.Length / 1024}KB, {watch.ElapsedMilliseconds}ms)");
                result.Result = new SttResult
                {
                    text = text,
                    confidence = confidence,
                    isEmpty = string.IsNullOrWhiteSpace(text)
                };
                return result;
            }
        }

        private static bool RedirectsToSlash(string path, string location)
        {
            if (string.IsNullOrEmpty(location) || path.EndsWith("/")) return false;
            string target = location;
            if (Uri.TryCreate(location, UriKind.Absolute, out var abs)) target = abs.AbsolutePath;
            int q = target.IndexOf('?');
            if (q >= 0) target = target.Substring(0, q);
            return string.Equals(target, path + "/", StringComparison.Ordinal);
        }

        // Location이 같은 서버 안의 경로면 그 경로(쿼리 제외), 아니면 null
        private string SameServerPath(string location)
        {
            if (string.IsNullOrEmpty(location)) return null;
            if (location.StartsWith("/")) return location.Split('?')[0];
            if (Uri.TryCreate(location, UriKind.Absolute, out var abs) &&
                string.Equals(abs.GetLeftPart(UriPartial.Authority), _baseUrl, StringComparison.OrdinalIgnoreCase))
                return abs.AbsolutePath;
            return null;
        }

        private static SendOutcome Classify(long code)
        {
            if (code == 401 || code == 403) return SendOutcome.AuthRequired;
            if (code == 404 || code == 405) return SendOutcome.NotFound;
            if (code >= 500) return SendOutcome.ServerError;
            return SendOutcome.BadRequest; // 400, 413, 415, 422 등
        }

        private static string AuthHint(long code) =>
            $"서버가 인증을 요구함(HTTP {code}) — .env에 STT_SERVER_API_KEY를 넣거나 서버의 인증을 끄세요";

        // 모델 id. 지정값(.env STT_SERVER_MODEL) → 서버 /v1/models 중 음성 인식 모델 → 문서의 기본값 →
        // whisper-1(OpenAI 호환 경로만) 순. 목록에서 못 고른 경우는 기억해 두고 보낼 때마다 다시 묻지 않는다.
        private async UniTask<string> ResolveModelAsync(SttEndpoint endpoint, CancellationToken ct)
        {
            if (_modelId != null) return _modelId;

            if (!_modelListUnavailable)
            {
                foreach (var path in new[] { "/v1/models?task=automatic-speech-recognition", "/v1/models" })
                {
                    using (var req = UnityWebRequest.Get(_baseUrl + path))
                    {
                        req.timeout = ProbeTimeoutSeconds;
                        ApplyAuth(req);
                        if (await SendAsync(req, ct) != SendOutcome.Success) continue;
                        if (SttServerProtocol.TryPickModel(req.downloadHandler.text, out var id))
                        {
                            _modelId = id;
                            Debug.Log($"[ServerSTT] 모델 {id} ({path})");
                            return _modelId;
                        }
                    }
                }
                _modelListUnavailable = true;
            }

            if (endpoint.ModelFallback != null) return endpoint.ModelFallback;
            if (SttServerProtocol.IsOpenAiTranscriptionPath(endpoint.Path)) return "whisper-1";

            if (!_missingModelLogged)
            {
                _missingModelLogged = true;
                Debug.LogWarning($"[ServerSTT] {endpoint.Path}의 필수 '{endpoint.ModelField}' 값을 알 수 없음 — .env에 STT_SERVER_MODEL을 지정하세요");
            }
            return null; // 보낼 값이 없다 — 필드를 비워 두고 서버 판단에 맡긴다 (거부되면 로그에 남는다)
        }

        private void ApplyAuth(UnityWebRequest req)
        {
            if (_apiKey != null) req.SetRequestHeader("Authorization", "Bearer " + _apiKey);
        }

        private enum SendOutcome { Success, TransportError, Blocked, NotFound, Redirect, BadRequest, AuthRequired, ServerError, HttpError }

        // UniTask는 실패한 요청을 UnityWebRequestException으로 던진다. 여기서 결과 종류로 바꾼다.
        // 취소는 OperationCanceledException 그대로 올려 호출자가 정리하게 둔다.
        private static async UniTask<SendOutcome> SendAsync(UnityWebRequest req, CancellationToken ct)
        {
            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (UnityWebRequestException)
            {
                // 아래에서 req.result로 판정
            }
            catch (InvalidOperationException e)
            {
                // Player Settings > Allow downloads over HTTP 가 Not allowed면 http:// 요청이 여기서 막힌다
                Debug.LogError($"[ServerSTT] {e.Message}");
                return SendOutcome.Blocked;
            }

            switch (req.result)
            {
                case UnityWebRequest.Result.Success:
                    return SendOutcome.Success;
                case UnityWebRequest.Result.ProtocolError:
                    return SendOutcome.HttpError;
                default:
                    // ConnectionError(연결 거부·타임아웃·호스트 없음), DataProcessingError
                    return req.responseCode > 0 ? SendOutcome.HttpError : SendOutcome.TransportError;
            }
        }

        private void Disable(string reason)
        {
            _disabled = true;
            _state = ServerState.Offline;
            Debug.LogError($"[ServerSTT] 서버 STT 끔 {_baseUrl}: {reason} — 이번 실행 동안 기기 음성 인식 사용");
        }

        private void MarkOffline(string reason)
        {
            _state = ServerState.Offline;
            _offlineSince = Time.realtimeSinceStartup;
            Debug.LogWarning($"[ServerSTT] 서버 사용 중지 {_baseUrl}: {reason} — {RetryOfflineAfterSeconds:F0}초 동안 기기 음성 인식 사용");
        }

        private static string Truncate(string s) =>
            string.IsNullOrEmpty(s) ? "(빈 응답)" : (s.Length > 300 ? s.Substring(0, 300) + "…" : s);

        private static SttResult Empty => new SttResult { text = "", isEmpty = true };
    }
}
