using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 프로필 등록 3단계(완료) — ProfileCreateSceneBuilder가 2단계 위, 1단계 아래에 만든다. 처음에는 꺼져 있고
    // 2단계 '다음'에서 저장이 끝나면 ProfileCreateView가 켠다.
    // 에셋: profiles/35 프로필 카드(원 테두리, 편집 배지, @artti_user, 인용문)  36 제목/부제
    //       37 대한민국 줄  38 기본 스타일 줄  39 안내 카드  40 시작하기
    // 생년월일 줄은 값이 바뀌어야 해서 이미지가 없고 코드로 그린다(37/38과 같은 크기).
    // 배치는 1단계와 같은 1920x1080 Stage, 좌상단 기준(y 아래가 -).
    public static class ProfileDoneStepBuilder
    {
        public struct Refs
        {
            public RectTransform root;
            public TMP_Text name;
            public Image avatar;
            public TMP_Text birth;
            public Button start;
            public Button edit;
            public Button close;
        }

        // 35 카드: 본체 (63,192)-(1023,1286) px. 원 테두리 중심 (540,546), 안쪽 반지름 약 262, 편집 배지 중심 (742,740)
        static readonly Rect CardBody = Rect.MinMaxRect(63, 192, 1023, 1286);
        const float CardImgW = 1086f, CardImgH = 1448f;
        const float CardBodyH = 640f;
        static readonly Vector2 CardCenter = new Vector2(590f, -630f);
        static float CardScale => CardBodyH / CardBody.height;
        // 카드 이미지 px -> Stage (본체 중심이 CardCenter에 온다)
        static Vector2 CardPx(float x, float y) =>
            CardCenter + new Vector2((x - CardBody.center.x) * CardScale, -(y - CardBody.center.y) * CardScale);

        static readonly Rect TitleBody  = Rect.MinMaxRect(391, 256, 1784, 483);   // 36
        static readonly Rect RowBody    = Rect.MinMaxRect(136, 167, 2036, 552);   // 37 (38도 거의 같음)
        static readonly Rect Row38Body  = Rect.MinMaxRect(161, 199, 2012, 537);   // 38
        static readonly Rect TipBody    = Rect.MinMaxRect(153, 129, 2020, 605);   // 39
        static readonly Rect StartBody  = Rect.MinMaxRect(159, 191, 2015, 524);   // 40

        // 오른쪽 열: 폭 620, 줄 높이는 이미지 비율(약 126), 간격 20
        const float ColX = 1301f, ColW = 620f, Gap = 20f, Top = 310f;

        static readonly Color PanelFill = new Color(0.05f, 0.08f, 0.20f, 0.86f);
        static readonly Color PanelLine = new Color(0.35f, 0.55f, 1.00f, 0.55f);
        static readonly Color RowFill   = new Color(0.06f, 0.10f, 0.26f, 0.92f);
        static readonly Color RowLine   = new Color(0.30f, 0.50f, 1.00f, 0.85f);
        static readonly Color TextWhite = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color TextDim   = new Color(0.62f, 0.68f, 0.88f, 0.75f);

        public static Refs Build(Transform canvas, TMP_FontAsset font)
        {
            var r = new Refs();

            // ===== 루트 + 배경 + 패널 (1단계와 같은 틀) =====
            var root = ChildRect("DoneStep", canvas);
            StretchFull(root);
            r.root = root;
            var bg = ChildRect("Background", root);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(Dir + "10.png", 2048);
            bgImg.raycastTarget = true;   // 아래 2단계로 터치가 새지 않게
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1448f / 1086f;

            var stage = ChildRect("Stage", root);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(1920f, 1080f);
            RoundedBox("PanelLine", stage, new Vector2(960f, -540f), new Vector2(1846f, 1038f), PanelLine);
            RoundedBox("Panel", stage, new Vector2(960f, -540f), new Vector2(1840f, 1032f), PanelFill);

            var logo = AddText(stage, "Logo", "ARTTI", font, 46, TextWhite, true, new Vector2(100f, -52f), new Vector2(300f, 64f));
            logo.characterSpacing = 4f;
            var logoSub = AddText(stage, "LogoSub", "AR CONVENIENCE STORE", font, 16, TextDim, false, new Vector2(102f, -112f), new Vector2(360f, 26f));
            logoSub.characterSpacing = 6f;
            BuildSteps(stage, font, activeIndex: 2);
            r.close = MakeImageButton("CloseBtn", stage, Dir + "18.png", Rect.MinMaxRect(423, 421, 832, 824), 1254f, 1254f,
                new Vector2(1812f, -96f), 64f);

            // ===== 제목 (36) =====
            Place("Title", stage, Dir + "36.png", TitleBody, PillW, PillH, new Vector2(960f, -230f), 620f);   // 단계 라벨(y -138)과 겹치지 않게 폭 620

            // ===== 왼쪽: 프로필 카드 (35) + 원 안 아바타 + 이름 =====
            float cardW = CardBody.width * CardScale;
            Place("ProfileCard", stage, Dir + "35.png", CardBody, CardImgW, CardImgH, CardCenter, cardW);

            // 원 테두리 안쪽을 둥글게 잘라 아바타를 넣는다(Knob 원형 마스크)
            float d = 524f * CardScale;
            var mask = ChildRect("AvatarMask", stage);
            mask.anchorMin = mask.anchorMax = new Vector2(0f, 1f);
            mask.pivot = new Vector2(0.5f, 0.5f);
            mask.anchoredPosition = CardPx(540f, 546f);
            mask.sizeDelta = new Vector2(d, d);
            var maskImg = mask.gameObject.AddComponent<Image>();
            maskImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            maskImg.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var av = ChildRect("Avatar", mask);
            av.anchorMin = av.anchorMax = new Vector2(0.5f, 0f);
            av.pivot = new Vector2(0.5f, 0f);
            av.anchoredPosition = Vector2.zero;
            av.sizeDelta = new Vector2(d * 1.05f, d * 1.05f);
            r.avatar = av.gameObject.AddComponent<Image>();
            r.avatar.sprite = LoadSprite(Dir + "19.png", 1024);
            r.avatar.preserveAspect = true;
            r.avatar.raycastTarget = false;

            // 편집 배지(카드 이미지에 그려져 있음)가 원 안쪽까지 걸쳐 있어 아바타에 일부 덮인다.
            // 카드 이미지를 배지 크기 원형 마스크로 한 번 더 얹어 배지를 아바타 위로 되살린다.
            var badgeCenter = CardPx(742f, 740f);
            float badgeD = 170f * CardScale;
            var badge = ChildRect("EditBadge", stage);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 1f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = badgeCenter;
            badge.sizeDelta = new Vector2(badgeD, badgeD);
            var badgeMask = badge.gameObject.AddComponent<Image>();
            badgeMask.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            badgeMask.raycastTarget = false;
            badge.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var cardCopy = ChildRect("Card", badge);
            cardCopy.anchorMin = cardCopy.anchorMax = cardCopy.pivot = new Vector2(0.5f, 0.5f);
            cardCopy.sizeDelta = new Vector2(CardImgW, CardImgH) * CardScale;
            var cardImgCenter = CardCenter + new Vector2((CardImgW / 2f - CardBody.center.x) * CardScale, (CardBody.center.y - CardImgH / 2f) * CardScale);
            cardCopy.anchoredPosition = cardImgCenter - badgeCenter;
            var copyImg = cardCopy.gameObject.AddComponent<Image>();
            copyImg.sprite = LoadSprite(Dir + "35.png", 2048);
            copyImg.preserveAspect = true;
            copyImg.raycastTarget = false;

            // 배지 위 투명 클릭 영역 -> 1단계로 돌아가 아바타부터 다시 고치기
            r.edit = Hit("EditBtn", stage, badgeCenter, new Vector2(180f, 180f) * CardScale);

            // 이름: 원 아래 ~ @artti_user 위 (카드 px y 890 부근)
            r.name = AddText(stage, "Name", "이름", font, 44, TextWhite, true,
                CardPx(540f, 890f) + new Vector2(-250f, 32f), new Vector2(500f, 64f), TextAlignmentOptions.Center);

            // ===== 오른쪽 열: 생년월일 / 대한민국 / 기본 스타일 / 안내 / 시작하기 =====
            float rowH = ColW * RowBody.height / RowBody.width;
            float y = -Top - rowH / 2f;
            r.birth = BirthRow(stage, font, new Vector2(ColX, y), new Vector2(ColW * 0.955f, rowH * 0.92f));
            y -= rowH + Gap;
            Place("RowCountry", stage, Dir + "37.png", RowBody, PillW, PillH, new Vector2(ColX, y), ColW);
            y -= rowH + Gap;
            Place("RowStyle", stage, Dir + "38.png", Row38Body, PillW, PillH, new Vector2(ColX, y), ColW * Row38Body.width / RowBody.width);
            float tipH = ColW * TipBody.height / TipBody.width;
            y -= rowH / 2f + Gap + tipH / 2f;
            Place("Tip", stage, Dir + "39.png", TipBody, PillW, PillH, new Vector2(ColX, y), ColW);
            const float startW = 400f;
            float startH = startW * StartBody.height / StartBody.width;
            y -= tipH / 2f + 24f + startH / 2f;
            r.start = MakeImageButton("StartBtn", stage, Dir + "40.png", StartBody, PillW, PillH,
                new Vector2(ColX + ColW / 2f - startW / 2f, y), startW);

            root.gameObject.SetActive(false);
            return r;
        }

        // 생년월일 줄: 37/38과 같은 크기의 둥근 칸 + 왼쪽 원 아이콘 자리 + 날짜 글자
        static TMP_Text BirthRow(Transform parent, TMP_FontAsset font, Vector2 center, Vector2 size)
        {
            RoundedBox("RowBirthLine", parent, center, size + new Vector2(6f, 6f), RowLine);
            var row = RoundedBox("RowBirth", parent, center, size, RowFill);
            float h = size.y;
            var icon = ChildRect("Icon", row);
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 0.5f);
            icon.anchoredPosition = new Vector2(h * 0.18f, 0f);
            icon.sizeDelta = new Vector2(h * 0.64f, h * 0.64f);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            iconImg.color = new Color(0.12f, 0.18f, 0.40f, 1f);
            iconImg.raycastTarget = false;
            var iconText = AddText(icon, "Label", "생일", font, 20, TextWhite, true, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            StretchFull(iconText.rectTransform);

            var t = AddText(row, "Value", "생년월일 미입력", font, 32, TextWhite, false,
                new Vector2(h * 0.18f + h * 0.64f + 28f, -(h - 50f) / 2f), new Vector2(size.x - h * 1.2f, 50f), TextAlignmentOptions.Left);
            return t;
        }

        // 이미지 속 본체(px)가 center/bodyWidth에 오도록 전체 이미지를 놓는다 (클릭 없음)
        static void Place(string name, Transform parent, string path, Rect body, float imgW, float imgH, Vector2 center, float bodyWidth)
        {
            float scale = bodyWidth / body.width;
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(imgW * scale, imgH * scale);
            rt.anchoredPosition = center + new Vector2((imgW / 2f - body.center.x) * scale, (body.center.y - imgH / 2f) * scale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, 2048);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        static Button Hit(string name, Transform parent, Vector2 center, Vector2 size)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            return btn;
        }
    }
}
