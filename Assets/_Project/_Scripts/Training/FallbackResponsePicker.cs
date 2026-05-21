using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Artti.Training
{
    // fallback_responses.json (PLAN.md 11.3.4) - {scenario_id, responses: {condition: [texts]}}
    public class FallbackResponsePicker
    {
        // scenarioId -> condition -> texts
        private readonly Dictionary<string, Dictionary<string, string[]>> _data
            = new Dictionary<string, Dictionary<string, string[]>>();

        public FallbackResponsePicker(string jsonContent)
        {
            try
            {
                var root = JObject.Parse(jsonContent);
                var fallbacks = root["fallbacks"] as JArray;
                if (fallbacks == null) return;
                foreach (var fb in fallbacks)
                {
                    var scenarioId = (string)fb["scenario_id"];
                    var responses = fb["responses"] as JObject;
                    if (string.IsNullOrEmpty(scenarioId) || responses == null) continue;

                    var conditions = new Dictionary<string, string[]>();
                    foreach (var prop in responses.Properties())
                    {
                        var arr = prop.Value as JArray;
                        if (arr == null) continue;
                        var texts = new string[arr.Count];
                        for (int i = 0; i < arr.Count; i++) texts[i] = (string)arr[i];
                        conditions[prop.Name] = texts;
                    }
                    _data[scenarioId] = conditions;
                }
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"[FallbackResponsePicker] JSON parse failed: {e.Message}");
            }
        }

        public string Pick(string scenarioId, string condition)
        {
            if (_data.TryGetValue(scenarioId, out var conds))
            {
                if (conds.TryGetValue(condition, out var texts) && texts.Length > 0)
                    return texts[Random.Range(0, texts.Length)];
                if (conds.TryGetValue("default", out var def) && def.Length > 0)
                    return def[Random.Range(0, def.Length)];
            }
            return "잠시만요, 다시 한 번 말씀해주세요.";
        }
    }
}
