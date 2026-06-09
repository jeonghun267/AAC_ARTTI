using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ResultPanel의 UI 레이아웃을 자동으로 설정합니다.
/// ResultPanel 오브젝트에 추가하고 인스펙터에서 연결 후
/// 컴포넌트 이름 우클릭 → SetLayout 실행하세요.
/// 설정 완료 후 이 스크립트는 삭제해도 됩니다.
/// </summary>
public class ResultPanelLayoutSetter : MonoBehaviour
{
    [Header("연결할 오브젝트들")]
    [SerializeField] private RectTransform header;
    [SerializeField] private RectTransform capturedImage;
    [SerializeField] private RectTransform resultCard;
    [SerializeField] private RectTransform aacImage;
    [SerializeField] private RectTransform categoryBadge;
    [SerializeField] private RectTransform categoryText;
    [SerializeField] private RectTransform descriptionText;

    [ContextMenu("SetLayout")]
    public void SetLayout()
    {
        SetResultPanel();
        SetHeader();
        SetMainArea();
        CreateTopRow();
        SetDescriptionText();

        Debug.Log("[Layout] 레이아웃 설정 완료!");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    // ResultPanel → 전체 화면 꽉 채우기
    private void SetResultPanel()
    {
        var rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Vertical Layout Group
        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 0f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
    }

    // Header → 상단 고정
    private void SetHeader()
    {
        if (header == null) return;

        var le = header.GetComponent<LayoutElement>();
        if (le == null) le = header.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 120f;
        le.flexibleHeight = 0f;
    }

    // 메인 영역 → Header 아래 가로로 배치 (사진 + ResultCard)
    private void SetMainArea()
    {
        // CapturedImage와 ResultCard를 담을 MainArea 빈 오브젝트 생성
        GameObject mainArea = new GameObject("MainArea");
        mainArea.transform.SetParent(transform, false);

        // Header 다음 순서로 설정
        mainArea.transform.SetSiblingIndex(1);

        var mainRt = mainArea.AddComponent<RectTransform>();
        var mainLe = mainArea.AddComponent<LayoutElement>();
        mainLe.flexibleHeight = 1f;

        // Horizontal Layout Group (사진 | ResultCard 가로 배치)
        var hlg = mainArea.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 20f;
        hlg.padding = new RectOffset(30, 30, 30, 30);

        // CapturedImage → MainArea로 이동
        if (capturedImage != null)
        {
            capturedImage.SetParent(mainArea.transform, false);
            var le = capturedImage.GetComponent<LayoutElement>();
            if (le == null) le = capturedImage.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
        }

        // ResultCard → MainArea로 이동
        if (resultCard != null)
        {
            resultCard.SetParent(mainArea.transform, false);
            var le = resultCard.GetComponent<LayoutElement>();
            if (le == null) le = resultCard.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;

            // ResultCard 안에 Vertical Layout Group
            var vlg = resultCard.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = resultCard.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 20f;
            vlg.padding = new RectOffset(20, 20, 20, 20);
        }
    }

    // TopRow → AacImage + CategoryBadge 가로 배치
    private void CreateTopRow()
    {
        if (resultCard == null || aacImage == null || categoryBadge == null) return;

        // TopRow 빈 오브젝트 생성
        GameObject topRow = new GameObject("TopRow");
        topRow.transform.SetParent(resultCard, false);
        topRow.transform.SetSiblingIndex(0);

        var topRt = topRow.AddComponent<RectTransform>();
        var topLe = topRow.AddComponent<LayoutElement>();
        topLe.preferredHeight = 280f;
        topLe.flexibleWidth = 1f;

        // Horizontal Layout Group
        var hlg = topRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 20f;

        // AacImage → TopRow로 이동
        aacImage.SetParent(topRow.transform, false);
        var aacLe = aacImage.GetComponent<LayoutElement>();
        if (aacLe == null) aacLe = aacImage.gameObject.AddComponent<LayoutElement>();
        aacLe.preferredWidth = 280f;
        aacLe.preferredHeight = 280f;
        aacLe.flexibleWidth = 0f;

        // CategoryBadge → TopRow로 이동
        categoryBadge.SetParent(topRow.transform, false);
        var badgeLe = categoryBadge.GetComponent<LayoutElement>();
        if (badgeLe == null) badgeLe = categoryBadge.gameObject.AddComponent<LayoutElement>();
        badgeLe.flexibleWidth = 1f;
        badgeLe.flexibleHeight = 1f;

        // CategoryText 정렬
        if (categoryText != null)
        {
            var tmp = categoryText.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 80f;
                tmp.fontStyle = FontStyles.Bold;
            }
            categoryText.anchorMin = Vector2.zero;
            categoryText.anchorMax = Vector2.one;
            categoryText.offsetMin = Vector2.zero;
            categoryText.offsetMax = Vector2.zero;
        }
    }

    // DescriptionText → ResultCard 하단
    private void SetDescriptionText()
    {
        if (descriptionText == null) return;

        var le = descriptionText.GetComponent<LayoutElement>();
        if (le == null) le = descriptionText.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.flexibleHeight = 1f;

        var tmp = descriptionText.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.fontSize = 60f;
        }
    }
}