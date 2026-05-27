using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Artti.Common.Speech
{
    // Editor Play 모드 검증용. Android Native STT는 빌드에서만 동작하므로
    // Editor에선 짧은 가짜 지연 후 더미 문자열을 반환해 풀 모드 흐름이 진행되도록 함.
    public class EditorMockSttService : ISttService
    {
        public bool IsAvailable => true;

        private const int DelayMs = 600;
        private const string DummyText = "테스트 입력";

        public async UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default)
        {
            Debug.Log("[MockSTT] Editor 더미 결과 반환 — Android 빌드에서는 NativeSTT가 실제 인식 수행");
            try { await UniTask.Delay(DelayMs, cancellationToken: ct); }
            catch (System.OperationCanceledException) { return new SttResult { text = "", isEmpty = true }; }
            return new SttResult { text = DummyText, confidence = 1f, isEmpty = false };
        }

        public void Cancel() { }
    }
}
