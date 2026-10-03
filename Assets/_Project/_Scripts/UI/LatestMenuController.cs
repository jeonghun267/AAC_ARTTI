using System;
using System.Collections.Generic;
using System.Globalization;
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

    // 변경점: 브랜드별 최신 메뉴 목록(Inspector에서 추가/수정). 비어 있으면 아래 코드 기본 메뉴 4개를 쓴다.
    //   카드는 최대 cards 수(HelpDetailPanel Fix가 20장으로 만든다)까지 보이고, 넘치는 카드는 스크롤로 본다.
    [Header("Menu Lists (Inspector에서 추가)")]
    [SerializeField] private List<LatestMenuItem> megaMenus = new List<LatestMenuItem>();
    [SerializeField] private List<LatestMenuItem> tenPercentMenus = new List<LatestMenuItem>();
    [Tooltip("메뉴가 바뀌면 맨 위로 올린다")]
    [SerializeField] private ScrollRect menuScroll;

    [Header("TTS")]
    [Tooltip("메뉴 카드를 누르면 \"메뉴 이름 주세요\"를 읽는다")]
    [SerializeField] private TTSManager tts;

    // UI 표시용 고정 가을 시즌 메뉴 데이터입니다. 가격은 사용자가 지정한 표시 금액입니다.
    [SerializeField] private List<LatestMenuItem> testMenus = CreateInitialMenus();

    private string _lastEvaluatedPlaceName = string.Empty;
    private bool _hasEvaluatedPlace;

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
    private void SpeakMenu(int index)
    {
        if (tts == null || index >= cards.Length || cards[index]?.menuName == null) return;
        string menuName = cards[index].menuName.text?.Trim();
        if (string.IsNullOrEmpty(menuName)) return;
        tts.Speak(menuName + " 주세요");
    }

    private void OnEnable()
    {
        string placeName = PlaceContext.PlaceName;
        if (!_hasEvaluatedPlace || !string.Equals(_lastEvaluatedPlaceName, placeName, StringComparison.Ordinal))
        {
            UpdateForPlace(placeName);
        }
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

        _lastEvaluatedPlaceName = placeName ?? string.Empty;
        _hasEvaluatedPlace = true;

        bool isMega = IsMegaCoffee(placeName);
        Debug.Log($"[LatestMenu DEBUG] IsMegaCoffee = {isMega}");

        if (isMega)
        {
            ShowMegaMenu();
            if (storePriceLoader != null)
            {
                storePriceLoader.LoadPricesForStore(placeName);
            }
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
        // 변경점: Inspector 목록이 있으면 그것(복사본, 가격 갱신이 원본을 바꾸지 않게), 없으면 코드 기본 메뉴
        testMenus = megaMenus.Count > 0
            ? CopyMenus(megaMenus)
            : CreateInitialMenus(
                housemilkLatteImage,
                orzoLatteImage,
                goldenappleBlackteaImage,
                bamgogumaBreadImage);
        SetMenus(testMenus);
    }

    public void ShowTenPercentMenu()
    {
        testMenus = tenPercentMenus.Count > 0 ? CopyMenus(tenPercentMenus) : CreateTenPercentMenus();
        SetMenus(testMenus);
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
        // 늦게 도착한 메가커피 가격 응답으로 텐퍼센트 표시 가격을 변경하지 않습니다.
        if (IsTenPercentCoffee(_lastEvaluatedPlaceName)) return;

        if (prices == null || testMenus == null || cards == null)
        {
            return;
        }

        foreach (StoreMenuPrice priceData in prices)
        {
            if (priceData == null || string.IsNullOrWhiteSpace(priceData.menuName))
            {
                continue;
            }

            if (!TryFormatPrice(priceData.price, out string formattedPrice))
            {
                continue;
            }

            for (int i = 0; i < testMenus.Count && i < cards.Length; i++)
            {
                LatestMenuItem menu = testMenus[i];
                if (menu == null || !string.Equals(
                        menu.menuName?.Trim(),
                        priceData.menuName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                menu.price = formattedPrice;
                if (cards[i] != null && cards[i].priceText != null)
                {
                    cards[i].priceText.text = formattedPrice;
                }

                Debug.Log($"[StorePrice] {menu.menuName} → {formattedPrice}");
                break;
            }
        }
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

            bool hasMenu = menus != null && i < menus.Count && menus[i] != null;
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
