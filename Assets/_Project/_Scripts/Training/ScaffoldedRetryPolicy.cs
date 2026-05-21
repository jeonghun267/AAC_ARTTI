using Artti.AAC;

namespace Artti.Training
{
    public class ScaffoldedRetryPolicy
    {
        public ScaffoldLevel GetScaffoldLevel(int attemptCount)
        {
            if (attemptCount <= 1) return ScaffoldLevel.None;
            if (attemptCount == 2) return ScaffoldLevel.MildHint;
            if (attemptCount >= 3) return ScaffoldLevel.StrongHint;
            return ScaffoldLevel.StrongHint;
        }

        public bool ShouldForceComplete(int attemptCount)
        {
            return attemptCount >= 4;
        }
    }
}
