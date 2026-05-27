using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.Common.Speech
{
    // Editor/플랫폼 미지원 환경용. 발화하지 않고 즉시 완료.
    public class NoopTtsService : ITtsService
    {
        public bool IsAvailable => false;
        public UniTask SpeakAsync(string text, CancellationToken ct = default) => UniTask.CompletedTask;
        public void StopAll() { }
        public void SetVoice(string voiceName) { }
    }
}
