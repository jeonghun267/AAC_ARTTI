using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.Common.Speech
{
    // 서버 STT를 먼저 쓰고, 서버를 쓸 수 없으면 기기 음성 인식(Android 네이티브 / 에디터 더미)으로 넘긴다.
    //   - 서버가 끊긴 상태면 확인을 기다리지 않고 바로 기기 인식으로 간다. 재확인은 뒤에서 하고 다음 발화부터 반영된다.
    //   - 이 기기에서 Unity 마이크 녹음이 안 되면(마이크 없는 PC 등) 기기 인식을 쓴다.
    //   - 서버가 녹음 뒤 요청 단계에서 실패하면 그 한 번은 빈 결과가 된다(사용자는 "다시 말해 주세요" 안내를 받는다).
    //     이미 녹음한 발화를 기기 인식으로 다시 들을 방법이 없기 때문이다.
    public sealed class FailoverSttService : ISttService
    {
        private readonly ServerSttService _server;
        private readonly ISttService _fallback;

        public FailoverSttService(ServerSttService server, ISttService fallback)
        {
            _server = server;
            _fallback = fallback;
        }

        public bool IsAvailable => _server.IsAvailable || _fallback.IsAvailable;

        // 씬 시작 때 서버 연결 확인·엔드포인트 탐색을 미리 해 둔다 (첫 발화 지연 방지)
        public UniTask<bool> WarmUpAsync(CancellationToken ct) => _server.EnsureReadyAsync(ct);

        // 이번 발화에 쓸 인식기를 고른다. 서버를 쓰게 되면 녹음 직전에 beforeServerRecording을 기다린다
        // (점원 음성이 스피커로 나오는 동안 녹음하지 않도록). 기기 인식이면 기다리지 않는다.
        public async UniTask<ISttService> PrepareTurnAsync(Func<CancellationToken, UniTask> beforeServerRecording,
            CancellationToken ct)
        {
            if (!UsesServerNow)
            {
                _server.RefreshInBackground(ct);
                return _fallback;
            }

            // 첫 확인이 아직 진행 중이면 결과를 먼저 기다린다(씬 시작 직후에만 해당, 최대 수 초).
            // 점원 음성 대기는 그 뒤에 한다 — 확인을 기다리는 동안 다시 듣기로 음성이 나올 수 있기 때문이다.
            if (!await _server.EnsureReadyAsync(ct) || !_server.CanRecord) return _fallback;

            if (beforeServerRecording != null) await beforeServerRecording(ct);
            return _server;
        }

        private bool UsesServerNow => _server.IsAvailable && _server.CanRecord;

        public async UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default)
        {
            ISttService chosen;
            try
            {
                chosen = await PrepareTurnAsync(null, ct);
            }
            catch (OperationCanceledException)
            {
                return new SttResult { text = "", isEmpty = true };
            }
            return await chosen.ListenOnceAsync(ct);
        }

        public void Cancel()
        {
            _server.Cancel();
            _fallback.Cancel();
        }
    }
}
