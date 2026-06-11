using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 메인(모드 선택) 화면. 가로 1920x1080. (시안: 302.png)
    // 좌상단 타이틀 + 인사말 칩, 우상단 레포트 보기, 카드 2개(말하기 훈련/AR 현장도우미), 우하단 나가기
    public static class MainSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);   // #1A56DB
        static readonly Color32 BgColor    = new Color32(247, 248, 252, 255);
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color32 CardBtnBg  = new Color32(238, 240, 244, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath  = "Assets/_Project/Art/UI/RoundedRect.png";
        const string IconTraining = "Assets/_Project/Art/UI/Mode/mode_training.png";
        const string IconAR       = "Assets/_Project/Art/UI/Mode/mode_ar.png";
        // openmoji 'emergency exit door' — 열린 문으로 나가는 픽토그램
        const string IconExit     = "Assets/_Project/openmoji-master/color/svg/E0A8.svg";

        static readonly Vector2 CardSize = new Vector2(700, 520);

        [MenuItem("Artti/Build MainScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.Main);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            var font = SceneBuilderUtils.GetKoreanFont();

            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<Image>().color = BgColor;

            // 좌상단 타이틀: "내 손 안의" 진남색 + "AAC" primary
            var title = MakeText("Title", canvasGo.transform, "내 손 안의 <color=#1A56DB>AAC</color>", 64, TitleColor, font, bold: true);
            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(64, -48);
            title.rectTransform.sizeDelta = new Vector2(700, 90);

            // 우상단 레포트 보기 (흰 pill)
            var reportBtn = MakePillButton("ReportBtn", canvasGo.transform, "레포트 보기", 36, White, TitleColor, font);
            var reportRect = reportBtn.GetComponent<RectTransform>();
            reportRect.anchorMin = reportRect.anchorMax = new Vector2(1f, 1f);
            reportRect.pivot = new Vector2(1f, 1f);
            reportRect.anchoredPosition = new Vector2(-64, -48);
            reportRect.sizeDelta = new Vector2(300, 88);

            // 인사말 칩 (흰 pill: 아바타 + "반갑습니다 {닉네임} 님!")
            var chip = ChildRect("GreetingChip", canvasGo.transform);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
            chip.pivot = new Vector2(0f, 1f);
            chip.anchoredPosition = new Vector2(64, -168);
            chip.sizeDelta = new Vector2(520, 84);
            var chipImg = chip.gameObject.AddComponent<Image>();
            chipImg.sprite = Rounded(); chipImg.type = Image.Type.Sliced; chipImg.pixelsPerUnitMultiplier = 1f;
            chipImg.color = White;
            chipImg.raycastTarget = false;

            var avatar = ChildRect("Avatar", chip);
            avatar.anchorMin = avatar.anchorMax = new Vector2(0f, 0.5f);
            avatar.pivot = new Vector2(0f, 0.5f);
            avatar.anchoredPosition = new Vector2(20, 0);
            avatar.sizeDelta = new Vector2(56, 56);
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;

            var greeting = MakeText("GreetingText", chip, "반갑습니다!", 34, TitleColor, font, bold: false);
            greeting.alignment = TextAlignmentOptions.Left;
            greeting.rectTransform.anchorMin = greeting.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            greeting.rectTransform.pivot = new Vector2(0f, 0.5f);
            greeting.rectTransform.anchoredPosition = new Vector2(96, 0);
            greeting.rectTransform.sizeDelta = new Vector2(410, 60);

            // 모드 카드 2개 (흰 카드 + 부드러운 그림자)
            var trainingBtn = MakeModeCard(canvasGo.transform, "TrainingModeBtn", new Vector2(-400, -80),
                IconTraining, "말하기 훈련", "AI 캐릭터와 함께\n여러 상황을 연습해봅니다.", font);
            var arBtn = MakeModeCard(canvasGo.transform, "ARFieldModeBtn", new Vector2(400, -80),
                IconAR, "AR 현장도우미", "실제 현장에서 카메라를 이용해\n도움을 받을 수 있습니다.", font);

            // 우하단 나가기 (프로필 선택으로)
            MakeExitButton(canvasGo.transform);

            var view = canvasGo.AddComponent<MainSceneView>();
            var so = new SerializedObject(view);
            so.FindProperty("trainingModeBtn").objectReferenceValue = trainingBtn;
            so.FindProperty("arFieldModeBtn").objectReferenceValue = arBtn;
            so.FindProperty("reportBtn").objectReferenceValue = reportBtn;
            so.FindProperty("greetingText").objectReferenceValue = greeting;
            so.FindProperty("greetingAvatar").objectReferenceValue = avatarImg;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[MainSceneBuilder] 완료");
        }

        // ===== 모드 카드 =====
        // 구조: Shadow(부드러운 그림자) > Body(흰 배경) > Icon / Name / Desc
        static Button MakeModeCard(Transform parent, string goName, Vector2 pos,
            string iconPath, string cardTitle, string desc, TMP_FontAsset font)
        {
            var root = ChildRect(goName, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = pos;
            root.sizeDelta = CardSize;

            var shadow = ChildRect("Shadow", root);
            shadow.anchorMin = Vector2.zero;
            shadow.anchorMax = Vector2.one;
            shadow.offsetMin = new Vector2(-36, -48);   // 아래로 12px 치우친 그림자
            shadow.offsetMax = new Vector2(36, 24);
            var shadowImg = shadow.gameObject.AddComponent<Image>();
            shadowImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0.1f, 0.12f, 0.2f, 0.22f);
            shadowImg.raycastTarget = false;

            var body = ChildRect("Body", root);
            StretchFull(body, 0);
            var bodyImg = body.gameObject.AddComponent<Image>();
            bodyImg.sprite = Rounded();
            bodyImg.type = Image.Type.Sliced;
            bodyImg.pixelsPerUnitMultiplier = 1f;
            bodyImg.color = White;

            var icon = ChildRect("Icon", body);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0, -36);
            icon.sizeDelta = new Vector2(330, 250);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadModeSprite(iconPath);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var nameText = MakeText("Name", body, cardTitle, 54, TitleColor, font, bold: true);
            nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            nameText.rectTransform.pivot = new Vector2(0.5f, 1f);
            nameText.rectTransform.anchoredPosition = new Vector2(0, -300);
            nameText.rectTransform.sizeDelta = new Vector2(560, 76);

            var descText = MakeText("Desc", body, desc, 32, SubColor, font, bold: false);
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.rectTransform.anchorMin = descText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            descText.rectTransform.pivot = new Vector2(0.5f, 1f);
            descText.rectTransform.anchoredPosition = new Vector2(0, -388);
            descText.rectTransform.sizeDelta = new Vector2(580, 110);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bodyImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            return btn;
        }

        // ===== 우하단 나가기 버튼 (openmoji emergency exit door) =====
        static GameObject MakeExitButton(Transform parent)
        {
            var rect = ChildRect("ExitButton", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48, 40);
            rect.sizeDelta = new Vector2(110, 110);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 투명 레이캐스트
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var icon = ChildRect("Icon", rect);
            StretchFull(icon, 4);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadExitIcon();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var back = rect.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            back.SetTarget("ProfileSelectScene");
            var method = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), back, method);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
            return rect.gameObject;
        }

        // ===== 공통 UI 헬퍼 =====
        static Button MakePillButton(string name, Transform parent, string label, int fontSize, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = ChildRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded(); img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, textColor, font, bold: true);
            StretchFull(t.rectTransform, 6);
            return btn;
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

        // SVG는 ScriptedImporter 산출물에서 Sprite 서브에셋을 찾아 사용
        static Sprite LoadExitIcon()
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(IconExit))
                if (obj is Sprite s) return s;
            Debug.LogWarning($"[MainSceneBuilder] 나가기 아이콘 Sprite 없음: {IconExit} — SVG Importer의 Generated Asset Type을 Sprite로 설정 필요");
            return null;
        }

        static Sprite LoadModeSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[MainSceneBuilder] 모드 이미지 없음: {path}");
            return sprite;
        }

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
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
