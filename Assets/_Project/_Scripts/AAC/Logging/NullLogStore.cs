using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.AAC.Logging
{
    // Used when the user (or guardian) has opted out of logging via 8.5 privacy settings.
    public sealed class NullLogStore : ILogStore
    {
        public void Log(AACEvent ev) { }
        public UniTask FlushAsync(CancellationToken ct = default) => UniTask.CompletedTask;
        public UniTask DisposeAsync(CancellationToken ct = default) => UniTask.CompletedTask;
    }
}
