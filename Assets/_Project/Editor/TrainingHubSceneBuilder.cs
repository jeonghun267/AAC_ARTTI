using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 말하기 훈련 시나리오 선택 화면. 가로 1920x1080. (시안: 203.png)
    // 카드 3개(편의점/약국/음식점): primary(#1A56DB) stroke + 후광 halo
    public static class TrainingHubSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color32 Primary    = new Color32(26, 86, 219, 255);   // #1A56DB
        static readonly Color32 BgColor    = new Color32(247, 248, 252, 255);
        static readonly Color32 TitleColor = new Color32(33, 41, 60, 255);
        static readonly Color32 SubColor   = new Color32(110, 118, 135, 255);
        static readonly Color   White      = Color.white;

        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";
        const string IconConvenience = "Assets/_Project/Art/UI/Scenario/scenario_convenience.png";
        const string IconPharmacy    = "Assets/_Project/Art/UI/Scenario/scenario_pharmacy.png";
        const string IconRestaurant  = "Assets/_Project/Art/UI/Scenario/scenario_restaurant.png";

        static readonly Vector2 CardSize = new Vector2(440, 540);
        const float StrokeWidth = 4f;   // stroke 두께 (border inset)
        const float HaloExtent  = 56f;  // 카드 밖으로 퍼지는 halo 폭

        [MenuItem("Artti/Build TrainingHubScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingHub);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);

            var font = SceneBuilderUtils.GetKoreanFont();

            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<Image>().color = BgColor;

            MakeCircleBackButton(canvasGo.transform, font);

            var title = MakeText("Title", canvasGo.transform, "말하기 훈련", 80, TitleColor, font, bold: true);
            PlaceTop(title.rectTransform, new Vector2(0, -64), new Vector2(900, 110));

            var subtitle = MakeText("Subtitle", canvasGo.transform, "오늘은 어떤 연습을 해볼까요?", 44, SubColor, font, bold: false);
            PlaceTop(subtitle.rectTransform, new Vector2(0, -190), new Vector2(900, 70));

            var convBtn = MakeScenarioCard(canvasGo.transform, "ConvenienceBtn", new Vector2(-500, -40),
                IconConvenience, "편의점", "편의점에 방문하여\n물건을 구입해볼까요?", font);
            var pharmBtn = MakeScenarioCard(canvasGo.transform, "PharmacyBtn", new Vector2(0, -40),
                IconPharmacy, "약국", "약국에 방문하여\n필요한 약품을 구매해보세요.", font);
            var restBtn = MakeScenarioCard(canvasGo.transform, "RestaurantBtn", new Vector2(500, -40),
                IconRestaurant, "음식점", "음식점에 방문하여\n음식을 주문하고 계산해보아요.", font);

            var footer = MakeText("Footer", canvasGo.transform, "원하는 시나리오를 선택하면, 연습이 시작돼요.", 42, TitleColor, font, bold: true);
            footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(0, 56);
            footer.rectTransform.sizeDelta = new Vector2(1400, 70);

            var view = canvasGo.AddComponent<TrainingHubView>();
            var so = new SerializedObject(view);
            so.FindProperty("pharmacyBtn").objectReferenceValue = pharmBtn;
            so.FindProperty("convenienceBtn").objectReferenceValue = convBtn;
            so.FindProperty("restaurantBtn").objectReferenceValue = restBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[TrainingHubSceneBuilder] 완료");
        }

        // ===== 시나리오 카드 =====
        // 구조: Halo(후광, 카드보다 큼) > Border(stroke) > Body(흰 배경) > Icon / Name / Desc
        static Button MakeScenarioCard(Transform parent, string goName, Vector2 pos,
            string iconPath, string cardTitle, string desc, TMP_FontAsset font)
        {
            var root = ChildRect(goName, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = pos;
            root.sizeDelta = CardSize;

            var halo = ChildRect("Halo", root);
            StretchFull(halo, -HaloExtent);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.sprite = SceneBuilderUtils.EnsureGlowSprite();
            haloImg.type = Image.Type.Sliced;
            haloImg.color = new Color(Primary.r / 255f, Primary.g / 255f, Primary.b / 255f, 0.7f);
            haloImg.raycastTarget = false;

            var border = ChildRect("Border", root);
            StretchFull(border, 0);
            var borderImg = border.gameObject.AddComponent<Image>();
            borderImg.sprite = Rounded();
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 1f;
            borderImg.color = Primary;

            var body = ChildRect("Body", root);
            StretchFull(body, StrokeWidth);
            var bodyImg = body.gameObject.AddComponent<Image>();
            bodyImg.sprite = Rounded();
            bodyImg.type = Image.Type.Sliced;
            bodyImg.pixelsPerUnitMultiplier = 1f;
            bodyImg.color = White;

            var icon = ChildRect("Icon", body);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 1f);
            icon.anchoredPosition = new Vector2(0, -40);
            icon.sizeDelta = new Vector2(320, 240);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadScenarioSprite(iconPath);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var nameText = MakeText("Name", body, cardTitle, 50, TitleColor, font, bold: true);
            PlaceTop(nameText.rectTransform, new Vector2(0, -300), new Vector2(380, 70));

            var descText = MakeText("Desc", body, desc, 30, SubColor, font, bold: false);
            descText.textWrappingMode = TextWrappingModes.Normal;
            PlaceTop(descText.rectTransform, new Vector2(0, -380), new Vector2(390, 110));

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

        static Sprite LoadScenarioSprite(string path)
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
                Debug.LogWarning($"[TrainingHubSceneBuilder] 시나리오 이미지 없음: {path}");
            return sprite;
        }

        // ===== 공통 UI 헬퍼 =====
        static GameObject MakeCircleBackButton(Transform parent, TMP_FontAsset font)
        {
            var rect = ChildRect("BackButton", parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(48, -40);
            rect.sizeDelta = new Vector2(96, 96);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Builtin("UI/Skin/Knob.psd");
            img.color = White;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, "←", 44, TitleColor, font, bold: true);
            StretchFull(t.rectTransform, 4);

            var back = rect.gameObject.AddComponent<Artti.Common.SceneBackButton>();
            back.SetTarget("MainScene");
            var method = typeof(Artti.Common.SceneBackButton).GetMethod(nameof(Artti.Common.SceneBackButton.GoBack));
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), back, method);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
            return rect.gameObject;
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

        static RectTransform ChildRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void PlaceTop(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
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

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }
    }
}
