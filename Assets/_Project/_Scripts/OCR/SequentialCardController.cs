using System.Collections;
using UnityEngine;

public class SequentialCardController : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float interval = 0.5f; // 카드 사이의 간격 (0.5초 추천)
    [SerializeField] private float fadeSpeed = 2.0f; // 스르륵 나타나는 속도

    private CanvasGroup[] cardGroups;

    private void Awake()
    {
        // 1. 자식들에게서 CanvasGroup을 다 찾아옵니다.
        // (주의: 부모인 HelpDetailPanel 자신에게 CanvasGroup이 있다면 그것도 포함되니 조심!)
        cardGroups = GetComponentsInChildren<CanvasGroup>();
    }

    private void OnEnable()
    {
        // 2. 패널이 켜질 때 모든 카드를 일단 투명하게(0) 만듭니다.
        foreach (var cg in cardGroups)
        {
            cg.alpha = 0;
        }

        // 3. 순차적으로 나타나는 코루틴 시작
        StopAllCoroutines();
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        foreach (var cg in cardGroups)
        {
            // 부모 자신에게 CanvasGroup이 붙어있는 경우 건너뛰기 (자식들만 나오게)
            if (cg.gameObject == gameObject) continue;

            StartCoroutine(FadeIn(cg)); // 카드 하나 페이드인 시작
            yield return new WaitForSeconds(interval); // 다음 카드까지 대기
        }
    }

    private IEnumerator FadeIn(CanvasGroup cg)
    {
        while (cg.alpha < 1.0f)
        {
            cg.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }
        cg.alpha = 1.0f;
    }
}