namespace Artti.Report
{
    // 레포트 -> 세션 상세 리포트 씬 전환 시 선택한 세션 id 전달용 (씬 간 정적 홀더).
    // null이면 상세 씬은 최신 세션으로 폴백.
    public static class RecordDetailContext
    {
        public static string SessionId;
    }
}
