using UnityEditor;
using UnityEngine;
using Artti.UI;

namespace Artti.Editor
{
    public static class TrainingHubSceneBuilder
    {
        [MenuItem("Artti/Build TrainingHubScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingHub);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas();

            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);

            SceneBuilderUtils.CreateBackButton("MainScene", canvasGo.transform);

            var title = SceneBuilderUtils.CreateTMPText("Title", canvasGo.transform, "훈련 시나리오 선택", 80);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -140);
            titleRect.sizeDelta = new Vector2(900, 160);

            var pharmBtn = SceneBuilderUtils.CreateButton("약국", canvasGo.transform, 64);
            var pharmRect = pharmBtn.GetComponent<RectTransform>();
            pharmRect.anchorMin = new Vector2(0.5f, 0.5f);
            pharmRect.anchorMax = new Vector2(0.5f, 0.5f);
            pharmRect.anchoredPosition = new Vector2(0, 350);
            pharmRect.sizeDelta = new Vector2(720, 240);
            pharmBtn.gameObject.name = "PharmacyBtn";

            var convBtn = SceneBuilderUtils.CreateButton("편의점", canvasGo.transform, 64);
            var convRect = convBtn.GetComponent<RectTransform>();
            convRect.anchorMin = new Vector2(0.5f, 0.5f);
            convRect.anchorMax = new Vector2(0.5f, 0.5f);
            convRect.anchoredPosition = new Vector2(0, 0);
            convRect.sizeDelta = new Vector2(720, 240);
            convBtn.gameObject.name = "ConvenienceBtn";

            var restBtn = SceneBuilderUtils.CreateButton("음식점", canvasGo.transform, 64);
            var restRect = restBtn.GetComponent<RectTransform>();
            restRect.anchorMin = new Vector2(0.5f, 0.5f);
            restRect.anchorMax = new Vector2(0.5f, 0.5f);
            restRect.anchoredPosition = new Vector2(0, -350);
            restRect.sizeDelta = new Vector2(720, 240);
            restBtn.gameObject.name = "RestaurantBtn";

            var view = canvasGo.AddComponent<TrainingHubView>();
            var so = new SerializedObject(view);
            so.FindProperty("pharmacyBtn").objectReferenceValue = pharmBtn;
            so.FindProperty("convenienceBtn").objectReferenceValue = convBtn;
            so.FindProperty("restaurantBtn").objectReferenceValue = restBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[TrainingHubSceneBuilder] 완료");
        }
    }
}
