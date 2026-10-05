using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 말하기 훈련 모드 선택 화면 (다크 네온). 가로 1920x1080 Stage 기준, 좌상단 원점(y 아래가 -).
    // 에셋: Art/UI/TrainingHub/
    //   56 배경(1672x941, 매장 / 왼쪽 문구 "작은 변화가 더 나은 내일을 만들어요." / 오른쪽 위 ARTTI 간판 / 왼쪽 위 뒤로 버튼이 그려져 있음)
    //   57 제목 "말하기 훈련 / 오늘은 어떤 연습을 해볼까요?"
    //   58 정확도 모드 카드(여자 캐릭터, 그림 글자 "구음진단모드")   59 대화 모드 카드(남자 캐릭터, 그림 글자 "소통연습모드")
    //   (2026-10-03 새 시안 "58 1"/"59 1" -> 2026-10-05 58/59 이름으로 정리 -> 같은 날 새 시안(1536x1024)으로 다시 교체)
    //   60 연습하기(파랑, 정확도)   61 연습하기(보라, 대화)   tip_bulb 안내 바 전구(코드로 그린 안내 바에 씀)
    // 좌표는 시안(1672x941)을 1920 폭으로 환산(x1.1483)한 값. 카드는 56 왼쪽 문구(Stage x ~505, y 258~597)를 피해 오른쪽에 두고,
    // 태블릿(16:10)에서 좌우가 44씩 잘려도 오른쪽 카드 끝이 남는 크기(0.4525배)로 정했다.
    // 배치: 좌 = 대화 모드, 우 = 정확도 모드 (2026-10-03 좌우를 바꿈).
    // 카드와 그 아래 연습하기 버튼 어디를 눌러도 그 모드로 시작한다(모드마다 Button 하나).
    public static class TrainingHubSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const string HubDir = "Assets/_Project/Art/UI/TrainingHub/";

        // 뒤로: 56에 그려진 원(시안 px 67~142, 47~122) 위에 투명 버튼. 누르는 동안만 옅은 원이 뜬다.
        static readonly Vector2 BackCenter = new Vector2(119.8f, -97.4f);
        const float BackDiameter = 87f, BackHit = 124f;

        // 제목(57, 2172x724). 본체 = 알파>200. 두 카드 묶음의 가운데 위.
        const float TitleImgW = 2172f, TitleImgH = 724f;
        static readonly Rect TitleBody = Rect.MinMaxRect(499, 175, 1681, 557);
        static readonly Vector2 TitleCenter = new Vector2(1172f, -164f);
        const float TitleWidth = 400f;

        // 카드(58/59, 1536x1024) 0.4525배. 위치 = 이미지 좌상단. 두 카드는 같은 높이.
        // 본체(알파>200): 59 x 22~1525 y 20~1019, 58 x 60~1468 y 29~1017.
        // 좌 카드 본체 왼쪽 515(56 왼쪽 문구 피함), 우 카드 본체 오른쪽 1831(태블릿 잘림 44 + 여유),
        // 두 카드 사이 가장 좁은 틈 25가 되는 가장 큰 배율(본체 위 y ~255, 아래 y ~707).
        const float CardImgW = 1536f, CardImgH = 1024f, CardScale = 0.4525f;
        static readonly Vector2 ConversationCardPos = new Vector2(505f, -245.9f);
        static readonly Vector2 AccuracyCardPos = new Vector2(1166.7f, -245.9f);
        // 누르는 영역(카드 px, 위에서부터 y). 기울어진 두 카드 사이에서 서로 겹치지 않게 Stage x 1203 기준으로 나눈다.
        static readonly Rect ConversationCardHit = Rect.MinMaxRect(62, 50, 1495, 994);
        static readonly Rect AccuracyCardHit = Rect.MinMaxRect(90, 59, 1428, 992);

        // 연습하기(60/61, 2172x724). 본체 = 알파>250(알약 테두리). 두 버튼은 높이를 맞춘다.
        // 카드 본체 가운데 x, 카드 아래 가장자리(Stage y ~707)에서 17 밑.
        const float PillImgW = 2172f, PillImgH = 724f, PillHeight = 76f;
        static readonly Rect BluePillBody = Rect.MinMaxRect(259, 135, 1912, 559);
        static readonly Rect PurplePillBody = Rect.MinMaxRect(291, 162, 1878, 542);
        static readonly Vector2 ConversationPillCenter = new Vector2(855.1f, -762f);
        static readonly Vector2 AccuracyPillCenter = new Vector2(1512.4f, -762f);

        // 아래 안내 바(코드로 그림). 폰(20:9)은 배경 위아래가 잘려 Stage y 49~965만 보이므로 그 안에 둔다.
        static readonly Vector2 TipCenter = new Vector2(1172f, -913f);
        static readonly Vector2 TipSize = new Vector2(730f, 66f);   // 안내문(26pt) 폭 586 + 전구 + 좌우 여백
        const string TipText = "원하는 모드를 선택하면, 바로 연습을 시작할 수 있어요.";

        static readonly Color TipFill = new Color(0.04f, 0.07f, 0.20f, 0.62f);
        static readonly Color TipLine = new Color(0.55f, 0.68f, 1.00f, 0.55f);
        static readonly Color TipTextColor = new Color(0.93f, 0.95f, 1.00f, 1.00f);
        static readonly Color BackFlash = new Color(0.62f, 0.80f, 1.00f, 1.00f);

        [MenuItem("Artti/Build TrainingHubScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.TrainingHub);
            SceneBuilderUtils.ClearRootObjects();

            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();
            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var font = SceneBuilderUtils.GetKoreanFont();

            // ===== 배경 (56, 화면 꽉 채움) + Stage (배경을 따라 같이 늘어남) =====
            var bg = BuildBackground(canvasGo.transform);
            var stage = ChildRect("Stage", bg);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = ReferenceResolution;
            bg.GetComponent<CoverImageFitter>().SetFollower(stage, ReferenceResolution);

            BuildBackButton(stage);

            // ===== 제목 (57) =====
            float ts = TitleWidth / TitleBody.width;
            var title = ChildRect("Title", stage);
            title.anchorMin = title.anchorMax = new Vector2(0f, 1f);
            title.pivot = new Vector2(0.5f, 0.5f);
            title.sizeDelta = new Vector2(TitleImgW, TitleImgH) * ts;
            title.anchoredPosition = TitleCenter + new Vector2((TitleImgW / 2f - TitleBody.center.x) * ts,
                (TitleBody.center.y - TitleImgH / 2f) * ts);
            AddImage(title, HubDir + "57.png");

            // ===== 모드 카드 2장: 좌 = 대화 모드(59 + 61), 우 = 정확도 모드(58 + 60) =====
            var conversationBtn = BuildModeCard(stage, "ConversationModeCard", HubDir + "59.png", ConversationCardPos, ConversationCardHit,
                HubDir + "61.png", PurplePillBody, ConversationPillCenter);
            var accuracyBtn = BuildModeCard(stage, "AccuracyModeCard", HubDir + "58.png", AccuracyCardPos, AccuracyCardHit,
                HubDir + "60.png", BluePillBody, AccuracyPillCenter);

            BuildTipBar(stage, font);

            var view = canvasGo.AddComponent<TrainingHubView>();
            var so = new SerializedObject(view);
            so.FindProperty("accuracyBtn").objectReferenceValue = accuracyBtn;
            so.FindProperty("conversationBtn").objectReferenceValue = conversationBtn;
            so.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[TrainingHubSceneBuilder] 완료 (배경 56 꽉 채움 + 제목 57 + 모드 카드 좌 대화(59) / 우 정확도(58) + 연습하기 61/60 + 안내 바)");
        }

        // 배경(56): 화면을 빈틈 없이 꽉 채운다. 왼쪽 문구와 뒤로 버튼이 가장자리 가까이 그려져 있어
        // 사용자 선택 화면(48)과 같이 CoverImageFitter로 비율을 최대 6%만 바꿔 태블릿 좌우 잘림을 줄인다.
        // 폰(20:9)은 위아래가 잘리는데, 뒤로 버튼(Stage y 54~)이 남도록 기준점을 위쪽(0.70)에 둔다.
        static RectTransform BuildBackground(Transform canvas)
        {
            var bg = ChildRect("Background", canvas);
            var img = bg.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(HubDir + "56.png", 2048);
            img.raycastTarget = false;
            bg.gameObject.AddComponent<CoverImageFitter>().Setup(1672f / 941f, 1.06f, new Vector2(0.5f, 0.70f));
            return bg;
        }

        // 뒤로: 56에 그려진 원 위의 투명 버튼 -> MainScene. 누르는 동안(마우스는 올린 동안) 원 위에 옅은 빛을 띄운다.
        static void BuildBackButton(Transform stage)
        {
            var rt = ChildRect("BackButton", stage);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = BackCenter;
            rt.sizeDelta = new Vector2(BackHit, BackHit);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var flashRt = ChildRect("PressFlash", rt);
            flashRt.anchorMin = flashRt.anchorMax = flashRt.pivot = new Vector2(0.5f, 0.5f);
            flashRt.sizeDelta = new Vector2(BackDiameter, BackDiameter);
            var flash = flashRt.gameObject.AddComponent<Image>();
            flash.sprite = SceneBuilderUtils.EnsureCircleSprite();
            flash.color = new Color(BackFlash.r, BackFlash.g, BackFlash.b, 0f);   // 평소 투명(에디터 화면에서도)
            flash.raycastTarget = false;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;

            var glow = rt.gameObject.AddComponent<HomeButtonGlow>();
            glow.Setup(flash, null);
            var gso = new SerializedObject(glow);
            gso.FindProperty("baseAlpha").floatValue = 0f;
            gso.FindProperty("hoverAlpha").floatValue = 0.3f;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var back = rt.gameObject.AddComponent<SceneBackButton>();
            back.SetTarget("MainScene");
            var method = typeof(SceneBackButton).GetMethod(nameof(SceneBackButton.GoBack));
            var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), back, method);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        // 모드 카드: 루트 = 카드 이미지 자리(가운데 기준). Visual(카드 + 연습하기 그림)만 HomeButtonGlow가 살짝 키우고/줄이고,
        // 누르는 영역(CardHit + PillHit, 투명)은 그대로 둔다. 두 영역 모두 루트의 Button 하나로 이어진다.
        static Button BuildModeCard(Transform stage, string name, string cardPath, Vector2 cardPos, Rect cardHit,
            string pillPath, Rect pillBody, Vector2 pillCenter)
        {
            var cardSize = new Vector2(CardImgW, CardImgH) * CardScale;
            var root = ChildRect(name, stage);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = cardPos + new Vector2(cardSize.x / 2f, -cardSize.y / 2f);
            root.sizeDelta = cardSize;
            Vector2 rootCenter = root.anchoredPosition;

            var visual = ChildRect("Visual", root);
            StretchFull(visual);
            var card = ChildRect("Card", visual);
            StretchFull(card);
            AddImage(card, cardPath);

            // 연습하기: 본체(알약) 가운데가 pillCenter(Stage 좌표)에 오게 한다
            float ps = PillHeight / pillBody.height;
            var pillBodyPos = pillCenter - rootCenter;
            var pill = ChildRect("PracticeBtn", visual);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(PillImgW, PillImgH) * ps;
            pill.anchoredPosition = pillBodyPos + new Vector2((PillImgW / 2f - pillBody.center.x) * ps,
                (pillBody.center.y - PillImgH / 2f) * ps);
            AddImage(pill, pillPath);

            // 누르는 영역 (카드 px -> 루트 가운데 기준)
            var cardHitImg = AddHit(root, "CardHit",
                new Vector2((cardHit.center.x - CardImgW / 2f) * CardScale, (CardImgH / 2f - cardHit.center.y) * CardScale),
                cardHit.size * CardScale);
            AddHit(root, "PillHit", pillBodyPos, new Vector2(pillBody.width * ps, PillHeight));

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardHitImg;
            btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HomeButtonGlow>().Setup(null, visual);
            return btn;
        }

        // 아래 안내 바: 어두운 반투명 알약 + 옅은 파란 테두리 + 전구 + 안내문
        static void BuildTipBar(Transform stage, TMP_FontAsset font)
        {
            var line = RoundedBox("TipBar", stage, TipCenter, TipSize + new Vector2(3f, 3f), TipLine);
            line.GetComponent<Image>().pixelsPerUnitMultiplier = 44f / ((TipSize.y + 3f) / 2f);   // RoundedRect 테두리 44 -> 반원 끝
            var fill = ChildRect("Fill", line);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(1.5f, 1.5f); fill.offsetMax = new Vector2(-1.5f, -1.5f);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = Rounded(); fillImg.type = Image.Type.Sliced;
            fillImg.pixelsPerUnitMultiplier = 44f / (TipSize.y / 2f);
            fillImg.color = TipFill;
            fillImg.raycastTarget = false;

            var bulb = ChildRect("Bulb", line);
            bulb.anchorMin = bulb.anchorMax = new Vector2(0f, 0.5f);
            bulb.pivot = new Vector2(0f, 0.5f);
            bulb.anchoredPosition = new Vector2(34f, 1f);
            bulb.sizeDelta = new Vector2(44f, 44f);
            AddImage(bulb, HubDir + "tip_bulb.png", 256);

            var tip = AddText(line, "Tip", TipText, font, 26, TipTextColor, false, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            tip.rectTransform.anchorMin = Vector2.zero; tip.rectTransform.anchorMax = Vector2.one;
            tip.rectTransform.offsetMin = new Vector2(92f, 0f); tip.rectTransform.offsetMax = new Vector2(-44f, 0f);
        }

        static Image AddImage(RectTransform rt, string path, int maxSize = 2048)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, maxSize);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // 투명 raycast 영역 (부모 가운데 기준 center/size)
        static Image AddHit(RectTransform parent, string name, Vector2 center, Vector2 size)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }
    }
}
