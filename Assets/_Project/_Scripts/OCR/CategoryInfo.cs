using System;
using System.Collections.Generic;

/// <summary>
/// JSON에서 로드되는 카테고리 정보
/// 
/// JSON 구조:
///   "카페": {
///     "description": "커피와 음료를 파는 곳입니다.",
///     "image": "cafe",
///     "keywords": ["스타벅스", "STARBUCKS", ...]
///   }
/// </summary>
[Serializable]
public class CategoryInfo
{
    public string description;
    public string image;
    public List<string> keywords;
}