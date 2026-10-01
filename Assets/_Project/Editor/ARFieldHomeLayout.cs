using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;
using static Artti.Editor.ARFieldResultLayout;

namespace Artti.Editor
{
    // AR 현장모드 첫 화면(ARFieldScene / Canvas/HomePanel, 사진 촬영 / 갤러리 선택)을 사진 추가하기 시안(Art/UI/Home/ARField 110~117)으로 바꾼다.
    // ARFieldScene은 손배치 씬이라 다시 만들지 않고, 있는 오브젝트를 옮기고 모양만 바꾼다.
    //   OcrTestController의 연결(takePhotoButton = CameraCard, pickImageButton = GalleryCard, statusText)과 뒤로 가기(SceneBackButton)는 그대로 둔다.
    //   예전 귀여운 장식(DecorationLayer)과 카드 틀(MainContainer)은 지우지 않고 끈다.
    //
    // 구성 (모든 자리는 시안 1448x1086 px, 좌상단 기준)
    //   HomeBackground = 112(배경, 화면 cover). 자식 HomeBoard가 배경을 따라가고, 폰처럼 넓은 화면에서는 제목~안내 바가 안 잘리게 줄어든다.
    //   Stage = 110(발판, 두 카드 아래 하나) + 도는 빛 고리 3개(UIOrbitRing, 띠 그림은 Art_Source/ui_arfield/make_home_stage.py):
    //     윗면 안쪽 눈금 고리 / 윗면 끊긴 빛 호 고리(서로 반대로) + 아래층 빛 테두리를 따라 달리는 빛 점(뒤쪽 반은 위층에 가려 안 보이게).
    //   카드 111(촬영) / 113(갤러리)은 발판 윗면 가운데 줄에 바닥을 대고 서 있다. 카드 안 화살표 115 / 116, 아래 안내 바 117.
    //   화면이 열릴 때마다 두 카드가 위에서 내려오며 서로 엮이듯 꼬였다가 발판 위 제자리에 내려앉는다(HomeBoard의 UIWeaveDropIntro).
    // 여러 번 실행해도 같은 결과가 되게(이미 옮긴 오브젝트는 이름으로 다시 찾는다) 만들었다.
    public static class ARFieldHomeLayout
    {
        const string Dir = "Assets/_Project/Art/UI/Home/ARField/";
        const string BgPath = Dir + "112.png";
        const string StagePath = Dir + "110.png";
        const string CameraPath = Dir + "111.png";
        const string GalleryPath = Dir + "113.png";
        const string BackPath = Dir + "114.png";
        const string CameraArrowPath = Dir + "115.png";
        const string GalleryArrowPath = Dir + "116.png";
        const string InfoPath = Dir + "117.png";
        const string OrbitOuterPath = Dir + "derived/orbit_strip_outer.png";
        const string OrbitInnerPath = Dir + "derived/orbit_strip_inner.png";
        const string OrbitCometPath = Dir + "derived/orbit_strip_comets.png";

        static readonly Vector2 BoardSize = new Vector2(1448f, 1086f);
        static readonly Rect BoardRect = new Rect(Vector2.zero, BoardSize);
        // 화면에 꼭 보여야 하는 영역: 뒤로 가기 ~ 안내 바 아래
        static readonly Rect BoardSafe = Rect.MinMaxRect(40f, 40f, 1408f, 1062f);

        // ===== 그림 속 몸통(알파 245 이상, px) =====
        static readonly Vector2 CardSize = new Vector2(1086f, 1448f);
        static readonly Rect CameraBody = Rect.MinMaxRect(113f, 147f, 988f, 1323f);
        static readonly Rect GalleryBody = Rect.MinMaxRect(101f, 165f, 1019f, 1357f);
        static readonly Vector2 CameraArrowAt = new Vector2(585f, 1170f);    // 카드 글 아래 가운데(시안 비율로 잰 자리)
        static readonly Vector2 GalleryArrowAt = new Vector2(547f, 1195f);
        const float ArrowArtDiameter = 150f;                                  // 카드 그림 px
        static readonly Vector2 RoundSize = new Vector2(1254f, 1254f);
        static readonly Rect BackBody = Rect.MinMaxRect(254f, 250f, 1000f, 983f);
        static readonly Rect CameraArrowBody = Rect.MinMaxRect(357f, 372f, 897f, 885f);
        static readonly Rect GalleryArrowBody = Rect.MinMaxRect(322f, 331f, 924f, 911f);
        static readonly Vector2 PillSize = new Vector2(2172f, 724f);
        static readonly Rect InfoBody = Rect.MinMaxRect(170f, 218f, 2002f, 520f);
        // 발판 110: 위층 윗면 빛 테두리 타원 가운데 (1099,261), 반지름 가로 724 / 세로 99(납작함 0.137)
        //   아래층 윗면 빛 테두리 타원 가운데 (1091,300), 반지름 가로 926 / 세로 105
        static readonly Vector2 StageTopCenter = new Vector2(1099f, 261f);
        const float StageTopRadius = 724f, StageFlat = 99f / 724f;
        static readonly Vector2 StageLowCenter = new Vector2(1091f, 300f);
        static readonly Vector2 StageLowRadius = new Vector2(926f, 105f);

        // ===== 시안 속 자리 =====
        static readonly Rect BackBox = Rect.MinMaxRect(58f, 50f, 140f, 132f);
        static readonly Rect BrandBox = Rect.MinMaxRect(196f, 64f, 420f, 116f);
        static readonly Rect NoteLine = Rect.MinMaxRect(1143f, 76f, 1238f, 79f);
        static readonly Rect NoteBox = Rect.MinMaxRect(1255f, 62f, 1420f, 122f);
        static readonly Rect TitleBox = Rect.MinMaxRect(424f, 148f, 1024f, 214f);
        static readonly Rect SubBox = Rect.MinMaxRect(374f, 222f, 1074f, 258f);
        static readonly Rect TagBox = Rect.MinMaxRect(424f, 266f, 1024f, 290f);
        // 카드: 몸통 높이 410, 아래 틈 24, 둘이 가운데 정렬. 바닥(y 710)이 발판 윗면 가운데 줄(y 704) 살짝 앞
        const float CardTop = 300f, CardHeight = 410f, CardGap = 24f;
        const float StageCenterY = 704f, StageTopHalfWidth = 410f;           // 발판 윗면 타원(시안 px)
        // 윗면 고리 반지름(윗면 반지름 비율). 그려진 위층 테두리(1.0)와 겹치면 묻히므로 안쪽에 둔다
        const float OrbitOuterScale = 0.74f, OrbitInnerScale = 0.52f;
        const float OrbitOuterBand = 12f, OrbitInnerBand = 9f, OrbitCometBand = 16f;
        const float OrbitOuterSpeed = 24f, OrbitInnerSpeed = -14f, OrbitCometSpeed = 32f;   // 도/초
        const float OrbitBackAlpha = 0.45f;
        static readonly Rect InfoBox = Rect.MinMaxRect(394f, 940f, 1054f, 940f + 660f * 302f / 1832f);

        static readonly Color TextWhite = new Color(0.96f, 0.97f, 1f, 1f);
        static readonly Color TextSub = new Color(0.78f, 0.85f, 1f, 0.95f);
        static readonly Color TextFaint = new Color(0.6f, 0.7f, 0.95f, 0.9f);
        static readonly Color LineBlue = new Color32(80, 160, 255, 230);

        [MenuItem("Artti/Fix/ARField - Home Panel Photo Design (keep hand placement)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ARFieldHomeLayout] Play 모드에서는 실행하지 않는다."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePaths.ARField)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
            }

            RectTransform panel = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.transform.Find("HomePanel");
                if (t != null) { panel = (RectTransform)t; break; }
            }
            if (panel == null) { Debug.LogError("[ARFieldHomeLayout] Canvas/HomePanel 없음"); return; }

            var controller = Object.FindFirstObjectByType<OcrTestController>(FindObjectsInactive.Include);
            if (controller == null) { Debug.LogError("[ARFieldHomeLayout] OcrTestController 없음"); return; }
            var cso = new SerializedObject(controller);
            var cameraBtn = cso.FindProperty("takePhotoButton").objectReferenceValue as Button;
            var galleryBtn = cso.FindProperty("pickImageButton").objectReferenceValue as Button;
            var status = cso.FindProperty("statusText").objectReferenceValue as TMP_Text;
            if (cameraBtn == null || galleryBtn == null) { Debug.LogError("[ARFieldHomeLayout] 촬영/갤러리 버튼 연결이 비어 있음"); return; }
            var back = Need(panel, "HomeBackButton");
            if (back == null) return;
            var font = status != null ? status.font : panel.GetComponentInChildren<TMP_Text>(true)?.font;

            // ===== 배경 + 보드 =====
            var bg = FindDeep(panel, "HomeBackground") ?? NewRect("HomeBackground", panel);
            bg.SetAsFirstSibling();
            var bgImg = GetOrAdd<Image>(bg);
            bgImg.sprite = LoadSprite(BgPath, 2048);
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Simple;
            bgImg.raycastTarget = true;   // 뒤(AR 화면)로 눌림이 지나가지 않게
            var board = FindDeep(bg, "HomeBoard") ?? NewRect("HomeBoard", bg);
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
            board.anchoredPosition = Vector2.zero;
            board.sizeDelta = BoardSize;
            var fitter = GetOrAdd<CoverImageFitter>(bg);
            fitter.Setup(BoardSize.x / BoardSize.y, 1f, new Vector2(0.5f, 0.5f));
            fitter.SetFollower(board, BoardSize);
            fitter.SetSafeRect(BoardSafe, 4f);

            // ===== 위쪽 머리 =====
            Place(back, board, BackBox, BoardRect);
            DisableOutline(back);
            if (back.TryGetComponent<Mask>(out var backMask)) backMask.enabled = false;   // 투명 누름 칸이 그림을 가리지 않게
            foreach (Transform child in back)
                if (child.name != "Visual") child.gameObject.SetActive(false);              // 화살표는 114 그림에 들어 있다
            HitArea(back);
            Visual(back, BackPath, BackBody, RoundSize, 512);
            UseVisualAsTarget(back);

            var brand = Text(board, "Brand", font, BrandBox, 36f, false, TextWhite, TextAlignmentOptions.MidlineLeft, "ARTTI");
            brand.characterSpacing = 22f;
            var line = FindDeep(board, "NoteLine") ?? NewRect("NoteLine", board);
            Place(line, board, NoteLine, BoardRect);
            var lineImg = GetOrAdd<Image>(line);
            lineImg.color = LineBlue;
            lineImg.raycastTarget = false;
            var note = Text(board, "Note", font, NoteBox, 19f, false, TextSub, TextAlignmentOptions.MidlineLeft, "AI와 만드는\n더 특별한 이야기");
            note.lineSpacing = -8f;
            Text(board, "Title", font, TitleBox, 54f, true, TextWhite, TextAlignmentOptions.Center, "사진 추가하기");
            Text(board, "Subtitle", font, SubBox, 27f, false, TextSub, TextAlignmentOptions.Center, "원하는 방식으로 사진을 선택해주세요");
            var tag = Text(board, "Tagline", font, TagBox, 14f, false, TextFaint, TextAlignmentOptions.Center,
                "—   ARTTI와 함께, 특별한 순간을 더 특별하게   —");
            tag.characterSpacing = 3f;

            // ===== 발판 + 도는 빛 고리 =====
            var stage = FindDeep(board, "Stage") ?? NewRect("Stage", board);
            Place(stage, board, BoardRect, BoardRect);
            float ps = StageTopHalfWidth / StageTopRadius;
            var stageBase = FindDeep(stage, "StageBase") ?? NewRect("StageBase", stage);
            Place(stageBase, stage, new Rect(BoardSize.x * 0.5f - StageTopCenter.x * ps, StageCenterY - StageTopCenter.y * ps,
                StagePathSize.x * ps, StagePathSize.y * ps), BoardRect);
            var stageImg = GetOrAdd<Image>(stageBase);
            stageImg.sprite = LoadSprite(StagePath, 2048);
            stageImg.color = Color.white;
            stageImg.preserveAspect = true;
            stageImg.raycastTarget = false;
            var top = new Vector2(BoardSize.x * 0.5f, StageCenterY);
            Orbit(stage, "OrbitComets", OrbitCometPath, top + (StageLowCenter - StageTopCenter) * ps, StageLowRadius * ps,
                OrbitCometBand, OrbitCometSpeed, 0f);   // 뒤쪽 반은 위층 뒤로 들어가 안 보이게
            Orbit(stage, "OrbitInner", OrbitInnerPath, top, TopRadius(OrbitInnerScale), OrbitInnerBand, OrbitInnerSpeed, OrbitBackAlpha);
            Orbit(stage, "OrbitOuter", OrbitOuterPath, top, TopRadius(OrbitOuterScale), OrbitOuterBand, OrbitOuterSpeed, OrbitBackAlpha);

            // ===== 카드 두 장 (발판 위) =====
            float camS = CardHeight / CameraBody.height, galS = CardHeight / GalleryBody.height;
            float camW = CameraBody.width * camS, galW = GalleryBody.width * galS;
            float x0 = BoardSize.x * 0.5f - (camW + CardGap + galW) * 0.5f;
            var camRect = new Rect(x0, CardTop, camW, CardHeight);
            var galRect = new Rect(x0 + camW + CardGap, CardTop, galW, CardHeight);
            Card((RectTransform)cameraBtn.transform, board, CameraPath, CameraBody, camRect, CameraArrowPath, CameraArrowBody, CameraArrowAt);
            Card((RectTransform)galleryBtn.transform, board, GalleryPath, GalleryBody, galRect, GalleryArrowPath, GalleryArrowBody, GalleryArrowAt);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(board.gameObject);   // 예전 등장 연출(UISpinIntro, 지움) 자리
            var intro = GetOrAdd<UIWeaveDropIntro>(board);
            intro.Setup((RectTransform)cameraBtn.transform, (RectTransform)galleryBtn.transform);
            EditorUtility.SetDirty(intro);

            // ===== 아래 안내 바 =====
            var info = FindDeep(board, "InfoBar") ?? NewRect("InfoBar", board);
            Place(info, board, InfoBox, BoardRect);
            Visual(info, InfoPath, InfoBody, PillSize, 2048);

            // 그리는 순서: 글 -> 뒤로 가기 -> 발판 -> 카드 -> 안내 바
            foreach (var name in new[] { "Brand", "NoteLine", "Note", "Title", "Subtitle", "Tagline" })
                FindDeep(board, name)?.SetAsLastSibling();
            back.SetAsLastSibling();
            stage.SetAsLastSibling();
            ((RectTransform)cameraBtn.transform).SetAsLastSibling();
            ((RectTransform)galleryBtn.transform).SetAsLastSibling();
            info.SetAsLastSibling();

            // ===== 예전 틀은 끈다(지우지 않음) =====
            foreach (var name in new[] { "MainContainer", "DecorationLayer" })
            {
                var old = panel.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ARFieldHomeLayout] 사진 추가하기 시안 적용 완료 - 촬영/갤러리/뒤로 가기 연결은 그대로");
        }

        static readonly Vector2 StagePathSize = new Vector2(2172f, 724f);

        // 카드 버튼: 루트 = 몸통 크기의 투명 누름 칸, Visual = 카드 그림(빛 번짐까지), Arrow = 글 아래 동그란 화살표
        static void Card(RectTransform card, RectTransform board, string path, Rect body, Rect rect,
            string arrowPath, Rect arrowBody, Vector2 arrowAt)
        {
            Place(card, board, rect, BoardRect);
            DisableOutline(card);
            if (card.TryGetComponent<Mask>(out var mask)) mask.enabled = false;
            foreach (var layout in card.GetComponents<LayoutGroup>()) layout.enabled = false;   // 자식 자리는 여기서 정한다
            foreach (Transform child in card)
                if (child.name != "Visual" && child.name != "Arrow") child.gameObject.SetActive(false);   // 예전 아이콘/글(그림에 들어 있다)
            HitArea(card);
            Visual(card, path, body, CardSize, 1024);
            UseVisualAsTarget(card);

            float s = rect.width / body.width;
            float d = ArrowArtDiameter * s;
            var arrow = FindDeep(card, "Arrow") ?? NewRect("Arrow", card);
            arrow.gameObject.SetActive(true);
            Place(arrow, card, new Rect(rect.x + (arrowAt.x - body.xMin) * s - d * 0.5f, rect.y + (arrowAt.y - body.yMin) * s - d * 0.5f, d, d), rect);
            Visual(arrow, arrowPath, arrowBody, RoundSize, 512);
            arrow.SetAsLastSibling();
        }

        // 윗면 타원과 같은 납작함의 고리 반지름(시안 px)
        static Vector2 TopRadius(float scale)
        {
            float a = StageTopHalfWidth * scale;
            return new Vector2(a, a * StageFlat);
        }

        // 가운데 center, 반지름 radius(시안 px) 타원을 따라 도는 빛 고리
        static void Orbit(RectTransform stage, string name, string stripPath, Vector2 center, Vector2 radius,
            float bandWidth, float speed, float backAlpha)
        {
            var rt = FindDeep(stage, name) ?? NewRect(name, stage);
            Place(rt, stage, new Rect(center - radius, radius * 2f), BoardRect);
            var ring = GetOrAdd<UIOrbitRing>(rt);
            ring.color = Color.white;
            ring.Setup(LoadStrip(stripPath), bandWidth, speed, backAlpha);
            EditorUtility.SetDirty(ring);
        }

        // 띠 그림은 둘레 방향으로 이어 붙여 쓰므로 스프라이트가 아니라 가로 Repeat 텍스처로 들인다
        static Texture2D LoadStrip(string path)
        {
            if (AssetImporter.GetAtPath(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = ti.textureType != TextureImporterType.Default || ti.wrapModeU != TextureWrapMode.Repeat
                             || ti.wrapModeV != TextureWrapMode.Clamp || ti.mipmapEnabled || !ti.alphaIsTransparency
                             || ti.textureCompression != TextureImporterCompression.Uncompressed;
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.wrapModeU = TextureWrapMode.Repeat;
                    ti.wrapModeV = TextureWrapMode.Clamp;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SaveAndReimport();
                }
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning($"[ARFieldHomeLayout] 이미지 없음: {path}");
            return tex;
        }

        // 루트 Image를 투명 누름 칸으로(그림은 Visual 자식이 그린다)
        static void HitArea(RectTransform root)
        {
            var hit = GetOrAdd<Image>(root);
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
        }

        // 누를 때 색이 바뀌는 대상을 그림(Visual)으로
        static void UseVisualAsTarget(RectTransform root)
        {
            if (!root.TryGetComponent<Button>(out var button)) return;
            var visual = root.Find("Visual");
            if (visual != null && visual.TryGetComponent<Image>(out var img)) button.targetGraphic = img;
            EditorUtility.SetDirty(button);
        }

        static TMP_Text Text(RectTransform board, string name, TMP_FontAsset font, Rect box, float size, bool bold, Color color,
            TextAlignmentOptions align, string value)
        {
            var rt = FindDeep(board, name) ?? NewRect(name, board);
            Place(rt, board, box, BoardRect);
            var tmp = GetOrAdd<TextMeshProUGUI>(rt);
            if (font != null) tmp.font = font;
            Style(rt, size, bold, color, align);
            tmp.text = value;
            return tmp;
        }

        static RectTransform Need(Transform root, string name)
        {
            var t = FindDeep(root, name);
            if (t == null) Debug.LogError($"[ARFieldHomeLayout] {root.name} 아래 {name} 없음");
            return t;
        }

        // 첫 화면은 실행 중에만 켜지므로 켜서 태블릿·폰·16:9로 찍는다. 씬은 저장하지 않는다.
        [MenuItem("Artti/Debug/Capture ARField Home - Tab + Phone + 16x9")]
        static void CaptureHome()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            try
            {
                foreach (var (w, h, tag) in new[] { (2560, 1600, "tab"), (2400, 1080, "phone"), (1920, 1080, "169") })
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        var panel = root.transform.Find("HomePanel");
                        if (panel == null) continue;
                        foreach (Transform sib in root.transform)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                    }
                    string file = Path.Combine(outDir, $"{tag}_ARFieldHome.png");
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);
                    DeviceLayoutCapture.CaptureActiveScene(w, h, file);   // 첫 번은 캔버스 크기 맞추기
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[ARFieldHomeLayout] 첫 화면 캡처 -> {outDir}");
        }

        // 카드 등장 연출(UIWeaveDropIntro)을 중간 장면 몇 개로 찍는다(태블릿). 에디터에서는 움직이지 않으므로 Apply(k)를 직접 불러 맞춘다.
        [MenuItem("Artti/Debug/Capture ARField Home Intro Frames - Tab")]
        static void CaptureIntroFrames()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = EditorSceneManager.GetActiveScene().path;
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LayoutCaptures"));
            Directory.CreateDirectory(outDir);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            float[] frames = { 0.15f, 0.27f, 0.38f, 0.5f, 0.62f, 0.75f, 0.88f, 1f };
            try
            {
                for (int f = 0; f < frames.Length; f++)
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    UIWeaveDropIntro intro = null;
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        var panel = root.transform.Find("HomePanel");
                        if (panel == null) continue;
                        foreach (Transform sib in root.transform)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                        intro = panel.GetComponentInChildren<UIWeaveDropIntro>(true);
                    }
                    if (intro == null) { Debug.LogError("[ARFieldHomeLayout] UIWeaveDropIntro 없음"); return; }
                    string file = Path.Combine(outDir, $"tab_ARFieldHome_Intro_{f + 1}.png");
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);   // 캔버스 크기 맞추기
                    typeof(UIWeaveDropIntro).GetMethod("Awake", flags).Invoke(intro, null);
                    typeof(UIWeaveDropIntro).GetMethod("Apply", flags).Invoke(intro, new object[] { frames[f] });
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                }
                // 내려앉은 뒤 두둥실: 떠오른 다음 흔들림의 두 순간(왼쪽 카드 맨 아래 / 맨 위)
                float[] floatTimes = { 0.9f + 3.2f * 0.75f, 0.9f + 3.2f * 1.25f };
                for (int f = 0; f < floatTimes.Length; f++)
                {
                    EditorSceneManager.OpenScene(ScenePaths.ARField, OpenSceneMode.Single);
                    UIWeaveDropIntro intro = null;
                    foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        var panel = root.transform.Find("HomePanel");
                        if (panel == null) continue;
                        foreach (Transform sib in root.transform)
                            if (sib is RectTransform) sib.gameObject.SetActive(sib == panel);
                        intro = panel.GetComponentInChildren<UIWeaveDropIntro>(true);
                    }
                    if (intro == null) return;
                    string file = Path.Combine(outDir, $"tab_ARFieldHome_Float_{f + 1}.png");
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                    var type = typeof(UIWeaveDropIntro);
                    type.GetMethod("Awake", flags).Invoke(intro, null);
                    type.GetField("_floating", flags).SetValue(intro, true);
                    type.GetField("_floatTime", flags).SetValue(intro, floatTimes[f]);
                    type.GetMethod("Update", flags).Invoke(intro, null);
                    DeviceLayoutCapture.CaptureActiveScene(2560, 1600, file);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            Debug.Log($"[ARFieldHomeLayout] 카드 등장 장면 캡처 -> {outDir}");
        }
    }
}
