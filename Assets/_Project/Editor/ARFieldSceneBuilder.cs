using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using Artti.AAC;
using Artti.ARField;
using Artti.UI;

namespace Artti.Editor
{
    public static class ARFieldSceneBuilder
    {
        [MenuItem("Artti/Build ARFieldScene Hierarchy")]
        public static void BuildMenu() => BuildScene();

        public static void BuildScene()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.ARField);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            Build();
            SceneBuilderUtils.SaveActiveScene();
        }

        public static void Build()
        {
            // [AR] — AR Foundation 컴포넌트 와이어링
            var arRoot = new GameObject("[AR]");

            var arSession = new GameObject("AR Session");
            arSession.transform.SetParent(arRoot.transform);
            arSession.AddComponent<ARSession>();
            arSession.AddComponent<ARInputManager>();

            // AR Foundation 6.x 표준 구조: XROrigin → Camera Offset → AR Camera
            var arOrigin = new GameObject("XR Origin");
            arOrigin.transform.SetParent(arRoot.transform);
            var xrOrigin = arOrigin.AddComponent<XROrigin>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(arOrigin.transform, false);
            xrOrigin.CameraFloorOffsetObject = cameraOffset;

            var arCamera = new GameObject("AR Camera");
            arCamera.transform.SetParent(cameraOffset.transform, false);
            arCamera.tag = "MainCamera";
            var cam = arCamera.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            arCamera.AddComponent<ARCameraManager>();
            arCamera.AddComponent<ARCameraBackground>();
            xrOrigin.Camera = cam;

            // [Canvas]
            var canvasGo = SceneBuilderUtils.CreateCanvas();

            // CameraPanel — 투명 (AR Camera Background가 뒤에서 영상 그림)
            var cameraPanel = SceneBuilderUtils.CreatePanel("CameraPanel", canvasGo.transform);
            var cameraRaw = SceneBuilderUtils.CreateRawImage("CameraRawImage", cameraPanel.transform);
            SceneBuilderUtils.FillStretch(cameraRaw.rectTransform);
            cameraRaw.color = new Color(0f, 0f, 0f, 0f); // alpha 0 → AR 영상 비치게
            var guideText = SceneBuilderUtils.CreateTMPText("GuideText", cameraPanel.transform, "가게 간판을 비춰주세요", 64);
            var guideRect = guideText.rectTransform;
            guideRect.anchorMin = new Vector2(0.5f, 1f);
            guideRect.anchorMax = new Vector2(0.5f, 1f);
            guideRect.pivot = new Vector2(0.5f, 1f);
            guideRect.anchoredPosition = new Vector2(0, -100);
            guideRect.sizeDelta = new Vector2(960, 140);
            var captureBtn = SceneBuilderUtils.CreateButton("간판 촬영", cameraPanel.transform, 56);
            var captureRect = captureBtn.GetComponent<RectTransform>();
            captureRect.anchorMin = new Vector2(0.5f, 0f);
            captureRect.anchorMax = new Vector2(0.5f, 0f);
            captureRect.pivot = new Vector2(0.5f, 0f);
            captureRect.anchoredPosition = new Vector2(0, 240);
            captureRect.sizeDelta = new Vector2(560, 200);
            captureBtn.gameObject.name = "CaptureBtn";

            // ResultConfirmPanel — 위에서 아래로: 캡처이미지 / 카드(아이콘+텍스트) / 가로 버튼행
            var resultPanel = SceneBuilderUtils.CreatePanel("ResultConfirmPanel", canvasGo.transform);
            resultPanel.SetActive(false);
            resultPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);
            SceneBuilderUtils.AddVerticalLayout(resultPanel, spacing: 32, padding: new RectOffset(60, 60, 80, 80), alignment: TextAnchor.UpperCenter, expandWidth: true, expandHeight: false);

            var capturedImage = SceneBuilderUtils.CreateRawImage("CapturedImage", resultPanel.transform);
            capturedImage.color = new Color(0.82f, 0.82f, 0.86f, 1f);
            SceneBuilderUtils.AddLayoutElement(capturedImage.gameObject, preferredHeight: 700);

            var resultCard = new GameObject("ResultCard");
            resultCard.transform.SetParent(resultPanel.transform, false);
            resultCard.AddComponent<RectTransform>();
            resultCard.AddComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            SceneBuilderUtils.AddLayoutElement(resultCard, preferredHeight: 420);
            SceneBuilderUtils.AddVerticalLayout(resultCard, spacing: 16, padding: new RectOffset(32, 32, 32, 32), alignment: TextAnchor.MiddleCenter);
            var aacIcon = SceneBuilderUtils.CreateImage("AACIcon", resultCard.transform);
            aacIcon.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            SceneBuilderUtils.AddLayoutElement(aacIcon.gameObject, preferredHeight: 200);
            var categoryLabel = SceneBuilderUtils.CreateTMPText("CategoryLabel", resultCard.transform, "카테고리", 64);
            SceneBuilderUtils.AddLayoutElement(categoryLabel.gameObject, preferredHeight: 88);
            var descriptionText = SceneBuilderUtils.CreateTMPText("DescriptionText", resultCard.transform, "설명", 48);
            SceneBuilderUtils.AddLayoutElement(descriptionText.gameObject, preferredHeight: 72);

            // 가로 버튼행: [틀렸어요 | 맞아요]
            var buttonRow = new GameObject("ConfirmButtonRow");
            buttonRow.transform.SetParent(resultPanel.transform, false);
            buttonRow.AddComponent<RectTransform>();
            var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 40;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            SceneBuilderUtils.AddLayoutElement(buttonRow, preferredHeight: 220);

            var retryBtn = SceneBuilderUtils.CreateButton("틀렸어요", buttonRow.transform, 64);
            retryBtn.gameObject.name = "RetryBtn";
            var retryImg = retryBtn.GetComponent<Image>();
            if (retryImg != null) retryImg.color = new Color(0.95f, 0.95f, 0.97f, 1f);

            var confirmBtn = SceneBuilderUtils.CreateButton("맞아요", buttonRow.transform, 64);
            confirmBtn.gameObject.name = "ConfirmBtn";
            var confirmImg = confirmBtn.GetComponent<Image>();
            if (confirmImg != null) confirmImg.color = new Color(0.3f, 0.7f, 0.45f, 1f);
            var confirmTextTmp = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (confirmTextTmp != null) confirmTextTmp.color = Color.white;

            var resultConfirmView = resultPanel.AddComponent<ARFieldResultConfirmView>();
            var soResult = new SerializedObject(resultConfirmView);
            soResult.FindProperty("capturedImage").objectReferenceValue = capturedImage;
            soResult.FindProperty("aacIcon").objectReferenceValue = aacIcon;
            soResult.FindProperty("categoryLabel").objectReferenceValue = categoryLabel;
            soResult.FindProperty("descriptionText").objectReferenceValue = descriptionText;
            soResult.FindProperty("retryButton").objectReferenceValue = retryBtn;
            soResult.ApplyModifiedProperties();

            // ManualSelectPanel
            var manualPanel = SceneBuilderUtils.CreatePanel("ManualSelectPanel", canvasGo.transform);
            manualPanel.SetActive(false);
            manualPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);
            SceneBuilderUtils.AddVerticalLayout(manualPanel, spacing: 50, padding: new RectOffset(80, 80, 240, 240));
            var convBtn = SceneBuilderUtils.CreateButton("편의점", manualPanel.transform, 72);
            convBtn.gameObject.name = "ConvenienceBtn";
            SceneBuilderUtils.AddLayoutElement(convBtn.gameObject, preferredHeight: 340);
            var pharmBtn = SceneBuilderUtils.CreateButton("약국", manualPanel.transform, 72);
            pharmBtn.gameObject.name = "PharmacyBtn";
            SceneBuilderUtils.AddLayoutElement(pharmBtn.gameObject, preferredHeight: 340);
            var restBtn = SceneBuilderUtils.CreateButton("음식점", manualPanel.transform, 72);
            restBtn.gameObject.name = "RestaurantBtn";
            SceneBuilderUtils.AddLayoutElement(restBtn.gameObject, preferredHeight: 340);

            // CategoryPanel
            var categoryPanel = SceneBuilderUtils.CreatePanel("CategoryPanel", canvasGo.transform);
            categoryPanel.SetActive(false);
            categoryPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);
            SceneBuilderUtils.AddGridLayout(categoryPanel, new Vector2(460, 620), new Vector2(40, 40), 2, new RectOffset(60, 60, 220, 220));
            var catSlots = new AACCardSlotUI[4];
            for (int i = 0; i < 4; i++)
                catSlots[i] = CreateCardSlot($"CardSlot_0{i + 1}", categoryPanel.transform);

            // SubCategoryPanel
            var subPanel = SceneBuilderUtils.CreatePanel("SubCategoryPanel", canvasGo.transform);
            subPanel.SetActive(false);
            subPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);
            SceneBuilderUtils.AddGridLayout(subPanel, new Vector2(460, 620), new Vector2(40, 40), 2, new RectOffset(60, 60, 220, 220));
            var subSlots = new AACCardSlotUI[4];
            for (int i = 0; i < 4; i++)
                subSlots[i] = CreateCardSlot($"CardSlot_0{i + 1}", subPanel.transform);

            // ARFieldUIController
            var uiController = canvasGo.AddComponent<ARFieldUIController>();
            var soUI = new SerializedObject(uiController);
            soUI.FindProperty("cameraPanel").objectReferenceValue = cameraPanel;
            soUI.FindProperty("resultConfirmPanel").objectReferenceValue = resultPanel;
            soUI.FindProperty("manualSelectPanel").objectReferenceValue = manualPanel;
            soUI.FindProperty("categoryPanel").objectReferenceValue = categoryPanel;
            soUI.FindProperty("subCategoryPanel").objectReferenceValue = subPanel;
            soUI.FindProperty("convenienceBtn").objectReferenceValue = convBtn;
            soUI.FindProperty("pharmacyBtn").objectReferenceValue = pharmBtn;
            soUI.FindProperty("restaurantBtn").objectReferenceValue = restBtn;

            var catSlotsProp = soUI.FindProperty("categorySlots");
            catSlotsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
                catSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = catSlots[i];

            var subSlotsProp = soUI.FindProperty("subCategorySlots");
            subSlotsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
                subSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = subSlots[i];

            soUI.ApplyModifiedProperties();

            // onClick: 패널 토글
            WireOnClick(retryBtn,   uiController, nameof(ARFieldUIController.ShowManualSelect));
            // captureBtn/confirmBtn은 ARFieldFlow에 wire (아래 Managers 섹션)

            // 뒤로가기 버튼 (Main으로)
            SceneBuilderUtils.CreateBackButton("MainScene", canvasGo.transform);

            // [Managers] + ARFieldFlow (시나리오/카테고리/서브 흐름 + TTS)
            var managers = new GameObject("[Managers]");
            var arFieldManager = new GameObject("ARFieldManager");
            arFieldManager.transform.SetParent(managers.transform);
            var flow = arFieldManager.AddComponent<ARFieldFlow>();
            var soFlow = new SerializedObject(flow);
            soFlow.FindProperty("uiController").objectReferenceValue = uiController;
            var db = AssetDatabase.LoadAssetAtPath<AACDatabase>("Assets/_Project/_Data/AAC/AACDatabase.asset");
            if (db != null) soFlow.FindProperty("database").objectReferenceValue = db;
            else Debug.LogWarning("[ARFieldSceneBuilder] AACDatabase.asset 없음 — Tools/AAC/Import Seed Data 먼저 실행");
            soFlow.FindProperty("resultConfirmView").objectReferenceValue = resultConfirmView;
            soFlow.ApplyModifiedProperties();

            // captureBtn(간판 촬영) → ARFieldFlow.Capture (NativeCamera → ML Kit OCR → 결과 패널)
            WireToFlow(captureBtn, flow, nameof(ARFieldFlow.Capture));
            // confirmBtn(맞아요) → ARFieldFlow.ConfirmRecognition (OCR 추론 시나리오로 진행)
            WireToFlow(confirmBtn, flow, nameof(ARFieldFlow.ConfirmRecognition));

            new GameObject("OCRManager").transform.SetParent(managers.transform);
            new GameObject("KeywordMatchManager").transform.SetParent(managers.transform);

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);

            Debug.Log("[ARFieldSceneBuilder] Hierarchy + ARFieldFlow + onClick 와이어링 완료");
            Selection.activeGameObject = canvasGo;
        }

        static void WireToFlow(Button btn, ARFieldFlow flow, string methodName)
        {
            var method = typeof(ARFieldFlow).GetMethod(methodName);
            if (method == null) { Debug.LogError($"[ARFieldSceneBuilder] {methodName} not found"); return; }
            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), flow, method);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        static void WireOnClick(Button btn, ARFieldUIController target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName);
            if (method == null)
            {
                Debug.LogError($"[ARFieldSceneBuilder] {methodName} 메소드 없음");
                return;
            }
            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        static AACCardSlotUI CreateCardSlot(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 1f);
            var btn = go.AddComponent<Button>();

            SceneBuilderUtils.AddVerticalLayout(go, spacing: 16, padding: new RectOffset(24, 24, 24, 24));

            var icon = SceneBuilderUtils.CreateImage("Icon", go.transform);
            icon.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            SceneBuilderUtils.AddLayoutElement(icon.gameObject, preferredHeight: 380);

            var label = SceneBuilderUtils.CreateTMPText("Label", go.transform, "", 48);
            SceneBuilderUtils.AddLayoutElement(label.gameObject, preferredHeight: 100);

            var cardUI = go.AddComponent<AACCardSlotUI>();
            var so = new SerializedObject(cardUI);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("labelText").objectReferenceValue = label;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedProperties();

            return cardUI;
        }
    }
}
