using System.Text;
using Newtonsoft.Json.Linq;

namespace Artti.Training
{
    // system_prompts.json (PLAN.md 5.5.4) 파서.
    // shared_preamble(공통 규칙) + 시나리오별 persona + few_shot_examples를 합쳐
    // Gemini system_instruction에 넣을 단일 텍스트로 제공.
    public class SystemPromptProvider
    {
        private readonly string _sharedPreamble;
        private readonly JArray _scenarios;

        public SystemPromptProvider(string json)
        {
            var root = JObject.Parse(json);
            _sharedPreamble = root["shared_preamble"]?.ToString() ?? string.Empty;
            _scenarios = root["scenarios"] as JArray ?? new JArray();
        }

        // 시나리오 persona 원문만 반환. 자유 대화(대화하기) 모드용 —
        // shared_preamble에는 "매 턴 반드시 도구를 하나 호출하라" 같은 훈련 흐름 규칙이 들어 있어
        // 도구 없이 평문으로 답해야 하는 자유 대화에 그대로 쓰면 안 된다.
        public string BuildPersona(string scenarioId)
        {
            foreach (var s in _scenarios)
            {
                if (s["scenario_id"]?.ToString() != scenarioId) continue;
                return s["persona"]?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        // 해당 시나리오의 persona + few-shot을 공통 preamble 뒤에 이어붙여 반환. 시나리오 미발견 시 preamble만.
        public string BuildSystemPrompt(string scenarioId)
        {
            var sb = new StringBuilder();
            sb.Append(_sharedPreamble);

            foreach (var s in _scenarios)
            {
                if (s["scenario_id"]?.ToString() != scenarioId) continue;

                var persona = s["persona"]?.ToString();
                if (!string.IsNullOrEmpty(persona))
                    sb.Append("\n\n--- SCENARIO PERSONA ---\n").Append(persona);

                if (s["few_shot_examples"] is JArray fs && fs.Count > 0)
                {
                    sb.Append("\n\n--- EXAMPLES ---");
                    foreach (var ex in fs)
                        sb.Append('\n').Append(ex.ToString());
                }
                break;
            }
            return sb.ToString();
        }
    }
}
