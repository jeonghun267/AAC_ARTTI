using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.Common.Speech
{
    public struct SttResult
    {
        public string text;
        public float confidence;
        public bool isEmpty;
    }

    // PLAN.MD v3 5.4 - shared STT abstraction. Used only by 훈련모드.
    // Concrete implementations: CloudSttService (Google Cloud STT), MockSttService (tests).
    public interface ISttService
    {
        bool IsAvailable { get; }

        // Begin a single-utterance recognition session. Returns when the user
        // stops speaking, on timeout, or on cancellation.
        UniTask<SttResult> ListenOnceAsync(CancellationToken ct = default);

        // Aborts an in-flight recognition. Safe to call when idle.
        void Cancel();
    }
}
