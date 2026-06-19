using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 말하기 훈련 시나리오 선택 화면. 가로 1920x1080. (완성본: kk.png)
    // 카드 3개(편의점/약국/음식점) = 글래스 카드(드롭섀도우 + 반투명 흰 바디 + 밝은 림).
    //   각 카드: 컬러 원형 아이콘 + 제목 + 설명 + 둥근 매장 씬 이미지 + "연습하기" 버튼.
    // 하단: 글래스 팁바(파란 챗 마스코트 + 안내문).
    public static class TrainingHubSceneBuilder
    {
        // 카드별 업종 환경 효과 구분
        enum CardKind { Convenience, Pharmacy, Restaurant }

        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 BgColor    = new Color32(233, 239, 250, 255);
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color   White      = Color.white;

        // 글래스 색
        static readonly Color GlassEdge   = new Color(1f, 1f, 1f, 0.85f);   // 밝은 림
        static readonly Color GlassFill   = new Color(1f, 1f, 1f, 0.62f);   // 반투명 프로스트 내부
        static readonly Color GlassShadow = new Color(0.15f, 0.20f, 0.42f, 0.16f);

        // 업종 테마색 (편의점 파랑 / 약국 초록 / 음식점 주황) — 카드 테두리 글로우용
        static readonly Color ThemeBlue   = new Color(0.23f, 0.51f, 0.96f, 1f);
        static readonly Color ThemeGreen  = new Color(0.18f, 0.72f, 0.42f, 1f);
        static readonly Color ThemeOrange = new Color(0.96f, 0.60f, 0.18f, 1f);

        static Color ThemeColor(CardKind kind) => kind switch
        {
            CardKind.Convenience => ThemeBlue,
            CardKind.Pharmacy    => ThemeGreen,
            CardKind.Restaurant  => ThemeOrange,
            _                    => ThemeBlue,
        };

        const string RoundedPath  = "Assets/_Project/Art/UI/RoundedRect.png";
        const string ScenarioDir  = "Assets/_Project/Art/UI/Scenario/";
        const string CardConvenience = ScenarioDir + "card_convenience.png";
        const string CardPharmacy    = ScenarioDir + "card_pharmacy.png";
        const string CardRestaurant  = ScenarioDir + "card_restaurant.png";
        const string IconConvenience = ScenarioDir + "icon_convenience.png";
        const string IconPharmacy    = ScenarioDir + "icon_pharmacy.png";
        const string IconRestaurant  = ScenarioDir + "icon_restaurant.png";
        const string BtnBlue   = ScenarioDir + "btn_practice_blue.png";
        const string BtnGreen  = ScenarioDir + "btn_practice_green.png";
        const string BtnOrange = ScenarioDir + "btn_practice_orange.png";
        const string HelperChat = ScenarioDir + "helper_chat.png";

        static readonly Vector2 CardSize = new Vector2(430, 720);

        [MenuItem("Artti/Build TrainingHubScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingHub);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var font = SceneBuilderUtils.GetKoreanFont();

            BuildBackground(canvasGo.transform);

            MakeCircleBackButton(canvasGo.transform, font);

            var title = MakeText("Title", canvasGo.transform, "말하기 훈련", 72, TitleColor, font, bold: true);
            PlaceTop(title.rectTransform, new Vector2(0, -60), new Vector2(900, 100));

            var subtitle = MakeText("Subtitle", canvasGo.transform, "오늘은 어떤 연습을 해볼까요?", 40, SubColor, font, bold: false);
            PlaceTop(subtitle.rectTransform, new Vector2(0, -168), new Vector2(900, 64));

            var convBtn = MakeScenarioCard(canvasGo.transform, "ConvenienceBtn", new Vector2(-480, -30),
                IconConvenience, CardConvenience, BtnBlue,
                "편의점", "편의점에 방문하여\n물건을 구매해볼까요?", font, CardKind.Convenience);
            var pharmBtn = MakeScenarioCard(canvasGo.transform, "PharmacyBtn", new Vector2(0, -30),
                IconPharmacy, CardPharmacy, BtnGreen,
                "약국", "약국에 방문하여\n필요한 약품을 구매해보아요.", font, CardKind.Pharmacy);
            var restBtn = MakeScenarioCard(canvasGo.transform, "RestaurantBtn", new Vector2(480, -30),
                IconRestaurant, CardRestaurant, BtnOrange,
                "음식점", "음식점에 방문하여\n음식을 주문하고 픽업해볼까요?", font, CardKind.Restaurant);

            MakeTipBar(canvasGo.transform, font);

            var view = canvasGo.AddComponent<TrainingHubView>();
            var so = new SerializedObject(view);
            so.FindProperty("convenienceBtn").objectReferenceValue = convBtn;
            so.FindProperty("pharmacyBtn").objectReferenceValue = pharmBtn;
            so.FindProperty("restaurantBtn").objectReferenceValue = restBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[TrainingHubSceneBuilder] 완료 (글래스 카드 3개 + 팁바)");
        }

        // ===== 시나리오 글래스 카드 → "연습하기" 버튼 반환 =====
        static Button MakeScenarioCard(Transform parent, string goName, Vector2 pos,
            string iconPath, string cardImagePath, string buttonPath, string title, string desc, TMP_FontAsset font,
            CardKind kind)
        {
            var theme = ThemeColor(kind);
            var root = MakeGlassCard(parent, goName, pos, CardSize, 0.6f, theme);

            // 마우스 hover 시 살짝 커지고 떠오르며 그림자 강화
            AddHover(root);

            // 컬러 원형 아이콘 배지 (상단 중앙)
            var icon = ChildRect("Icon", root);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0, -34);
            icon.sizeDelta = new Vector2(92, 92);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadSprite(iconPath); iconImg.preserveAspect = true; iconImg.raycastTarget = false;

            var nameText = MakeText("Name", root, title, 42, TitleColor, font, bold: true);
            PlaceTop(nameText.rectTransform, new Vector2(0, -140), new Vector2(390, 56));

            var descText = MakeText("Desc", root, desc, 25, SubColor, font, bold: false);
            descText.textWrappingMode = TextWrappingModes.Normal;
            PlaceTop(descText.rectTransform, new Vector2(0, -200), new Vector2(394, 84));

            // 둥근 매장 씬 이미지 (이미지 비율 그대로)
            var sceneSp = LoadSprite(cardImagePath);
            float ratio = (sceneSp != null && sceneSp.rect.width > 0) ? sceneSp.rect.height / sceneSp.rect.width : 0.82f;
            float sw = 366f;
            var scene = ChildRect("Scene", root);
            scene.anchorMin = scene.anchorMax = new Vector2(0.5f, 1f);
            scene.pivot = new Vector2(0.5f, 1f);
            scene.anchoredPosition = new Vector2(0, -300);
            scene.sizeDelta = new Vector2(sw, sw * ratio);
            var sceneImg = scene.gameObject.AddComponent<Image>();
            sceneImg.sprite = sceneSp; sceneImg.preserveAspect = true; sceneImg.raycastTarget = false;

            // 업종별 환경 효과 — 평소엔 숨기고 hover/터치 중에만 표시
            var ambience = ChildRect("Ambience", scene);
            StretchFull(ambience, 0);
            var ambienceCg = ambience.gameObject.AddComponent<CanvasGroup>();
            ambienceCg.alpha = 0f;            // 평소 숨김 (CardHoverEffect가 hover 시 페이드 인)
            ambienceCg.interactable = false;
            ambienceCg.blocksRaycasts = false;

            switch (kind)
            {
                case CardKind.Convenience: AddConvenienceAmbience(ambience); break;
                case CardKind.Pharmacy:    AddPharmacyAmbience(ambience);    break;
                case CardKind.Restaurant:  AddRestaurantAmbience(ambience);  break;
            }

            // hover 효과에 ambience 페이드 연결
            var hoverFx = root.GetComponent<CardHoverEffect>();
            if (hoverFx != null)
            {
                var hso = new SerializedObject(hoverFx);
                hso.FindProperty("ambience").objectReferenceValue = ambienceCg;
                hso.ApplyModifiedProperties();
            }

            // "연습하기" 버튼 (색상별 베이크 이미지)
            var btnSp = LoadSprite(buttonPath);
            float bratio = (btnSp != null && btnSp.rect.width > 0) ? btnSp.rect.height / btnSp.rect.width : 0.27f;
            float bw = 300f;
            var btnRT = ChildRect("PracticeBtn", root);
            btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 1f);
            btnRT.pivot = new Vector2(0.5f, 1f);
            btnRT.anchoredPosition = new Vector2(0, -630);
            btnRT.sizeDelta = new Vector2(bw, bw * bratio);
            var btnImg = btnRT.gameObject.AddComponent<Image>();
            btnImg.sprite = btnSp; btnImg.preserveAspect = true;
            var btn = btnRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.88f, 0.9f, 0.94f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            return btn;
        }

        // ===== Hover 효과 =====
        // 카드 root에 투명 raycast 카처를 깔고 CardHoverEffect를 붙인다.
        // (카드 전체 영역에서 hover 감지 → 버튼 위에서도 enter 유지)
        static void AddHover(RectTransform cardRoot)
        {
            var catcher = cardRoot.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f); // 투명, 단 raycast는 받음
            catcher.raycastTarget = true;

            var hover = cardRoot.gameObject.AddComponent<CardHoverEffect>();
            var shadow = cardRoot.Find("Shadow")?.GetComponent<Image>();
            if (shadow != null)
            {
                var so = new SerializedObject(hover);
                so.FindProperty("shadow").objectReferenceValue = shadow;
                so.ApplyModifiedProperties();
            }
        }

        // ===== 업종별 환경 효과 =====
        // 편의점: 상품 반짝임 (여러 점이 짧게 깜빡이며 반짝)
        static void AddConvenienceAmbience(RectTransform parent)
        {
            var container = ChildRect("Sparkles", parent);
            StretchFull(container, 0);

            // 매장 이미지 내 비율 좌표(0..1)로 배치 → 이미지 크기와 무관하게 안정적
            Vector2[] pts   = { new(0.28f, 0.62f), new(0.70f, 0.55f), new(0.50f, 0.78f), new(0.38f, 0.40f), new(0.66f, 0.74f) };
            float[]   sizes = { 26f, 34f, 22f, 30f, 24f };

            var sparkles = new Graphic[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var s = ChildRect($"Sparkle{i}", container);
                s.anchorMin = s.anchorMax = pts[i];
                s.pivot = new Vector2(0.5f, 0.5f);
                s.anchoredPosition = Vector2.zero;
                s.sizeDelta = new Vector2(sizes[i], sizes[i]);
                var im = s.gameObject.AddComponent<Image>();
                im.sprite = SceneBuilderUtils.EnsureGlowSprite(); im.type = Image.Type.Sliced;
                im.color = new Color(1f, 0.97f, 0.8f, 0f); // 따뜻한 화이트, 시작은 투명
                im.raycastTarget = false;
                sparkles[i] = im;
            }

            var field = container.gameObject.AddComponent<SparkleField>();
            var so = new SerializedObject(field);
            var arr = so.FindProperty("sparkles");
            arr.arraySize = sparkles.Length;
            for (int i = 0; i < sparkles.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sparkles[i];
            so.ApplyModifiedProperties();
        }

        // 약국: 십자가 은은한 펄스 (초록 십자 + 후광이 알파/스케일로 펄스)
        static void AddPharmacyAmbience(RectTransform parent)
        {
            var cross = ChildRect("Cross", parent);
            cross.anchorMin = cross.anchorMax = new Vector2(0.78f, 0.80f); // 우상단 간판 자리
            cross.pivot = new Vector2(0.5f, 0.5f);
            cross.anchoredPosition = Vector2.zero;
            cross.sizeDelta = new Vector2(66, 66);
            cross.gameObject.AddComponent<CanvasGroup>().alpha = 0.85f; // 에디터 미리보기용 초기값

            var green = new Color(0.18f, 0.72f, 0.42f, 1f);

            // 후광 (십자 뒤, 가장 먼저 생성 → 뒤에 렌더)
            var halo = ChildRect("Halo", cross);
            halo.anchorMin = halo.anchorMax = new Vector2(0.5f, 0.5f); halo.pivot = new Vector2(0.5f, 0.5f);
            halo.anchoredPosition = Vector2.zero; halo.sizeDelta = new Vector2(112, 112);
            var ha = halo.gameObject.AddComponent<Image>();
            ha.sprite = SceneBuilderUtils.EnsureGlowSprite(); ha.type = Image.Type.Sliced;
            ha.color = new Color(0.25f, 0.85f, 0.5f, 0.35f); ha.raycastTarget = false;

            // 세로 막대
            var v = ChildRect("V", cross);
            v.anchorMin = v.anchorMax = new Vector2(0.5f, 0.5f); v.pivot = new Vector2(0.5f, 0.5f);
            v.anchoredPosition = Vector2.zero; v.sizeDelta = new Vector2(20, 60);
            var vi = v.gameObject.AddComponent<Image>();
            vi.sprite = Rounded(); vi.type = Image.Type.Sliced; vi.pixelsPerUnitMultiplier = 2f;
            vi.color = green; vi.raycastTarget = false;

            // 가로 막대
            var h = ChildRect("H", cross);
            h.anchorMin = h.anchorMax = new Vector2(0.5f, 0.5f); h.pivot = new Vector2(0.5f, 0.5f);
            h.anchoredPosition = Vector2.zero; h.sizeDelta = new Vector2(60, 20);
            var hi = h.gameObject.AddComponent<Image>();
            hi.sprite = Rounded(); hi.type = Image.Type.Sliced; hi.pixelsPerUnitMultiplier = 2f;
            hi.color = green; hi.raycastTarget = false;

            var pulse = cross.gameObject.AddComponent<GlowPulse>();
            var so = new SerializedObject(pulse);
            so.FindProperty("minAlpha").floatValue = 0.45f;
            so.FindProperty("maxAlpha").floatValue = 0.95f;
            so.FindProperty("period").floatValue = 2.6f;
            so.FindProperty("scaleAmount").floatValue = 0.06f;
            so.ApplyModifiedProperties();
        }

        // 음식점: 커피 김 + 따뜻한 조명
        static void AddRestaurantAmbience(RectTransform parent)
        {
            // 따뜻한 조명: 장면 위 은은한 주황빛 (느린 펄스)
            var warm = ChildRect("WarmLight", parent);
            warm.anchorMin = warm.anchorMax = new Vector2(0.5f, 0.52f);
            warm.pivot = new Vector2(0.5f, 0.5f);
            warm.anchoredPosition = Vector2.zero;
            warm.sizeDelta = new Vector2(280, 230); // 매장 이미지 안쪽으로 (밖으로 번짐 방지)
            warm.gameObject.AddComponent<CanvasGroup>().alpha = 0.22f; // 에디터 미리보기용 초기값
            var wi = warm.gameObject.AddComponent<Image>();
            wi.sprite = SceneBuilderUtils.EnsureGlowSprite(); wi.type = Image.Type.Sliced;
            wi.color = new Color(1f, 0.72f, 0.38f, 1f); wi.raycastTarget = false;
            var wp = warm.gameObject.AddComponent<GlowPulse>();
            var wso = new SerializedObject(wp);
            wso.FindProperty("minAlpha").floatValue = 0.16f;
            wso.FindProperty("maxAlpha").floatValue = 0.34f;
            wso.FindProperty("period").floatValue = 3.6f;
            wso.ApplyModifiedProperties();

            // 커피 김: 가운데 아래에서 위로 올라가는 wisp들
            var steamRoot = ChildRect("Steam", parent);
            StretchFull(steamRoot, 0);
            Vector2[] origin = { new(0.46f, 0.50f), new(0.50f, 0.48f), new(0.54f, 0.50f) };
            var wisps = new Image[origin.Length];
            for (int i = 0; i < origin.Length; i++)
            {
                var w = ChildRect($"Wisp{i}", steamRoot);
                w.anchorMin = w.anchorMax = origin[i];
                w.pivot = new Vector2(0.5f, 0.5f);
                w.anchoredPosition = Vector2.zero;
                w.sizeDelta = new Vector2(34, 34);
                var im = w.gameObject.AddComponent<Image>();
                im.sprite = SceneBuilderUtils.EnsureGlowSprite(); im.type = Image.Type.Sliced;
                im.color = new Color(1f, 1f, 1f, 0f); im.raycastTarget = false;
                wisps[i] = im;
            }
            var steam = steamRoot.gameObject.AddComponent<SteamRise>();
            var sso = new SerializedObject(steam);
            var arr = sso.FindProperty("wisps");
            arr.arraySize = wisps.Length;
            for (int i = 0; i < wisps.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = wisps[i];
            sso.ApplyModifiedProperties();
        }

        // ===== 하단 글래스 팁바 =====
        static void MakeTipBar(Transform parent, TMP_FontAsset font)
        {
            var root = ChildRect("TipBar", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0, 52);
            root.sizeDelta = new Vector2(1000, 96);
            DressGlass(root, 0.9f); // 높이의 절반만큼 둥근 알약

            var mascot = ChildRect("Mascot", root);
            mascot.anchorMin = mascot.anchorMax = new Vector2(0f, 0.5f);
            mascot.pivot = new Vector2(0f, 0.5f);
            mascot.anchoredPosition = new Vector2(40, 0);
            mascot.sizeDelta = new Vector2(64, 64);
            var mImg = mascot.gameObject.AddComponent<Image>();
            mImg.sprite = LoadSprite(HelperChat); mImg.preserveAspect = true; mImg.raycastTarget = false;

            var tip = MakeText("Tip", root, "원하는 시나리오를 선택하면, 연습이 시작돼요.", 34, TitleColor, font, bold: true);
            tip.alignment = TextAlignmentOptions.Center;
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tip.rectTransform.anchoredPosition = new Vector2(40, 0);
            tip.rectTransform.sizeDelta = new Vector2(820, 60);
        }

        // ===== 글래스 빌딩 블록 =====
        // theme 지정 시 카드 바깥/안쪽 테두리에 테마색 글로우를 입힌다.
        static RectTransform MakeGlassCard(Transform parent, string name, Vector2 pos, Vector2 size, float corner, Color? theme = null)
        {
            var root = ChildRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = pos;
            root.sizeDelta = size;
            DressGlass(root, corner, theme);
            return root; // 콘텐츠는 이 위에 자식으로 추가
        }

        // 이미 크기/위치가 잡힌 root에 글래스 레이어를 입힌다.
        // 레이어 순서(뒤→앞): OuterGlow(테마) → Shadow → Edge → InnerGlow(테마) → Fill
        static void DressGlass(RectTransform root, float corner, Color? theme = null)
        {
            // 겉 테두리 컬러 글로우 — 카드 바깥으로 은은히 번지는 테마색 후광
            if (theme.HasValue)
            {
                var outer = ChildRect("OuterGlow", root);
                outer.anchorMin = Vector2.zero; outer.anchorMax = Vector2.one;
                outer.offsetMin = new Vector2(-30, -34); outer.offsetMax = new Vector2(30, 24);
                var og = outer.gameObject.AddComponent<Image>();
                og.sprite = SceneBuilderUtils.EnsureGlowSprite(); og.type = Image.Type.Sliced;
                og.color = new Color(theme.Value.r, theme.Value.g, theme.Value.b, 0.5f);
                og.raycastTarget = false;
            }

            var shadow = ChildRect("Shadow", root);
            shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one;
            shadow.offsetMin = new Vector2(-16, -30); shadow.offsetMax = new Vector2(16, -2); // 아래로 퍼지는 부드러운 그림자
            var sh = shadow.gameObject.AddComponent<Image>();
            sh.sprite = SceneBuilderUtils.EnsureGlowSprite(); sh.type = Image.Type.Sliced;
            sh.color = GlassShadow; sh.raycastTarget = false;

            var edge = ChildRect("Edge", root);
            StretchFull(edge, 0);
            var e = edge.gameObject.AddComponent<Image>();
            e.sprite = Rounded(); e.type = Image.Type.Sliced; e.pixelsPerUnitMultiplier = corner;
            // 테마가 있으면 밝은 림을 테마색 쪽으로 살짝 물들임 (안쪽 테두리 발광)
            e.color = theme.HasValue ? Color.Lerp(GlassEdge, theme.Value, 0.45f) : GlassEdge;
            e.raycastTarget = false;

            // 안쪽 테두리 컬러 글로우 — 림 안쪽을 따라 도는 소프트 테마색 링
            if (theme.HasValue)
            {
                var inner = ChildRect("InnerGlow", root);
                StretchFull(inner, 3f);
                var ig = inner.gameObject.AddComponent<Image>();
                ig.sprite = SceneBuilderUtils.EnsureGlowSprite(); ig.type = Image.Type.Sliced;
                ig.color = new Color(theme.Value.r, theme.Value.g, theme.Value.b, 0.5f);
                ig.raycastTarget = false;
            }

            var fill = ChildRect("Fill", root);
            // 테마 글로우가 있으면 Fill을 더 안쪽으로 인셋해 안쪽 글로우 띠가 보이게
            StretchFull(fill, theme.HasValue ? 12f : 3f);
            var f = fill.gameObject.AddComponent<Image>();
            f.sprite = Rounded(); f.type = Image.Type.Sliced; f.pixelsPerUnitMultiplier = corner;
            f.color = theme.HasValue ? new Color(GlassFill.r, GlassFill.g, GlassFill.b, 0.78f) : GlassFill;
            f.raycastTarget = false;
        }

        static void BuildBackground(Transform parent)
        {
            var bg = SceneBuilderUtils.CreatePanel("Background", parent);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = BgColor; bgImg.raycastTarget = false;
            // 부드러운 그라데이션 근사: 상단 밝게 + 하단 푸른끼 + 은은한 장식 blob
            AddGlow(parent, new Vector2(0, 420),  new Vector2(2200, 900), new Color(1f, 1f, 1f, 0.6f));
            AddGlow(parent, new Vector2(0, -380), new Vector2(2200, 820), new Color(0.78f, 0.84f, 0.98f, 0.5f));
            AddGlow(parent, new Vector2(-770, 250), new Vector2(360, 360), new Color(0.80f, 0.86f, 1f, 0.45f));
            AddGlow(parent, new Vector2(790, -160), new Vector2(320, 320), new Color(0.86f, 0.90f, 1f, 0.45f));
        }

        static void AddGlow(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var rt = ChildRect("Glow", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SceneBuilderUtils.EnsureGlowSprite(); img.type = Image.Type.Sliced;
            img.color = color; img.raycastTarget = false;
        }

        static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[TrainingHubSceneBuilder] 이미지 없음: {path}");
            return sprite;
        }

        // ===== 공통 UI 헬퍼 =====
        static GameObject MakeCircleBackButton(Transform parent, TMP_FontAsset font)
        {
            // 그림자
            var shadow = ChildRect("BackShadow", parent);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0f, 1f);
            shadow.pivot = new Vector2(0f, 1f);
            shadow.anchoredPosition = new Vector2(44, -44);
            shadow.sizeDelta = new Vector2(104, 104);
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.sprite = SceneBuilderUtils.EnsureGlowSprite(); shImg.type = Image.Type.Sliced;
            shImg.color = GlassShadow; shImg.raycastTarget = false;

            var rect = ChildRect("BackButton", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(48, -40);
            rect.sizeDelta = new Vector2(88, 88);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Builtin("UI/Skin/Knob.psd");
            img.color = White;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, "←", 44, TitleColor, font, bold: true);
            StretchFull(t.rectTransform, 4);

            var back = rect.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            back.SetTarget("MainScene");
            var method = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), back, method);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
            return rect.gameObject;
        }

        static TMP_Text MakeText(string name, Transform parent, string text, int fontSize, Color color, TMP_FontAsset font, bool bold)
        {
            var tmp = SceneBuilderUtils.CreateTMPText(name, parent, text, fontSize);
            tmp.color = color;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void PlaceTop(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void StretchFull(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }
    }
}
