using UnityEngine;

public class OrderManager : MonoBehaviour
{
    [Header("UI 패널 연결")]
    public GameObject tempPanel; // 1. 온도 선택 패널
    public GameObject menuPanel; // 2. 메뉴 선택 패널
    public GameObject sizePanel; // 3. 사이즈 선택 패널

    [Header("TTS 매니저 연결")]
    public TTSManager ttsManager;

    // 사용자가 선택한 단어를 기억할 빈 바구니
    private string selectedTemp = "";
    private string selectedMenu = "";
    private string selectedSize = "";

    private void OnEnable()
    {
        // 주문 화면이 켜질 때마다 무조건 '온도 선택' 화면으로 초기화
        ResetOrder();
    }

    // 1. 온도 버튼을 눌렀을 때 호출될 함수
    public void SelectTemperature(string temp)
    {
        selectedTemp = temp;           // 예: "따뜻한" 저장
        tempPanel.SetActive(false);    // 온도 화면 끄기
        menuPanel.SetActive(true);     // 메뉴 화면 켜기
    }

    // 2. 메뉴 버튼을 눌렀을 때 호출될 함수
    public void SelectMenu(string menu)
    {
        selectedMenu = menu;           // 예: "아메리카노" 저장
        menuPanel.SetActive(false);    // 메뉴 화면 끄기
        sizePanel.SetActive(true);     // 사이즈 화면 켜기
    }

    // 3. 사이즈 버튼을 눌렀을 때 호출될 함수 (마지막 단계)
    public void SelectSize(string size)
    {
        selectedSize = size;           // 예: "큰" 저장

        // 최종 문장 조립 (예: "큰 사이즈의 따뜻한 아메리카노 주세요")
        string finalSentence = $"{selectedSize} 사이즈의 {selectedTemp} {selectedMenu} 주세요";

        Debug.Log("[주문 조합 완료] " + finalSentence);

        // TTS로 읽어주기
        if (ttsManager != null)
        {
            ttsManager.Speak(finalSentence);
        }
        else
        {
            Debug.LogWarning("TTSManager가 연결되지 않았습니다.");
        }

        // 원하신다면 여기서 결제 패널로 넘어가거나, 3초 뒤에 다시 초기화하는 로직을 추가할 수 있습니다.
    }

    // 주문 초기화 함수 (뒤로가기 버튼 등에 연결)
    public void ResetOrder()
    {
        selectedTemp = "";
        selectedMenu = "";
        selectedSize = "";

        if (tempPanel != null) tempPanel.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (sizePanel != null) sizePanel.SetActive(false);
    }
}