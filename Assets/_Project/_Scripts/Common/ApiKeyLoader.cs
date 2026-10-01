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
        // 변경점: 서버PC STT 서버 설정. 주소는 TrainingSceneRoot Inspector(Stt Server Url)가 비어 있을 때만 쓴다.
        public const string SttServerUrl    = "STT_SERVER_URL";
        public const string SttServerApiKey = "STT_SERVER_API_KEY"; // 서버가 Bearer 인증을 요구할 때만
        public const string SttServerModel  = "STT_SERVER_MODEL";   // OpenAI 호환 서버의 모델 id (비우면 서버 목록에서 고름)
        // 변경점: 코드에 박혀 있던 장소 검색 키를 .env로 옮김 (KakaoLocalSearcher, NaverLocalSearcher)
        public const string KakaoRestApi      = "KAKAO_REST_API_KEY";
        public const string NaverClientId     = "NAVER_CLIENT_ID";
        public const string NaverClientSecret = "NAVER_CLIENT_SECRET";

        static Dictionary<string, string> _keys;

        // Domain Reload 꺼져 있어도 Play 진입마다 캐시 초기화 (.env 수정 즉시 반영)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { _keys = null; }

        public static string Get(string keyName)
        {
            EnsureLoaded();
            var value = _keys.TryGetValue(keyName, out var v) ? v : null;
            if (keyName == KakaoRestApi)
                Debug.Log($"[ApiKeyLoader] Kakao REST API Key loaded = {!string.IsNullOrWhiteSpace(value)}");
            return value;
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
                // 변경점: BOM으로 시작하는 파일(TextAsset은 BOM을 남긴다)과 셸 형식 "export KEY=값"도 받는다
                var line = rawLine.Trim().TrimStart('\uFEFF');
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                if (line.StartsWith("export ")) line = line.Substring(7).TrimStart();
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var raw = line.Substring(idx + 1);
                var value = raw.Trim();
                // "KEY=   # 설명"처럼 값 없이 주석만 있으면 빈 값
                if (value.StartsWith("#") && raw.Length > 0 && char.IsWhiteSpace(raw[0])) value = string.Empty;
                // 변경점: python-dotenv처럼 따옴표 값 뒤의 주석, 따옴표 없는 값의 공백+# 주석을 떼어 낸다.
                //   (KEY="값" # 설명, KEY=값  # 설명) 따옴표 안의 #는 값으로 둔다.
                if (value.Length >= 1 && (value[0] == '"' || value[0] == '\''))
                {
                    int close = value.IndexOf(value[0], 1);
                    if (close > 0) value = value.Substring(1, close - 1);
                }
                else
                {
                    // 공백(스페이스·탭) 뒤의 #부터 주석
                    for (int i = 1; i < value.Length; i++)
                    {
                        if (value[i] == '#' && char.IsWhiteSpace(value[i - 1]))
                        {
                            value = value.Substring(0, i).TrimEnd();
                            break;
                        }
                    }
                }
                _keys[key] = value;
            }
        }
    }
}
