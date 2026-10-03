using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 주문하기 화면 2번(메뉴를 선택해주세요) 칸. 도움 화면 최신 메뉴(LatestMenuController)와 같은 메뉴를 사진과 이름만 카드로 보여 준다.
    //   이름의 ICE/HOT은 뺀다(온도는 3번에서 고른다). 빼고 같아진 이름((HOT)헛개리카노 / (ICE)헛개리카노)은 한 장만 둔다.
    //   메뉴를 모르는 가게(메가/텐퍼센트 밖)는 fallbackMenus(예전 고정 음료)를 보여 준다.
    //   카드는 꺼 둔 template을 복제해 다시 쓰고(지우지 않음), 누르면 OrderManager.SelectDrink(이름).
    //   OrderManager가 화면을 켤 때 Rebuild를 부른다. 자리/그림/연결은 에디터 스크립트(ARFieldOrderLayout)가 채운다.
    [DisallowMultipleComponent]
    public sealed class OrderMenuListView : MonoBehaviour
    {
        [SerializeField] private LatestMenuController latestMenu;   // 도움 화면 최신 메뉴(메뉴 목록의 원본)
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private OrderChoiceView template;          // 꺼 둔 메뉴 카드
        [SerializeField] private Sprite placeholderImage;           // 사진 없는 메뉴
        [SerializeField] private List<LatestMenuItem> fallbackMenus = new List<LatestMenuItem>();

        private readonly List<OrderChoiceView> _cards = new List<OrderChoiceView>();
        private readonly List<(string name, Sprite image)> _items = new List<(string, Sprite)>();
        private readonly HashSet<string> _keys = new HashSet<string>();
        private OrderManager _owner;

        // 지금 보이는 첫 메뉴 이름(에디터 캡처에서 고른 모양을 만들 때 쓴다)
        public string FirstValue => _items.Count > 0 ? _items[0].name : null;

        public void Rebuild(OrderManager owner, string placeName)
        {
            _owner = owner;
            _items.Clear();
            _keys.Clear();
            if (latestMenu != null) AddItems(latestMenu.MenusForPlace(placeName));
            if (_items.Count == 0) AddItems(fallbackMenus);
            if (template == null || content == null) return;

            template.gameObject.SetActive(false);
            while (_cards.Count < _items.Count)
            {
                var card = Instantiate(template, content);
                if (card.TryGetComponent<Button>(out var button)) button.onClick.AddListener(() => Select(card));
                _cards.Add(card);
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                bool on = i < _items.Count;
                _cards[i].gameObject.SetActive(on);
                if (on) _cards[i].Bind(_items[i].name, _items[i].image != null ? _items[i].image : placeholderImage);
            }

            // 목록이 바뀌면 맨 왼쪽부터
            if (scroll != null) scroll.StopMovement();
            content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
        }

        private void AddItems(List<LatestMenuItem> menus)
        {
            if (menus == null) return;
            foreach (var menu in menus)
            {
                if (menu == null) continue;
                string name = LatestMenuController.WithoutTemperature(menu.menuName);
                if (name.Length == 0 || !_keys.Add(OfficialMenuCatalog.MenuKey(name))) continue;
                _items.Add((name, menu.image));
            }
        }

        private void Select(OrderChoiceView card)
        {
            if (_owner != null && card != null) _owner.SelectDrink(card.Value);
        }
    }
}
