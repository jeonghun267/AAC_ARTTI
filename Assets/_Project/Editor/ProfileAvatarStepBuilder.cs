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
    //   10 배경(1448x1086)  11~15 분류 탭(전체/여성/남성/중성/스타일)  16 랜덤으로 선택하기  17 다음  18 닫기(X)
    //   19~27 아바타 9종(1254x1254, 19만 1086x1448)  28 선택 카드 완성본(19번 캐릭터가 박혀 있어 미사용)
    // 시안에 있지만 이미지가 아직 없는 것(패널 창, 단계 표시, 칸 테두리, 선택 체크)은 코드로 그린다.
    //
    // 배치: 1920x1080 Stage를 화면 중앙에 두고 Stage 좌상단 기준(y 아래가 -)으로 놓는다.
    // 시안은 태블릿(약 1.12:1)이라 가로 16:9에 맞춰 다시 배치했다. 그리드는 5x2로 넓혀 9종이 한 화면에 다 들어간다.
    public static class ProfileAvatarStepBuilder
    {
        internal const string Dir = "Assets/_Project/Art/UI/Home/profiles/";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";

        // 이미지 속 불투명 본체 영역(알파>200 실측, 픽셀)
        internal const float PillW = 2172f, PillH = 724f;
        static readonly Rect[] TabBodies =
        {
            Rect.MinMaxRect(757, 242, 1413, 482),   // 11 전체
            Rect.MinMaxRect(629, 221, 1544, 500),   // 12 여성
            Rect.MinMaxRect(670, 232, 1502, 491),   // 13 남성
            Rect.MinMaxRect(684, 235, 1488, 490),   // 14 중성
            Rect.MinMaxRect(647, 219, 1525, 499),   // 15 스타일
        };
        static readonly Rect RandomBody = Rect.MinMaxRect(166, 202, 2006, 533);
        internal static readonly Rect NextBody = Rect.MinMaxRect(126, 126, 2046, 569);
        static readonly Rect CloseBody  = Rect.MinMaxRect(423, 421, 832, 824);   // 1254x1254

        // 아바타 목록. 첫 칸이 기본 선택. 분류는 탭 필터용이라 필요하면 여기서만 고치면 된다.
        struct AvatarEntry { public string file; public AvatarTile.Category cat; }
        static readonly AvatarEntry[] Avatars =
        {
            new AvatarEntry { file = "19", cat = AvatarTile.Category.Female },                                  // 점원 (웨이브 머리)
            new AvatarEntry { file = "21", cat = AvatarTile.Category.Male },                                    // 검은 머리 후드
            new AvatarEntry { file = "20", cat = AvatarTile.Category.Female },                                  // 단발
            new AvatarEntry { file = "24", cat = AvatarTile.Category.Male | AvatarTile.Category.Style },         // 안경
            new AvatarEntry { file = "22", cat = AvatarTile.Category.Male | AvatarTile.Category.Style },         // 모자
            new AvatarEntry { file = "23", cat = AvatarTile.Category.Neutral | AvatarTile.Category.Style },      // 은발
            new AvatarEntry { file = "27", cat = AvatarTile.Category.Female },                                  // 올림머리
            new AvatarEntry { file = "25", cat = AvatarTile.Category.Male | AvatarTile.Category.Neutral },       // 검은 머리 티셔츠
            new AvatarEntry { file = "26", cat = AvatarTile.Category.Female | AvatarTile.Category.Style },       // 헤드폰
        };

        static readonly Color PanelFill   = new Color(0.05f, 0.08f, 0.20f, 0.86f);
        static readonly Color PanelLine   = new Color(0.35f, 0.55f, 1.00f, 0.55f);
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
            // ===== 루트 (캔버스 전체) + 배경 =====
            var root = ChildRect("AvatarStep", canvas);
            StretchFull(root);
            var bg = ChildRect("Background", root);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(Dir + "10.png", 2048);
            bgImg.raycastTarget = true;   // 아래 폼(2단계)으로 터치가 새지 않게 막는다
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1448f / 1086f;

            var stage = ChildRect("Stage", root);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(1920f, 1080f);

            // ===== 패널 창 (x 40~1880, y 24~1056) =====
            RoundedBox("PanelLine", stage, new Vector2(960f, -540f), new Vector2(1846f, 1038f), PanelLine);
            RoundedBox("Panel", stage, new Vector2(960f, -540f), new Vector2(1840f, 1032f), PanelFill);

            // ===== 헤더: 로고 / 단계 표시 / 닫기 =====
            var logo = AddText(stage, "Logo", "ARTTI", font, 46, TextWhite, true, new Vector2(100f, -52f), new Vector2(300f, 64f));
            logo.characterSpacing = 4f;
            var logoSub = AddText(stage, "LogoSub", "AR CONVENIENCE STORE", font, 16, TextDim, false, new Vector2(102f, -112f), new Vector2(360f, 26f));
            logoSub.characterSpacing = 6f;

            BuildSteps(stage, font, activeIndex: 0);

            var closeBtn = MakeImageButton("CloseBtn", stage, Dir + "18.png", CloseBody, 1254f, 1254f,
                new Vector2(1812f, -96f), 64f);

            // ===== 제목 =====
            AddText(stage, "Title", "프로필을 선택하세요", font, 54, TextWhite, true,
                new Vector2(460f, -174f), new Vector2(1000f, 72f), TextAlignmentOptions.Center);
            AddText(stage, "Subtitle", "나를 가장 잘 표현하는 아바타를 선택해보세요.", font, 28, TextSub, false,
                new Vector2(460f, -252f), new Vector2(1000f, 42f), TextAlignmentOptions.Center);

            // ===== 분류 탭 (본체 높이 58, 간격 22, 가운데 정렬) =====
            const float tabH = 58f, tabGap = 22f, tabY = -350f;
            var tabW = new float[TabBodies.Length];
            float total = tabGap * (TabBodies.Length - 1);
            for (int i = 0; i < TabBodies.Length; i++) { tabW[i] = tabH * TabBodies[i].width / TabBodies[i].height; total += tabW[i]; }
            var tabs = new Button[TabBodies.Length];
            float x = 960f - total / 2f;
            for (int i = 0; i < TabBodies.Length; i++)
            {
                tabs[i] = MakeImageButton("Tab" + i, stage, Dir + (11 + i) + ".png", TabBodies[i], PillW, PillH,
                    new Vector2(x + tabW[i] / 2f, tabY), tabW[i]);
                x += tabW[i] + tabGap;
            }

            // ===== 왼쪽: 선택한 아바타 큰 카드 (x 90~520, y 400~1000) =====
            var cardCenter = new Vector2(305f, -700f);
            RoundedBox("PreviewGlow", stage, cardCenter, new Vector2(446f, 616f), new Color(0.35f, 0.55f, 1f, 0.35f));
            RoundedBox("PreviewLine", stage, cardCenter, new Vector2(438f, 608f), Accent);
            var card = RoundedBox("PreviewCard", stage, cardCenter, new Vector2(430f, 600f), new Color(0.12f, 0.25f, 0.62f, 0.95f));
            var avatarRt = ChildRect("Avatar", card);
            avatarRt.anchorMin = avatarRt.anchorMax = avatarRt.pivot = new Vector2(0.5f, 0f);
            avatarRt.anchoredPosition = new Vector2(0f, 6f);
            avatarRt.sizeDelta = new Vector2(414f, 560f);
            var preview = avatarRt.gameObject.AddComponent<Image>();
            preview.sprite = LoadSprite(Dir + Avatars[0].file + ".png", 1024);
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            var quoteBand = RoundedBox("QuoteBand", card, Vector2.zero, new Vector2(410f, 118f), new Color(0.04f, 0.07f, 0.20f, 0.78f));
            quoteBand.anchorMin = quoteBand.anchorMax = quoteBand.pivot = new Vector2(0.5f, 0f);
            quoteBand.anchoredPosition = new Vector2(0f, 10f);
            var quote = AddText(quoteBand, "Quote", "“ 당신만의 새로운 이야기가\n시작됩니다. ”", font, 24, TextWhite, false,
                Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(quote.rectTransform);

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

        // 단계 표시: 원 3개(가운데 x 760/960/1160) + 연결선 + 아래 라벨
        static void BuildSteps(Transform parent, TMP_FontAsset font, int activeIndex)
        {
            string[] labels = { "아바타 선택", "프로필 설정", "완료" };
            const float y = -96f, d = 52f;
            for (int i = 0; i < 2; i++)
            {
                var line = ChildRect("StepLine" + i, parent);
                line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
                line.pivot = new Vector2(0.5f, 0.5f);
                line.anchoredPosition = new Vector2(860f + i * 200f, y);
                line.sizeDelta = new Vector2(200f - d - 8f, 3f);
                var li = line.gameObject.AddComponent<Image>();
                li.color = new Color(0.5f, 0.6f, 0.9f, 0.5f);
                li.raycastTarget = false;
            }
            for (int i = 0; i < 3; i++)
            {
                bool on = i == activeIndex;
                var c = new Vector2(760f + i * 200f, y);
                if (on) Circle("StepGlow" + i, parent, c, d + 16f, new Color(0.40f, 0.60f, 1f, 0.35f));
                Circle("StepRing" + i, parent, c, d, on ? Accent : new Color(0.6f, 0.7f, 1f, 0.6f));
                if (!on) Circle("StepInner" + i, parent, c, d - 5f, PanelFill);
                AddText(parent, "StepNum" + i, (i + 1).ToString(), font, 26, on ? new Color(0.06f, 0.10f, 0.28f, 1f) : TextSub, true,
                    new Vector2(c.x - d / 2f, c.y + d / 2f), new Vector2(d, d), TextAlignmentOptions.Center);
                AddText(parent, "StepLabel" + i, labels[i], font, 20, on ? TextWhite : TextDim, on,
                    new Vector2(c.x - 100f, y - 34f), new Vector2(200f, 30f), TextAlignmentOptions.Center);
            }
        }

        // 한 칸: SelectedBorder(선택 시, 칸보다 사방 5 크게) + Bg + Avatar. 루트가 클릭을 받는다.
        static void MakeTile(Transform grid, AvatarEntry a)
        {
            var tileRt = ChildRect("Avatar_" + a.file, grid);
            var hit = tileRt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var border = ChildRect("SelectedBorder", tileRt);
            border.anchorMin = Vector2.zero; border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(-5f, -5f); border.offsetMax = new Vector2(5f, 5f);
            var bImg = border.gameObject.AddComponent<Image>();
            bImg.sprite = Rounded(); bImg.type = Image.Type.Sliced; bImg.color = Accent; bImg.raycastTarget = false;

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

        static void Circle(string name, Transform parent, Vector2 center, float d, Color color)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(d, d);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            img.color = color;
            img.raycastTarget = false;
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
