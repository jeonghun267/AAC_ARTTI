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
    //     (2026-10-01 다시 교체: 처음 시안과 패널 자리는 같고 카드 안 작은 유리창만 빠졌다. 중간에 쓴 1536x1024 시안은 버림)
    //     -> 1_left(왼쪽 글래스) / 1_cards(카드 + 캐릭터)로 나눠 쓴다(Art_Source/ui_main/split_panels.py). 1.png를 바꾸면 다시 나눈다.
    //   2 배경(야경 로비) 1672x941
    //   3 좌측 타이틀(ARTTI / 내 손 안의 AAC / 부제) 1086x1448
    //   4 프로필 보기  5 훈련 시작하기  6 도움 시작하기  (2172x724)
    //   7 종료하기  8 리포트보기  (1774x887). 9 설정은 프로필 설정과 겹쳐 쓰지 않는다.
    //   10 현장모드 제목+부제(1580x995)  11 훈련모드 제목+부제(1447x1087): 빛나는 글자 그림. 글자(TMP) 대신 쓴다.
    //
    // 배치 원칙
    //  - 1번 합본이 레이아웃의 기준이다. 패널/캐릭터가 한 장이라 개별로 옮길 수 없으므로
    //    글자와 버튼을 합본 위에 얹는다. 위치 값은 에디터 손배치 결과(Stage px)를 그대로 옮겼다.
    //  - 1920x1080 "Stage"를 화면 중앙에 두고 그 안에 배치한다. 캔버스는 Expand라 20:9 기기에서는
    //    Stage 좌우로 배경만 더 보이고 배치는 무너지지 않는다. 배경(2번)만 캔버스 전체를 덮는다.
    //  - 버튼 이미지는 글로우 투명 여백이 커서, 클릭 영역은 알약 본체 크기로 두고
    //    이미지(Visual)는 여백만큼 바깥으로 넘치게 붙인다. 본체 위치는 이미지 속 본체 비율(Body*)로 계산한다.
    public static class MainSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        const string Dir = "Assets/_Project/Art/UI/Home/main/";
        const string CardsPath     = Dir + "1_cards.png";   // 1.png에서 나눈 카드 + 캐릭터
        const string LeftGlassPath = Dir + "1_left.png";    // 1.png에서 나눈 왼쪽 글래스
        const string BgPath      = Dir + "2.png";
        const string TitlePath   = Dir + "3.png";
        const string ProfilePath = Dir + "4.png";
        const string TrainPath   = Dir + "5.png";
        const string HelpPath    = Dir + "6.png";
        const string QuitPath    = Dir + "7.png";
        const string ReportPath  = Dir + "8.png";
        // 변경점(2026-10-05 새 시안): 제목 + 부제 그림(40/41). 시안 원본에는 아래에 기능 3줄도 있으나 카드 빈 곳이 좁아
        //   글씨가 너무 작아지므로(사용자 결정) 부제 아래를 잘라 넣었다(41: 위 560px, 40: 위 565px). 예전 10/11은 폴더에 남겨 둠
        const string ARTitlePath    = Dir + "40.png";   // 현장지원모드 + 실제 현장에서 빠르게 지원을 받아보세요.
        const string TrainTitlePath = Dir + "41.png";   // 훈련지원모드 + 일상 속 상황에서 나의 생각을 전하는 연습을 해요.

        // 이미지 속 불투명 본체 영역 (x0, y0, x1, y1) — 원본 대비 비율, y는 위에서부터.
        // 4·5·3번은 알파 실측(>200), 6~9번은 화면 판독값이라 몇 px 오차가 있을 수 있다.
        static readonly Rect BodyProfile = Frac(309, 161, 1691, 494, 2000, 667);
        static readonly Rect BodyTrain   = Frac(136, 160, 1866, 503, 2000, 667);
        static readonly Rect BodyHelp    = Frac(148, 152, 1852, 497, 2000, 667);
        static readonly Rect BodyQuit    = Frac(342, 270, 1432, 605, 1774, 887);
        static readonly Rect BodyReport  = Frac(195, 237, 1580, 633, 1774, 887);
        static readonly Rect BodyTitle   = Frac(89, 207, 997, 1283, 1086, 1448);   // 글자 영역
        const float PillAspect = 2172f / 724f;   // 4~6번 원본 비율
        const float TopAspect  = 1774f / 887f;   // 7~9번 원본 비율
        const float TitleAspect = 1086f / 1448f;

        // 합본 px -> Stage 단위. 1920 / 1672 = 1.14833
        const float S = 1920f / 1672f;

        // ===== 1.png(1672x941) 배치 =====
        // 합본이 Stage(1920x1080)와 같은 16:9라 Stage를 꽉 채운다(태블릿에선 패널이 화면 좌우 끝 가까이까지 온다).
        // 왼쪽 글래스는 화면 왼쪽 끝에 붙어 있어 더 옮길 자리가 없고, 처음 시안의 틈(약 60px)이라 옮기지 않는다.
        // 1_left/1_cards로 나눠 둔 것은 틈을 다시 조절할 때 쓰려고 남겨 둔다(값만 바꾸면 된다).
        const float PanelsShiftX = 0f, LeftGlassShiftX = 0f;
        // 글자/버튼 자리 (Stage px, 좌상단 기준, y 아래가 -). 처음 시안의 손배치 값(MainScene.unity, 2026-09-29) 그대로.
        //   카드 안 작은 유리창이 빠졌어도 메모는 같은 자리(캐릭터 오른쪽 빈 유리, 도움 시작하기 오른쪽)에 둔다.
        static readonly Vector2 TitlePos      = new Vector2(238.9f, -438f);
        static readonly Vector2 ProfileBtnPos = new Vector2(238.9f, -744f);
        const float LeftScale = 1f;
        static readonly Vector2 TrainPos      = new Vector2(574.2f, -91.9f);
        const float TrainRot = -4f, TrainScale = 1f;
        static readonly Vector2 TrainNotePos  = new Vector2(1678.9f, -341.1f);
        const float TrainNoteRot = -7f;
        static readonly Vector2 ARPos         = new Vector2(643.1f, -734.9f);
        const float ARRot = 2.5f, ARScale = 1f;
        static readonly Vector2 ARNoteChildPos = new Vector2(425f, -56f);   // ARGroup 안 "필요한 순간" 메모
        static readonly Vector2 ARSideNotePos = new Vector2(1711f, -677.5f);
        const float ARSideNoteRot = 3f;

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

            // 패널 합본 (카드 + 왼쪽 글래스. 그림과 자리는 ApplyNewPanelLayout이 정한다)
            var panels = ChildRect("Panels", stage);
            StretchFull(panels);
            var panelsImg = panels.gameObject.AddComponent<Image>();
            panelsImg.preserveAspect = true;
            panelsImg.raycastTarget = false;

            // 아래 묶음 안 자식 위치/회전/크기 값은 손배치 결과(MainScene.unity, 2026-09-29)를 옮긴 것이다. 단위는 Stage px,
            // Stage 좌상단 기준(y 아래가 -). 크기는 이미지 본체 비율로 계산되므로 폭만 적는다.
            // 묶음 자리/배율(새 1.png 기준)은 마지막에 ApplyNewPanelLayout이 정한다.

            // ===== 좌측 패널: 타이틀 + 프로필 보기 =====
            var left = ChildRect("LeftGroup", stage);
            StretchFull(left);
            var title = ChildRect("Title", left);
            PlaceImageByBody(title, TitlePath, BodyTitle, TitleAspect, bodyCenter: Vector2.zero, bodyWidth: 300f * S);
            title.anchoredPosition = new Vector2(238.9f, -438.0f);
            title.GetComponent<Image>().raycastTarget = false;

            var profileBtn = MakeImageButton("ProfileBtn", left, ProfilePath, BodyProfile, PillAspect,
                bodyCenter: new Vector2(238.9f, -744.0f), bodyWidth: 367.5f);
            var pback = profileBtn.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            pback.SetTarget("ProfileSelectScene");
            var pmethod = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var paction = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), pback, pmethod);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(profileBtn.onClick, paction);

            // ===== 훈련 카드 글자 + 버튼 =====
            // 카드 윗변 기울기에 맞춰 묶음을 -4도 돌린다. 자식 좌표는 묶음 좌상단 기준.
            var train = Group("TrainGroup", stage, new Vector2(574.2f, -91.9f), -4f);
            AddText(train, "Title", "훈련모드", font, 84, TextWhite, true, new Vector2(0f, 0f), new Vector2(436.4f, 114.8f));
            AddText(train, "Desc", "다양한 상황에서 말을 연습해보세요.", font, 28, TextSub, false, new Vector2(0f, -120.6f), new Vector2(482.3f, 50.5f));
            AddText(train, "Note", "함께\n연습하면\n더 잘할 수 있어요!", font, 22, TextNote, false, new Vector2(11.5f, -189.5f), new Vector2(275.6f, 126.3f));
            var trainBtn = MakeImageButton("TrainingModeBtn", train, TrainPath, BodyTrain, PillAspect,
                bodyCenter: new Vector2(149.3f, -378.9f), bodyWidth: 344.5f);
            // 카드 오른쪽 유리 창 속 메모
            var trainNote = Group("TrainSideNote", stage, new Vector2(1678.9f, -341.1f), -7f, centerPivot: true);
            AddText(trainNote, "Note", "같이\n연습해요 :)", font, 30, TextNote, false, new Vector2(-161.7f, 29.9f), new Vector2(298.6f, 137.8f),
                TextAlignmentOptions.Center, rotZ: 14.3f);

            // ===== 현장 카드 글자 + 버튼 =====
            var ar = Group("ARGroup", stage, new Vector2(643.1f, -734.9f), 2.5f);
            AddText(ar, "Title", "현장모드", font, 64, TextWhite, true, new Vector2(7.0f, 113.0f), new Vector2(367.5f, 82.7f), rotZ: -2.45f);
            AddText(ar, "Desc", "실생활에서 도움을 받아보세요.", font, 26, TextSub, false, new Vector2(12.2f, -2.9f), new Vector2(413.4f, 43.6f), rotZ: -5.34f);
            AddText(ar, "Note", "필요한 순간,\n언제나\n함께할게요.", font, 22, TextNote, false, new Vector2(425.0f, -56.0f), new Vector2(172.2f, 126.3f));
            var arBtn = MakeImageButton("ARFieldModeBtn", ar, HelpPath, BodyHelp, PillAspect,
                bodyCenter: new Vector2(141.0f, -141.0f), bodyWidth: 344.5f);
            arBtn.transform.localRotation = Quaternion.Euler(0f, 0f, -4.78f);
            var arNote = Group("ARSideNote", stage, new Vector2(1711.0f, -677.5f), 3f, centerPivot: true);
            AddText(arNote, "Note", "언제든\n도와드릴게요 :)", font, 26, TextNote, false, new Vector2(-137.8f, 51.7f), new Vector2(275.6f, 103.3f),
                TextAlignmentOptions.Center);

            // ===== 우상단 버튼: 종료하기 / 리포트보기 =====
            // Stage가 아니라 캔버스(화면) 우상단에 붙인다. 20:9 기기에서도 화면 모서리에 붙고, 16:9 밖으로 나가지 않는다.
            // 손배치 값: 본체 높이 64에 배율 1.168, 버튼 사이 약 21. 가로 위치는 손배치 그대로(Stage 오른쪽 끝 기준으로 환산).
            // 세로는 손배치 때 화면이 16:9보다 길어 Stage 위(+87)에 놓였는데, 16:9 폰에서는 화면 밖이 되므로
            // 화면 윗변에서 TopMargin만큼 내린 자리로 고정한다.
            // 변경점: 설정 버튼은 프로필 설정과 겹쳐 뺐다. 리포트보기 오른쪽 끝이 설정 오른쪽 끝(화면 끝에서 35.9)에
            // 오도록 두 버튼을 설정 폭 + 간격(249.1)만큼 오른쪽으로 옮겼다.
            var top = ChildRect("TopRightMenu", canvasGo.transform);
            StretchFull(top);
            const float topH = 64f, topScale = 1.168f, TopMargin = 24f, TopShift = 249.1f;
            float topY = -(TopMargin + topH * topScale / 2f);
            float wRep = topH * BodyReport.width * TopAspect / BodyReport.height;
            float wQuit = topH * BodyQuit.width * TopAspect / BodyQuit.height;
            var quitBtn = MakeImageButton("QuitBtn", top, QuitPath, BodyQuit, TopAspect, new Vector2(1231.0f - 1920f + TopShift, topY), wQuit, anchorRight: true);
            var reportBtn = MakeImageButton("ReportBtn", top, ReportPath, BodyReport, TopAspect, new Vector2(1504.3f - 1920f + TopShift, topY), wRep, anchorRight: true);
            foreach (var b in new[] { quitBtn, reportBtn }) b.transform.localScale = Vector3.one * topScale;

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

            ApplyNewPanelLayout(stage);
            ApplyModeTitleImages(stage);

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 완료 v5 (새 1.png: 왼쪽 글래스 분리 + 글자/버튼 재배치)");
        }

        // 손배치 씬(MainScene)에 새 1.png 배치만 다시 적용한다. 다른 손배치 값(묶음 안 글자 위치, 우상단 버튼 등)은 그대로 둔다.
        [MenuItem("Artti/Fix/MainScene - Apply New Panels 1.png (keep hand placement)")]
        public static void ApplyNewPanelsMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[MainSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.Main)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.Main);
            }
            RectTransform stage = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "[Canvas]") stage = root.transform.Find("Stage") as RectTransform;
            if (stage == null) { Debug.LogError("[MainSceneBuilder] [Canvas]/Stage 없음"); return; }

            ApplyNewPanelLayout(stage);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 새 1.png 배치 적용 완료 (왼쪽 글래스 분리 + 글자/버튼 재배치) - 손배치 유지");
        }

        // 손배치 씬(MainScene)에 모드 제목 그림(40/41)과 두 버튼 자리만 적용한다. 다른 손배치 값은 그대로 둔다.
        [MenuItem("Artti/Fix/MainScene - Mode Title Images 40 41 (keep hand placement)")]
        public static void ApplyModeTitleImagesMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[MainSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.Main)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.Main);
            }
            RectTransform stage = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "[Canvas]") stage = root.transform.Find("Stage") as RectTransform;
            if (stage == null) { Debug.LogError("[MainSceneBuilder] [Canvas]/Stage 없음"); return; }

            ApplyModeTitleImages(stage);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 모드 제목 그림(40/41) 적용 완료 - 손배치 유지");
        }

        // 모드 제목 그림: 묶음(TrainGroup/ARGroup)의 제목(Title)+부제(Desc) 글자를 끄고 그림 한 장(TitleImage)으로 바꾼다.
        //   그림 속 제목 글자 왼쪽 위(glyph, 그림 px)가 예전 글자 왼쪽 위 자리(묶음 좌표)에 오고, 제목 글자 폭이 예전과 같도록 줄인다.
        //   글자 폭/자리는 예전 TMP 렌더 실측(훈련 84pt 약 317, 현장 64pt 약 245 Stage px).
        //   변경점: 제목과 버튼 사이 메모 글이 빠져 둘이 멀어 보이므로, 부제와 버튼 사이를 약 80으로 좁힌다.
        //   훈련 카드는 제목을 55 내리고 버튼을 57 올려 묶음이 카드 세로 가운데에 오게, 현장 카드는 제목만 22 내린다.
        //   변경점(2026-10-05 새 시안 40/41, 제목 + 부제): 그림 본체(알파>200) 왼쪽 위를 기준점으로, 두 카드 같은 배율(0.306)로 놓는다.
        //   훈련: 본체 Stage (580,165)부터 폭 400(오른쪽 = 캐릭터 머리·소매 왼쪽, 태블릿 캡처 실측), 버튼은 그림 아래 40.
        //   현장: 본체 Stage (618,658)부터(왼쪽 = 버튼 알약 왼쪽 끝), 버튼은 예전 손배치 그대로(그림 아래 약 42).
        static readonly Vector2 TrainBtnCenter = new Vector2(149.3f, -282.1f);   // 예전 손배치 -378.9, 10/11 그림 때 -321.9
        static readonly Vector2 ARBtnCenter = new Vector2(141f, -141f);          // 예전 손배치 그대로
        static void ApplyModeTitleImages(RectTransform stage)
        {
            ModeTitleImage(stage, "TrainGroup", TrainTitlePath, new Vector2(1448f, 560f),
                glyphTopLeft: new Vector2(70f, 113f), glyphWidth: 1308f, target: new Vector2(10.89f, -72.52f), targetWidth: 400f, rotZ: 0f);
            ModeTitleImage(stage, "ARGroup", ARTitlePath, new Vector2(1447f, 565f),
                glyphTopLeft: new Vector2(92f, 112f), glyphWidth: 1281f, target: new Vector2(-21.72f, 77.92f), targetWidth: 391.74f, rotZ: -2.45f);
            if (stage.Find("TrainGroup/TrainingModeBtn") is RectTransform trainBtn) trainBtn.anchoredPosition = TrainBtnCenter;
            if (stage.Find("ARGroup/ARFieldModeBtn") is RectTransform arBtn) arBtn.anchoredPosition = ARBtnCenter;
        }

        static void ModeTitleImage(RectTransform stage, string group, string path, Vector2 imageSize,
            Vector2 glyphTopLeft, float glyphWidth, Vector2 target, float targetWidth, float rotZ)
        {
            if (!(stage.Find(group) is RectTransform g)) { Debug.LogWarning($"[MainSceneBuilder] {group} 없음"); return; }
            foreach (var name in new[] { "Title", "Desc" })
            {
                var old = g.Find(name);
                if (old != null) old.gameObject.SetActive(false);   // 글자는 그림에 들어 있다(오브젝트는 남겨 둔다)
            }
            var rt = g.Find("TitleImage") as RectTransform;
            if (rt == null) rt = ChildRect("TitleImage", g);
            rt.SetAsFirstSibling();
            float k = targetWidth / glyphWidth;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(glyphTopLeft.x / imageSize.x, 1f - glyphTopLeft.y / imageSize.y);   // 글자 왼쪽 위를 기준점으로
            rt.sizeDelta = imageSize * k;
            rt.anchoredPosition = target;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            rt.localScale = Vector3.one;
            var img = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        // 1.png 배치: Panels = 카드(1_cards), 그 자식 LeftGlass = 왼쪽 글래스(1_left, LeftGlassShiftX만큼 옮김).
        // 글자/버튼 묶음은 자리/회전/배율만 바꾼다(묶음 안 자식 배치는 그대로). 빌드와 Fix 메뉴가 같이 쓴다.
        static void ApplyNewPanelLayout(RectTransform stage)
        {
            if (stage.Find("Panels") is RectTransform panels)
            {
                panels.offsetMin = new Vector2(PanelsShiftX, panels.offsetMin.y);
                panels.offsetMax = new Vector2(PanelsShiftX, panels.offsetMax.y);
                if (panels.TryGetComponent<Image>(out var cards)) cards.sprite = LoadSprite(CardsPath);
                for (int i = panels.childCount - 1; i >= 0; i--)
                    if (panels.GetChild(i).name == "LeftGlass") Object.DestroyImmediate(panels.GetChild(i).gameObject);
                var glass = ChildRect("LeftGlass", panels);
                StretchFull(glass);
                glass.offsetMin = new Vector2(LeftGlassShiftX, 0f);
                glass.offsetMax = new Vector2(LeftGlassShiftX, 0f);
                var glassImg = glass.gameObject.AddComponent<Image>();
                glassImg.sprite = LoadSprite(LeftGlassPath);
                glassImg.preserveAspect = true;
                glassImg.raycastTarget = false;
            }
            else Debug.LogWarning("[MainSceneBuilder] Stage/Panels 없음");

            Place(stage, "LeftGroup/Title", TitlePos, 0f, LeftScale);
            Place(stage, "LeftGroup/ProfileBtn", ProfileBtnPos, 0f, LeftScale);
            Place(stage, "TrainGroup", TrainPos, TrainRot, TrainScale);
            Place(stage, "TrainSideNote", TrainNotePos, TrainNoteRot, TrainScale);
            Place(stage, "ARGroup", ARPos, ARRot, ARScale);
            Place(stage, "ARSideNote", ARSideNotePos, ARSideNoteRot, ARScale);
            if (stage.Find("ARGroup/Note") is RectTransform arNote) arNote.anchoredPosition = ARNoteChildPos;
        }

        static void Place(Transform stage, string path, Vector2 pos, float rotZ, float scale)
        {
            if (!(stage.Find(path) is RectTransform rt)) { Debug.LogWarning($"[MainSceneBuilder] {path} 없음"); return; }
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        // ===== 배치 헬퍼 =====

        // 픽셀 좌표를 이미지 크기 대비 비율로
        static Rect Frac(float x0, float y0, float x1, float y1, float w, float h) =>
            Rect.MinMaxRect(x0 / w, y0 / h, x1 / w, y1 / h);

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
        // anchorRight면 부모 우상단 기준(bodyCenter.x가 음수)
        static Button MakeImageButton(string name, Transform parent, string path, Rect body, float aspect,
            Vector2 bodyCenter, float bodyWidth, bool anchorRight = false)
        {
            float bodyH = bodyWidth * (body.height / body.width) / aspect;
            var root = ChildRect(name, parent);
            root.anchorMin = root.anchorMax = anchorRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
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

        // pos/size는 Stage px(부모 좌상단 기준, 아래가 -)
        static TMP_Text AddText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            bool bold, Vector2 pos, Vector2 boxSize, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float rotZ = 0f)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = boxSize;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
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
