namespace Artti.Training
{
    public enum ClarityVerdict
    {
        Pass,             // 기준 이상 -> 진행
        NotMeasured,      // 명료도 없음(다른 후보 문장을 말함, 명료도를 주지 않는 STT) -> 막지 않고 진행
        Retry,            // 기준 미달 -> 같은 단계에서 다시 말하기
        PassAfterRetries  // 기준 미달이지만 이 단계에서 이미 maxRetries번 다시 말함 -> 막히지 않게 진행
    }

    // 정확도 모드 명료도 관문: 목표 문장을 말했는데 서버 명료도가 기준보다 낮으면 같은 단계에서 다시 말하게 한다.
    // 발달장애 사용자가 한 단계에 갇히지 않도록 같은 단계에서 기준 미달이 maxRetries번 쌓이면 그다음 시도는 통과시킨다.
    // 실패 횟수는 이 정책이 따로 센다. DialogueManager.AttemptCount(4회면 시나리오 강제 종료)에는 더하지 않는다.
    public class ClarityGatePolicy
    {
        public int MinPercent { get; }
        public int MaxRetries { get; }

        private string _objectiveId;
        private int _failures;

        public ClarityGatePolicy(int minPercent, int maxRetries)
        {
            MinPercent = minPercent;
            MaxRetries = maxRetries < 0 ? 0 : maxRetries;
        }

        // 현재 단계에서 쌓인 기준 미달 횟수 (Retry를 돌려준 횟수)
        public int Failures => _failures;

        public ClarityVerdict Evaluate(string objectiveId, bool hasClarity, int percent)
        {
            if (objectiveId != _objectiveId)
            {
                _objectiveId = objectiveId;
                _failures = 0;
            }
            if (!hasClarity) return ClarityVerdict.NotMeasured;
            if (percent >= MinPercent) return ClarityVerdict.Pass;
            if (_failures >= MaxRetries) return ClarityVerdict.PassAfterRetries;
            _failures++;
            return ClarityVerdict.Retry;
        }

        // 점원이 다시 말해 달라고 하는 말. 두 번째부터는 더 구체적으로 천천히 말하도록 안내한다.
        public string RetryLine() =>
            _failures <= 1
                ? "죄송해요, 잘 못 들었어요. 조금 더 또렷하게 한 번 더 말씀해 주시겠어요?"
                : "천천히, 또박또박 한 번만 더 말씀해 주세요.";
    }
}
