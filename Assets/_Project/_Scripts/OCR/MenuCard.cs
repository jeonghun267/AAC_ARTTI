using UnityEngine;
using TMPro;

public class MenuCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI menuText; // 카드에 보일 텍스트 컴포넌트

    // 이 함수를 호출하면 카드의 글씨가 바뀝니다.
    public void SetupMenu(string menuName)
    {
        if (menuText != null)
        {
            menuText.text = menuName + " 주문하고 싶어요.";
        }
    }
}