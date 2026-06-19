using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 이 스크립트는 OrderPanel 오브젝트에 붙이세요.
// OrderPanel이 켜지면 → 온도(TempPanel)부터 시작.
// 온도 고르면 → 음료(MenuPanel), 음료 고르면 → 사이즈(SizePanel),
// 사이즈까지 고르면 → 주문 문장을 TTS로 읽어줍니다.
public class OrderManager : MonoBehaviour
{
    [Header("연결 (인스펙터에서 드래그)")]
    [SerializeField] private GeminiMenuManager geminiManager;
    [SerializeField] private TTSManager ttsManager;

    [Header("단계 패널 (3개 드래그)")]
    [SerializeField] private GameObject tempPanel;   // 온도
    [SerializeField] private GameObject menuPanel;   // 음료
    [SerializeField] private GameObject sizePanel;   // 사이즈

    [Header("음료 (Gemini 자동 생성)")]
    [Tooltip("음료 카드 프리팹. 인사 카드와 같은 Button 프리팹을 그대로 써도 됩니다.")]
    [SerializeField] private GameObject drinkCardPrefab;
    [Tooltip("음료 카드가 들어갈 영역. MenuPanel 아래 Grid Layout Group이 붙은 오브젝트를 연결하세요.")]
    [SerializeField] private Transform drinkParent;
    [Range(2, 8)]
    [SerializeField] private int drinkCount = 6;
    [Tooltip("켤 때마다 메뉴 새로 생성(체크) / 처음 한 번만(해제, API 절약)")]
    [SerializeField] private bool regenerateEveryTime = false;

    [Header("안내/결과 표시 (선택)")]
    [Tooltip("각 단계 안내 문구를 보여줄 텍스트. (예: HeaderTitle) 필요 없으면 비워두세요.")]
    [SerializeField] private TMP_Text stepTitle;
    [Tooltip("최종 주문 문장을 보여줄 텍스트. 필요 없으면 비워두세요.")]
    [SerializeField] private TMP_Text orderText;

    // 현재 선택값
    private string selectedTemp = "";
    private string selectedDrink = "";
    private string selectedSize = "";

    private bool hasGeneratedDrinks = false;

    void OnEnable()
    {
        ResetSelection();
        GoToTemp();                                  // 온도 단계부터 시작
        if (regenerateEveryTime || !hasGeneratedDrinks)
            GenerateDrinks();                        // 음료는 미리 받아둠(다음 단계에서 바로 보이게)
    }

    private void ResetSelection()
    {
        selectedTemp = "";
        selectedDrink = "";
        selectedSize = "";
        if (orderText != null) orderText.text = "";
    }

    // ───────── 단계 전환 ─────────
    private void ShowOnly(GameObject panel)
    {
        if (tempPanel != null) tempPanel.SetActive(panel == tempPanel);
        if (menuPanel != null) menuPanel.SetActive(panel == menuPanel);
        if (sizePanel != null) sizePanel.SetActive(panel == sizePanel);
    }

    private void GoToTemp()
    {
        ShowOnly(tempPanel);
        if (stepTitle != null) stepTitle.text = "온도를 골라주세요";
    }

    private void GoToMenu()
    {
        ShowOnly(menuPanel);
        if (stepTitle != null) stepTitle.text = "음료를 골라주세요";
    }

    private void GoToSize()
    {
        ShowOnly(sizePanel);
        if (stepTitle != null) stepTitle.text = "사이즈를 골라주세요";
    }

    // ───────── 고정 버튼이 호출하는 함수 (인스펙터 OnClick에 연결) ─────────

    // 온도 버튼: "차가운" / "따뜻한"
    public void SelectTemp(string temp)
    {
        selectedTemp = temp;
        Debug.Log($"[Order] 온도 선택: {temp}");
        GoToMenu();                                  // 다음 단계: 음료
    }

    // 사이즈 버튼: "작은" / "중간" / "큰"
    public void SelectSize(string size)
    {
        selectedSize = size;
        Debug.Log($"[Order] 사이즈 선택: {size}");
        TrySpeakOrder();                             // 마지막 단계: 문장 출력
    }

    // 음료 카드(자동 생성)가 코드에서 호출하는 함수
    public void SelectDrink(string drink)
    {
        selectedDrink = drink;
        Debug.Log($"[Order] 음료 선택: {drink}");
        GoToSize();                                  // 다음 단계: 사이즈
    }

    // ───────── 셋 다 골랐으면 문장 조립 후 TTS ─────────
    private void TrySpeakOrder()
    {
        if (string.IsNullOrEmpty(selectedTemp) ||
            string.IsNullOrEmpty(selectedDrink) ||
            string.IsNullOrEmpty(selectedSize))
            return;

        // 예: "작은 사이즈의 차가운 아메리카노 주세요."
        string sentence = $"{selectedSize} 사이즈의 {selectedTemp} {selectedDrink} 주세요.";

        if (stepTitle != null) stepTitle.text = "주문";
        if (orderText != null) orderText.text = sentence;
        Debug.Log($"[Order] 주문 문장: {sentence}");

        if (ttsManager != null) ttsManager.Speak(sentence);
    }

    // ───────── 음료 목록 자동 생성 ─────────
    public void GenerateDrinks()
    {
        string prompt =
            "발달장애인이 사용하는 AAC 의사소통 앱이야. 사용자가 카페에서 음료를 주문할 거야. " +
            $"카페에서 주문할 수 있는 대표 음료 이름 {drinkCount}개를 한국어로 만들어줘. " +
            "'아메리카노', '카페라떼'처럼 음료 이름만 넣어. " +
            "온도(차가운/따뜻한)나 사이즈(작은/중간/큰) 같은 수식어, '주세요' 같은 말은 절대 넣지 마. " +
            "음료 이름끼리 겹치면 안 돼. " +
            "설명이나 코드블록 없이 아래 JSON 형식으로만 응답해: " +
            "{\"options\": [\"음료1\", \"음료2\"]}";

        Debug.Log("[Order] 음료 목록 요청");
        geminiManager.GenerateList(prompt, OnDrinksResult);
    }

    private void OnDrinksResult(List<string> drinks)
    {
        if (drinks == null || drinks.Count == 0)
        {
            Debug.LogError("[Order] 음료 응답 실패 (null)");
            return;
        }

        hasGeneratedDrinks = true;

        if (drinks.Count > drinkCount)
            drinks = drinks.GetRange(0, drinkCount);

        // 기존 음료 카드 비우기
        foreach (Transform child in drinkParent)
            Destroy(child.gameObject);

        foreach (string drink in drinks)
        {
            if (string.IsNullOrWhiteSpace(drink)) continue;

            GameObject card = Instantiate(drinkCardPrefab, drinkParent);
            card.SetActive(true);

            TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = drink;

            Button btn = card.GetComponent<Button>();
            if (btn != null)
            {
                string captured = drink;                 // 클로저 캡처
                btn.onClick.AddListener(() => SelectDrink(captured));
            }
        }

        Debug.Log($"[Order] 음료 카드 {drinks.Count}개 생성 완료");
    }
}