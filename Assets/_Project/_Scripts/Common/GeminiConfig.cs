using Newtonsoft.Json.Linq;

namespace Artti.Common
{
    // Gemini 모델·주소·'생각' 설정을 한곳에서 정한다(점원 대화, 대화 채점, 리포트 문구가 모두 이것을 쓴다).
    // 2026-10-02 사용자 결정: 전부 gemini-3.5-flash-lite(점원 대화 1초 안팎, 2.5-flash 무료 한도가 하루 20번이라 바꿈).
    // .env GEMINI_MODEL로 바꿀 수 있다(비우면 DefaultModel).
    public static class GeminiConfig
    {
        public const string DefaultModel = "gemini-3.5-flash-lite";
        const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        public static string Model
        {
            get
            {
                var m = ApiKeyLoader.Get(ApiKeyLoader.GeminiModel);
                return string.IsNullOrWhiteSpace(m) ? DefaultModel : m.Trim();
            }
        }

        public static string GenerateUrl => BaseUrl + Model + ":generateContent";

        // '생각'을 가장 적게: 빠르게 답하고, 생각 토큰이 maxOutputTokens를 먹어 답이 잘리지 않게.
        //   2.x는 thinkingBudget 0, 3.x는 thinkingBudget을 거절해(400) thinkingLevel을 쓴다
        //   (lite는 minimal, 나머지 3.x는 minimal을 거절해 low).
        public static JObject MinimalThinking()
        {
            var model = Model;
            if (model.StartsWith("gemini-2.")) return new JObject { ["thinkingBudget"] = 0 };
            return new JObject { ["thinkingLevel"] = model.Contains("lite") ? "minimal" : "low" };
        }
    }
}
