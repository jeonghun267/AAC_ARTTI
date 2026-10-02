using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    public enum OrderChoiceGroup { Temperature, Drink, Size }

    // 주문하기 화면(한 화면 모드)의 선택 카드 하나(온도/음료/사이즈). 누르기는 Button OnClick(OrderManager.SelectXxx)이 그대로 하고,
    // 이 컴포넌트는 OrderManager가 알려 주는 선택 상태에 맞춰 바탕 그림과 선택 빛 테두리만 바꾼다.
    //   value = 버튼 OnClick이 OrderManager에 넘기는 글(예: "따뜻한", "아메리카노", "작은"). 에디터 스크립트(ARFieldOrderLayout)가 채운다.
    [DisallowMultipleComponent]
    public sealed class OrderChoiceView : MonoBehaviour
    {
        [SerializeField] private OrderChoiceGroup group;
        [SerializeField] private string value = "";
        [SerializeField] private Image background;
        [SerializeField] private Sprite normalSprite;
        [Tooltip("비우면 고른 뒤에도 바탕은 그대로(온도 카드처럼 색이 정해진 카드)")]
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private GameObject selectedGlow;

        public OrderChoiceGroup Group => group;
        public string Value => value;

        public void SetSelected(bool selected)
        {
            if (background != null && normalSprite != null)
                background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
            if (selectedGlow != null) selectedGlow.SetActive(selected);
        }
    }
}
