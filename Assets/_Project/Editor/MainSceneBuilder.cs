using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    public static class MainSceneBuilder
    {
        [MenuItem("Artti/Build MainScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.Main);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas();

            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);

            SceneBuilderUtils.CreateBackButton("ProfileSelectScene", canvasGo.transform, "← 프로필");

            var title = SceneBuilderUtils.CreateTMPText("Title", canvasGo.transform, "아르띠", 120);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -160);
            titleRect.sizeDelta = new Vector2(800, 200);

            var trainingBtn = SceneBuilderUtils.CreateButton("훈련모드", canvasGo.transform, 72);
            var trainingRect = trainingBtn.GetComponent<RectTransform>();
            trainingRect.anchorMin = new Vector2(0.5f, 0.5f);
            trainingRect.anchorMax = new Vector2(0.5f, 0.5f);
            trainingRect.anchoredPosition = new Vector2(0, 200);
            trainingRect.sizeDelta = new Vector2(720, 300);
            trainingBtn.gameObject.name = "TrainingModeBtn";

            var arBtn = SceneBuilderUtils.CreateButton("AR현장모드", canvasGo.transform, 72);
            var arRect = arBtn.GetComponent<RectTransform>();
            arRect.anchorMin = new Vector2(0.5f, 0.5f);
            arRect.anchorMax = new Vector2(0.5f, 0.5f);
            arRect.anchoredPosition = new Vector2(0, -200);
            arRect.sizeDelta = new Vector2(720, 300);
            arBtn.gameObject.name = "ARFieldModeBtn";

            var reportBtn = SceneBuilderUtils.CreateButton("리포트", canvasGo.transform, 36);
            var reportRect = reportBtn.GetComponent<RectTransform>();
            reportRect.anchorMin = new Vector2(1f, 1f);
            reportRect.anchorMax = new Vector2(1f, 1f);
            reportRect.pivot = new Vector2(1f, 1f);
            reportRect.anchoredPosition = new Vector2(-40, -40);
            reportRect.sizeDelta = new Vector2(180, 100);
            reportBtn.gameObject.name = "ReportBtn";

            // ReportPanel — 활동 리포트 모달
            var reportPanel = SceneBuilderUtils.CreatePanel("ReportPanel", canvasGo.transform);
            reportPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var reportCard = new GameObject("ReportCard");
            reportCard.transform.SetParent(reportPanel.transform, false);
            var reportCardRect = reportCard.AddComponent<RectTransform>();
            reportCardRect.anchorMin = new Vector2(0f, 0f);
            reportCardRect.anchorMax = new Vector2(1f, 1f);
            reportCardRect.offsetMin = new Vector2(80, 200);
            reportCardRect.offsetMax = new Vector2(-80, -200);
            reportCard.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            SceneBuilderUtils.AddVerticalLayout(reportCard, spacing: 32, padding: new RectOffset(48, 48, 48, 48), alignment: TextAnchor.UpperCenter);

            var reportTitle = SceneBuilderUtils.CreateTMPText("ReportTitle", reportCard.transform, "활동 리포트", 64);
            reportTitle.textWrappingMode = TextWrappingModes.NoWrap;
            SceneBuilderUtils.AddLayoutElement(reportTitle.gameObject, preferredHeight: 100);

            // 본문 텍스트 (Layout으로 expand하면서 자동 늘어남)
            var reportContent = SceneBuilderUtils.CreateTMPText("ReportContent", reportCard.transform, "활동 기록 로딩 중...", 36);
            reportContent.alignment = TextAlignmentOptions.TopLeft;
            reportContent.textWrappingMode = TextWrappingModes.Normal;
            var contentLe = SceneBuilderUtils.AddLayoutElement(reportContent.gameObject);
            contentLe.flexibleHeight = 1;

            var closeReportBtn = SceneBuilderUtils.CreateButton("닫기", reportCard.transform, 44);
            closeReportBtn.gameObject.name = "CloseReportBtn";
            SceneBuilderUtils.AddLayoutElement(closeReportBtn.gameObject, preferredHeight: 120);

            reportPanel.SetActive(false);

            var view = canvasGo.AddComponent<MainSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("trainingModeBtn").objectReferenceValue = trainingBtn;
            so.FindProperty("arFieldModeBtn").objectReferenceValue = arBtn;
            so.FindProperty("reportBtn").objectReferenceValue = reportBtn;
            so.FindProperty("reportPanel").objectReferenceValue = reportPanel;
            so.FindProperty("reportText").objectReferenceValue = reportContent;
            so.FindProperty("closeReportBtn").objectReferenceValue = closeReportBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 완료");
        }
    }
}
