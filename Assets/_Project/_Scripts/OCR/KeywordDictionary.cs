using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 키워드 사전 로더 + 매칭 엔진
/// 
/// 사용법:
///   var dict = new KeywordDictionary();
///   yield return dict.LoadAsync();           // 코루틴으로 로드
///   var match = dict.Match("STARBUCKS");
///   // match.Category == "카페"
/// </summary>
public class KeywordDictionary
{
    // JSON 키(카테고리명) → CategoryInfo
    private Dictionary<string, CategoryInfo> _dict = new Dictionary<string, CategoryInfo>();
    private bool _loaded = false;

    public bool IsLoaded => _loaded;

    /// <summary>
    /// StreamingAssets에서 JSON 로드 (코루틴)
    /// Android에서 StreamingAssets는 jar:// 경로라 UnityWebRequest 필요
    /// </summary>
    public IEnumerator<UnityEngine.Networking.UnityWebRequestAsyncOperation> LoadAsync()
    {
        if (_loaded) yield break;

        string path = Path.Combine(Application.streamingAssetsPath, "signboard_keywords.json");

        // Android의 StreamingAssets는 직접 File.ReadAllText 불가능
        // UnityWebRequest로 읽어야 함
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError("[KeywordDictionary] JSON 로드 실패: " + request.error + "\n경로: " + path);
                yield break;
            }

            string json = request.downloadHandler.text;
            ParseJson(json);
            _loaded = true;
            Debug.Log("[KeywordDictionary] 로드 완료. 카테고리 수: " + _dict.Count);
        }
    }

    /// <summary>
    /// JSON 파싱
    /// 
    /// Unity의 JsonUtility는 Dictionary를 직접 못 읽으므로 SimpleJSON 방식으로 수동 파싱
    /// 외부 라이브러리 없이 동작하도록 Unity 내장 JsonUtility 우회
    /// </summary>
    private void ParseJson(string json)
    {
        _dict.Clear();

        // Unity JsonUtility는 최상위가 Dictionary인 JSON을 못 읽음
        // 직접 파싱: 카테고리 단위로 끊어서 각각 JsonUtility로 처리

        // 1) 최상위 객체의 키들을 추출 (정규식 사용)
        var categoryNames = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(
            json,
            "\"([^\"]+)\"\\s*:\\s*\\{"
        );

        // 최상위 키만 추려내기 위해, "description"이나 "image", "keywords" 같은
        // 내부 키는 제외 (이들은 각 카테고리 안의 속성)
        var innerKeys = new HashSet<string> { "description", "image", "keywords" };
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            string key = m.Groups[1].Value;
            if (!innerKeys.Contains(key))
            {
                categoryNames.Add(key);
            }
        }

        // 2) 각 카테고리의 객체를 추출해서 JsonUtility로 파싱
        foreach (var categoryName in categoryNames)
        {
            string objJson = ExtractCategoryObject(json, categoryName);
            if (string.IsNullOrEmpty(objJson)) continue;

            try
            {
                var info = JsonUtility.FromJson<CategoryInfo>(objJson);
                if (info != null)
                {
                    _dict[categoryName] = info;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KeywordDictionary] '" + categoryName + "' 파싱 실패: " + e.Message);
            }
        }
    }

    /// <summary>
    /// JSON 문자열에서 특정 카테고리의 객체 부분만 추출
    /// 예: "카페": { ... } 에서 { ... } 부분 반환
    /// </summary>
    private string ExtractCategoryObject(string json, string categoryName)
    {
        string searchKey = "\"" + categoryName + "\"";
        int keyIndex = json.IndexOf(searchKey);
        if (keyIndex < 0) return null;

        int braceStart = json.IndexOf('{', keyIndex);
        if (braceStart < 0) return null;

        // 중괄호 매칭으로 끝나는 위치 찾기
        int depth = 0;
        for (int i = braceStart; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return json.Substring(braceStart, i - braceStart + 1);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// OCR 텍스트에서 카테고리 추론
    /// - 부분 일치 (대소문자/공백 무시)
    /// - 매칭 시 KeywordMatch 반환, 없으면 null
    /// </summary>
    public KeywordMatch Match(string ocrText)
    {
        if (string.IsNullOrEmpty(ocrText) || !_loaded) return null;

        string normalized = ocrText.ToUpper().Replace(" ", "").Replace("\n", "");

        foreach (var entry in _dict)
        {
            string categoryName = entry.Key;
            CategoryInfo info = entry.Value;

            if (info.keywords == null) continue;

            foreach (var keyword in info.keywords)
            {
                if (string.IsNullOrEmpty(keyword)) continue;
                string keywordNorm = keyword.ToUpper().Replace(" ", "");
                if (string.IsNullOrEmpty(keywordNorm)) continue;

                if (normalized.Contains(keywordNorm))
                {
                    return new KeywordMatch(
                        category: categoryName,
                        description: info.description ?? "",
                        imageName: info.image ?? "",
                        matchedKeyword: keyword
                    );
                }
            }
        }
        return null;
    }
}