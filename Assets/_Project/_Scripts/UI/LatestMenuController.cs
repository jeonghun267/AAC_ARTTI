using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class LatestMenuItem
{
    public string menuName;
    public string description;
    public string price;
    public Sprite image;

    public LatestMenuItem(string menuName, string description, string price, Sprite image = null)
    {
        this.menuName = menuName;
        this.description = description;
        this.price = price;
        this.image = image;
    }
}

[Serializable]
public sealed class LatestMenuCardView
{
    public GameObject cardRoot;
    public Image menuImage;
    public TMP_Text menuName;
    public TMP_Text menuDescription;
    public TMP_Text priceText;
    // 변경점: 카드를 누르면 "메뉴 이름 주세요"를 읽는다(카드 전체가 누름 칸)
    public Button button;
}

[DisallowMultipleComponent]
public sealed class LatestMenuController : MonoBehaviour
{
    public const string MegaCoffeeBrand = "메가MGC커피";
    public const string TenPercentCoffeeBrand = "텐퍼센트 커피";
    [SerializeField] private LatestMenuCardView[] cards = new LatestMenuCardView[4];

    [Header("Mega MGC Coffee Menu Images")]
    [SerializeField] private Sprite housemilkLatteImage;
    [SerializeField] private Sprite orzoLatteImage;
    [SerializeField] private Sprite goldenappleBlackteaImage;
    [SerializeField] private Sprite bamgogumaBreadImage;

    [Header("Ten Percent Coffee Menu Images")]
    [SerializeField] private Sprite saltyGlazedLatteImage;
    [SerializeField] private Sprite avocadoBananaFrecinoImage;
    [SerializeField] private Sprite thickCoffeeBingsuImage;
    [SerializeField] private Sprite dirtyPeanutLatteImage;

    [Header("Store Price")]
    [SerializeField] private MegaStorePriceLoader storePriceLoader;

    // 최초 실행/통신 실패 시 사용할 브랜드별 메뉴. 표시 카드는 최대 14개이며 UI 구조는 유지한다.
    [Header("Menu Lists (Inspector에서 추가)")]
    [SerializeField] private List<LatestMenuItem> megaMenus = new List<LatestMenuItem>();
    [SerializeField] private List<LatestMenuItem> tenPercentMenus = new List<LatestMenuItem>();
    [Header("Official Menu Refresh (14 days / up to 14 menus)")]
    [SerializeField] private bool automaticMenuRefresh = true;
    [Header("Manual Prices (brand / menuName / price)")]
    [Tooltip("가격은 이 목록에서 수동 입력합니다. 공식 메뉴 갱신으로 덮어쓰지 않습니다.")]
    [SerializeField] private List<LatestMenuManualPrice> manualPrices = new List<LatestMenuManualPrice>();
    [Tooltip("메뉴가 바뀌면 맨 위로 올린다")]
    [SerializeField] private ScrollRect menuScroll;

    [Header("TTS")]
    [Tooltip("메뉴 카드를 누르면 \"메뉴 이름 주세요\"를 읽는다")]
    [SerializeField] private TTSManager tts;

    // UI 표시용 고정 가을 시즌 메뉴 데이터입니다. 가격은 사용자가 지정한 표시 금액입니다.
    [SerializeField] private List<LatestMenuItem> testMenus = CreateInitialMenus();

    private readonly HashSet<string> _refreshingBrands = new HashSet<string>();
    private readonly Dictionary<string, DateTime> _retryAfter = new Dictionary<string, DateTime>();
    private readonly Dictionary<string, Sprite> _cachedImages = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null || cards[i].button == null) continue;
            int index = i;
            cards[i].button.onClick.AddListener(() => SpeakMenu(index));
        }
    }

    // 변경점: 카드에 보이는 메뉴 이름 + " 주세요"를 읽는다(예: "하우스밀크 라떼 주세요")
    // 변경점: 이름에 붙은 ICE/HOT은 읽지 않는다(예: "(HOT)헛개리카노" -> "헛개리카노 주세요")
    private void SpeakMenu(int index)
    {
        if (tts == null || index >= cards.Length || cards[index]?.menuName == null) return;
        string menuName = WithoutTemperature(cards[index].menuName.text);
        if (string.IsNullOrEmpty(menuName)) return;
        tts.Speak(menuName + " 주세요");
    }

    // 공식 메뉴 이름 앞/뒤의 온도 표기: (ICE) [HOT] "ICE " / 뒤의 (ICE). ICED, HOT초코처럼 붙은 낱말은 그대로 둔다.
    private static readonly Regex TemperatureTag = new Regex(
        @"^\s*(?:[\(\[]\s*(?:ICE|HOT)\s*[\)\]]|(?:ICE|HOT)(?=\s))\s*|\s*[\(\[]\s*(?:ICE|HOT)\s*[\)\]]\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // 변경점: 메뉴 이름에서 ICE/HOT 표기를 뺀다. 빼고 남는 게 없으면 원래 이름. 주문하기 화면(OrderMenuListView)도 쓴다.
    public static string WithoutTemperature(string menuName)
    {
        if (string.IsNullOrWhiteSpace(menuName)) return string.Empty;
        string trimmed = menuName.Trim();
        string stripped = TemperatureTag.Replace(trimmed, string.Empty).Trim();
        return stripped.Length > 0 ? stripped : trimmed;
    }

    private void OnEnable()
    {
        string placeName = PlaceContext.PlaceName;
        // Apply a completed refresh only at the next entry, even for the same store.
        UpdateForPlace(placeName);
    }

    private void OnDestroy()
    {
        ReleaseCachedImages();
    }

    private static List<LatestMenuItem> CreateInitialMenus(
        Sprite housemilkLatteImage = null,
        Sprite orzoLatteImage = null,
        Sprite goldenappleBlackteaImage = null,
        Sprite bamgogumaBreadImage = null)
    {
        return new List<LatestMenuItem>
        {
            new LatestMenuItem("하우스밀크 라떼", "하우스밀크와 에스프레소가 어우러진 부드러운 라떼", "4,000원", housemilkLatteImage),
            new LatestMenuItem("무카페인 오르조라떼", "오르조와 우유가 어우러진 무카페인 라떼", "3,800원", orzoLatteImage),
            new LatestMenuItem("저당 골든애플 블랙티", "사과와 블랙티를 조합한 저당 티 음료", "4,000원", goldenappleBlackteaImage),
            new LatestMenuItem("밤고구마단호박팥크림치즈빵", "고구마·밤·단호박·팥과 크림치즈를 담은 시즌 디저트", "4,700원", bamgogumaBreadImage)
        };
    }

    public void UpdateForPlace(string placeName)
    {
        Debug.Log($"[LatestMenu DEBUG] UpdateForPlace 입력값 = '{placeName}'");

        bool isMega = IsMegaCoffee(placeName);
        Debug.Log($"[LatestMenu DEBUG] IsMegaCoffee = {isMega}");

        if (isMega)
        {
            ShowMegaMenu();
            // Price loading is intentionally not called: prices are manually maintained.
            Debug.Log("[LatestMenu] 메가MGC커피 감지 - 최신 메뉴 표시");
            return;
        }

        if (IsTenPercentCoffee(placeName))
        {
            ShowTenPercentMenu();
            Debug.Log("[LatestMenu] 텐퍼센트 커피 감지 - 최신 메뉴 표시");
            return;
        }

        HideMenus();
        Debug.Log($"[LatestMenu] 지원하지 않는 브랜드: {placeName}");
    }

    public void ShowMegaMenu()
    {
        ShowBrandMenus(MegaCoffeeBrand);
    }

    public void ShowTenPercentMenu()
    {
        ShowBrandMenus(TenPercentCoffeeBrand);
    }

    private List<LatestMenuItem> LocalMenus(string brand)
    {
        if (brand == MegaCoffeeBrand)
            return megaMenus != null && megaMenus.Count > 0 ? CopyMenus(megaMenus)
                : CreateInitialMenus(housemilkLatteImage, orzoLatteImage, goldenappleBlackteaImage, bamgogumaBreadImage);
        return tenPercentMenus != null && tenPercentMenus.Count > 0 ? CopyMenus(tenPercentMenus) : CreateTenPercentMenus();
    }

    private void ShowBrandMenus(string brand)
    {
        ReleaseCachedImages();
        OfficialMenuSnapshot snapshot = automaticMenuRefresh ? OfficialMenuCatalog.ReadCache(brand) : null;
        testMenus = BuildMenus(brand, snapshot);
        SetMenus(testMenus);
        if (automaticMenuRefresh)
        {
            if (OfficialMenuCatalog.IsRefreshDue(snapshot, DateTime.UtcNow)) RefreshIfNeeded(brand, null);
            else if (OfficialMenuCatalog.HasMissingImages(snapshot)) RefreshIfNeeded(brand, snapshot);
        }
    }

    // 변경점: 주문하기 화면(OrderMenuListView)도 이 가게의 같은 메뉴를 쓴다. 카드/스크롤/갱신은 건드리지 않고 목록만 돌려준다.
    //   지원하지 않는 가게면 빈 목록.
    public List<LatestMenuItem> MenusForPlace(string placeName)
    {
        string brand = GetSupportedBrandName(placeName);
        if (brand == null) return new List<LatestMenuItem>();
        return BuildMenus(brand, automaticMenuRefresh ? OfficialMenuCatalog.ReadCache(brand) : null);
    }

    // 공식 메뉴 캐시가 있으면 그 순서(사진은 캐시, 없으면 Inspector 사진), 없으면 Inspector/기본 메뉴
    private List<LatestMenuItem> BuildMenus(string brand, OfficialMenuSnapshot snapshot)
    {
        List<LatestMenuItem> local = LocalMenus(brand);
        var menus = new List<LatestMenuItem>();
        if (snapshot != null)
        {
            foreach (OfficialMenuEntry entry in snapshot.menus)
            {
                LatestMenuItem fallback = FindMenu(local, entry.menuName);
                Sprite image = CachedImage(entry.imageUrl) ?? fallback?.image;
                string description = string.IsNullOrWhiteSpace(entry.description) ? fallback?.description ?? string.Empty : entry.description;
                menus.Add(new LatestMenuItem(entry.menuName, description,
                    ManualPrice(brand, entry.menuName, local), image));
            }
        }
        else
        {
            foreach (LatestMenuItem item in local)
            {
                if (menus.Count == OfficialMenuCatalog.MaximumMenus) break;
                item.price = ManualPrice(brand, item.menuName, local);
                menus.Add(item);
            }
        }
        return menus;
    }

    private static LatestMenuItem FindMenu(List<LatestMenuItem> menus, string name)
    {
        string key = OfficialMenuCatalog.MenuKey(name);
        return menus.Find(menu => menu != null && OfficialMenuCatalog.MenuKey(menu.menuName) == key);
    }

    private string ManualPrice(string brand, string name, List<LatestMenuItem> local)
    {
        string key = OfficialMenuCatalog.MenuKey(name);
        if (manualPrices != null)
        {
            foreach (LatestMenuManualPrice price in manualPrices)
            {
                if (price != null && GetSupportedBrandName(price.brand) == brand
                    && OfficialMenuCatalog.MenuKey(price.menuName) == key)
                    return TryFormatPrice(price.price, out string formatted) ? formatted : OfficialMenuCatalog.PricePending;
            }
        }
        return TryFormatPrice(FindMenu(local, name)?.price, out string fallbackPrice)
            ? fallbackPrice : OfficialMenuCatalog.PricePending;
    }

    private void RefreshIfNeeded(string brand, OfficialMenuSnapshot photosOnly)
    {
        if (_refreshingBrands.Contains(brand)
            || (_retryAfter.TryGetValue(brand, out DateTime retry) && DateTime.UtcNow < retry)) return;
        _refreshingBrands.Add(brand);
        RefreshMenusAsync(brand, photosOnly).Forget();
    }

    private async UniTaskVoid RefreshMenusAsync(string brand, OfficialMenuSnapshot photosOnly)
    {
        try
        {
            if (photosOnly == null)
            {
                Debug.Log($"[LatestMenu] {brand} 공식 메뉴 갱신 시작 (최대 14개 / 14일 주기)");
                OfficialMenuSnapshot snapshot = await OfficialMenuCatalog.FetchAsync(brand, destroyCancellationToken);
                OfficialMenuCatalog.SaveCache(snapshot);
                Debug.Log($"[LatestMenu] {brand} 메뉴 {snapshot.menus.Count}개 저장 완료 - 다음 진입 시 반영");
            }
            else
            {
                await OfficialMenuCatalog.CacheImagesAsync(photosOnly.menus, destroyCancellationToken);
                _retryAfter[brand] = DateTime.UtcNow.AddMinutes(10);
            }
            // Do not call SetMenus here: neither the visible list nor its scroll moves.
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            _retryAfter[brand] = DateTime.UtcNow.AddMinutes(10);
            Debug.LogWarning($"[LatestMenu] {brand} 갱신 실패 - 기존 메뉴 유지: {exception.Message}");
        }
        finally { _refreshingBrands.Remove(brand); }
    }

    private Sprite CachedImage(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (_cachedImages.TryGetValue(url, out Sprite cached)) return cached;
        byte[] bytes = OfficialMenuCatalog.ReadImage(url);
        if (bytes == null) return null;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, true)) throw new InvalidDataException("Invalid cached photo.");
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            _cachedImages.Add(url, sprite);
            return sprite;
        }
        catch (Exception)
        {
            Destroy(texture);
            return null;
        }
    }

    private void ReleaseCachedImages()
    {
        foreach (Sprite sprite in _cachedImages.Values)
        {
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
        _cachedImages.Clear();
    }

    // 사용자가 제공한 표시용 메뉴/가격입니다. 실시간 시즌 메뉴나 지점별 가격 조회가 아닙니다.
    private List<LatestMenuItem> CreateTenPercentMenus()
    {
        return new List<LatestMenuItem>
        {
            new LatestMenuItem("솔티 글레이즈드 라떼", "솔티 글레이즈드 풍미의 라떼", "3,800원", saltyGlazedLatteImage),
            new LatestMenuItem("아보카도 바나나 프레치노", "아보카도와 바나나를 담은 프레치노", "5,000원", avocadoBananaFrecinoImage),
            new LatestMenuItem("꾸덕 커피 빙수", "꾸덕한 커피 빙수 메뉴", "5,300원", thickCoffeeBingsuImage),
            new LatestMenuItem("더티땅콩라떼", "고소한 땅콩 풍미의 라떼", "3,900원", dirtyPeanutLatteImage)
        };
    }

    private static List<LatestMenuItem> CopyMenus(List<LatestMenuItem> source)
    {
        var copy = new List<LatestMenuItem>(source.Count);
        foreach (LatestMenuItem m in source)
        {
            if (m != null) copy.Add(new LatestMenuItem(m.menuName, m.description, m.price, m.image));
        }
        return copy;
    }

    // 변경점: Inspector 메뉴 목록이 비어 있으면 지금 코드 기본 메뉴(사진 포함)로 채운다. 에디터 Fix가 부른다.
    public void FillMenuListsIfEmpty()
    {
        if (megaMenus.Count == 0)
        {
            megaMenus = CreateInitialMenus(housemilkLatteImage, orzoLatteImage, goldenappleBlackteaImage, bamgogumaBreadImage);
        }

        if (tenPercentMenus.Count == 0)
        {
            tenPercentMenus = CreateTenPercentMenus();
        }
    }

    public void HideMenus()
    {
        SetMenus(null);
    }

    public void UpdatePrices(StoreMenuPrice[] prices)
    {
        // Keep compatibility with the existing loader, but never overwrite manual prices.
        if (prices != null && prices.Length > 0)
            Debug.Log("[LatestMenu] 자동 가격 응답 무시 - 수동 가격표 유지");
    }

    private static bool TryFormatPrice(string rawPrice, out string formattedPrice)
    {
        formattedPrice = null;
        if (string.IsNullOrWhiteSpace(rawPrice))
        {
            return false;
        }

        string numericPrice = rawPrice.Trim();
        if (numericPrice.EndsWith("원", StringComparison.Ordinal))
        {
            numericPrice = numericPrice.Substring(0, numericPrice.Length - 1).Trim();
        }

        numericPrice = numericPrice.Replace(",", string.Empty);
        if (!long.TryParse(numericPrice, NumberStyles.Integer, CultureInfo.InvariantCulture, out long price)
            || price <= 0)
        {
            return false;
        }

        formattedPrice = price.ToString("N0", CultureInfo.InvariantCulture) + "원";
        return true;
    }

    public static bool IsMegaCoffee(string placeName)
    {
        if (string.IsNullOrWhiteSpace(placeName))
        {
            return false;
        }

        string normalized = placeName.ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("\t", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        return normalized.Contains("메가MGC커피")
            || normalized.Contains("메가커피")
            || normalized.Contains("MEGAMGCCOFFEE")
            || normalized.Contains("MEGACOFFEE")
            || normalized == "메가"
            || normalized == "MEGA";
    }
    public static bool IsTenPercentCoffee(string placeName)
    {
        if (string.IsNullOrWhiteSpace(placeName)) return false;

        // 간판의 TENPERCENT COFFEE 표기와 한글 지점명을 같은 브랜드로 처리합니다.
        string normalized = System.Text.RegularExpressions.Regex.Replace(
            placeName.ToUpperInvariant(), @"\s+", string.Empty);
        return normalized.Contains("텐퍼센트")
            || normalized.Contains("TENPERCENT")
            || normalized.Contains("10%COFFEE")
            || normalized.Contains("10PERCENTCOFFEE");
    }

    public static string GetSupportedBrandName(string placeName)
    {
        if (IsMegaCoffee(placeName)) return MegaCoffeeBrand;
        if (IsTenPercentCoffee(placeName)) return TenPercentCoffeeBrand;
        return null;
    }

    public void SetMenus(List<LatestMenuItem> menus)
    {
        if (cards == null)
        {
            return;
        }

        for (int i = 0; i < cards.Length; i++)
        {
            LatestMenuCardView card = cards[i];
            if (card == null)
            {
                continue;
            }

            bool hasMenu = i < OfficialMenuCatalog.MaximumMenus && menus != null && i < menus.Count && menus[i] != null;
            if (card.cardRoot != null)
            {
                card.cardRoot.SetActive(hasMenu);
            }

            if (!hasMenu)
            {
                continue;
            }

            LatestMenuItem menu = menus[i];
            if (card.menuName != null)
            {
                card.menuName.text = menu.menuName;
            }

            if (card.menuDescription != null)
            {
                card.menuDescription.text = menu.description;
            }

            if (card.priceText != null)
            {
                card.priceText.text = menu.price;
            }

            // 변경점: 사진이 없는 메뉴는 이전 카드 사진이 남지 않게 사진을 숨긴다
            if (card.menuImage != null)
            {
                card.menuImage.sprite = menu.image;
                card.menuImage.enabled = menu.image != null;
            }
        }

        // 변경점: 메뉴가 바뀌면 목록을 맨 위로
        if (menuScroll != null && menuScroll.content != null)
        {
            menuScroll.StopMovement();
            menuScroll.content.anchoredPosition = new Vector2(menuScroll.content.anchoredPosition.x, 0f);
        }
    }
}
