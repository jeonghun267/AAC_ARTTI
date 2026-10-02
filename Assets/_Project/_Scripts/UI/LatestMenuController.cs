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

    // UI 표시용 고정 가을 시즌 메뉴 데이터입니다. 가격은 사용자가 지정한 표시 금액입니다.
    [SerializeField] private List<LatestMenuItem> testMenus = CreateInitialMenus();

    private string _lastEvaluatedPlaceName = string.Empty;
    private bool _hasEvaluatedPlace;

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
        testMenus = CreateInitialMenus(
            housemilkLatteImage,
            orzoLatteImage,
            goldenappleBlackteaImage,
            bamgogumaBreadImage);
        SetMenus(testMenus);
    }

    public void ShowTenPercentMenu()
    {
        // 사용자가 제공한 표시용 메뉴/가격입니다. 실시간 시즌 메뉴나 지점별 가격 조회가 아닙니다.
        testMenus = new List<LatestMenuItem>
        {
            new LatestMenuItem("솔티 글레이즈드 라떼", "솔티 글레이즈드 풍미의 라떼", "3,800원", saltyGlazedLatteImage),
            new LatestMenuItem("아보카도 바나나 프레치노", "아보카도와 바나나를 담은 프레치노", "5,000원", avocadoBananaFrecinoImage),
            new LatestMenuItem("꾸덕 커피 빙수", "꾸덕한 커피 빙수 메뉴", "5,300원", thickCoffeeBingsuImage),
            new LatestMenuItem("더티땅콩라떼", "고소한 땅콩 풍미의 라떼", "3,900원", dirtyPeanutLatteImage)
        };
        SetMenus(testMenus);
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

            if (card.menuImage != null && menu.image != null)
            {
                card.menuImage.sprite = menu.image;
            }
        }
    }
}
