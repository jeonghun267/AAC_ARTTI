using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.Common.Speech
{
    // PLAN.MD v3 5.4 - shared TTS abstraction. Training mode plays NPC responses;
    // AR field mode plays card text on tap. Concrete implementations: CloudTtsService
    // (Google Cloud TTS), AndroidNativeTtsService (fallback), MockTtsService (tests).
    public interface ITtsService
    {
        bool IsAvailable { get; }

        // Speaks the given Korean text. Returns when audio playback completes,
        // or earlier if cancelled. Caller is expected to await this so subsequent
        // turn UI can sync to playback completion (PLAN.MD 11.3 latency policy).
        UniTask SpeakAsync(string text, CancellationToken ct = default);

        // Stops any in-flight playback immediately. Safe to call when idle.
        void StopAll();

        // Optional voice/language hint. Implementations may ignore.
        void SetVoice(string voiceName);
    }
}
