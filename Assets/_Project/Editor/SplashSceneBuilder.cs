using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 51.png/52.png 스플래시. 가로(landscape) 1920x1080.
    // 정지 배치 = 52 상태(타이틀 위 + 이름칩/시작하기 + team ARTTI 아래).
    // 런타임 인트로(51 -> 52)는 SplashSceneView가 처리.
    public static class SplashSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        // 색상 (51/52.png 기준)
        static readonly Color Slate     = new Color32(63, 74, 94, 255);    // 진한 슬레이트 네이비
        static readonly Color Blue      = new Color32(26, 92, 255, 255);   // AAC 비비드 블루
        static readonly Color BtnBlue   = new Color32(42, 109, 224, 255);  // 시작하기 버튼 블루
        static readonly Color ChipBg     = new Color32(228, 232, 238, 255); // 이름칩 연회색
        static readonly Color ChipText   = new Color32(90, 102, 117, 255);  // 이름칩 글자
        static readonly Color White     = Color.white;

        // 유틸의 GetKoreanFont()가 가리키는 Regular SDF는 삭제됨 → 현존 Medium SDF를 직접 로드
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";

        [MenuItem("Artti/Build SplashScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.Splash);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            // 흰 배경
            var bg = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bg.AddComponent<Image>().color = White;

            var font = LoadFont();

            // 타이틀 그룹 (내 손 안의 + AAC) — 인트로에서 한 덩어리로 이동
            var titleGroup = MakeRect("TitleGroup", canvasGo.transform, new Vector2(0, 225), new Vector2(1400, 400));
            var line1 = MakeText("Line1_Korean", titleGroup, "내 손 안의", 92, Slate, font);
            Place(line1.rectTransform, new Vector2(0, 95), new Vector2(1400, 150));
            SetFaceDilate(line1, KoreanBoldDilate); // 한글 줄을 AAC만큼 두껍게
            var aac = MakeText("Line2_AAC", titleGroup, "AAC", 230, Blue, font);
            Place(aac.rectTransform, new Vector2(0, -95), new Vector2(1400, 320));

            // 이름칩 (선택된 프로필 닉네임) — 클릭 시 프로필 생성/선택 (다음 단계 와이어링)
            var nameChip = MakeChip("NameChip", canvasGo.transform, new Vector2(0, -55), new Vector2(300, 96),
                ChipBg, "김연영", 40, ChipText, font, out var nameText, out var nameChipBtn, out var chipGroup);

            // 시작하기 버튼 — 클릭 시 선택 씬 (다음 단계 와이어링)
            var startChip = MakeChip("StartButton", canvasGo.transform, new Vector2(0, -195), new Vector2(380, 120),
                BtnBlue, "시작하기", 56, White, font, out _, out var startBtn, out var startGroup);

            // team ARTTI — 인트로에서 아래로 이동
            var team = MakeText("Line3_Team", canvasGo.transform, "team ARTTI", 50, Slate, font);
            Place(team.rectTransform, new Vector2(0, -430), new Vector2(900, 110));

            // View 부착 + 와이어링
            var view = canvasGo.AddComponent<SplashSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
            so.FindProperty("teamLabel").objectReferenceValue = team.rectTransform;
            so.FindProperty("nameChipGroup").objectReferenceValue = chipGroup;
            so.FindProperty("startButtonGroup").objectReferenceValue = startGroup;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("nameChipButton").objectReferenceValue = nameChipBtn;
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("nameChipTargetScene").stringValue = "ProfileSelectScene"; // 이름칩 → 사용자 선택(빈/목록은 거기서 판단)
            so.FindProperty("startTargetScene").stringValue = "MainScene";             // 시작하기 → 메인
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[SplashSceneBuilder] 완료");
        }

        // 둥근 칩/버튼 (Image + Button + CanvasGroup + 가운데 텍스트)
        static RectTransform MakeChip(string name, Transform parent, Vector2 pos, Vector2 size, Color bg,
            string label, int fontSize, Color labelColor, TMP_FontAsset font,
            out TMP_Text text, out Button button, out CanvasGroup group)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = GetPillSprite();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            group = rect.gameObject.AddComponent<CanvasGroup>();
            button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.fadeDuration = 0.08f;
            colors.highlightedColor = new Color(0.93f, 0.96f, 1f);
            colors.pressedColor = new Color(0.82f, 0.88f, 1f);
            button.colors = colors;

            text = MakeText("Text", rect, label, fontSize, labelColor, font);
            SceneBuilderUtils.FillStretch(text.rectTransform, 8);
            return rect;
        }

        // 한글 줄 추가 두께 (faux-bold 위에 Face Dilate로 보강). AAC와 굵기 매칭용.
        const float KoreanBoldDilate = 0.12f;

        // 전용 머티리얼 인스턴스로 글자 두께(Face Dilate)만 키움 — 공유 머티리얼/타 씬 영향 없음
        static void SetFaceDilate(TMP_Text tmp, float dilate)
        {
            if (tmp.fontSharedMaterial == null) return;
            var mat = new Material(tmp.fontSharedMaterial);
            mat.name = tmp.fontSharedMaterial.name + " (Dilate)";
            mat.SetFloat("_FaceDilate", dilate);
            tmp.fontMaterial = mat;
        }

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
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        // 둥근 모서리 9-slice 스프라이트 (없으면 생성). PillRadius 만큼 라운드.
        const string PillSpritePath = "Assets/_Project/Art/UI/RoundedRect.png";
        const int PillSize = 96;
        const int PillRadius = 44;

        static Sprite GetPillSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
            if (existing != null) return existing;

            var tex = new Texture2D(PillSize, PillSize, TextureFormat.RGBA32, false);
            var px = new Color[PillSize * PillSize];
            for (int y = 0; y < PillSize; y++)
                for (int x = 0; x < PillSize; x++)
                    px[y * PillSize + x] = new Color(1f, 1f, 1f, RoundedAlpha(x + 0.5f, y + 0.5f));
            tex.SetPixels(px);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PillSpritePath));
            System.IO.File.WriteAllBytes(PillSpritePath, bytes);
            AssetDatabase.ImportAsset(PillSpritePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PillSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(PillRadius, PillRadius, PillRadius, PillRadius);
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
        }

        // 둥근 사각형 알파: 안쪽 직사각형[r, size-r]에서의 거리로 모서리만 라운드
        static float RoundedAlpha(float x, float y)
        {
            float cx = Mathf.Clamp(x, PillRadius, PillSize - PillRadius);
            float cy = Mathf.Clamp(y, PillRadius, PillSize - PillRadius);
            float dx = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(PillRadius - dist + 0.5f);
        }

        static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null) font = SceneBuilderUtils.GetKoreanFont();
            if (font == null)
                Debug.LogWarning($"[SplashSceneBuilder] 한글 폰트를 찾지 못함 ({KoreanFontPath}) — 기본 폰트 사용.");
            return font;
        }

        static void OpenOrCreateScene(string path)
        {
            if (System.IO.File.Exists(path))
            {
                SceneBuilderUtils.OpenScene(path);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
