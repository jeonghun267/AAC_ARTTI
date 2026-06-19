using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardGenerator : MonoBehaviour
{
    [Header("연결 (인스펙터에서 드래그)")]
    [SerializeField] private GeminiMenuManager geminiManager;
    [SerializeField] private TTSManager ttsManager;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardParent;   // 이 패널의 CardArea

    [Header("이 패널 설정")]
    [Tooltip("패널마다 다르게: 인사 / 주문 / 계산 / 편의")]
    [SerializeField] private string category = "인사";
    [Range(2, 6)]
    [SerializeField] private int cardCount = 6;
    [Tooltip("아침/점심/저녁 시간대를 반영 (인사 패널에서 유용)")]
    [SerializeField] private bool useTimeOfDay = false;
    [Tooltip("켤 때마다 새로 생성(체크) / 처음 한 번만 생성(해제, API 절약)")]
    [SerializeField] private bool regenerateEveryTime = false;

    [Header("테스트용 장소")]
    [Tooltip("ResultPanel이 장소를 안 넣어줬을 때 쓸 기본 장소. 예: 카페. 비워두면 장소 없이 생성")]
    [SerializeField] private string fallbackPlace = "";

    private bool hasGenerated = false;

    // 패널이 SetActive(true)로 켜질 때 자동 호출됨
    void OnEnable()
    {
        if (regenerateEveryTime || !hasGenerated)
            Generate();
    }

    // 패널 전환을 SetActive로 안 한다면, 전환 코드에서 이 메서드를 직접 호출하세요
    public void Generate()
    {
        // 1) 장소 맥락: ResultPanel이 넣어준 값 우선, 없으면 테스트용 fallback 사용
        string place = PlaceContext.HasContext ? PlaceContext.Category : fallbackPlace;
        string placeName = PlaceContext.HasContext ? PlaceContext.PlaceName : "";

        string placeContext = "";
        if (!string.IsNullOrEmpty(place))
        {
            placeContext = string.IsNullOrEmpty(placeName)
                ? $"사용자는 지금 '{place}'에 있어. "
                : $"사용자는 지금 '{placeName}'(이)라는 {place}에 있어. ";
        }

        // 2) 시간대 맥락 (옵션)
        string timeContext = useTimeOfDay
            ? $"지금은 {GetTimeOfDay()} 시간대야. 그 시간에 어울리는 표현을 포함해줘. "
            : "";

        // 3) 최종 프롬프트
        string prompt =
            "발달장애인이 사용하는 AAC 의사소통 앱이야. " +
            placeContext +
            $"이 사람이 '{category}' 상황에서 직접 말할 수 있는 짧은 한국어 문장 {cardCount}개를 만들어줘. " +
            timeContext +
            $"오직 '{category}' 행동에 직접 필요한 말만 만들어. " +
            "인사말이나(예: '안녕하세요', '안녕히 계세요') 다른 상황의 표현은 절대 넣지 마. " +
            "각 문장은 12자 이내의 쉽고 공손한 1인칭 표현이어야 해. " +
            "문장끼리 의미가 겹치면 안 돼. 같은 뜻을 다른 말로 바꾼 중복(예: '결제해주세요'와 '계산해주세요')은 금지야. " +
            "각 카드는 서로 다른 의도(요청·질문·확인·감사 등)를 담아 다양하게 만들어줘. " +
            "설명이나 코드블록 없이 아래 JSON 형식으로만 응답해: " +
            "{\"options\": [\"문장1\", \"문장2\"]}";

        Debug.Log($"[CardGenerator] '{category}' 요청 (장소: {(string.IsNullOrEmpty(place) ? "없음" : place)})");
        geminiManager.GenerateList(prompt, OnResult);
    }

    private string GetTimeOfDay()
    {
        int hour = DateTime.Now.Hour;
        if (hour >= 5 && hour < 11) return "아침";
        if (hour >= 11 && hour < 17) return "점심(낮)";
        if (hour >= 17 && hour < 21) return "저녁";
        return "밤";
    }

    private void OnResult(List<string> phrases)
    {
        if (phrases == null || phrases.Count == 0)
        {
            Debug.LogError($"[CardGenerator] '{category}' 응답 실패 (null)");
            return;
        }

        hasGenerated = true;

        if (phrases.Count > 6)
            phrases = phrases.GetRange(0, 6);

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);

        foreach (string phrase in phrases)
        {
            if (string.IsNullOrWhiteSpace(phrase)) continue;

            GameObject card = Instantiate(cardPrefab, cardParent);
            card.SetActive(true);

            TMP_Text label = card.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = phrase;

            Button btn = card.GetComponent<Button>();
            if (btn != null)
            {
                string captured = phrase;
                btn.onClick.AddListener(() =>
                {
                    Debug.Log($"[CardGenerator] 카드 탭: {captured}");
                    if (ttsManager != null) ttsManager.Speak(captured); // TTS 메서드 이름 확인
                });
            }
        }

        Debug.Log($"[CardGenerator] '{category}' 카드 {phrases.Count}개 생성 완료");
    }
}