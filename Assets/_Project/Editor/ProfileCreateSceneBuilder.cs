using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.UI;

namespace Artti.Editor
{
    // 프로필 생성 폼 씬. 가로 1920x1080, 흰 배경.
    // 이름 입력(네이티브 키보드) + 생년/월 드롭다운 + 대표 아바타(동물 얼굴 10종) 선택.
    public static class ProfileCreateSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        static readonly Color Slate    = new Color32(63, 74, 94, 255);
        static readonly Color BtnBlue  = new Color32(42, 109, 224, 255);
        static readonly Color FieldBg  = new Color32(238, 240, 244, 255);
        static readonly Color White    = Color.white;

        // 새 글래스 디자인 (_createfull.png)
        static readonly Color Sky         = new Color32(232, 230, 248, 255); // 전체 하늘 (연라벤더)
        static readonly Color TitlePurple = new Color32(122, 92, 210, 255);  // 타이틀 보라
        static readonly Color SubPurple   = new Color32(150, 140, 198, 255); // 부제 연보라

        // 글래스 카드 (사용자 손배치 값 - 403기준 스케일 흡수)
        const float CardW = 1427f;
        const float CardH = 1291f;

        const string KoreanFontPath = "Assets/Fonts/NotoSansKR-Medium SDF.asset";
        const string RoundedPath = "Assets/_Project/Art/UI/RoundedRect.png";

        // 동물 얼굴 아바타 10종 (OpenMoji hex)
        static readonly string[] AvatarHex =
        {
            "1F436", "1F431", "1F42D", "1F439", "1F430",
            "1F98A", "1F43B", "1F43C", "1F428", "1F981"
        };

        [MenuItem("Artti/Build ProfileCreateScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            OpenOrCreateScene(ScenePaths.ProfileCreate);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution);
            var canvas = canvasGo.transform;

            var font = LoadFont();
            var res = BuildTmpResources();

            // === 배경 레이어 (뒤 -> 앞) ===
            // 1) 하늘 (연라벤더 전체 채움)
            var sky = SceneBuilderUtils.CreatePanel("Sky", canvas);
            sky.AddComponent<Image>().color = Sky;

            // 1-2) 파스텔 글로우 (카드 둘레 하늘을 따뜻하게 - 은은한 호흡)
            BuildPastelGlow(canvas);

            // 2) 구름 (글래스 카드 뒤에서 한 방향으로 흐름)
            BuildClouds(canvas);

            // 3) 바깥 라벤더 카드 (가운데 글래스 없는 배경)
            var card = MakeRect("GlassCard", canvas, Vector2.zero, new Vector2(CardW, CardH));
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = LoadProfileSprite("ProfileBackNoGlass.png");
            cardImg.preserveAspect = false; // 가로로 늘려 채움
            cardImg.raycastTarget = false;

            // 3-2) 안쪽 프로스트 글래스 패널 (폼을 담는 영역). 사용자 손배치 (스케일 1.2651x/1.4605y 흡수)
            AddDecor("InnerGlass", canvas, LoadProfileSprite("CenterGlassPanel.png"),
                new Vector2(0, -91), new Vector2(1290, 1057), false);

            // === 헤더 ===
            // 뒤로 가기 (← 프로필 선택. 생성은 선택 화면에서 들어옴)
            SceneBuilderUtils.CreateBackButton("ProfileSelectScene", canvas, "← 뒤로");

            var title = MakeText("Title", canvas, "프로필 만들기", 60, TitlePurple, font);
            Place(title.rectTransform, new Vector2(-50, 549), new Vector2(560, 90));

            // 부제 (스케일 0.867 -> 폰트 28->24)
            var subtitle = MakeText("Subtitle", canvas, "나만의 프로필을 만들어요!", 24, SubPurple, font);
            Place(subtitle.rectTransform, new Vector2(-46, 469), new Vector2(560, 50));

            // 상단 캐릭터 (말풍선+책). 사용자 손배치 (스케일 1.4419 흡수)
            AddDecor("Character", canvas, LoadProfileSprite("CharacterBook.png"),
                new Vector2(565, 496), new Vector2(257, 211));

            // === 폼 (아이콘 + 라벨 + 입력). 사용자 손배치 값 ===
            const float IconX = -494f;
            var iconSize = new Vector2(80, 74);

            // 이름
            AddDecor("Icon_Name", canvas, LoadProfileSprite("icon_name.png"), new Vector2(IconX, 302), iconSize);
            MakeLabelAt("Label_Name", canvas, "이름", font, -434, 302, 36, 140);
            var input = MakeInput("NameInput", canvas, res, font, "이름을 입력하세요",
                new Vector2(26, 302), new Vector2(740, 78));

            // 생년 / 월
            AddDecor("Icon_Birth", canvas, LoadProfileSprite("icon_birth.png"), new Vector2(IconX, 175), iconSize);
            MakeLabelAt("Label_Birth", canvas, "생년 / 월", font, -434, 175, 36, 200);
            var yearDd = MakeDropdown("YearDropdown", canvas, res, font,
                new Vector2(-114, 175), new Vector2(320, 78));
            var monthDd = MakeDropdown("MonthDropdown", canvas, res, font,
                new Vector2(241, 175), new Vector2(310, 78));

            // 대표 이미지
            AddDecor("Icon_Avatar", canvas, LoadProfileSprite("icon_avatar.png"), new Vector2(IconX, 46), iconSize);
            MakeLabelAt("Label_Avatar", canvas, "대표 이미지 선택", font, -434, 49, 36, 360);
            // 그리드는 값 미제공 -> 아바타 라벨 아래로 추정 배치
            var gridRect = MakeRect("AvatarGrid", canvas, new Vector2(0, -158), new Vector2(850, 280));
            SceneBuilderUtils.AddGridLayout(gridRect.gameObject, new Vector2(120, 120), new Vector2(52, 40), 5,
                new RectOffset(0, 0, 0, 0));
            for (int i = 0; i < AvatarHex.Length; i++)
                MakeAvatarItem(AvatarHex[i], gridRect.transform);

            // 만들기 버튼. 사용자 손배치 (스케일 1.1945 흡수)
            var createBtn = MakeImageButton("CreateButton", canvas, LoadProfileSprite("BtnCreate.png"),
                new Vector2(0, -480), new Vector2(669, 155));

            // 4) 나뭇잎 (맨 앞 측면 하늘에서 떨어짐)
            BuildLeaves(canvas);

            // 생성 확인 팝업 (프로필을 생성할까요?)
            var confirmPopup = MakePopup("ConfirmPopup", canvasGo.transform, new Vector2(760, 360), out var confirmCard);
            var confirmTitle = MakeText("Title", confirmCard, "프로필을 생성할까요?", 48, Slate, font);
            Place(confirmTitle.rectTransform, new Vector2(0, 75), new Vector2(680, 90));
            var confirmNo = MakeButton("CancelButton", confirmCard, "취소", 40,
                new Vector2(-150, -85), new Vector2(220, 100), FieldBg, Slate, font);
            var confirmYes = MakeButton("ConfirmButton", confirmCard, "확인", 40,
                new Vector2(150, -85), new Vector2(220, 100), BtnBlue, White, font);
            var confirmX = MakeIconButton("Close", confirmCard, "×", new Vector2(330, 130), new Vector2(64, 64), font);

            // 유효성 팝업 (입력 정보를 확인해주세요!)
            var validationPopup = MakePopup("ValidationPopup", canvasGo.transform, new Vector2(760, 320), out var valCard);
            var valTitle = MakeText("Title", valCard, "입력 정보를 확인해주세요!", 46, Slate, font);
            Place(valTitle.rectTransform, new Vector2(0, 55), new Vector2(700, 90));
            var valOk = MakeButton("OkButton", valCard, "확인", 40,
                new Vector2(0, -75), new Vector2(240, 100), BtnBlue, White, font);
            var valX = MakeIconButton("Close", valCard, "×", new Vector2(330, 110), new Vector2(64, 64), font);

            confirmPopup.SetActive(false);
            validationPopup.SetActive(false);

            // View 와이어링
            var view = canvasGo.AddComponent<ProfileCreateView>();
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("confirmTitleText").objectReferenceValue = confirmTitle;
            so.FindProperty("nameInput").objectReferenceValue = input;
            so.FindProperty("yearDropdown").objectReferenceValue = yearDd;
            so.FindProperty("monthDropdown").objectReferenceValue = monthDd;
            so.FindProperty("avatarGrid").objectReferenceValue = gridRect;
            so.FindProperty("createButton").objectReferenceValue = createBtn;
            so.FindProperty("confirmPopup").objectReferenceValue = confirmPopup;
            so.FindProperty("confirmYesButton").objectReferenceValue = confirmYes;
            so.FindProperty("confirmNoButton").objectReferenceValue = confirmNo;
            so.FindProperty("confirmCloseButton").objectReferenceValue = confirmX;
            so.FindProperty("validationPopup").objectReferenceValue = validationPopup;
            so.FindProperty("validationOkButton").objectReferenceValue = valOk;
            so.FindProperty("validationCloseButton").objectReferenceValue = valX;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileCreateSceneBuilder] 완료 (글래스 카드 + 구름/나뭇잎 효과)");
        }

        // 전체화면 stretch 컨테이너 (구름/나뭇잎 레이어용)
        static RectTransform FullContainer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        // 장식 이미지 (레이캐스트 X). preserveAspect 기본 true, 글래스 패널은 false로 채움
        static Image AddDecor(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size,
            bool preserveAspect = true)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        // 좌측 정렬 라벨 (leftX 가 글자 왼쪽 끝)
        static TMP_Text MakeLabelAt(string name, Transform parent, string text, TMP_FontAsset font,
            float leftX, float y, int fontSize, float width)
        {
            var t = MakeText(name, parent, text, fontSize, Slate, font);
            t.alignment = TextAlignmentOptions.Left;
            var rect = t.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(leftX, y);
            rect.sizeDelta = new Vector2(width, 50);
            return t;
        }

        // 이미지 버튼 (스프라이트 그대로, 비율 유지)
        static Button MakeImageButton(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        // 파스텔 글로우 레이어: 카드 둘레 하늘에 따뜻한 파스텔 빛덩이. PastelGlow 와이어링
        static void BuildPastelGlow(Transform canvas)
        {
            var container = FullContainer("PastelGlow", canvas);
            var glow = SceneBuilderUtils.EnsureGlowSprite();
            // 카드 반폭 713 바깥 둘레에 배치 (복숭아/핑크/라벤더/골드/민트)
            var defs = new (Vector2 pos, float size, Color color)[]
            {
                (new Vector2(-770f, 380f), 520f, new Color(1.00f, 0.85f, 0.72f, 0.18f)),
                (new Vector2(790f, 400f), 480f, new Color(1.00f, 0.78f, 0.86f, 0.18f)),
                (new Vector2(-800f, -360f), 560f, new Color(0.90f, 0.80f, 1.00f, 0.18f)),
                (new Vector2(810f, -380f), 500f, new Color(1.00f, 0.92f, 0.74f, 0.18f)),
                (new Vector2(-860f, 20f), 420f, new Color(0.82f, 0.96f, 0.90f, 0.15f)),
                (new Vector2(860f, 40f), 440f, new Color(1.00f, 0.84f, 0.78f, 0.16f)),
            };
            var list = new System.Collections.Generic.List<Graphic>();
            foreach (var d in defs)
            {
                var rt = MakeRect("Glow", container, d.pos, new Vector2(d.size, d.size));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = glow;
                img.type = Image.Type.Simple;
                img.raycastTarget = false;
                img.color = d.color;
                list.Add(img);
            }
            var pg = container.gameObject.AddComponent<PastelGlow>();
            var so = new SerializedObject(pg);
            var arr = so.FindProperty("orbs");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
        }

        // 구름 레이어: 글래스 뒤에서 한 방향으로 흐름. CloudDrift 와이어링
        static void BuildClouds(Transform canvas)
        {
            var container = FullContainer("Clouds", canvas);
            var sprite = LoadProfileSprite("cloud_transparent.png");
            float ratio = 336f / 688f;
            var defs = new (Vector2 pos, float w)[]
            {
                (new Vector2(-840f, -460f), 420f),
                (new Vector2(820f, -490f), 440f),
                (new Vector2(-300f, 320f), 280f),
                (new Vector2(220f, -120f), 260f),
            };
            var list = new System.Collections.Generic.List<RectTransform>();
            foreach (var d in defs)
            {
                var rt = MakeRect("Cloud", container, d.pos, new Vector2(d.w, d.w * ratio));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0.92f);
                list.Add(rt);
            }
            var drift = container.gameObject.AddComponent<CloudDrift>();
            var so = new SerializedObject(drift);
            so.FindProperty("speed").floatValue = 34f; // 더 빠르게
            var arr = so.FindProperty("clouds");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
        }

        // 나뭇잎 레이어: 측면 하늘에서 U자로 떨어짐. LeafFall 와이어링
        static void BuildLeaves(Transform canvas)
        {
            var container = FullContainer("Leaves", canvas);
            var sprite = LoadProfileSprite("leaf_transparent.png");
            float ratio = 86f / 78f;
            // x = 좌우 측면 하늘 (넓어진 카드 폭 ±713 바깥, 좁은 띠라 흔들림 줄임)
            var defs = new (float x, float w)[]
            {
                (-780f, 70f), (-820f, 58f), (-880f, 64f), (-800f, 52f),
                (780f, 66f), (820f, 58f), (880f, 72f), (800f, 54f),
            };
            var list = new System.Collections.Generic.List<Image>();
            foreach (var d in defs)
            {
                var rt = MakeRect("Leaf", container, new Vector2(d.x, 0f), new Vector2(d.w, d.w * ratio));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                list.Add(img);
            }
            var fall = container.gameObject.AddComponent<LeafFall>();
            var so = new SerializedObject(fall);
            so.FindProperty("swayAmplitude").floatValue = 50f; // 좁은 측면 띠라 흔들림 축소
            var arr = so.FindProperty("leaves");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedProperties();
        }

        static TMP_InputField MakeInput(string name, Transform parent, TMP_DefaultControls.Resources res,
            TMP_FontAsset font, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateInputField(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, pos, size);

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.color = FieldBg; }

            var input = go.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.textComponent.fontSize = 44;
            input.textComponent.color = Slate;
            if (input.placeholder is TMP_Text ph) { ph.text = placeholder; ph.fontSize = 44; }
            ApplyFont(go, font);
            return input;
        }

        static TMP_Dropdown MakeDropdown(string name, Transform parent, TMP_DefaultControls.Resources res,
            TMP_FontAsset font, Vector2 pos, Vector2 size)
        {
            var go = TMP_DefaultControls.CreateDropdown(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Anchor(rect, pos, size);

            var img = go.GetComponent<Image>();
            if (img != null) { img.sprite = Rounded(); img.type = Image.Type.Sliced; img.color = FieldBg; }

            var dd = go.GetComponent<TMP_Dropdown>();
            if (dd.captionText != null) { dd.captionText.fontSize = 40; dd.captionText.color = Slate; }
            StyleDropdownList(dd, itemHeight: 96, listHeight: 520);
            ApplyFont(go, font);
            return dd;
        }

        // 드롭다운 펼친 목록: 항목 높이/목록 박스 키우고 라벨 좌측 여백 (글자 겹침 방지)
        static void StyleDropdownList(TMP_Dropdown dd, int itemHeight, int listHeight)
        {
            var template = dd.template;
            if (template != null)
                template.sizeDelta = new Vector2(template.sizeDelta.x, listHeight);

            var item = template != null ? template.Find("Viewport/Content/Item") as RectTransform : null;
            if (item != null)
                item.sizeDelta = new Vector2(item.sizeDelta.x, itemHeight);

            if (dd.itemText != null)
            {
                dd.itemText.fontSize = 36;
                dd.itemText.color = Slate;
                dd.itemText.alignment = TextAlignmentOptions.Left;
                var lr = dd.itemText.rectTransform;
                lr.offsetMin = new Vector2(44, 0);
                lr.offsetMax = new Vector2(-30, 0);
            }
        }

        // 아바타 한 칸: SelectedBorder(선택 시) + Icon(이모지) + Button + AvatarItem
        static void MakeAvatarItem(string hex, Transform parent)
        {
            var item = MakeRect($"Avatar_{hex}", parent, Vector2.zero, new Vector2(140, 140));

            var border = MakeRect("Selected", item.transform, Vector2.zero, new Vector2(146, 146));
            var borderImg = border.gameObject.AddComponent<Image>();
            borderImg.sprite = Rounded();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = BtnBlue;
            border.gameObject.SetActive(false);

            var icon = MakeRect("Icon", item.transform, Vector2.zero, new Vector2(140, 140));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = LoadAvatar(hex);
            iconImg.preserveAspect = true;

            var btn = item.gameObject.AddComponent<Button>();
            btn.targetGraphic = iconImg;

            var ai = item.gameObject.AddComponent<AvatarItem>();
            ai.avatarId = hex;
            ai.button = btn;
            ai.selectedMark = border.gameObject;
        }

        static Button MakeButton(string name, Transform parent, string label, int fontSize,
            Vector2 pos, Vector2 size, Color bg, Color textColor, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, label, fontSize, textColor, font);
            SceneBuilderUtils.FillStretch(t.rectTransform, 8);
            return btn;
        }

        // 모달 팝업: 전체화면 반투명 오버레이 + 둥근 흰 카드
        static GameObject MakePopup(string name, Transform parent, Vector2 cardSize, out RectTransform card)
        {
            var overlay = SceneBuilderUtils.CreatePanel(name, parent);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            card = MakeRect("Card", overlay.transform, Vector2.zero, cardSize);
            var img = card.gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.color = White;
            return overlay;
        }

        // 아이콘 버튼 (투명 배경 + 글리프). X 닫기 버튼용.
        static Button MakeIconButton(string name, Transform parent, string glyph, Vector2 pos, Vector2 size, TMP_FontAsset font)
        {
            var rect = MakeRect(name, parent, pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 투명 (레이캐스트만)
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var t = MakeText("Text", rect, glyph, 44, Slate, font);
            SceneBuilderUtils.FillStretch(t.rectTransform, 6);
            return btn;
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

        static void ApplyFont(GameObject go, TMP_FontAsset font)
        {
            if (font == null) return;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            Anchor(rect, pos, size);
            return rect;
        }

        static void Anchor(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static void Place(RectTransform rect, Vector2 pos, Vector2 size) => Anchor(rect, pos, size);

        static TMP_DefaultControls.Resources BuildTmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard   = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob       = Builtin("UI/Skin/Knob.psd"),
                checkmark  = Builtin("UI/Skin/Checkmark.psd"),
                dropdown   = Builtin("UI/Skin/DropdownArrow.psd"),
                mask       = Builtin("UI/Skin/UIMask.psd"),
            };
        }

        // Art/UI/Profile/ PNG → Sprite (미임포트면 강제 임포트)
        static Sprite LoadProfileSprite(string file)
        {
            string p = "Assets/_Project/Art/UI/Profile/" + file;
            if (AssetImporter.GetAtPath(p) == null)
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            EnsureSprite(p);
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }

        static void EnsureSprite(string p)
        {
            if (AssetImporter.GetAtPath(p) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.SaveAndReimport();
            }
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Rounded()
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
            return s != null ? s : Builtin("UI/Skin/UISprite.psd");
        }

        static Sprite LoadAvatar(string hex)
        {
            string p = $"Assets/_Project/Art/Avatars/{hex}.png";
            var importer = AssetImporter.GetAtPath(p) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(p) as TextureImporter;
            }
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; // Multiple → Single (슬라이스 없으면 null이 됨)
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
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
