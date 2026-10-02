using System.Linq;
using Artti.AAC.Logging;
using Artti.Common;
using Artti.Report;

namespace Artti.UI
{
    // 모드별 리포트 열기. 메인 화면 '리포트 보기'는 기록에서 마지막으로 연습한 모드의 리포트를 연다(사용자 결정, 2026-10-02).
    public static class ReportNavigation
    {
        public const string AccuracyScene = "AccuracyReportScene";
        public const string ConversationScene = "ConversationReportScene";

        public static string SceneFor(TrainingMode mode) => mode == TrainingMode.Conversation ? ConversationScene : AccuracyScene;

        public static void Open(TrainingMode mode) => SceneFader.Load(SceneFor(mode));

        public static void OpenLastTrained() => Open(LastTrainedMode());

        // 기록이 없거나(프로필 없음 포함) 읽을 수 없으면 정확도 모드
        public static TrainingMode LastTrainedMode()
        {
            var path = (AppBootstrap.Instance?.LogStore as JsonAppendLogStore)?.FilePath;
            if (string.IsNullOrEmpty(path)) return TrainingMode.Accuracy;
            var latest = new ReportDataService(path).GetSessions().FirstOrDefault();
            return latest?.mode ?? TrainingMode.Accuracy;
        }
    }
}
