using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // AR 현장모드 결과 화면(HJ 시안)의 "인식 결과" 카드에서 OcrTestController가 채우지 않는 칸을 채운다. 보여 주기만 한다.
    //   분류 칩 = 지점 전체 이름("메가MGC커피 인천송도점", 글 길이에 맞춰 칩 폭이 바뀜), 큰 이름 = 장소명에서 지점명을 뺀 이름("메가MGC커피"),
    //   아래 세 줄 = 지점 전체 이름 / 분류 의사소통 문구 / 분류별 도움 카드.
    //   1줄을 누르면 장소 이름을 읽어 주고, 2·3줄은 '도움 카드' 버튼과 같은 일을 한다(그 버튼의 클릭을 그대로 부른다).
    // 결과를 못 찾은 경우(PlaceContext 비어 있음)는 OcrTestController가 쓴 글("인식 실패" 등)을 그대로 두고 칩만 숨긴다.
    // 분류 아이콘: OcrTestController는 Resources.Load("cafe")로 찾는데 그림은 Resources/AAC/ 아래라 비어 있게 된다.
    //   비어 있을 때만 여기서 분류에 맞는 그림(Resources/AAC/...)을 넣는다.
    // 결과 패널이 켜질 때와, 켜진 채로 장소가 바뀐 프레임에만 글을 다시 쓴다.
    public class ARResultView : MonoBehaviour
    {
        [SerializeField] private GameObject chip;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private TMP_Text nameText;           // OcrTestController.categoryText와 같은 글
        [SerializeField] private Image iconImage;             // OcrTestController.aacImageComponent와 같은 칸
        [SerializeField] private TMP_Text[] rowTexts = new TMP_Text[3];
        [SerializeField] private Button[] rowButtons = new Button[3];
        [SerializeField] private Button helpButton;           // '도움 카드'
        [SerializeField] private TTSManager tts;
        [SerializeField] private KakaoLocalSearcher kakao;

        private string _shownCategory;
        private string _shownPlace;
        private object _shownStore;
        private TextChipAutoWidth _chipFit;

        public void Setup(GameObject chipRoot, TMP_Text chipLabel, TMP_Text name, Image icon, TMP_Text[] rows, Button[] rowHits,
            Button help, TTSManager speaker, KakaoLocalSearcher searcher)
        {
            iconImage = icon;
            chip = chipRoot;
            chipText = chipLabel;
            nameText = name;
            rowTexts = rows;
            rowButtons = rowHits;
            helpButton = help;
            tts = speaker;
            kakao = searcher;
        }

        private void Awake()
        {
            if (chip != null) _chipFit = chip.GetComponent<TextChipAutoWidth>();
            if (rowButtons == null) return;
            if (rowButtons.Length > 0 && rowButtons[0] != null) rowButtons[0].onClick.AddListener(SpeakPlace);
            for (int i = 1; i < rowButtons.Length; i++)
                if (rowButtons[i] != null) rowButtons[i].onClick.AddListener(OpenHelp);
        }

        private void OnEnable() => Refresh();

        private void LateUpdate()
        {
            var store = kakao != null ? kakao.SelectedPlace : null;
            if (!ReferenceEquals(_shownCategory, PlaceContext.Category) ||
                !ReferenceEquals(_shownPlace, PlaceContext.PlaceName) ||
                !ReferenceEquals(_shownStore, store))
                Refresh();
        }

        private void Refresh()
        {
            _shownCategory = PlaceContext.Category;
            _shownPlace = PlaceContext.PlaceName;
            var store = kakao != null ? kakao.SelectedPlace : null;
            _shownStore = store;

            bool has = PlaceContext.HasContext;
            string category = PlaceContext.Category;
            string fullName = store != null && !string.IsNullOrEmpty(store.place_name) ? store.place_name : PlaceContext.PlaceName;

            if (chip != null) chip.SetActive(has);
            string chipLabel = has ? ChipLabel(store != null ? store.place_name : null, PlaceContext.PlaceName, category) : string.Empty;
            if (_chipFit != null) _chipFit.SetText(chipLabel);
            else if (chipText != null) chipText.text = chipLabel;
            if (has && nameText != null) nameText.text = string.IsNullOrEmpty(fullName) ? category : BrandOf(fullName);
            if (has && iconImage != null && iconImage.sprite == null)
            {
                string iconPath = IconPath(category);
                var icon = iconPath != null ? Resources.Load<Sprite>(iconPath) : null;
                if (icon != null) { iconImage.sprite = icon; iconImage.color = Color.white; }
            }

            SetRow(0, !string.IsNullOrEmpty(fullName) ? fullName : (has ? category : "장소 정보 없음"));
            SetRow(1, has ? category + " 관련 의사소통 문구" : "의사소통 문구");
            SetRow(2, HelpRowText(category));
        }

        private void SetRow(int i, string text)
        {
            if (rowTexts != null && i < rowTexts.Length && rowTexts[i] != null) rowTexts[i].text = text;
        }

        // 변경점: 칩 = 지점 이름(카카오 "텐퍼센트커피 성모힐병원점" -> "텐퍼센트 성모힐병원점", 브랜드는 줄임말).
        //   지점을 못 찾았으면(위치 없음 등) 인식한 브랜드("텐퍼센트"), 그것도 아니면 분류("카페").
        //   사진 글자에서 맞은 일반 낱말(예: COFFEE)은 칩에 쓰지 않는다.
        private static string ChipLabel(string storeName, string placeName, string category)
        {
            if (!string.IsNullOrWhiteSpace(storeName)) return ShortBrand(storeName.Trim());
            string brand = LatestMenuController.GetSupportedBrandName(placeName);
            return brand != null ? ShortBrand(brand) : category;
        }

        // 칩에 보일 브랜드 줄임말(앞부분만 바꾼다)
        private static readonly (string from, string to)[] BrandShortNames =
        {
            ("메가MGC커피", "메가커피"),
            ("텐퍼센트커피", "텐퍼센트"),
            ("텐퍼센트 커피", "텐퍼센트"),
        };

        private static string ShortBrand(string name)
        {
            foreach (var (from, to) in BrandShortNames)
                if (name.StartsWith(from, System.StringComparison.Ordinal)) return to + name.Substring(from.Length);
            return name;
        }

        // "메가MGC커피 인천송도점" -> "메가MGC커피". 마지막 낱말이 "점"으로 끝나는 지점명일 때만 뗀다.
        private static string BrandOf(string placeName)
        {
            string s = placeName.Trim();
            int space = s.LastIndexOf(' ');
            if (space > 0 && s.EndsWith("점")) return s.Substring(0, space).Trim();
            return s;
        }

        private static string IconPath(string category)
        {
            switch (category)
            {
                case "카페": return "AAC/cafe";
                case "약국": return "AAC/pharmacy";
                case "음식점": return "AAC/restaurant";
                case "편의점": return "AAC/convenience_store";
                default: return null;
            }
        }

        private static string HelpRowText(string category)
        {
            switch (category)
            {
                case "카페":
                case "음식점": return "메뉴, 주문, 결제 도움 카드";
                case "편의점": return "물건 찾기, 결제 도움 카드";
                case "약국": return "증상 말하기, 약 사기 도움 카드";
                default: return "도움 카드 보기";
            }
        }

        private void SpeakPlace()
        {
            if (tts == null || rowTexts == null || rowTexts.Length == 0 || rowTexts[0] == null) return;
            tts.Speak(rowTexts[0].text);
        }

        private void OpenHelp()
        {
            if (helpButton != null) helpButton.onClick.Invoke();
        }
    }
}
