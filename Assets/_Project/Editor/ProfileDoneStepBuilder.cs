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
        const float CardBodyH = 730f;                                     // 내용 영역 y 295~1025를 꽉 채운다
        static readonly Vector2 CardCenter = new Vector2(590f, -660f);
        static float CardScale => CardBodyH / CardBody.height;
        // 카드 이미지 px -> Stage (본체 중심이 CardCenter에 온다)
        static Vector2 CardPx(float x, float y) =>
            CardCenter + new Vector2((x - CardBody.center.x) * CardScale, -(y - CardBody.center.y) * CardScale);

        static readonly Rect TitleBody  = Rect.MinMaxRect(391, 256, 1784, 483);   // 36
        static readonly Rect RowBody    = Rect.MinMaxRect(136, 167, 2036, 552);   // 37 (38도 거의 같음)
        static readonly Rect Row38Body  = Rect.MinMaxRect(161, 199, 2012, 537);   // 38
        static readonly Rect BirthBody  = Rect.MinMaxRect(178, 184, 1995, 543);   // 41
        static readonly Rect TipBody    = Rect.MinMaxRect(153, 129, 2020, 605);   // 39
        static readonly Rect StartBody  = Rect.MinMaxRect(159, 191, 2015, 524);   // 40

        // 오른쪽 열: 폭 640, 줄 높이는 이미지 비율(약 130), 간격 16. 카드(폭 약 641)와 100 띄워 둘을 가운데 정렬
        const float ColX = 1331f, ColW = 640f, Gap = 16f, Top = 295f;

        static readonly Color TextWhite = new Color(0.96f, 0.97f, 1.00f, 1.00f);

        public static Refs Build(Transform canvas, TMP_FontAsset font)
        {
            var r = new Refs();

            // ===== 1단계와 같은 틀 (배경 + 패널 + 로고 + 단계 표시 + 닫기) =====
            var stage = BuildFrame(canvas, "DoneStep", font, 2, out r.close);
            var root = (RectTransform)stage.parent;
            r.root = root;

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
            r.birth = BirthRow(stage, font, new Vector2(ColX, y));
            y -= rowH + Gap;
            Place("RowCountry", stage, Dir + "37.png", RowBody, PillW, PillH, new Vector2(ColX, y), ColW);
            y -= rowH + Gap;
            Place("RowStyle", stage, Dir + "38.png", Row38Body, PillW, PillH, new Vector2(ColX, y), ColW * Row38Body.width / RowBody.width);
            float tipH = ColW * TipBody.height / TipBody.width;
            y -= rowH / 2f + Gap + tipH / 2f;
            Place("Tip", stage, Dir + "39.png", TipBody, PillW, PillH, new Vector2(ColX, y), ColW);
            // 시작하기: 시안처럼 열 폭 그대로 크게
            float startH = ColW * StartBody.height / StartBody.width;
            y -= tipH / 2f + 20f + startH / 2f;
            r.start = MakeImageButton("StartBtn", stage, Dir + "40.png", StartBody, PillW, PillH,
                new Vector2(ColX, y), ColW);

            root.gameObject.SetActive(false);
            return r;
        }

        // 생년월일 줄: 41(달력 아이콘이 있는 빈 칸) 위에 날짜 글자를 얹는다. 글자 시작점은 37의 "대한민국"과 맞춘다.
        static TMP_Text BirthRow(Transform parent, TMP_FontAsset font, Vector2 center)
        {
            float bodyW = ColW; // 41은 빛 번짐이 얇아 본체 폭을 37과 같게 두어야 겉보기 폭이 맞는다
            float bodyH = bodyW * BirthBody.height / BirthBody.width;
            Place("RowBirth", parent, Dir + "41.png", BirthBody, PillW, PillH, center, bodyW);

            const float textStart = 0.23f; // 본체 왼쪽 끝에서 글자 시작까지 (본체 너비 비율)
            float left = center.x - bodyW / 2f + bodyW * textStart;
            float width = bodyW * (1f - textStart) - 24f;
            return AddText(parent, "BirthValue", "생년월일 미입력", font, 36, TextWhite, false,
                new Vector2(left, center.y + bodyH / 2f), new Vector2(width, bodyH), TextAlignmentOptions.Left);
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
