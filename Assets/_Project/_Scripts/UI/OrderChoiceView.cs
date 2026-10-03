using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    public enum OrderChoiceGroup { Temperature, Drink, Size }

    // 주문하기 화면(한 화면 모드)의 선택 카드 하나(온도/음료/사이즈). 누르기는 Button OnClick(OrderManager.SelectXxx)이 그대로 하고,
    // 이 컴포넌트는 OrderManager가 알려 주는 선택 상태에 맞춰 바탕 그림과 선택 빛 테두리만 바꾼다.
    //   value = 버튼 OnClick이 OrderManager에 넘기는 글(예: "따뜻한", "아메리카노", "작은"). 에디터 스크립트(ARFieldOrderLayout)가 채운다.
    //   메뉴 카드(OrderMenuListView가 복제)는 실행 중에 Bind로 value/이름/사진을 바꾼다.
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

        // 변경점: 메뉴 카드만 쓴다(사진은 둥근 칸을 꽉 채우게 AspectRatioFitter EnvelopeParent)
        [Header("메뉴 카드 (실행 중에 채움)")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image photo;
        [SerializeField] private AspectRatioFitter photoFitter;

        public OrderChoiceGroup Group => group;
        public string Value => value;

        public void SetSelected(bool selected)
        {
            if (background != null && normalSprite != null)
                background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
            if (selectedGlow != null) selectedGlow.SetActive(selected);
        }

        // 변경점: 메뉴 카드 내용 바꾸기(이름 = OrderManager에 넘길 글)
        public void Bind(string newValue, Sprite photoSprite)
        {
            value = newValue ?? string.Empty;
            if (label != null) label.text = value;
            if (photo != null)
            {
                photo.sprite = photoSprite;
                photo.enabled = photoSprite != null;
            }
            if (photoFitter != null && photoSprite != null && photoSprite.rect.height > 0f)
                photoFitter.aspectRatio = photoSprite.rect.width / photoSprite.rect.height;
        }
    }
}
