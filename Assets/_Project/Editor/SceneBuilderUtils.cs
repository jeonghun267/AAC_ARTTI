using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Artti.Editor
{
    public static class ScenePaths
    {
        public const string Splash             = "Assets/_Project/Scenes/SplashScene.unity";
        public const string ProfileCreate      = "Assets/_Project/Scenes/ProfileCreateScene.unity";
        public const string ProfileSelect      = "Assets/_Project/Scenes/ProfileSelectScene.unity";
        public const string ProfileSettings    = "Assets/_Project/Scenes/ProfileSettingsScene.unity"; // 변경점: 프로필 설정 화면
        public const string Main               = "Assets/_Project/Scenes/MainScene.unity";
        public const string TrainingHub        = "Assets/_Project/Scenes/TrainingHubScene.unity";
        public const string TrainingConvenience = "Assets/_Project/Scenes/TrainingConvenienceScene.unity";
        public const string ARField            = "Assets/_Project/Scenes/ARFieldScene.unity";
        public const string ARStore            = "Assets/_Project/Scenes/ARStoreScene.unity"; // Blender 매장 월드 고정 AR 쇼케이스
        public const string Report             = "Assets/_Project/Scenes/ReportScene.unity";
        public const string RecordDetail       = "Assets/_Project/Scenes/RecordDetailScene.unity"; // 세션 상세 리포트
        public const string ConversationReport = "Assets/_Project/Scenes/ConversationReportScene.unity"; // 변경점: 대화 모드 리포트
        public const string AccuracyReport     = "Assets/_Project/Scenes/AccuracyReportScene.unity";     // 변경점: 정확도 모드 리포트
    }

    public static class SceneBuilderUtils
    {
        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        public const string GlowSpritePath = "Assets/_Project/Art/UI/CardGlow.png";
        static TMP_FontAsset _cachedKoreanFont;

        // 둥근 사각 SDF 기반 글로우 텍스처를 1회 생성해 에셋으로 저장.
        // 흰색 + 알파 falloff — Image color로 틴트해서 halo(후광)·그림자 양쪽에 사용.
        public static Sprite EnsureGlowSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
            if (existing != null) return existing;

            const int size = 256;
            const int margin = 80;  // 글로우 falloff 폭 (px)
            const int radius = 36;  // 내부 둥근 사각 코너 반경
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            var innerHalf = new Vector2(half - margin - radius, half - margin - radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - innerHalf;
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                    float a = d <= 0f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / margin), 2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(GlowSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GlowSpritePath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(GlowSpritePath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            float b = margin + radius + 8;
            ti.spriteBorder = new Vector4(b, b, b, b);
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
        }

        // [추가] 원형 Mask용 매끈한 원(256px, 가장자리 1px 안티에일리어싱)을 1회 생성해 에셋으로 저장.
        // 내장 Knob.psd는 해상도가 낮아 크게 쓰면 Mask 경계(알파 > 0)가 각져서, 위는 평평하게 아래는 계단처럼 잘린다.
        public const string CircleSpritePath = "Assets/_Project/Art/UI/Circle256.png";

        public static Sprite EnsureCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (existing != null) return existing;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - r;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(CircleSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(CircleSpritePath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(CircleSpritePath);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        }

        public static TMP_FontAsset GetKoreanFont()
        {
            if (_cachedKoreanFont != null) return _cachedKoreanFont;
            _cachedKoreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (_cachedKoreanFont == null)
                Debug.LogWarning($"[SceneBuilderUtils] Korean font not found at {KoreanFontPath} — TMP_Text will use default font.");
            return _cachedKoreanFont;
        }

        public static Scene OpenScene(string path)
        {
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        public static void SaveActiveScene()
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        // 변경점: CoverImageFitter 같은 [ExecuteAlways] 컴포넌트는 붙인 뒤 에디터 업데이트(OnValidate delayCall, Start)에서
        //   자리를 다시 맞추며, 내용은 그대로인데 방금 저장한 씬을 '수정됨'으로 표시한다. 그러면 레이아웃 캡처 메뉴가 저장 확인 창을 띄우고 멈춘다.
        //   빌드 직후 잠깐(ticks번의 에디터 업데이트) 같은 씬이 수정됨이면 다시 저장한다. 사람이 손대기 전의 아주 짧은 시간이다.
        public static void SaveActiveSceneAgainAfterEditorTicks(int ticks = 30)
        {
            var path = SceneManager.GetActiveScene().path;
            int left = ticks;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != path || --left <= 0 || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.update -= tick;
                    return;
                }
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            };
            EditorApplication.update += tick;
        }

        public static void ClearRootObjects()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }
        }

        // matchMode 기본값은 기존 동작(MatchWidthOrHeight, match=0 = 가로 폭 기준) 그대로 둔다.
        // Expand를 넘기면 캔버스가 referenceResolution "이상"으로 유지된다 —
        // 세로 위치를 절대 좌표로 손배치한 화면이 기기 종횡비에 따라 무너지는 것을 막는다.
        public static GameObject CreateCanvas(
            string name = "[Canvas]",
            Vector2? referenceResolution = null,
            CanvasScaler.ScreenMatchMode matchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? new Vector2(1080, 1920);
            scaler.screenMatchMode = matchMode;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        public static GameObject CreateEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return existing.gameObject;

            var go = new GameObject("[EventSystem]");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return go;
        }

        // ClearRootObjects 후 Main Camera가 사라져 AudioListener도 없어지므로 명시적으로 보장
        public static GameObject EnsureAudioListener()
        {
            var existing = Object.FindAnyObjectByType<AudioListener>();
            if (existing != null) return existing.gameObject;
            var go = new GameObject("[AudioListener]");
            go.AddComponent<AudioListener>();
            return go;
        }

        public static GameObject CreatePanel(string name, Transform parent, bool stretchFull = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            if (stretchFull)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            return go;
        }

        public static RawImage CreateRawImage(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<RawImage>();
        }

        public static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.AddComponent<Image>();
        }

        public static TMP_Text CreateTMPText(string name, Transform parent, string defaultText = "", int fontSize = 48)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            var korean = GetKoreanFont();
            if (korean != null) tmp.font = korean;
            return tmp;
        }

        public static Button CreateButton(string label, Transform parent, int fontSize = 48)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            var text = CreateTMPText("Text", go.transform, label, fontSize);
            FillStretch(text.rectTransform, padding: 12);

            // 클릭 피드백 강화
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.6f, 0.78f, 1f, 1f);
            colors.selectedColor = new Color(0.78f, 0.88f, 1f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            return btn;
        }

        public static GameObject CreateBackButton(string targetScene, Transform parent, string label = "← 뒤로")
        {
            var btn = CreateButton(label, parent, 32);
            btn.gameObject.name = "BackButton";
            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(32, -32);
            rect.sizeDelta = new Vector2(160, 88);
            var back = btn.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            back.SetTarget(targetScene);
            var method = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), back, method);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
            return btn.gameObject;
        }

        public static void FillStretch(RectTransform rect, float padding = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static GridLayoutGroup AddGridLayout(GameObject panel, Vector2 cellSize, Vector2 spacing, int columnCount, RectOffset padding = null)
        {
            var grid = panel.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = padding ?? new RectOffset(40, 40, 40, 40);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columnCount;
            grid.childAlignment = TextAnchor.MiddleCenter;
            return grid;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject panel, float spacing = 24, RectOffset padding = null, TextAnchor alignment = TextAnchor.MiddleCenter, bool expandWidth = true, bool expandHeight = false)
        {
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(40, 40, 40, 40);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = expandHeight;
            return layout;
        }

        public static LayoutElement AddLayoutElement(GameObject go, float preferredHeight = -1, float preferredWidth = -1, float minHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredHeight > 0) le.preferredHeight = preferredHeight;
            if (preferredWidth > 0) le.preferredWidth = preferredWidth;
            if (minHeight > 0) le.minHeight = minHeight;
            return le;
        }

        // 빌더 마지막에 호출 — inactive 패널까지 한 번 활성화해서 LayoutGroup 실행 후 다시 비활성화
        public static void ForceRebuildCanvasLayouts(GameObject canvasGo)
        {
            if (canvasGo == null) return;

            // inactive 자식 임시 활성화
            var inactiveList = new System.Collections.Generic.List<GameObject>();
            foreach (var rt in canvasGo.GetComponentsInChildren<RectTransform>(true))
            {
                if (!rt.gameObject.activeSelf)
                {
                    inactiveList.Add(rt.gameObject);
                    rt.gameObject.SetActive(true);
                }
            }

            Canvas.ForceUpdateCanvases();

            // 모든 LayoutGroup 강제 갱신
            foreach (var lg in canvasGo.GetComponentsInChildren<LayoutGroup>(true))
            {
                var rt = lg.GetComponent<RectTransform>();
                if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }

            // 원래 inactive 복구
            foreach (var go in inactiveList)
                go.SetActive(false);
        }
    }
}
