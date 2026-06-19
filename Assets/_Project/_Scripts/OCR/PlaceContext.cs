// 현재 사용자가 있는 장소 정보를 앱 전체에서 공유하는 곳.
// ResultPanel에서 장소를 판별하면 PlaceContext.Set(...)으로 저장하고,
// CardGenerator는 카드를 만들 때 이 값을 읽어 프롬프트에 넣는다.
public static class PlaceContext
{
    public static string Category = "";   // 예: "카페", "약국", "음식점"
    public static string PlaceName = "";  // 예: "메가커피" (선택, 더 구체적인 맥락)

    public static bool HasContext => !string.IsNullOrEmpty(Category);

    public static void Set(string category, string placeName = "")
    {
        Category = category;
        PlaceName = placeName;
    }

    public static void Clear()
    {
        Category = "";
        PlaceName = "";
    }
}