using UnityEngine;
using UnityEngine.UI;

namespace Artti.UI
{
    // 세그먼트 탭: 글래스 pill을 선택된 탭 위치로 부드럽게 슬라이드시켜
    // 현재 어떤 탭에 있는지 표시한다. 탭 버튼 클릭을 직접 구독하므로
    // ReportView 등 다른 로직과 독립적으로 동작한다. (프레임레이트 독립 보간)
    [DisallowMultipleComponent]
    public class TabSlider : MonoBehaviour
    {
        [SerializeField] private RectTransform pill;     // 슬라이드하는 표시기
        [SerializeField] private Button[] tabs;          // 탭 버튼들 (순서 = positions와 동일)
        [SerializeField] private Vector2[] positions;    // 각 탭에서 pill의 위치
        [SerializeField] private float lerpSpeed = 14f;  // 클수록 빠르게 따라붙음
        [SerializeField] private int defaultIndex = 0;   // 시작 선택 탭

        private Vector2 _target;

        private void Awake()
        {
            if (tabs == null) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i; // 클로저 캡처 고정
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => SelectTab(idx));
            }
        }

        private void OnEnable()
        {
            if (positions != null && positions.Length > 0)
            {
                int i = Mathf.Clamp(defaultIndex, 0, positions.Length - 1);
                _target = positions[i];
                if (pill != null) pill.anchoredPosition = positions[i];
            }
        }

        public void SelectTab(int index)
        {
            if (positions == null || index < 0 || index >= positions.Length) return;
            _target = positions[index];
        }

        private void Update()
        {
            if (pill == null) return;
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            pill.anchoredPosition = Vector2.Lerp(pill.anchoredPosition, _target, t);
        }
    }
}
