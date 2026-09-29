using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Artti.UI;

namespace Artti.Editor
{
    // 메인(모드 선택) 화면 v3 — 다크 네온 최종 시안.
    // 에셋: Art/UI/Home/main/1~9.png
    //   1 패널 합본(좌측 패널 + 훈련 카드 + 현장 카드 + 두 캐릭터, 투명 배경) 1672x941
    //   2 배경(야경 로비) 1672x941
    //   3 좌측 타이틀(ARTTI / 내 손 안의 AAC / 부제) 1086x1448
    //   4 프로필 보기  5 훈련 시작하기  6 도움 시작하기  (2172x724)
    //   7 종료하기  8 리포트보기  9 설정  (1774x887)
    //
    // 배치 원칙
    //  - 1번 합본이 레이아웃의 기준이다. 패널/캐릭터가 한 장이라 개별로 옮길 수 없으므로
    //    글자와 버튼을 합본 픽셀 좌표(1672x941)에 맞춰 얹는다. 좌표는 전부 합본 px로 적고 C()로 변환한다.
    //  - 1920x1080 "Stage"를 화면 중앙에 두고 그 안에 배치한다. 캔버스는 Expand라 20:9 기기에서는
    //    Stage 좌우로 배경만 더 보이고 배치는 무너지지 않는다. 배경(2번)만 캔버스 전체를 덮는다.
    //  - 버튼 이미지는 글로우 투명 여백이 커서, 클릭 영역은 알약 본체 크기로 두고
    //    이미지(Visual)는 여백만큼 바깥으로 넘치게 붙인다. 본체 위치는 이미지 속 본체 비율(Body*)로 계산한다.
    public static class MainSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        const string Dir = "Assets/_Project/Art/UI/Home/main/";
        const string PanelsPath  = Dir + "1.png";
        const string BgPath      = Dir + "2.png";
        const string TitlePath   = Dir + "3.png";
        const string ProfilePath = Dir + "4.png";
        const string TrainPath   = Dir + "5.png";
        const string HelpPath    = Dir + "6.png";
        const string QuitPath    = Dir + "7.png";
        const string ReportPath  = Dir + "8.png";
        const string SettingPath = Dir + "9.png";

        // 이미지 속 불투명 본체 영역 (x0, y0, x1, y1) — 원본 대비 비율, y는 위에서부터.
        // 4·5·3번은 알파 실측(>200), 6~9번은 화면 판독값이라 몇 px 오차가 있을 수 있다.
        static readonly Rect BodyProfile = Frac(309, 161, 1691, 494, 2000, 667);
        static readonly Rect BodyTrain   = Frac(136, 160, 1866, 503, 2000, 667);
        static readonly Rect BodyHelp    = Frac(148, 152, 1852, 497, 2000, 667);
        static readonly Rect BodyQuit    = Frac(342, 270, 1432, 605, 1774, 887);
        static readonly Rect BodyReport  = Frac(195, 237, 1580, 633, 1774, 887);
        static readonly Rect BodySetting = Frac(333, 257, 1440, 620, 1774, 887);
        static readonly Rect BodyTitle   = Frac(89, 207, 997, 1283, 1086, 1448);   // 글자 영역
        const float PillAspect = 2172f / 724f;   // 4~6번 원본 비율
        const float TopAspect  = 1774f / 887f;   // 7~9번 원본 비율
        const float TitleAspect = 1086f / 1448f;

        // 합본 px -> Stage 단위. 1920 / 1672 = 1.14833
        const float S = 1920f / 1672f;
        static Vector2 C(float x, float y) => new Vector2(x * S, -y * S);   // Stage 좌상단 기준 위치

        static readonly Color TextWhite = new Color(0.96f, 0.97f, 1f, 1f);
        static readonly Color TextSub   = new Color(0.84f, 0.88f, 1f, 0.92f);
        static readonly Color TextNote  = new Color(0.80f, 0.86f, 1f, 0.80f);

        [MenuItem("Artti/Build MainScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.Main);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();

            // ===== 카메라 (Screen Space - Camera 렌더용) =====
            var camGo = new GameObject("[UICamera]") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(8, 14, 34, 255);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // ===== 캔버스 =====
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var font = SceneBuilderUtils.GetKoreanFont();

            // ===== 배경 (캔버스 전체, 비율 유지 cover) =====
            var bg = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = LoadSprite(BgPath);
            bgImg.raycastTarget = false;
            var bgFit = bg.AddComponent<AspectRatioFitter>();
            bgFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bgFit.aspectRatio = 1672f / 941f;

            // ===== Stage (1920x1080, 화면 중앙) =====
            var stage = ChildRect("Stage", canvasGo.transform);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = ReferenceResolution;

            // 패널 합본
            var panels = ChildRect("Panels", stage);
            StretchFull(panels);
            var panelsImg = panels.gameObject.AddComponent<Image>();
            panelsImg.sprite = LoadSprite(PanelsPath);
            panelsImg.preserveAspect = true;
            panelsImg.raycastTarget = false;

            // ===== 좌측 패널: 타이틀 + 프로필 보기 =====
            // 좌측 패널 안쪽은 합본 x 45~370. 가운데 x 208에 맞춘다.
            var left = ChildRect("LeftGroup", stage);
            StretchFull(left);
            var title = ChildRect("Title", left);
            PlaceImageByBody(title, TitlePath, BodyTitle, TitleAspect,
                bodyCenter: C(208f, 105f + 300f / 2f * (1076f / 908f)), bodyWidth: 300f * S);
            title.GetComponent<Image>().raycastTarget = false;

            var profileBtn = MakeImageButton("ProfileBtn", left, ProfilePath, BodyProfile, PillAspect,
                bodyCenter: C(208f, 578f), bodyWidth: 320f * S);
            var pback = profileBtn.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            pback.SetTarget("ProfileSelectScene");
            var pmethod = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var paction = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), pback, pmethod);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(profileBtn.onClick, paction);

            // ===== 훈련 카드 글자 + 버튼 =====
            // 카드 윗변이 오른쪽으로 내려가는 기울기(약 7도)라 글자 묶음을 -4도 돌려 흐름을 맞춘다.
            var train = Group("TrainGroup", stage, C(500f, 80f), -4f);
            AddText(train, "Title", "훈련모드", font, 84, TextWhite, true, 0f, 0f, 380f, 100f);
            AddText(train, "Desc", "다양한 상황에서 말을 연습해보세요.", font, 28, TextSub, false, 0f, 105f, 420f, 44f);
            AddText(train, "Note", "함께\n연습하면\n더 잘할 수 있어요!", font, 22, TextNote, false, 10f, 165f, 240f, 110f);
            // 버튼 오른쪽 끝(합본 x 800)이 캐릭터 손(x 820~)에 닿지 않게 폭 300.
            var trainBtn = MakeImageButton("TrainingModeBtn", train, TrainPath, BodyTrain, PillAspect,
                bodyCenter: Local(-20f + 150f, 300f + 30f), bodyWidth: 300f * S);
            // 카드 오른쪽 유리 창(합본 1300~1625, 210~385) 속 메모
            var trainNote = Group("TrainSideNote", stage, C(1462f, 297f), -7f, centerPivot: true);
            AddText(trainNote, "Note", "같이\n연습해요 :)", font, 30, TextNote, false, -130f, -60f, 260f, 120f, TextAlignmentOptions.Center);

            // ===== 현장 카드 글자 + 버튼 =====
            // 안쪽 유리 창(합본 520~1060, 628~815) 안에 제목/설명/버튼을 넣는다. 카드 윗변이 오른쪽으로 올라가 +2.5도.
            var ar = Group("ARGroup", stage, C(560f, 640f), 2.5f);
            AddText(ar, "Title", "현장모드", font, 64, TextWhite, true, 0f, -8f, 320f, 72f);
            AddText(ar, "Desc", "실생활에서 도움을 받아보세요.", font, 26, TextSub, false, 0f, 62f, 360f, 38f);
            AddText(ar, "Note", "필요한 순간,\n언제나\n함께할게요.", font, 22, TextNote, false, 350f, 10f, 150f, 110f);
            var arBtn = MakeImageButton("ARFieldModeBtn", ar, HelpPath, BodyHelp, PillAspect,
                bodyCenter: Local(150f, 142f), bodyWidth: 300f * S);
            var arNote = Group("ARSideNote", stage, C(1490f, 590f), 3f, centerPivot: true);
            AddText(arNote, "Note", "언제든\n도와드릴게요 :)", font, 26, TextNote, false, -120f, -45f, 240f, 90f, TextAlignmentOptions.Center);

            // ===== 우상단 버튼: 종료하기 / 리포트보기 / 설정 =====
            // 본체 높이 64, 폭은 각 이미지 본체 비율. 오른쪽 끝 36 여백, 간격 18, 윗변 26.
            var top = ChildRect("TopRightMenu", stage);
            StretchFull(top);
            const float topH = 64f, topY = 26f + topH / 2f, gap = 18f;
            float wSet = topH * BodySetting.width * TopAspect / BodySetting.height;
            float wRep = topH * BodyReport.width * TopAspect / BodyReport.height;
            float wQuit = topH * BodyQuit.width * TopAspect / BodyQuit.height;
            float xSet = 1920f - 36f - wSet / 2f;
            float xRep = xSet - wSet / 2f - gap - wRep / 2f;
            float xQuit = xRep - wRep / 2f - gap - wQuit / 2f;
            var quitBtn = MakeImageButton("QuitBtn", top, QuitPath, BodyQuit, TopAspect, new Vector2(xQuit, -topY), wQuit);
            var reportBtn = MakeImageButton("ReportBtn", top, ReportPath, BodyReport, TopAspect, new Vector2(xRep, -topY), wRep);
            // 설정씬 미구현. 모양만 있고 클릭 동작은 없다.
            MakeImageButton("SettingsBtn", top, SettingPath, BodySetting, TopAspect, new Vector2(xSet, -topY), wSet);

            var menu = canvasGo.AddComponent<HomeMenu>();
            WireClick(quitBtn, menu, nameof(HomeMenu.Quit));
            WireClick(reportBtn, menu, nameof(HomeMenu.OpenReport));

            // ===== 진입 애니메이션 =====
            var intro = canvasGo.AddComponent<HomeIntroAnimator>();
            intro.AddItem(panels, new Vector2(0, -30), 0f, 0.00f, 0.55f, fade: true);
            intro.AddItem(left,   new Vector2(-120, 0), 0f, 0.10f, 0.55f, fade: true);
            intro.AddItem(train,  new Vector2(0, -24), 0f, 0.25f, 0.45f, fade: true);
            intro.AddItem(trainNote, Vector2.zero, 0f, 0.35f, 0.45f, fade: true);
            intro.AddItem(ar,     new Vector2(0, -24), 0f, 0.35f, 0.45f, fade: true);
            intro.AddItem(arNote, Vector2.zero, 0f, 0.45f, 0.45f, fade: true);
            intro.AddItem(top,    new Vector2(0, 40), 0f, 0.20f, 0.40f, fade: true);

            // ===== MainSceneView 와이어링 =====
            // 프로필 보기는 시안 문구 고정이라 greeting/profile 라벨은 비워 둔다(MainSceneView가 null을 건너뜀).
            var view = canvasGo.AddComponent<MainSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("trainingModeBtn").objectReferenceValue = trainBtn;
            so.FindProperty("arFieldModeBtn").objectReferenceValue = arBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 완료 v3 (다크 네온 최종 시안)");
        }

        // ===== 배치 헬퍼 =====

        // 픽셀 좌표를 이미지 크기 대비 비율로
        static Rect Frac(float x0, float y0, float x1, float y1, float w, float h) =>
            Rect.MinMaxRect(x0 / w, y0 / h, x1 / w, y1 / h);

        // Group 로컬 좌표(합본 px, 좌상단 기준, 아래가 +)를 Stage 단위로
        static Vector2 Local(float x, float y) => new Vector2(x * S, -y * S);

        // 글자/버튼 묶음. pivot 좌상단(centerPivot이면 중앙)에서 rotZ만큼 돌린다.
        static RectTransform Group(string name, Transform parent, Vector2 pos, float rotZ, bool centerPivot = false)
        {
            var g = ChildRect(name, parent);
            g.anchorMin = g.anchorMax = new Vector2(0f, 1f);
            g.pivot = centerPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            g.anchoredPosition = pos;
            g.sizeDelta = Vector2.zero;
            g.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return g;
        }

        // 이미지 속 본체(body)가 bodyCenter/bodyWidth에 오도록 이미지 전체 rect를 계산해 놓는다.
        // bodyCenter는 부모 좌상단 기준(Stage 단위).
        static void PlaceImageByBody(RectTransform rt, string path, Rect body, float aspect, Vector2 bodyCenter, float bodyWidth)
        {
            float fullW = bodyWidth / body.width;
            float fullH = fullW / aspect;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(fullW, fullH);
            rt.anchoredPosition = bodyCenter + BodyToFullOffset(body, fullW, fullH);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path);
            img.preserveAspect = true;
        }

        // 본체 중심 -> 이미지 전체 중심까지의 오프셋 (y 위가 +)
        static Vector2 BodyToFullOffset(Rect body, float fullW, float fullH) =>
            new Vector2((0.5f - body.center.x) * fullW, (body.center.y - 0.5f) * fullH);

        // 루트 = 클릭 영역(본체 크기, 투명), 자식 Visual = 여백 포함 이미지.
        // 눌림/오버는 Visual 스케일로만 표현한다(HomeButtonGlow, glow 없음). 클릭 영역은 그대로.
        static Button MakeImageButton(string name, Transform parent, string path, Rect body, float aspect,
            Vector2 bodyCenter, float bodyWidth)
        {
            float bodyH = bodyWidth * (body.height / body.width) / aspect;
            var root = ChildRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = bodyCenter;
            root.sizeDelta = new Vector2(bodyWidth, bodyH);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            float fullW = bodyWidth / body.width;
            float fullH = fullW / aspect;
            var visual = ChildRect("Visual", root);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(fullW, fullH);
            visual.anchoredPosition = BodyToFullOffset(body, fullW, fullH);
            var img = visual.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path);
            img.preserveAspect = true;
            img.raycastTarget = false;

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HomeButtonGlow>().Setup(null, visual);
            return btn;
        }

        // x, y, w, h는 합본 px(부모 좌상단 기준, 아래가 +)
        static TMP_Text AddText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            bool bold, float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Local(x, y);
            rt.sizeDelta = new Vector2(w * S, h * S);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void WireClick(Button btn, MonoBehaviour target, string method)
        {
            var m = target.GetType().GetMethod(method);
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, m);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // 1672~2172px 원본이라 최대 크기를 2048로 올린다(기본 1024면 합본/배경이 흐려진다).
        static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[MainSceneBuilder] 이미지 없음: {path}");
            return sprite;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
