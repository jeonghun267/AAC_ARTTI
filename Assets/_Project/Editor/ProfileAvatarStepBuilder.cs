using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;

namespace Artti.Editor
{
    // 프로필 등록 1단계(아바타 선택) — 다크 네온 시안. ProfileCreateSceneBuilder가 폼 위에 덮어 만든다.
    // 에셋: Art/UI/Home/profiles/10~28.png
    //   10 배경(1448x1086)  12 여성 / 13 남성 분류 탭  16 랜덤으로 선택하기  17 다음  18 닫기(X)
    //   (11 전체 / 14 중성 / 15 스타일 탭은 2026-10-01부터 쓰지 않는다. 탭은 여성/남성 두 개)
    //   19~27 아바타 9종(1254x1254, 19만 1086x1448)  28 선택 카드 완성본(19번 캐릭터가 박혀 있어 미사용)
    //   30~32 단계 원(1/2/3)  33 완료 체크  34 연결선  80 인용문 띠(따옴표가 그려진 네온 테두리, 1·2단계 큰 카드 아래)
    // 시안에 있지만 이미지가 아직 없는 것(패널 창, 칸 테두리)은 코드로 그린다.
    //
    // 배치: 1920x1080 Stage를 화면 중앙에 두고 Stage 좌상단 기준(y 아래가 -)으로 놓는다.
    // 시안은 태블릿(약 1.12:1)이라 가로 16:9에 맞춰 다시 배치했다. 그리드는 5x2로 넓혀 9종이 한 화면에 다 들어간다.
    public static class ProfileAvatarStepBuilder
    {
        internal const string Dir = "Assets/_Project/Art/UI/Home/profiles/";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";

        // 이미지 속 불투명 본체 영역(알파>200 실측, 픽셀)
        internal const float PillW = 2172f, PillH = 724f;
        // 분류 탭: 여성, 남성 순서 (ProfileAvatarStepView.TabFilter와 같은 순서)
        static readonly string[] TabFiles = { "12", "13" };
        static readonly string[] TabNames = { "TabFemale", "TabMale" };
        static readonly Rect[] TabBodies =
        {
            Rect.MinMaxRect(629, 221, 1544, 500),   // 12 여성
            Rect.MinMaxRect(670, 232, 1502, 491),   // 13 남성
        };
        const float TabH = 58f, TabGap = 22f, TabY = -350f;
        static readonly Rect RandomBody = Rect.MinMaxRect(166, 202, 2006, 533);
        internal static readonly Rect NextBody = Rect.MinMaxRect(126, 126, 2046, 569);
        static readonly Rect CloseBody  = Rect.MinMaxRect(423, 421, 832, 824);   // 1254x1254

        // 인용문 띠 80.png (2155x730). 본체(알파>200)와, 글자가 들어갈 따옴표 사이 가로 범위(본체 폭 비율).
        //   왼쪽 위 따옴표 x 219~335, 오른쪽 아래 따옴표 x 1825~1940 -> 본체 기준 0.133 / 0.869. 몇 % 여유를 둔다.
        const string QuoteFramePath = Dir + "80.png";
        static readonly Vector2 QuoteFrameSize = new Vector2(2155f, 730f);
        static readonly Rect QuoteFrameBody = Rect.MinMaxRect(65, 108, 2090, 599);
        const float QuoteTextX0 = 0.15f, QuoteTextX1 = 0.85f;
        const float QuoteBandBottom = 12f;   // 카드 아랫변에서 띠 본체까지 (빛 번짐이 카드 마스크에 덜 잘리게)

        // 고른 칸의 LED 테두리(완료 카드 35의 원 테두리 느낌). Art_Source/ui_profiles/make_led_frame.py가 만든다.
        //   2배 해상도 9-slice: 테두리 132px = 66단위, 그림이 칸 밖으로 LedFrameMargin만큼 넘친다(빛 번짐 자리).
        const string LedFramePath = Dir + "derived/led_tile_frame.png";
        const float LedFrameMargin = 18f, LedFramePx = 2f, LedFrameBorderPx = 132f;

        // 아바타 목록. 첫 칸이 기본 선택. 분류는 탭 필터용(여성/남성 중 하나)이라 필요하면 여기서만 고치면 된다.
        struct AvatarEntry { public string file; public AvatarTile.Category cat; }
        static readonly AvatarEntry[] Avatars =
        {
            new AvatarEntry { file = "19", cat = AvatarTile.Category.Female },   // 점원 (웨이브 머리)
            new AvatarEntry { file = "21", cat = AvatarTile.Category.Male },     // 검은 머리 후드
            new AvatarEntry { file = "20", cat = AvatarTile.Category.Female },   // 단발
            new AvatarEntry { file = "24", cat = AvatarTile.Category.Male },     // 안경
            new AvatarEntry { file = "22", cat = AvatarTile.Category.Male },     // 모자
            new AvatarEntry { file = "23", cat = AvatarTile.Category.Male },     // 은발
            new AvatarEntry { file = "27", cat = AvatarTile.Category.Female },   // 올림머리
            new AvatarEntry { file = "25", cat = AvatarTile.Category.Male },     // 검은 머리 티셔츠
            new AvatarEntry { file = "26", cat = AvatarTile.Category.Female },   // 헤드폰
        };

        // 변경점: 창이 너무 짙어(0.86) 배경이 더 비치게 낮췄다. 창 테두리는 약한 LED(카드 테두리보다 옅게).
        static readonly Color PanelFill   = new Color(0.05f, 0.08f, 0.20f, 0.62f);
        static readonly Color PanelGlowTint = new Color(0.80f, 0.88f, 1.00f, 0.55f);
        static readonly Color TileFill    = new Color(0.13f, 0.18f, 0.36f, 0.92f);
        internal static readonly Color Accent = new Color(0.50f, 0.72f, 1.00f, 1.00f);
        static readonly Color TextWhite   = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color TextSub     = new Color(0.78f, 0.84f, 1.00f, 0.90f);
        static readonly Color TextDim     = new Color(0.62f, 0.68f, 0.88f, 0.75f);

        // 다른 빌더(ProfileSelectSceneBuilder.EnsureAvatarLibrary)도 쓰는 아바타 파일 목록
        public static string[] AvatarPaths()
        {
            var paths = new string[Avatars.Length];
            for (int i = 0; i < Avatars.Length; i++) paths[i] = Dir + Avatars[i].file + ".png";
            return paths;
        }

        public static RectTransform Build(Transform canvas, ProfileCreateView form, TMP_FontAsset font)
        {
            var stage = BuildFrame(canvas, "AvatarStep", font, 0, out var closeBtn);
            var root = (RectTransform)stage.parent;

            // ===== 제목 =====
            AddHeading(stage, font, "프로필을 선택하세요", "나를 가장 잘 표현하는 아바타를 선택해보세요.");

            // ===== 분류 탭: 여성 / 남성 (본체 높이 58, 간격 22, 가운데 정렬) =====
            var tabs = new Button[TabBodies.Length];
            for (int i = 0; i < TabBodies.Length; i++)
            {
                TabSlot(i, out var center, out var width);
                tabs[i] = MakeImageButton(TabNames[i], stage, Dir + TabFiles[i] + ".png", TabBodies[i], PillW, PillH, center, width);
            }

            // ===== 왼쪽: 선택한 아바타 큰 카드 (x 90~520, y 400~1000) =====
            var preview = AvatarCard(stage, new Vector2(305f, -700f), new Vector2(430f, 600f),
                "당신만의 새로운 이야기가\n시작됩니다.", font);

            // ===== 오른쪽: 아바타 그리드 (5x2, 칸 200, 간격 18) =====
            var grid = ChildRect("AvatarGrid", stage);
            grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = new Vector2(659f, -400f);
            grid.sizeDelta = new Vector2(1072f, 418f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(200f, 200f);
            layout.spacing = new Vector2(18f, 18f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperLeft;
            foreach (var a in Avatars) MakeTile(grid, a);

            // ===== 아래: 랜덤으로 선택하기 / 다음 =====
            var randomBtn = MakeImageButton("RandomBtn", stage, Dir + "16.png", RandomBody, PillW, PillH,
                new Vector2(659f + 278f, -920f), 556f);
            var nextBtn = MakeImageButton("NextBtn", stage, Dir + "17.png", NextBody, PillW, PillH,
                new Vector2(1731f - 200f, -920f), 400f);

            var view = root.gameObject.AddComponent<ProfileAvatarStepView>();
            view.Setup(form, grid, tabs, preview, randomBtn, nextBtn, closeBtn);

            AddToAvatarLibrary();
            return root;
        }

        // i번째 탭 본체의 가운데와 폭 (탭 전체를 Stage 가운데 x 960에 맞춘다)
        static void TabSlot(int index, out Vector2 center, out float width)
        {
            float total = TabGap * (TabBodies.Length - 1);
            for (int i = 0; i < TabBodies.Length; i++) total += TabH * TabBodies[i].width / TabBodies[i].height;
            float x = 960f - total / 2f;
            for (int i = 0; i < index; i++) x += TabH * TabBodies[i].width / TabBodies[i].height + TabGap;
            width = TabH * TabBodies[index].width / TabBodies[index].height;
            center = new Vector2(x + width / 2f, TabY);
        }

        // 손배치가 있는 ProfileCreateScene에 분류 탭 변경만 적용한다: 전체/중성/스타일 탭을 지우고 여성/남성 두 개를 가운데로 모은다.
        // 아바타 칸의 분류도 Avatars 목록(여성/남성)대로 다시 넣는다. 다른 손배치 값은 그대로 둔다.
        [MenuItem("Artti/Fix/ProfileCreate - Avatar Tabs Female + Male Only (keep hand placement)")]
        public static void ApplyGenderTabs()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileAvatarStepBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ProfileCreate)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.ProfileCreate);
            }

            ProfileAvatarStepView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<ProfileAvatarStepView>(true);
                if (view != null) break;
            }
            var stage = view != null ? view.transform.Find("Stage") : null;
            if (stage == null) { Debug.LogError("[ProfileAvatarStepBuilder] AvatarStep/Stage 없음"); return; }

            // 예전 탭 이름: Tab0 전체, Tab1 여성, Tab2 남성, Tab3 중성, Tab4 스타일
            string[] oldNames = { "Tab1", "Tab2" };
            var tabs = new Button[TabBodies.Length];
            for (int i = 0; i < TabBodies.Length; i++)
            {
                var t = stage.Find(TabNames[i]);
                if (t == null) t = stage.Find(oldNames[i]);
                if (t == null || !t.TryGetComponent(out tabs[i])) { Debug.LogError($"[ProfileAvatarStepBuilder] {TabNames[i]} 없음"); return; }
                t.name = TabNames[i];
                TabSlot(i, out var center, out _);
                ((RectTransform)t).anchoredPosition = center;
            }
            foreach (var name in new[] { "Tab0", "Tab3", "Tab4" })
            {
                var old = stage.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var so = new SerializedObject(view);
            var prop = so.FindProperty("tabs");
            prop.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            int tiles = 0;
            foreach (var tile in view.GetComponentsInChildren<AvatarTile>(true))
                foreach (var a in Avatars)
                    if (tile.name == "Avatar_" + a.file) { tile.categories = a.cat; EditorUtility.SetDirty(tile); tiles++; }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log($"[ProfileAvatarStepBuilder] 분류 탭 여성/남성만 적용 (아바타 {tiles}칸 분류 갱신) - 손배치 유지");
        }

        // 단계 표시: 원 3개(가운데 x 760/960/1160) + 연결선(34) + 아래 라벨.
        // 이미지는 모두 켜진 상태라 단계 상태는 이렇게 나눈다.
        //   지난 단계 = 체크(33), 지금 단계 = 번호(30~32), 남은 단계 = 번호를 흐리게.
        //   연결선은 지난 단계 뒤쪽만 켜고 나머지는 흐리게.
        // 원 이미지마다 글로우 여백이 달라 본체(알파>200 실측) 지름을 StepD로 맞춘다.
        const float StepD = 50f, StepDimAlpha = 0.4f;
        static readonly string[] StepFiles = { "30", "31", "32" };
        static readonly Rect[] StepBodies =
        {
            Rect.MinMaxRect(258, 272, 994, 999),    // 30 (1)
            Rect.MinMaxRect(309, 307, 948, 944),    // 31 (2)
            Rect.MinMaxRect(249, 256, 1004, 1003),  // 32 (3)
        };
        static readonly Rect DoneBody = Rect.MinMaxRect(258, 257, 997, 987);   // 33 (체크)
        static readonly Rect LineBody = Rect.MinMaxRect(215, 336, 1958, 386);  // 34 (2172x724)

        internal static void BuildSteps(Transform parent, TMP_FontAsset font, int activeIndex)
        {
            string[] labels = { "아바타 선택", "프로필 설정", "완료" };
            const float y = -96f, gap = 200f, x0 = 760f;
            for (int i = 0; i < 2; i++)
            {
                // 원 가장자리 사이(간격 - 지름 - 여유 16)를 본체 길이로
                float len = gap - StepD - 16f;
                PlaceImage("StepLine" + i, parent, Dir + "34.png", LineBody, PillW, PillH,
                    new Vector2(x0 + gap / 2f + i * gap, y), len, i < activeIndex ? 1f : StepDimAlpha);
            }
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector2(x0 + i * gap, y);
                bool done = i < activeIndex, on = i == activeIndex;
                if (done) PlaceImage("Step" + i, parent, Dir + "33.png", DoneBody, 1254f, 1254f, c, StepD, 1f);
                else PlaceImage("Step" + i, parent, Dir + StepFiles[i] + ".png", StepBodies[i], 1254f, 1254f, c, StepD, on ? 1f : StepDimAlpha);
                AddText(parent, "StepLabel" + i, labels[i], font, 20, on ? TextWhite : TextDim, on,
                    new Vector2(c.x - 100f, y - 34f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
            }
        }

        // 이미지 속 본체(body, 픽셀)가 center/bodyWidth에 오도록 이미지 전체를 놓는다 (클릭 없음)
        static void PlaceImage(string name, Transform parent, string path, Rect body, float imgW, float imgH,
            Vector2 center, float bodyWidth, float alpha)
        {
            float scale = bodyWidth / body.width;
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(imgW * scale, imgH * scale);
            rt.anchoredPosition = center + new Vector2((imgW / 2f - body.center.x) * scale, (body.center.y - imgH / 2f) * scale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, 512);
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
        }

        // 한 칸: SelectedBorder(선택 시, 칸보다 사방 5 크게) + Bg + Avatar. 루트가 클릭을 받는다.
        static void MakeTile(Transform grid, AvatarEntry a)
        {
            var tileRt = ChildRect("Avatar_" + a.file, grid);
            var hit = tileRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var bgRt = ChildRect("Bg", tileRt);
            StretchFull(bgRt);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = Rounded(); bgImg.type = Image.Type.Sliced; bgImg.color = TileFill; bgImg.raycastTarget = false;

            var sprite = LoadSprite(Dir + a.file + ".png", 1024);
            var av = ChildRect("Avatar", tileRt);
            av.anchorMin = Vector2.zero; av.anchorMax = Vector2.one;
            av.offsetMin = new Vector2(8f, 0f); av.offsetMax = new Vector2(-8f, -8f);
            var avImg = av.gameObject.AddComponent<Image>();
            avImg.sprite = sprite; avImg.preserveAspect = true; avImg.raycastTarget = false;

            // 변경점: 선택 테두리 = LED 테두리, 칸 맨 위에 얹는다(안쪽 빛이 칸 가장자리 위로 살짝 번짐)
            var border = ChildRect("SelectedBorder", tileRt);
            ApplyLedFrame(border);

            var btn = tileRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            tileRt.gameObject.AddComponent<HomeButtonGlow>().Setup(null, av);

            var tile = tileRt.gameObject.AddComponent<AvatarTile>();
            tile.avatarId = sprite != null ? sprite.name : a.file;
            tile.button = btn;
            tile.selectedMark = border.gameObject;
            tile.categories = a.cat;
            tile.sprite = sprite;
            border.gameObject.SetActive(false);
        }

        // 선택 테두리를 LED 테두리로: 칸보다 LedFrameMargin만큼 크게, 9-slice(가운데 비움), 칸의 맨 위 자식.
        static void ApplyLedFrame(RectTransform border)
        {
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(-LedFrameMargin, -LedFrameMargin);
            border.offsetMax = new Vector2(LedFrameMargin, LedFrameMargin);
            border.SetAsLastSibling();
            SetupLedImage(border, Color.white);
        }

        // 대상(target, 둥근 사각형) 바로 뒤 형제로 LED 테두리(frame)를 얹는다. 그림은 대상보다 LedFrameMargin만큼 바깥으로 넘친다.
        // tint로 세기를 줄일 수 있다(흰색 = 그대로). 큰 카드/창 테두리용. 대상 모서리 반지름이 44(RoundedRect 1:1)여야 모양이 맞는다.
        static void ApplyLedFrameAround(RectTransform frame, RectTransform target, Color tint)
        {
            frame.anchorMin = target.anchorMin;
            frame.anchorMax = target.anchorMax;
            frame.pivot = target.pivot;
            var grow = new Vector2(LedFrameMargin * 2f, LedFrameMargin * 2f);
            frame.sizeDelta = target.sizeDelta + grow;
            frame.anchoredPosition = target.anchoredPosition + Vector2.Scale(target.pivot - new Vector2(0.5f, 0.5f), grow);
            frame.localRotation = target.localRotation;
            frame.localScale = target.localScale;
            SetupLedImage(frame, tint);
            if (frame.GetSiblingIndex() < target.GetSiblingIndex()) target.SetSiblingIndex(frame.GetSiblingIndex());
            else frame.SetSiblingIndex(target.GetSiblingIndex() + 1);
        }

        static void SetupLedImage(RectTransform frame, Color tint)
        {
            if (!frame.TryGetComponent<Image>(out var img)) img = frame.gameObject.AddComponent<Image>();
            img.sprite = LoadLedFrame();
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.pixelsPerUnitMultiplier = LedFramePx;
            img.color = tint;
            img.raycastTarget = false;
        }

        static Sprite LoadLedFrame()
        {
            var sprite = LoadSprite(LedFramePath, 512);
            if (AssetImporter.GetAtPath(LedFramePath) is TextureImporter ti)
            {
                var b = new Vector4(LedFrameBorderPx, LedFrameBorderPx, LedFrameBorderPx, LedFrameBorderPx);
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                if (ti.spriteBorder != b || settings.spriteMeshType != SpriteMeshType.FullRect)
                {
                    // spriteBorder도 settings에 들어 있어 settings로 같이 넣는다(따로 넣으면 SetTextureSettings가 덮어씀)
                    settings.spriteBorder = b;
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    ti.SetTextureSettings(settings);
                    ti.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LedFramePath);
                }
            }
            return sprite;
        }

        // 손배치가 있는 ProfileCreateScene의 아바타 칸 선택 테두리만 LED 테두리로 바꾼다. 칸 자리/크기는 그대로.
        [MenuItem("Artti/Fix/ProfileCreate - Avatar Tile LED Border (keep hand placement)")]
        public static void ApplyLedFrameMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileAvatarStepBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ProfileCreate)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.ProfileCreate);
            }

            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var tile in root.GetComponentsInChildren<AvatarTile>(true))
                {
                    if (!(tile.transform.Find("SelectedBorder") is RectTransform border)) continue;
                    ApplyLedFrame(border);
                    count++;
                }
            if (count == 0) { Debug.LogError("[ProfileAvatarStepBuilder] AvatarTile/SelectedBorder 없음"); return; }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log($"[ProfileAvatarStepBuilder] 아바타 칸 LED 테두리 적용 ({count}칸) - 손배치 유지");
        }

        // 손배치가 있는 ProfileCreateScene 1~3단계에: 창을 덜 짙게, 창 테두리는 약한 LED, 1·2단계 큰 카드 테두리는 LED로.
        // 자리/크기(손배치)는 그대로 두고 그림과 색, 테두리 크기만 바꾼다. 예전 카드 빛 번짐(PreviewGlow)은 지운다.
        [MenuItem("Artti/Fix/ProfileCreate - Glow Borders + Lighter Panel (keep hand placement)")]
        public static void ApplyGlowBordersMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileAvatarStepBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ProfileCreate)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.ProfileCreate);
            }

            var stages = new System.Collections.Generic.List<RectTransform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                    if (t.name == "Stage" && t.parent != null &&
                        (t.parent.name == "AvatarStep" || t.parent.name == "ProfileStep" || t.parent.name == "DoneStep"))
                        stages.Add(t);
            if (stages.Count == 0) { Debug.LogError("[ProfileAvatarStepBuilder] 단계 Stage 없음"); return; }

            int panels = 0, cards = 0;
            foreach (var stage in stages)
            {
                if (stage.Find("Panel") is RectTransform panel)
                {
                    if (panel.TryGetComponent<Image>(out var fill)) fill.color = PanelFill;
                    if (stage.Find("PanelLine") is RectTransform line) { ApplyLedFrameAround(line, panel, PanelGlowTint); panels++; }
                }
                if (stage.Find("PreviewCard") is RectTransform card && stage.Find("PreviewLine") is RectTransform cardLine)
                {
                    var glow = stage.Find("PreviewGlow");
                    if (glow != null) Object.DestroyImmediate(glow.gameObject);
                    ApplyLedFrameAround(cardLine, card, Color.white);
                    cards++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log($"[ProfileAvatarStepBuilder] 창 {panels}개(덜 짙게 + 약한 LED 테두리), 큰 카드 {cards}개 LED 테두리 적용 - 손배치 유지");
        }

        // 아바타 id는 스프라이트 이름(파일명)이다. 메인/레포트 등이 AvatarLibrary.GetById로 찾을 수 있게 넣어 둔다.
        static void AddToAvatarLibrary()
        {
            const string path = "Assets/_Project/Resources/AvatarLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AvatarLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }
            var list = new System.Collections.Generic.List<Sprite>(lib.sprites ?? new Sprite[0]);
            foreach (var p in AvatarPaths())
            {
                var sp = LoadSprite(p, 1024);
                if (sp != null && !list.Contains(sp)) list.Add(sp);
            }
            lib.sprites = list.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }

        // ===== 공통 헬퍼 (ProfileCreateSceneBuilder도 사용) =====

        // 이미지 속 본체(body, 픽셀)가 bodyCenter/bodyWidth에 오도록 놓는다. 루트 = 본체 크기 클릭 영역, Visual = 여백 포함 이미지.
        internal static Button MakeImageButton(string name, Transform parent, string path, Rect body, float imgW, float imgH,
            Vector2 bodyCenter, float bodyWidth)
        {
            float scale = bodyWidth / body.width;
            float bodyH = body.height * scale;
            var root = ChildRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = bodyCenter;
            root.sizeDelta = new Vector2(bodyWidth, bodyH);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var visual = ChildRect("Visual", root);
            visual.anchorMin = visual.anchorMax = visual.pivot = new Vector2(0.5f, 0.5f);
            visual.sizeDelta = new Vector2(imgW * scale, imgH * scale);
            // 본체 중심 -> 이미지 중심 (y 위가 +)
            visual.anchoredPosition = new Vector2((imgW / 2f - body.center.x) * scale, (body.center.y - imgH / 2f) * scale);
            var img = visual.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, 2048);
            img.preserveAspect = true;
            img.raycastTarget = false;

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HomeButtonGlow>().Setup(null, visual);
            return btn;
        }

        // 1~3단계 공통 틀: 배경(10) + 패널 창(x 40~1880, y 24~1056) + 로고 + 단계 표시 + 닫기(18).
        // 반환값은 1920x1080 Stage(좌상단 기준, y 아래가 -). 루트는 stage.parent.
        internal static RectTransform BuildFrame(Transform canvas, string rootName, TMP_FontAsset font, int stepIndex, out Button close)
        {
            var root = ChildRect(rootName, canvas);
            StretchFull(root);
            var bg = ChildRect("Background", root);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(Dir + "10.png", 2048);
            bgImg.raycastTarget = true;   // 아래 단계로 터치가 새지 않게 막는다
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1448f / 1086f;

            var stage = ChildRect("Stage", root);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(1920f, 1080f);

            var panel = RoundedBox("Panel", stage, new Vector2(960f, -540f), new Vector2(1840f, 1032f), PanelFill);
            ApplyLedFrameAround(ChildRect("PanelLine", stage), panel, PanelGlowTint);   // 변경점: 창 테두리 = 약한 LED

            var logo = AddText(stage, "Logo", "ARTTI", font, 46, TextWhite, true, new Vector2(100f, -52f), new Vector2(300f, 64f));
            logo.characterSpacing = 4f;
            // 변경점: 로고 아래 "AR CONVENIENCE STORE" 부제는 뺐다(2026-10-01 손배치에서 지움)

            BuildSteps(stage, font, stepIndex);

            close = MakeImageButton("CloseBtn", stage, Dir + "18.png", CloseBody, 1254f, 1254f, new Vector2(1812f, -96f), 64f);
            return stage;
        }

        // 제목 + 부제 (1·2단계 공통 위치). 제목 글자를 돌려준다(수정 모드에서 바꿔 쓰기용).
        internal static TMP_Text AddHeading(Transform stage, TMP_FontAsset font, string title, string subtitle)
        {
            var t = AddText(stage, "Title", title, font, 54, TextWhite, true,
                new Vector2(460f, -174f), new Vector2(1000f, 72f), TextAlignmentOptions.Center);
            AddText(stage, "Subtitle", subtitle, font, 28, TextSub, false,
                new Vector2(460f, -252f), new Vector2(1000f, 42f), TextAlignmentOptions.Center);
            return t;
        }

        // 고른 아바타를 크게 보여 주는 카드(1·2단계 공통). 아바타는 카드 높이의 90%로 키우고
        // 카드의 둥근 모양으로 잘라(Mask) 좌우 어깨가 카드 밖으로 나가지 않게 한다. 아래에 인용문 띠.
        internal static Image AvatarCard(Transform parent, Vector2 center, Vector2 size, string quote, TMP_FontAsset font)
        {
            var card = RoundedBox("PreviewCard", parent, center, size, new Color(0.12f, 0.25f, 0.62f, 0.95f));
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // 변경점: 카드 테두리 = LED 테두리(아바타 칸 선택 테두리와 같은 그림). 카드는 Mask라 형제로 카드 바로 뒤에 얹는다.
            ApplyLedFrameAround(ChildRect("PreviewLine", parent), card, Color.white);

            float side = size.y * 0.9f;
            var avatarRt = ChildRect("Avatar", card);
            avatarRt.anchorMin = avatarRt.anchorMax = avatarRt.pivot = new Vector2(0.5f, 0f);
            avatarRt.anchoredPosition = new Vector2(0f, size.y * 0.04f);
            avatarRt.sizeDelta = new Vector2(side, side);
            var avatar = avatarRt.gameObject.AddComponent<Image>();
            avatar.sprite = LoadSprite(Dir + Avatars[0].file + ".png", 1024);
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;

            // 변경점: 인용문 띠를 80.png(따옴표가 그려진 테두리)로. 3단계 완료 카드(35)와 같은 모양.
            var quoteBand = ChildRect("QuoteBand", card);
            var q = AddText(quoteBand, "Quote", quote, font, 24, TextWhite, false, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            ApplyQuoteFrame(quoteBand, q, size.x - 20f);
            return avatar;
        }

        // 인용문 띠를 80.png로 꾸민다: 띠(band) = 그림 본체 크기(폭 bodyW, 높이는 그림 비율), 그림(Frame)은 빛 번짐 여백만큼 바깥으로 넘친다.
        // 글자는 따옴표 사이 가로 범위에 두고, 그림에 따옴표가 있으므로 글자 속 따옴표는 뺀다. 빌드와 Fix 메뉴가 같이 쓴다.
        static void ApplyQuoteFrame(RectTransform band, TMP_Text text, float bodyW)
        {
            float s = bodyW / QuoteFrameBody.width;
            band.anchorMin = band.anchorMax = band.pivot = new Vector2(0.5f, 0f);
            band.anchoredPosition = new Vector2(0f, QuoteBandBottom);
            band.sizeDelta = new Vector2(bodyW, QuoteFrameBody.height * s);
            if (band.TryGetComponent<Image>(out var oldFill)) Object.DestroyImmediate(oldFill);   // 예전 어두운 둥근 띠

            var oldFrame = band.Find("Frame");
            if (oldFrame != null) Object.DestroyImmediate(oldFrame.gameObject);
            var frame = ChildRect("Frame", band);
            frame.SetAsFirstSibling();
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-QuoteFrameBody.xMin * s, -(QuoteFrameSize.y - QuoteFrameBody.yMax) * s);
            frame.offsetMax = new Vector2((QuoteFrameSize.x - QuoteFrameBody.xMax) * s, QuoteFrameBody.yMin * s);
            var img = frame.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(QuoteFramePath, 1024);
            img.raycastTarget = false;

            var tr = text.rectTransform;
            tr.anchorMin = new Vector2(QuoteTextX0, 0f);
            tr.anchorMax = new Vector2(QuoteTextX1, 1f);
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.text = text.text.Replace("“", "").Replace("”", "").Trim();
        }

        // 손배치가 있는 ProfileCreateScene의 1·2단계 큰 카드 인용문 띠만 80.png로 바꾼다. 카드 자리/크기(손배치)는 그대로 둔다.
        [MenuItem("Artti/Fix/ProfileCreate - Quote Band 80.png (keep hand placement)")]
        public static void ApplyQuoteFrameMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileAvatarStepBuilder] Play 모드에서는 실행하지 않는다."); return; }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ProfileCreate)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = SceneBuilderUtils.OpenScene(ScenePaths.ProfileCreate);
            }

            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (t.name != "QuoteBand" || !(t.parent is RectTransform card) || card.name != "PreviewCard") continue;
                    var text = t.Find("Quote") != null ? t.Find("Quote").GetComponent<TMP_Text>() : null;
                    if (text == null) { Debug.LogWarning($"[ProfileAvatarStepBuilder] {card.parent.name}/QuoteBand/Quote 없음"); continue; }
                    ApplyQuoteFrame(t, text, card.rect.width - 20f);
                    EditorUtility.SetDirty(text);
                    count++;
                }
            if (count == 0) { Debug.LogError("[ProfileAvatarStepBuilder] PreviewCard/QuoteBand 없음"); return; }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log($"[ProfileAvatarStepBuilder] 인용문 띠 80.png 적용 ({count}개) - 손배치 유지");
        }

        // 가운데 기준 둥근 사각형 (Stage 좌상단 기준 center)
        internal static RectTransform RoundedBox(string name, Transform parent, Vector2 center, Vector2 size, Color color)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        // pos/size는 부모 좌상단 기준(y 아래가 -)
        internal static TMP_Text AddText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            bool bold, Vector2 pos, Vector2 boxSize, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = boxSize;
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

        internal static Sprite LoadSprite(string path, int maxSize)
        {
            // 새로 받은 파일이 아직 임포트되지 않았으면 먼저 임포트한다(Auto Refresh를 끈 환경 대비)
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.maxTextureSize != maxSize) { ti.maxTextureSize = maxSize; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                // 스프라이트로 바꾼 설정이 아직 반영되지 않은 경우 한 번 더 강제 임포트
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (sprite == null) Debug.LogWarning($"[ProfileAvatarStepBuilder] 이미지 없음: {path}");
            return sprite;
        }

        internal static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        internal static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        internal static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
