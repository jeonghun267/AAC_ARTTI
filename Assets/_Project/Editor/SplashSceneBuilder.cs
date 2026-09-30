using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 부팅 스플래시 (다크 네온 편의점). 가로 1920x1080 Stage 기준, 좌상단 원점(y 아래가 -).
    // 에셋: Art/UI/Home/Splash/
    //   42 배경(1672x941)  43 ARTTI 로고(궤도 포함)  44 "더 나은 소통의 시작"  45 "불러오는 중..."
    //   46 로딩 점(켜짐)  47 로딩 점(꺼짐)
    // 좌표는 시안(1672x941)에 에셋을 겹쳐 맞춘 값을 1920 폭으로 환산(x1.1483)한 것.
    // 음악: Assets/_Project/Audio/splash_bgm.(mp3|wav|ogg)가 있으면 AudioSource에 연결한다. 없으면 무음으로 동작.
    // 런타임 연출(페이드, 점 깜빡임, 음악 줄이기, 다음 씬 이동)은 SplashSceneView가 처리.
    public static class SplashSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string SplashDir = "Assets/_Project/Art/UI/Home/Splash/";
        const string BgmBasePath = "Assets/_Project/Audio/splash_bgm";
        static readonly string[] BgmExts = { ".mp3", ".wav", ".ogg" };

        const float PillImgW = 2172f, PillImgH = 724f;   // 43~46 원본 크기
        const float DotImgW = 1254f;                     // 47 원본 크기(정사각)

        // 로딩 점: 가운데 x 960, 간격 48, y -901. 켜진 점 본체 지름 32, 꺼진 점 23 (시안 실측)
        const float DotY = -901f, DotGap = 48f, DotOnD = 32f, DotOffD = 23f;
        const float DotOnBodyPx = 249f, DotOffBodyPx = 456f;
        static readonly Color DotOffTint = new Color(0.42f, 0.48f, 0.72f, 0.85f);   // 47은 밝은 회색이라 시안처럼 어두운 남색으로 눌러 준다

        [MenuItem("Artti/Build SplashScene Hierarchy")]
        public static void BuildMenu() => Build();

        // 다른 씬을 모두 닫고 SplashScene만 단독으로 연다(에디터 겹침 해소용).
        [MenuItem("Artti/Open Splash Only (겹침 해소)")]
        public static void OpenSplashOnly()
        {
            EditorSceneManager.OpenScene(ScenePaths.Splash, OpenSceneMode.Single);
            Debug.Log($"[SplashSceneBuilder] SplashScene 단독 오픈. 현재 로드된 씬 수 = {UnityEngine.SceneManagement.SceneManager.sceneCount} (1이어야 정상)");
        }

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.Splash);
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // Expand: 20:9 같은 긴 화면에서도 Stage(1920x1080)가 잘리지 않게. 배경은 화면을 꽉 덮는다.
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var canvas = canvasGo.transform;

            // ===== 배경 (42) =====
            var bg = ChildRect("Background", canvas);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(SplashDir + "42.png", 2048);
            bgImg.raycastTarget = false;
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1672f / 941f;

            var stage = ChildRect("Stage", canvas);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = ReferenceResolution;

            // ===== 로고 (43) / 문구 (44) =====
            var logo = PlaceImage("Logo", stage, SplashDir + "43.png", PillImgW, PillImgH, 898f, new Vector2(969f, -471f));
            var logoGroup = logo.gameObject.AddComponent<CanvasGroup>();
            var tagline = PlaceImage("Tagline", stage, SplashDir + "44.png", PillImgW, PillImgH, 511f, new Vector2(959f, -607.5f));
            var taglineGroup = tagline.gameObject.AddComponent<CanvasGroup>();

            // ===== 로딩: 점 3개 (47 꺼짐 위에 46 켜짐) + "불러오는 중..." (45) =====
            var loading = ChildRect("Loading", stage);
            loading.anchorMin = loading.anchorMax = new Vector2(0f, 1f);
            loading.pivot = new Vector2(0f, 1f);
            loading.anchoredPosition = Vector2.zero;
            loading.sizeDelta = ReferenceResolution;
            var loadingGroup = loading.gameObject.AddComponent<CanvasGroup>();

            var dotOn = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector2(960f + (i - 1) * DotGap, DotY);
                var off = PlaceImage("Dot" + i + "Off", loading, SplashDir + "47.png", DotImgW, DotImgW, DotImgW * DotOffD / DotOffBodyPx, c);
                off.GetComponent<Image>().color = DotOffTint;
                dotOn[i] = PlaceImage("Dot" + i + "On", loading, SplashDir + "46.png", PillImgW, PillImgH, PillImgW * DotOnD / DotOnBodyPx, c).gameObject;
            }
            PlaceImage("LoadingText", loading, SplashDir + "45.png", PillImgW, PillImgH, 219.5f, new Vector2(960f, -955f));

            // ===== 화면 전체 암전 (시작 페이드 인 / 끝 페이드 아웃) =====
            var fade = ChildRect("Fade", canvas);
            StretchFull(fade);
            var fadeImg = fade.gameObject.AddComponent<Image>();
            fadeImg.color = Color.black;
            fadeImg.raycastTarget = false;

            // ===== 음악 + View 와이어링 =====
            var bgm = canvasGo.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.loop = false;
            bgm.clip = LoadBgm();

            var view = canvasGo.AddComponent<SplashSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("logoGroup").objectReferenceValue = logoGroup;
            so.FindProperty("logoRect").objectReferenceValue = logo;
            so.FindProperty("taglineGroup").objectReferenceValue = taglineGroup;
            so.FindProperty("loadingGroup").objectReferenceValue = loadingGroup;
            var dots = so.FindProperty("dotOn");
            dots.arraySize = dotOn.Length;
            for (int i = 0; i < dotOn.Length; i++) dots.GetArrayElementAtIndex(i).objectReferenceValue = dotOn[i];
            so.FindProperty("fadeOverlay").objectReferenceValue = fadeImg;
            so.FindProperty("bgm").objectReferenceValue = bgm;
            so.FindProperty("nextScene").stringValue = "MainScene";
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[SplashSceneBuilder] 완료 (배경 42 + 로고 43 + 문구 44 + 로딩 점 46/47 + 불러오는 중 45, 음악: "
                      + (bgm.clip != null ? bgm.clip.name : "없음") + ")");
        }

        // 이미지 전체를 폭 width로 놓고, 이미지 가운데가 center(Stage px)에 오게 한다. 클릭은 받지 않는다.
        static RectTransform PlaceImage(string name, Transform parent, string path, float imgW, float imgH, float width, Vector2 center)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(width, width * imgH / imgW);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, 2048);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        static AudioClip LoadBgm()
        {
            foreach (var ext in BgmExts)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmBasePath + ext);
                if (clip != null) return clip;
            }
            return null;
        }

        static void OpenOrCreateScene(string path)
        {
            if (System.IO.File.Exists(path)) { SceneBuilderUtils.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
