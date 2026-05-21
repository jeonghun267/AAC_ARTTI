using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.AAC.Logging
{
    // PLAN.MD v3 section 8.4 / 5.4 - abstract log store.
    // Implementations: NullLogStore (opt-out), JsonAppendLogStore (MVP local),
    // future: SqliteLogStore, FirestoreLogStore (Phase 3).
    public interface ILogStore
    {
        void Log(AACEvent ev);
        UniTask FlushAsync(CancellationToken ct = default);
        UniTask DisposeAsync(CancellationToken ct = default);
    }
}
