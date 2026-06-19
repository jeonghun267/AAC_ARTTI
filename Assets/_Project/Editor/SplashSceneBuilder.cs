using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 부팅 로딩 스플래시 (100.png). 가로 1920x1080.
    //  - SplashBG.png(로딩바 없는 전체 이미지)이 있으면 그걸 그대로 배경으로 쓰고 로딩바만 위에 덧댐.
    //  - 없으면 폴백으로 오브젝트 배치(AAC 뱃지/기능 아이콘/임시 히어로 Girl,Man).
    //  - 로딩바는 둥근 사각의 "폭"을 늘리는 방식 → 양끝이 항상 둥글다.
    // 런타임 진행/이동은 SplashSceneView가 처리.
    public static class SplashSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color Bg        = new Color32(228, 233, 248, 255); // 연보라 배경
        static readonly Color Blue      = new Color32(74, 108, 247, 255);  // AAC/진행바 블루
        static readonly Color Slate     = new Color32(63, 74, 94, 255);
        static readonly Color SubGray   = new Color32(110, 118, 140, 255);
        static readonly Color Track     = new Color32(214, 219, 232, 255); // 진행바 트랙
        static readonly Color White     = Color.white;

        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string RoundedPath    = "Assets/_Project/Art/UI/RoundedRect.png";
        const string HomeDir        = "Assets/_Project/Art/UI/Home/";
        const string EmojiDir       = "Assets/_Project/openmoji-master/color/svg/";
        // 100.png 배경(거실 3D, UI 베이크 없음). 있으면 cover로 배경에 깔고 그 위에 조각들을 얹는다.
        const string SplashBgPath   = "Assets/_Project/Art/UI/Home/SplashBG.png";
        // 허공에 둥둥 떠다니는 장식 PNG들을 여기에 두면 자동 배치 + 둥둥 모션.
        const string SplashDecoDir  = "Assets/_Project/Art/UI/Home/SplashDeco";
        // 센터 상단 AAC 로고 / 센터 캐릭터.
        const string SplashLogoPath = "Assets/_Project/Art/UI/Home/SplashLogo.png";
        const string HeroGirlPath   = "Assets/_Project/Art/UI/Home/SplashHeroGirl.png";
        const string HeroBoyPath    = "Assets/_Project/Art/UI/Home/SplashHeroBoy.png";

        // 로딩바 크기/위치
        const float BarWidth  = 940f;
        const float BarHeight = 36f;
        const float BarY      = -452f;
        // RoundedRect.png는 border=44px → 둥근 끝(반지름=높이/2)을 얻으려면 PPU배율 = 88/높이.
        const float RoundBorderPx = 44f;

        [MenuItem("Artti/Build SplashScene Hierarchy")]
        public static void BuildMenu() => Build();

        // 다른 씬을 모두 닫고 SplashScene만 단독으로 연다(에디터 겹침 해소용).
        [MenuItem("Artti/Open Splash Only (겹침 해소)")]
        public static void OpenSplashOnly()
        {
            var scene = EditorSceneManager.OpenScene(ScenePaths.Splash, OpenSceneMode.Single);
            Debug.Log($"[SplashSceneBuilder] SplashScene 단독 오픈. 현재 로드된 씬 수 = {UnityEngine.SceneManagement.SceneManager.sceneCount} (1이어야 정상)");
        }

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.Splash);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var font = LoadFont();

            // 아래 → 위 레이어 순서: 배경 → 떠다니는 카드 → 센터 캐릭터 → 센터 상단 로고 → 로딩바
            BuildBackground(canvasGo.transform);
            BuildFloatingDeco(canvasGo.transform);
            BuildCenterCharacters(canvasGo.transform);
            BuildLogo(canvasGo.transform, font);
            BuildLoadingBar(canvasGo, font);

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[SplashSceneBuilder] 완료 (조립형: 배경 + 떠다니는 카드 + 센터 캐릭터 + AAC 로고 + 로딩바)");
        }

        // ===== 배경: 100.png 거실 이미지(있으면) 아니면 연보라 폴백 =====
        static void BuildBackground(Transform parent)
        {
            var sp = System.IO.File.Exists(SplashBgPath) ? LoadPng(SplashBgPath) : null;
            if (sp != null)
            {
                // 이미지 비율 ≠ 화면 비율이어도 왜곡 없이 꽉 채움(cover).
                var rt = MakeRect("Background", parent, Vector2.zero, CoverSize(sp));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sp; img.color = White; img.preserveAspect = true; img.raycastTarget = false;
                return;
            }
            var bg = SceneBuilderUtils.CreatePanel("Background", parent);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = Bg; bgImg.raycastTarget = false;
            AddGlow(parent, new Vector2(0, 240),  new Vector2(1280, 980), new Color(1f, 1f, 1f, 0.55f));
            AddGlow(parent, new Vector2(0, -260), new Vector2(1500, 760), new Color(0.72f, 0.78f, 0.98f, 0.30f));
        }

        // ===== 센터 캐릭터 (boy + girl) — 거실 배경 위에 얹음(후광 없음: 방을 덮지 않도록) =====
        // 위치: 사용자 핸드배치(2048x1536 뷰) 환산값.
        static void BuildCenterCharacters(Transform parent)
        {
            AddHero(parent, "Hero_Girl", HeroGirlPath, new Vector2(-175, -141), 470f);
            AddHero(parent, "Hero_Boy",  HeroBoyPath,  new Vector2(175, -141),  470f);
        }

        // ===== 센터 상단 AAC 로고(크게, 아주 작게 둥둥) + 아래 태그라인 =====
        static void BuildLogo(Transform parent, TMP_FontAsset font)
        {
            var sp = LoadPng(SplashLogoPath);
            if (sp != null)
            {
                float ratio = (sp.rect.width > 0) ? sp.rect.height / sp.rect.width : 1f;
                float w = 296f; // 사용자 핸드배치: 폭 360 x localScale 0.8232 환산
                var rt = MakeRect("AacLogo", parent, new Vector2(0, 433f), new Vector2(w, w * ratio));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
                rt.gameObject.AddComponent<HomeDecorMotion>().ConfigureFloat(new Vector2(0f, 7f), 0.10f, 0f);
            }
            // 로고 아래 태그라인(104.png) — 사용자 핸드배치 환산값
            var tagline = MakeText("Tagline", parent, "대화가 어려울 때, AAC와 함께 소통해요", 38, SubGray, font);
            AnchorPivot(tagline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 213f), new Vector2(1100, 56));
        }

        // 높이 기준으로 비율 맞춰 캐릭터 배치(왜곡 없음).
        static void AddHero(Transform parent, string name, string path, Vector2 pos, float targetHeight)
        {
            var sp = LoadPng(path);
            if (sp == null) return;
            float ratio = (sp.rect.height > 0) ? sp.rect.width / sp.rect.height : 0.6f;
            var rt = MakeRect(name, parent, pos, new Vector2(targetHeight * ratio, targetHeight));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
        }

        // ===== 로딩바 (둥근 트랙 + 폭이 늘어나는 둥근 채움) =====
        static void BuildLoadingBar(GameObject canvasGo, TMP_FontAsset font)
        {
            const float fillInset = 4f;                       // 채움을 트랙 안쪽으로 들임
            float fillH   = BarHeight - fillInset * 2f;
            float fillMax = BarWidth  - fillInset * 2f;

            var track = MakeRect("LoadingTrack", canvasGo.transform, new Vector2(0, BarY), new Vector2(BarWidth, BarHeight));
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = Rounded(); trackImg.type = Image.Type.Sliced;
            trackImg.pixelsPerUnitMultiplier = (RoundBorderPx * 2f) / BarHeight; // 반지름=높이/2 → 알약형
            trackImg.color = Track; trackImg.raycastTarget = false;

            // 채움: 트랙 좌측 안쪽에 고정, 폭만 애니메이션 (양끝 둥근 알약)
            var fill = ChildRect("Fill", track.transform);
            fill.anchorMin = fill.anchorMax = new Vector2(0, 0.5f);
            fill.pivot = new Vector2(0, 0.5f);
            fill.anchoredPosition = new Vector2(fillInset, 0f);
            fill.sizeDelta = new Vector2(fillMax * 0.6f, fillH); // 에디터 미리보기
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = Rounded(); fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = (RoundBorderPx * 2f) / fillH;
            fillImg.color = Blue; fillImg.raycastTarget = false;

            // "로딩 중입니다..." — 바 위쪽 가운데
            var loadingText = MakeText("LoadingText", canvasGo.transform, "로딩 중입니다...", 30, SubGray, font);
            loadingText.alignment = TextAlignmentOptions.Center;
            AnchorPivot(loadingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, BarY + 50f), new Vector2(520, 46));

            // 퍼센트 — 바 오른쪽 끝 위
            var percentText = MakeText("PercentText", canvasGo.transform, "60%", 30, Blue, font);
            percentText.alignment = TextAlignmentOptions.Right;
            AnchorPivot(percentText.rectTransform, new Vector2(1, 0.5f), new Vector2(BarWidth * 0.5f, BarY + 50f), new Vector2(180, 46));

            var view = canvasGo.AddComponent<SplashSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.FindProperty("fillMaxWidth").floatValue = fillMax;
            so.FindProperty("percentText").objectReferenceValue = percentText;
            so.FindProperty("loadingText").objectReferenceValue = loadingText;
            so.FindProperty("nextScene").stringValue = "MainScene";
            so.ApplyModifiedProperties();
        }

        // ===== 둥둥 떠다니는 장식 (SplashDeco 폴더의 PNG들 자동 배치+모션) =====
        static void BuildFloatingDeco(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(SplashDecoDir)) return;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SplashDecoDir });
            if (guids == null || guids.Length == 0) return;

            var paths = new List<string>();
            foreach (var g in guids) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            paths.Sort(System.StringComparer.Ordinal); // 파일명 순(deco_01..)으로 슬롯 배정

            // 시안(100.png) 기준 슬롯: 좌측 위/중간/아래, 우측 위/중간/아래, 여분
            Vector2[] slots =
            {
                new Vector2(-720, 300), new Vector2(-848, 64), new Vector2(-700, -158),
                new Vector2(720, 300),  new Vector2(848, 64),  new Vector2(700, -158),
                new Vector2(-470, 250), new Vector2(470, 250),
            };

            for (int i = 0; i < paths.Count; i++)
            {
                var sp = LoadPng(paths[i]);
                if (sp == null) continue;
                Vector2 pos = i < slots.Length ? slots[i]
                    : new Vector2(((i % 2 == 0) ? -1 : 1) * 560f, 380f - (i * 70f));
                float ratio = (sp.rect.width > 0) ? sp.rect.height / sp.rect.width : 1f;
                float w = 150f;
                var rt = MakeRect("Deco_" + i, parent, pos, new Vector2(w, w * ratio));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
                // 항목마다 진폭/속도/위상이 달라 따로 둥둥 떠다님
                var motion = rt.gameObject.AddComponent<HomeDecorMotion>();
                motion.ConfigureFloat(new Vector2(12f + (i % 3) * 5f, 16f + (i % 2) * 8f), 0.15f + (i % 4) * 0.025f, 5f);
            }
        }

        // ===== 빌딩 블록 =====
        static void MakeIconChip(Transform parent, Vector2 pos, float size, string svgPath, string letter, TMP_FontAsset font)
        {
            var shadow = MakeRect("ChipShadow", parent, pos + new Vector2(0, -8), new Vector2(size + 18, size + 18));
            var shImg = shadow.gameObject.AddComponent<Image>();
            shImg.sprite = SceneBuilderUtils.EnsureGlowSprite(); shImg.type = Image.Type.Sliced;
            shImg.color = new Color(0.20f, 0.24f, 0.40f, 0.16f); shImg.raycastTarget = false;

            var chip = MakeRect("IconChip", parent, pos, new Vector2(size, size));
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.sprite = Rounded(); chipImg.type = Image.Type.Sliced; chipImg.pixelsPerUnitMultiplier = 0.8f;
            chipImg.color = new Color(1f, 1f, 1f, 0.96f); chipImg.raycastTarget = false;

            if (!string.IsNullOrEmpty(svgPath))
            {
                var icon = ChildRect("Icon", chip.transform);
                StretchFull(icon, size * 0.22f);
                var iconImg = icon.gameObject.AddComponent<Image>();
                iconImg.sprite = LoadSvg(svgPath); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
            }
            else
            {
                var t = MakeText("Label", chip.transform, letter, Mathf.RoundToInt(size * 0.42f), Blue, font);
                StretchFull(t.rectTransform, 6);
            }
        }

        static void AddCharacter(Transform parent, string name, string pngPath, Vector2 pos, Vector2 size)
        {
            var rt = MakeRect(name, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadPng(pngPath); img.preserveAspect = true; img.raycastTarget = false;
        }

        static void AddGlow(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var rt = MakeRect("Glow", parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = SceneBuilderUtils.EnsureGlowSprite(); img.type = Image.Type.Sliced;
            img.color = color; img.raycastTarget = false;
        }

        // ===== UI 헬퍼 =====
        static TMP_Text MakeText(string name, Transform parent, string text, int fontSize, Color color, TMP_FontAsset font)
        {
            var tmp = SceneBuilderUtils.CreateTMPText(name, parent, text, fontSize);
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rect = ChildRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void AnchorPivot(RectTransform rect, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
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

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        // 화면(레퍼런스 해상도)을 왜곡 없이 덮는 크기. 넘치는 축은 화면 밖으로 잘림.
        static Vector2 CoverSize(Sprite s)
        {
            float imgAspect = (s != null && s.rect.height > 0) ? s.rect.width / s.rect.height : 1.5f;
            float screenAspect = ReferenceResolution.x / ReferenceResolution.y;
            return imgAspect < screenAspect
                ? new Vector2(ReferenceResolution.x, ReferenceResolution.x / imgAspect)
                : new Vector2(ReferenceResolution.y * imgAspect, ReferenceResolution.y);
        }

        static Sprite LoadSvg(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s) return s;
            AssetDatabase.ImportAsset(path);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s) return s;
            Debug.LogWarning($"[SplashSceneBuilder] SVG Sprite 없음: {path}");
            return null;
        }

        static Sprite LoadPng(string path)
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
            if (sprite == null) Debug.LogWarning($"[SplashSceneBuilder] 이미지 없음: {path}");
            return sprite;
        }

        static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null) font = SceneBuilderUtils.GetKoreanFont();
            return font;
        }

        static void OpenOrCreateScene(string path)
        {
            if (System.IO.File.Exists(path)) { SceneBuilderUtils.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
