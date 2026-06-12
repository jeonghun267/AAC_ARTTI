using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Artti.Common
{
    public static class ApiKeyLoader
    {
        public const string GeminiApi    = "GEMINI_API_KEY";
        public const string GoogleSttApi = "GOOGLE_STT_API_KEY";
        public const string GoogleTtsApi = "GOOGLE_TTS_API_KEY";

        static Dictionary<string, string> _keys;

        // Domain Reload 꺼져 있어도 Play 진입마다 캐시 초기화 (.env 수정 즉시 반영)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { _keys = null; }

        public static string Get(string keyName)
        {
            EnsureLoaded();
            return _keys.TryGetValue(keyName, out var v) ? v : null;
        }

        public static string GetOrFallback(string primary, string fallback)
        {
            var v = Get(primary);
            return string.IsNullOrEmpty(v) ? Get(fallback) : v;
        }

        public static void Reload()
        {
            _keys = null;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (_keys != null) return;
            _keys = new Dictionary<string, string>();

#if UNITY_EDITOR
            // Editor: read .env from project root
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (projectRoot != null)
            {
                var envPath = Path.Combine(projectRoot, ".env");
                if (File.Exists(envPath))
                {
                    ParseEnvFormat(File.ReadAllText(envPath));
                    return;
                }
            }
#endif

            // Runtime (build): Resources/api_keys.txt — Android의 StreamingAssets는 APK 내부라 File IO 불가
            var keyAsset = Resources.Load<TextAsset>("api_keys");
            if (keyAsset != null)
            {
                ParseEnvFormat(keyAsset.text);
                return;
            }

            // 구버전 빌드 호환: StreamingAssets/api_keys.env (Android에선 도달 불가)
            var saPath = Path.Combine(Application.streamingAssetsPath, "api_keys.env");
            if (File.Exists(saPath))
            {
                ParseEnvFormat(File.ReadAllText(saPath));
                return;
            }

            Debug.LogWarning("[ApiKeyLoader] .env not found — API calls will fail. Place .env at project root for Editor; builds use Resources/api_keys.txt (Artti/Copy .env to Resources).");
        }

        static void ParseEnvFormat(string content)
        {
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') ||
                                          (value[0] == '\'' && value[value.Length - 1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                _keys[key] = value;
            }
        }
    }
}
