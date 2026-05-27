using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.AAC;
using Artti.Training;

namespace Artti.Editor
{
    public static class TrainingSceneBuilder
    {
        [MenuItem("Artti/Build TrainingPharmacyScene Hierarchy")]
        public static void BuildPharmacyMenu()
        {
            // 약국씬은 수동 배치된 상태라 빌더로 덮어쓰면 손배치 다 날아감. 확인 한 번 받고 진행.
            bool ok = UnityEditor.EditorUtility.DisplayDialog(
                "약국씬 재빌드 확인",
                "TrainingPharmacyScene을 빌더로 다시 생성하면 현재 수동 배치가 모두 사라집니다.\n정말 진행할까요?",
                "덮어쓰기", "취소");
            if (!ok) return;
            Build(ScenePaths.TrainingPharmacy, "pharmacy");
        }

        [MenuItem("Artti/Build TrainingConvenienceScene Hierarchy")]
        public static void BuildConvenienceMenu() => Build(ScenePaths.TrainingConvenience, "convenience");

        [MenuItem("Artti/Build TrainingRestaurantScene Hierarchy")]
        public static void BuildRestaurantMenu() => Build(ScenePaths.TrainingRestaurant, "restaurant");

        public static void Build(string scenePath, string scenarioId)
        {
            SceneBuilderUtils.OpenScene(scenePath);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // Canvas 배경 (밝은 색)
            // 이건 Canvas 만들기 전에 위치 — 일단 Canvas 후에 배경 패널 추가
            // [Managers]
            var managers = new GameObject("[Managers]");
            var sceneRootGo = new GameObject("TrainingSceneRoot");
            sceneRootGo.transform.SetParent(managers.transform);
            sceneRootGo.AddComponent<AudioSource>();
            var sceneRoot = sceneRootGo.AddComponent<TrainingSceneRoot>();

            // Canvas
            var canvasGo = SceneBuilderUtils.CreateCanvas();

            // Canvas 배경
            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);

            SceneBuilderUtils.CreateBackButton("TrainingHubScene", canvasGo.transform);

            // NPC Dialogue Panel (top) — 약국씬은 뒤로 버튼 아래에서 시작하도록 더 좁고/더 아래로
            var npcPanel = SceneBuilderUtils.CreatePanel("NPCDialoguePanel", canvasGo.transform, stretchFull: false);
            var npcRect = npcPanel.GetComponent<RectTransform>();
            npcRect.anchorMin = new Vector2(0.5f, 1f);
            npcRect.anchorMax = new Vector2(0.5f, 1f);
            npcRect.pivot = new Vector2(0.5f, 1f);
            // 풀 레이아웃 공용 — 뒤로 버튼(top -32, h 88) 아래에서 시작. 모든 훈련 씬 동일
            npcRect.anchoredPosition = new Vector2(0, -160);
            npcRect.sizeDelta = new Vector2(720, 280);
            npcPanel.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);
            var npcText = SceneBuilderUtils.CreateTMPText("NPCText", npcPanel.transform, "안녕하세요!", 56);
            var npcTextRect = npcText.rectTransform;
            npcTextRect.anchorMin = Vector2.zero;
            npcTextRect.anchorMax = Vector2.one;
            npcTextRect.offsetMin = new Vector2(40, 40);
            npcTextRect.offsetMax = new Vector2(-40, -40);

            // STT 듣는 중 — NPC 패널 외곽 4변을 시계방향으로 순차 채우는 진행 테두리
            var (borderRoot, borderTop, borderRight, borderBottom, borderLeft) = BuildNpcBorderProgress(npcPanel.transform);

            // Card slots — 모든 훈련 시나리오에 세로 스크롤 4장 풀 + 기타 버튼/모달 적용 (PLAN.MD 5.4.2 / 7.3.1)
            AACCardButton cardSlot1 = null;
            AACCardButton cardSlot2 = null;
            AACCardButton[] pharmacyCardSlots;
            Button pharmacyExtraButton;
            GameObject pharmacyExtraModal;
            AACCardButton[] pharmacyExtraSlots;
            Button pharmacyExtraCloseBtn;

            pharmacyCardSlots = BuildPharmacyScrollPool(canvasGo.transform);
            pharmacyExtraButton = BuildPharmacyExtraButton(canvasGo.transform);
            (pharmacyExtraModal, pharmacyExtraSlots, pharmacyExtraCloseBtn) = BuildPharmacyExtraModal(canvasGo.transform);

            // STT 표시는 NPC 패널 내부 인라인 스피너로 일원화 — 풀스크린 오버레이 제거

            // TrainingUIView on Canvas
            var uiView = canvasGo.AddComponent<TrainingUIView>();
            var soView = new SerializedObject(uiView);
            soView.FindProperty("cardSlot1").objectReferenceValue = cardSlot1;
            soView.FindProperty("cardSlot2").objectReferenceValue = cardSlot2;
            soView.FindProperty("npcBorderRoot").objectReferenceValue   = borderRoot;
            soView.FindProperty("npcBorderTop").objectReferenceValue    = borderTop;
            soView.FindProperty("npcBorderRight").objectReferenceValue  = borderRight;
            soView.FindProperty("npcBorderBottom").objectReferenceValue = borderBottom;
            soView.FindProperty("npcBorderLeft").objectReferenceValue   = borderLeft;

            if (pharmacyCardSlots != null)
            {
                var slotsProp = soView.FindProperty("pharmacyCardSlots");
                slotsProp.arraySize = pharmacyCardSlots.Length;
                for (int i = 0; i < pharmacyCardSlots.Length; i++)
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = pharmacyCardSlots[i];
            }

            if (pharmacyExtraButton != null)
                soView.FindProperty("extraButton").objectReferenceValue = pharmacyExtraButton;
            if (pharmacyExtraModal != null)
                soView.FindProperty("extraModal").objectReferenceValue = pharmacyExtraModal;
            if (pharmacyExtraCloseBtn != null)
                soView.FindProperty("extraCloseButton").objectReferenceValue = pharmacyExtraCloseBtn;
            if (pharmacyExtraSlots != null)
            {
                var exSlots = soView.FindProperty("extraCardSlots");
                exSlots.arraySize = pharmacyExtraSlots.Length;
                for (int i = 0; i < pharmacyExtraSlots.Length; i++)
                    exSlots.GetArrayElementAtIndex(i).objectReferenceValue = pharmacyExtraSlots[i];
            }

            soView.FindProperty("npcDialoguePanel").objectReferenceValue = npcText;
            // 레거시 풀스크린 오버레이 슬롯은 명시적 null — 약국씬 수동 와이어링과는 별개로,
            // 빌더가 만든 씬에서는 인라인 스피너 경로만 활성화됨
            soView.FindProperty("sttOverlay").objectReferenceValue = null;
            soView.FindProperty("sttStatusText").objectReferenceValue = null;
            soView.FindProperty("sttResultText").objectReferenceValue = null;
            soView.FindProperty("sttPulseCircle").objectReferenceValue = null;
            soView.ApplyModifiedProperties();

            // Wire TrainingSceneRoot
            var soRoot = new SerializedObject(sceneRoot);
            soRoot.FindProperty("scenarioId").stringValue = scenarioId;
            soRoot.FindProperty("uiView").objectReferenceValue = uiView;
            var aacDb = AssetDatabase.LoadAssetAtPath<AACDatabase>("Assets/_Project/_Data/AAC/AACDatabase.asset");
            if (aacDb != null)
                soRoot.FindProperty("aacDatabase").objectReferenceValue = aacDb;
            else
                Debug.LogWarning("[TrainingSceneBuilder] AACDatabase.asset 없음 — Tools/AAC/Import Seed Data 먼저 실행");
            soRoot.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log($"[TrainingSceneBuilder] {scenarioId} 완료");
        }

        const int PharmacyPoolSize = 4;

        // 약국용 세로 스크롤 카드 풀 빌드 — ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup) + 4 슬롯
        static AACCardButton[] BuildPharmacyScrollPool(Transform canvasParent)
        {
            // 스크롤 영역 — NPC 패널 아래부터 기타 버튼 위까지
            var scrollGo = new GameObject("PharmacyCardScroll");
            scrollGo.transform.SetParent(canvasParent, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(40, 360);  // 기타 버튼 위로(Y=200, h=120) + 40 마진
            scrollRect.offsetMax = new Vector2(-40, -460);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;

            // Viewport (RectMask2D)
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            scroll.viewport = vpRect;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 96;
            contentLayout.padding = new RectOffset(24, 24, 48, 48);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = false;     // 카드가 폭 풀로 확장되지 않게
            contentLayout.childForceExpandHeight = false;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = contentRect;

            // 카드 슬롯 4장
            var slots = new AACCardButton[PharmacyPoolSize];
            for (int i = 0; i < PharmacyPoolSize; i++)
                slots[i] = CreatePharmacyCardSlot($"CardSlot_{i + 1:00}", content.transform);
            return slots;
        }

        // 카드 row — CardBox(흰 박스) + Icon(외부 형제) + Label(외부 형제). row 자체가 layout 단위
        static AACCardButton CreatePharmacyCardSlot(string name, Transform parent)
        {
            // Row 컨테이너 (LayoutElement로 row 자체 크기 지정)
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var rowLE = SceneBuilderUtils.AddLayoutElement(row, preferredHeight: 200, preferredWidth: 880);
            rowLE.minWidth = 880;
            rowLE.minHeight = 200;

            // CardBox — 흰 배경 + Button, row의 가로 풀폭 사용, 높이는 row보다 작게 (180)
            var cardBox = new GameObject("CardBox");
            cardBox.transform.SetParent(row.transform, false);
            var cardRect = cardBox.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0, 0);
            cardRect.sizeDelta = new Vector2(0, 180);  // anchor 좌우 stretch → width 0 = 풀폭. height 180
            var bg = cardBox.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 1f);
            var btn = cardBox.AddComponent<Button>();

            // IconMask — CardBox와 같은 크기·위치의 컨테이너 (RectMask2D로 Icon 클립)
            var maskGo = new GameObject("IconMask");
            maskGo.transform.SetParent(row.transform, false);
            var maskRect = maskGo.AddComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0f, 0.5f);
            maskRect.anchorMax = new Vector2(1f, 0.5f);
            maskRect.pivot = new Vector2(0.5f, 0.5f);
            maskRect.anchoredPosition = new Vector2(0, 0);
            maskRect.sizeDelta = new Vector2(0, 180);  // CardBox와 동일 (가로 풀폭, 세로 180)
            maskGo.AddComponent<RectMask2D>();

            // Icon — IconMask 자식. Icon 자체는 그대로 (200×200, 좌측 중앙), Mask가 카드 경계 밖 잘라줌
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(maskGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(24, 0);
            iconRect.sizeDelta = new Vector2(200, 200);
            var icon = iconGo.AddComponent<Image>();
            icon.color = new Color(1f, 1f, 1f, 1f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // Label — Icon 우측부터 row 우측 끝까지, CardBox 내부 정렬
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(248, 0);  // 24(좌마진) + 200(icon) + 24(spacing)
            labelRect.offsetMax = new Vector2(-32, 0);
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = "";
            label.fontSize = 48;
            label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.color = Color.black;
            label.raycastTarget = false;
            var font = SceneBuilderUtils.GetKoreanFont();
            if (font != null) label.font = font;

            var cardButton = row.AddComponent<AACCardButton>();
            var so = new SerializedObject(cardButton);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("phraseText").objectReferenceValue = label;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedProperties();

            return cardButton;
        }

        // 약국 화면 하단 "기타" 버튼 (메인 ScrollRect 아래, 항상 노출)
        static Button BuildPharmacyExtraButton(Transform canvasParent)
        {
            var btn = SceneBuilderUtils.CreateButton("기타", canvasParent, 48);
            btn.gameObject.name = "PharmacyExtraBtn";
            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 200);  // 하단 가장자리에서 띄움
            rect.sizeDelta = new Vector2(-80, 120);
            var btnTxt = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnTxt != null) btnTxt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return btn;
        }

        const int PharmacyExtraSlotCount = 10;

        // 기타 모달 — 풀스크린 반투명 + 가운데 카드(타이틀 + 스크롤 카드 리스트 + 닫기)
        static (GameObject modal, AACCardButton[] slots, Button closeBtn) BuildPharmacyExtraModal(Transform canvasParent)
        {
            var modal = SceneBuilderUtils.CreatePanel("PharmacyExtraModal", canvasParent);
            modal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var card = new GameObject("Card");
            card.transform.SetParent(modal.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.offsetMin = new Vector2(40, 80);
            cardRect.offsetMax = new Vector2(-40, -80);
            card.AddComponent<Image>().color = Color.white;
            SceneBuilderUtils.AddVerticalLayout(card, spacing: 16, padding: new RectOffset(24, 24, 24, 24), alignment: TextAnchor.UpperCenter);

            var title = SceneBuilderUtils.CreateTMPText("ExtraTitle", card.transform, "기타 카드", 56);
            title.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            SceneBuilderUtils.AddLayoutElement(title.gameObject, preferredHeight: 100);

            // 내부 ScrollRect
            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(card.transform, false);
            scrollGo.AddComponent<RectTransform>();
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            var scrollLE = SceneBuilderUtils.AddLayoutElement(scrollGo);
            scrollLE.flexibleHeight = 1;
            scrollLE.flexibleWidth = 1;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            scroll.viewport = vpRect;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 16;
            contentLayout.padding = new RectOffset(16, 16, 16, 16);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = contentRect;

            var slots = new AACCardButton[PharmacyExtraSlotCount];
            for (int i = 0; i < PharmacyExtraSlotCount; i++)
                slots[i] = CreatePharmacyCardSlot($"ExtraSlot_{i + 1:00}", content.transform);

            // 닫기 버튼
            var closeBtn = SceneBuilderUtils.CreateButton("닫기", card.transform, 44);
            closeBtn.gameObject.name = "ExtraCloseBtn";
            SceneBuilderUtils.AddLayoutElement(closeBtn.gameObject, preferredHeight: 120);

            modal.SetActive(false);
            return (modal, slots, closeBtn);
        }

        const int BorderThickness = 8;
        static readonly Color BorderColor = new Color(0.2f, 0.75f, 0.4f, 1f); // STT 진행 강조색

        // NPC 패널 내부 stretch 자식으로 4 edge Image 배치 — top→right→bottom→left 시계방향 진행
        static (GameObject root, Image top, Image right, Image bottom, Image left)
            BuildNpcBorderProgress(Transform npcPanel)
        {
            var root = new GameObject("NPCSttBorder");
            root.transform.SetParent(npcPanel, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var top    = CreateBorderEdge(root.transform, "Top",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0, 0.5f),
                size: new Vector2(0, BorderThickness),
                fillMethod: Image.FillMethod.Horizontal, fillOrigin: (int)Image.OriginHorizontal.Left);

            var right  = CreateBorderEdge(root.transform, "Right",
                anchorMin: new Vector2(1, 0), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
                size: new Vector2(BorderThickness, 0),
                fillMethod: Image.FillMethod.Vertical, fillOrigin: (int)Image.OriginVertical.Top);

            var bottom = CreateBorderEdge(root.transform, "Bottom",
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0), pivot: new Vector2(1, 0.5f),
                size: new Vector2(0, BorderThickness),
                fillMethod: Image.FillMethod.Horizontal, fillOrigin: (int)Image.OriginHorizontal.Right);

            var left   = CreateBorderEdge(root.transform, "Left",
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 1), pivot: new Vector2(0.5f, 0),
                size: new Vector2(BorderThickness, 0),
                fillMethod: Image.FillMethod.Vertical, fillOrigin: (int)Image.OriginVertical.Bottom);

            root.SetActive(false);
            return (root, top, right, bottom, left);
        }

        static Image CreateBorderEdge(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size,
            Image.FillMethod fillMethod, int fillOrigin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            // Image.Type.Filled는 sprite가 있어야 fillAmount가 동작 — Unity 빌트인 UISprite 사용
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null) img.sprite = sprite;
            img.color = BorderColor;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = fillMethod;
            img.fillOrigin = fillOrigin;
            img.fillAmount = 0f;
            return img;
        }
    }
}
