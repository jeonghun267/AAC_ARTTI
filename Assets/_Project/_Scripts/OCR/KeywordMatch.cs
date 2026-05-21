/// <summary>
/// OCR 텍스트를 키워드 사전과 매칭한 결과
/// </summary>
public class KeywordMatch
{
    public string Category { get; }       // "카페", "편의점" 등
    public string Description { get; }    // "커피와 음료를 파는 곳입니다."
    public string ImageName { get; }      // "cafe" (Resources/AAC/cafe.png)
    public string MatchedKeyword { get; } // 어떤 키워드로 매칭됐는지

    public KeywordMatch(string category, string description, string imageName, string matchedKeyword)
    {
        Category = category;
        Description = description;
        ImageName = imageName;
        MatchedKeyword = matchedKeyword;
    }
}