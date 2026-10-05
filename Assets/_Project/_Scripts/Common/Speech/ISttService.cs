using System.Threading;
using Cysharp.Threading.Tasks;

namespace Artti.Common.Speech
{
    public struct SttResult
    {
        public string text;
        public float confidence;
        public bool isEmpty;

        // 변경점: 목표 문장 대비 발화 정확도(서버 명료도). 명료도를 계산하는 STT(서버 STT)만 채운다.
        //   hasClarity=false면 clarityPercent/clarityLight는 의미 없음 (구조체 기본값 0과 구분하기 위함).
        public bool hasClarity;
        public int clarityPercent;    // 0..100
        public string clarityLight;   // "green" / "yellow" / "red"
        public string clarityTarget;  // 명료도 비교 기준(목표 문장). 상세기록의 '이유' 설명에 쓴다
        // 변경점: 명료도를 계산한 모델이 들은 문장. 현장지원모드(대화 모드)의 두 모델 서버는 text(새 모델, NPC 대화용)와
        //   명료도용 인식(기존 모델)이 달라 상세기록의 '들린 문장'·이유 설명은 이 문장으로 한다.
        //   서버가 따로 주지 않으면 null(= text와 같은 모델)
        public string clarityText;

        // 변경점: 인식기의 다른 추측들(text 다음으로 그럴듯한 순서). 어눌한 말일수록 두세 번째 추측이 맞을 때가 많아
        //   대화 모드 문장 해석이 함께 본다. 주지 않는 STT는 null
        public string[] alternatives;
    }

    // 변경점: 목표 문장(탭한 카드 문구, 연습 문장)을 알아야 명료도를 계산할 수 있는 STT.
    //   호출부는 (_sttService as ITargetedSttService)?.SetTarget(...)으로 선택적으로 넘긴다.
    public interface ITargetedSttService
    {
        void SetTarget(string target);
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
