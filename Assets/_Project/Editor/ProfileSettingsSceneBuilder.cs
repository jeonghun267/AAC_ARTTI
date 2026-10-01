using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Artti.UI;
using static Artti.Editor.ProfileAvatarStepBuilder;

namespace Artti.Editor
{
    // 프로필 설정 화면 (다크 네온, 시안 완성본). 사용자 선택 화면의 "프로필 설정"에서 들어온다.
    // 변경점(2026-10-01): 배경 62를 사용자 선택 배경(48)과 같은 그림(가운데 유리판만 없음, 1672x941)으로 바꿨다.
    //   배경 맞춤(CoverImageFitter)과 Stage(1920x1080)를 사용자 선택 화면과 똑같이 써서, 두 화면을 오갈 때 배경과 왼쪽 메뉴가 움직이지 않는다.
    //   왼쪽 메뉴는 사용자 선택 화면 손배치 자리(Stage px) 그대로, 패널은 점원 오른쪽에 둔다.
    // 패널 안 좌표는 시안 완성본(1448x1086 px, 위에서부터 y)으로 적고 Stage 단위로 바꾼다:
    //   Stage = (시안 - MockOrigin) x K. 시안 패널 왼쪽 위(522, 84)가 Stage (PanelLeft, PanelTop)에 온다.
    // 20:9 폰은 배경 위아래가 잘려 패널이 화면 밖으로 나가므로, 그때만 Stage를 같은 비율로 줄여 패널 전체가 보이게 한다(SafeRect).
    // 에셋: Art/UI/Home/profile_T/
    //   62 배경(점원 / ARTTI 로고 / 좌하단 문구 / 오른쪽 안내 패널이 그려져 있음, 48에서 가운데 유리판을 뺀 그림)
    //   70 패널(제목 "프로필 설정" + 부제가 그려져 있음)   71~74 내 프로필 행(아이콘 + 이름이 그려져 있음)   75 꺾쇠
    //   76/77 스위치 손잡이(끔/켬)   78/79 스위치 트랙(끔/켬. 79는 저장 버튼에도 씀)   80~85 느리게/보통/빠르게(보통, 선택)
    //   86/87 미리듣기(평소, 읽는 중)   88~91 남자/여자 선택지(보통, 선택)
    //   derived/ = 원본에서 만든 조각(Art_Source/ui_profile_settings/make_derived.py)
    //     섹션 2개 + 행 바(69를 나누고 안쪽 상자 테두리를 지움), 드롭다운 칩(67, 글자를 지우고 TMP로 씀),
    //     왼쪽 메뉴(50/51의 켜짐/꺼짐을 바꾼 버전), 훈련 설정 행 아이콘 3개(원본에 없어 그림)
    // 이미지 비율이 시안과 다른 조각(패널, 섹션, 행, 트랙, 저장)은 9-slice로 늘려 시안 크기에 맞춘다(그려진 글자는 늘어나지 않는 쪽에 둔다).
    public static class ProfileSettingsSceneBuilder
    {
        static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        const float BgW = 1672f, BgH = 941f;                            // 62 = 48과 같은 크기
        static readonly Vector2 StageSize = new Vector2(1920f, 1080f);  // 사용자 선택 화면과 같은 Stage
        // 시안 px -> Stage 단위. 패널(시안 높이 878)이 Stage 57~1023에 들어가는 배율(태블릿에서 예전과 같은 글자 크기).
        const float K = 1.10f;
        const float PanelLeft = 720f, PanelTop = 57f;                   // 시안 패널 왼쪽 위(522, 84)가 오는 Stage 자리. 점원 어깨 오른쪽
        static readonly Vector2 MockOrigin = new Vector2(522f - PanelLeft / K, 84f - PanelTop / K);   // Stage (0,0)에 해당하는 시안 좌표
        const string Dir = "Assets/_Project/Art/UI/Home/profile_T/";
        const string Der = Dir + "derived/";

        // 왼쪽 메뉴: 사용자 선택 화면(ProfileSelectScene) 손배치 자리 그대로 (Stage px, 본체 가운데 / 폭)
        static readonly Vector2 NavUserStage = new Vector2(186f, 272f);
        static readonly Vector2 NavSettingsStage = new Vector2(186f, 377f);
        const float NavWidthStage = 244f;

        // 화면에 꼭 보여야 하는 영역(Stage px, 위에서부터 y): 왼쪽 메뉴 ~ 패널 + 저장 버튼 빛
        static readonly Rect SafeStage = Rect.MinMaxRect(50f, 40f, 1490f, 1040f);

        // 이미지 원본 크기(px)
        static readonly Vector2 Size2172 = new Vector2(2172f, 724f);
        static readonly Vector2 Size1774 = new Vector2(1774f, 887f);
        static readonly Vector2 Size1254 = new Vector2(1254f, 1254f);

        // 이미지 속 본체(px, 위에서부터 y). 테두리 선 또는 알파>200 실측.
        static readonly Rect PanelBody     = Rect.MinMaxRect(83, 112, 1038, 1279);     // 70 (1122x1402) 테두리 선
        static readonly Rect SecBody       = Rect.MinMaxRect(13, 12, 1017, 415);       // sec_profile (1030x428)
        static readonly Rect SecTrainBody  = Rect.MinMaxRect(13, 13, 1017, 375);       // sec_training (1030x389)
        static readonly Rect RowBlankBody  = Rect.MinMaxRect(13, 12, 1017, 122);       // row_blank (1030x135)
        static readonly Rect ChevronBody   = Rect.MinMaxRect(500, 416, 818, 918);      // 75
        static readonly Rect KnobOffBody   = Rect.MinMaxRect(254, 253, 1001, 983);     // 76
        static readonly Rect KnobOnBody    = Rect.MinMaxRect(273, 277, 980, 966);      // 77
        static readonly Rect TrackOffBody  = Rect.MinMaxRect(395, 126, 1782, 588);     // 78
        static readonly Rect TrackOnBody   = Rect.MinMaxRect(314, 91, 1857, 618);      // 79 (저장 버튼도)
        static readonly Rect PreviewBody   = Rect.MinMaxRect(247, 110, 1925, 617);     // 86
        static readonly Rect PreviewOnBody = Rect.MinMaxRect(302, 114, 1870, 639);     // 87
        static readonly Rect ChipBody      = Rect.MinMaxRect(29, 61, 539, 275);        // dd_closed (570x340)
        static readonly Rect ChipOpenBody  = Rect.MinMaxRect(60, 61, 569, 275);        // dd_open (630x340)
        static readonly Rect NavOffBody    = Rect.MinMaxRect(122, 340, 1328, 729);     // nav_user_off (1448x1086, 50과 같은 틀)
        static readonly Rect NavOnBody     = Rect.MinMaxRect(113, 275, 1559, 726);     // nav_settings_on (1672x941, 51과 같은 틀)

        // 내 프로필 행 71~74: 본체가 이미지마다 조금씩 다르다. 9-slice 왼쪽 900px 안에 아이콘과 이름이 들어 있다.
        static readonly (string file, Rect body)[] ProfileRows =
        {
            ("71", Rect.MinMaxRect(93, 192, 2080, 528)),    // 아바타
            ("72", Rect.MinMaxRect(83, 204, 2089, 528)),    // 닉네임
            ("73", Rect.MinMaxRect(117, 196, 2056, 535)),   // 한마디
            ("74", Rect.MinMaxRect(84, 201, 2090, 532)),    // 생년월일
        };
        // 말 빠르기 (보통 이미지, 선택 이미지)
        static readonly (string normal, Rect normalBody, string selected, Rect selectedBody, string name)[] SpeedButtons =
        {
            ("80", Rect.MinMaxRect(337, 125, 1835, 595), "81", Rect.MinMaxRect(411, 135, 1761, 587), "Slow"),
            ("82", Rect.MinMaxRect(391, 142, 1781, 581), "83", Rect.MinMaxRect(449, 150, 1726, 578), "Normal"),
            ("84", Rect.MinMaxRect(352, 139, 1819, 585), "85", Rect.MinMaxRect(455, 147, 1720, 591), "Fast"),
        };
        // 목소리 선택지 0 여자 / 1 남자 (1774x887)
        static readonly (string normal, Rect normalBody, string selected, Rect selectedBody, string name)[] VoiceOptions =
        {
            ("90", Rect.MinMaxRect(402, 273, 1372, 657), "91", Rect.MinMaxRect(295, 224, 1481, 704), "Female"),
            ("88", Rect.MinMaxRect(247, 215, 1530, 691), "89", Rect.MinMaxRect(229, 227, 1548, 680), "Male"),
        };

        // ===== 배치 (시안 px) =====
        static readonly Rect PanelRect = Rect.MinMaxRect(522, 84, 1199, 962);
        static readonly Rect SecProfileRect = Rect.MinMaxRect(561, 239, 1172, 573);
        static readonly Rect SecTrainingRect = Rect.MinMaxRect(561, 590, 1172, 860);
        const float RowX0 = 575f, RowX1 = 1156f;
        static readonly float[] ProfileRowTops = { 301f, 367f, 433f, 499f };
        const float ProfileRowH = 61f;
        static readonly float[] TrainingRowTops = { 651f, 718f, 785f };
        const float TrainingRowH = 62f;
        const float IconX = 609f, IconSize = 34f, LabelX = 656f, LabelSize = 19f;
        const float ValueRight = 1100f, ValueLeft = 730f, ValueSize = 18f, ChevronX = 1127f, ChevronH = 16f, AvatarD = 44f;
        const float ToggleX0 = 1074.5f, ToggleX1 = 1133.5f, ToggleH = 32f, KnobD = 28f;
        static readonly float[] SpeedCenters = { 852f, 972f, 1092f };
        const float SpeedH = 36f, SpeedHitW = 116f;
        const float ChipX = 958f, ChipH = 34f, ChipLabelOffset = 28.7f, ChipLabelSize = 14.5f;   // 라벨 = 칩 본체 왼쪽에서 28.7(지운 '여자' 자리)
        const float PreviewX = 1081f, PreviewH = 40f;
        static readonly Rect SaveRect = Rect.MinMaxRect(689, 882, 1022, 941);
        // 프로필 삭제: 사용자 선택 화면 카드에 있던 빨간 삭제 칩(Profile_C/54, 1536x1024)을 패널 오른쪽 위, 제목 줄에 둔다.
        //   누르는 영역은 보이는 칩보다 사방 DeleteHitPad 넓게.
        const string DeleteChipPath = "Assets/_Project/Art/UI/Profile_C/54.png";
        static readonly Rect DeleteChipBody = Rect.MinMaxRect(146, 327, 1389, 710);   // 54 알파>250
        static readonly Vector2 DeleteCenter = new Vector2(1122f, 150f);
        const float DeleteW = 92f, DeleteHitPad = 8f;
        const float SaveLabelSize = 22f;
        const float OptionH = 32f, OptionGap = 6f, OptionHitW = 110f;

        static readonly Color White      = new Color(0.96f, 0.97f, 1.00f, 1.00f);
        static readonly Color ValueColor = new Color(0.855f, 0.882f, 0.98f, 1.00f);
        static readonly Color ListFill   = new Color(0.04f, 0.07f, 0.20f, 0.94f);
        static readonly Color ListLine   = new Color(0.35f, 0.55f, 1.00f, 0.60f);
        static readonly Color AvatarBack = new Color(0.16f, 0.31f, 0.67f, 1.00f);

        [MenuItem("Artti/Build ProfileSettingsScene Hierarchy")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[ProfileSettingsSceneBuilder] Play 모드에서는 실행하지 않는다."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            OpenOrCreateScene();
            SceneBuilderUtils.ClearRootObjects();
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();   // 미리듣기 소리

            var canvasGo = SceneBuilderUtils.CreateCanvas("[Canvas]", ReferenceResolution, CanvasScaler.ScreenMatchMode.Expand);
            var font = SceneBuilderUtils.GetKoreanFont();

            // ===== 배경 (62, 화면 꽉 채움) + Stage (배경을 따라 늘어남, 패널이 잘리면 줄어듦) =====
            // 사용자 선택 화면(ProfileSelectSceneBuilder.BuildBackground)과 같은 맞춤: 비율 최대 6% 조정, 기준점 위쪽(0.72)
            var bg = ChildRect("Background", canvasGo.transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = LoadSprite(Dir + "62.png", 2048);
            bgImg.raycastTarget = false;
            var fitter = bg.gameObject.AddComponent<CoverImageFitter>();
            fitter.Setup(BgW / BgH, 1.06f, new Vector2(0.5f, 0.72f));
            var stage = ChildRect("Stage", bg);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = StageSize;
            fitter.SetFollower(stage, StageSize);
            fitter.SetSafeRect(SafeStage, 16f);
            var o = MockOrigin;   // Stage 좌상단에 해당하는 시안 좌표

            // ===== 왼쪽 메뉴: 사용자 선택(꺼짐, 누르면 사용자 선택 화면) / 프로필 설정(켜짐 = 지금 화면) =====
            // 사용자 선택 화면과 같은 자리·폭(그 화면의 51/50과 같은 틀이라 본체 크기도 같다)
            var navUser = StageToMock(NavUserStage);
            var navSettings = StageToMock(NavSettingsStage);
            float navW = NavWidthStage / K;
            var userSelectBtn = ImageButton("UserSelectBtn", stage, o, Der + "nav_user_off.png", new Vector2(1448f, 1086f), NavOffBody,
                navUser, navW, 2048);
            float navOnH = navW * NavOnBody.height / NavOnBody.width;
            ImageByBody("ProfileSettingsMenu", stage, o, Der + "nav_settings_on.png", new Vector2(1672f, 941f), NavOnBody,
                navSettings, navOnH, 2048);

            // ===== 패널(70) + 섹션 2개 =====
            SlicedByBody("Panel", stage, o, Dir + "70.png", new Vector2(1122f, 1402f), PanelBody,
                new Vector4(143, 183, 144, 320), PanelRect, PanelRect.width / PanelBody.width, 2048);
            float secS = SecProfileRect.width / SecBody.width;
            SlicedByBody("SectionProfile", stage, o, Der + "sec_profile.png", new Vector2(1030f, 428f), SecBody,
                new Vector4(63, 63, 63, 112), SecProfileRect, secS, 2048);
            SlicedByBody("SectionTraining", stage, o, Der + "sec_training.png", new Vector2(1030f, 389f), SecTrainBody,
                new Vector4(63, 64, 63, 113), SecTrainingRect, secS, 2048);

            // ===== 내 프로필 행 4개 (행 전체가 버튼) =====
            var rowButtons = new Button[4];
            TMP_Text[] values = new TMP_Text[4];
            Image avatarImage = null;
            string[] rowNames = { "AvatarRow", "NicknameRow", "IntroRow", "BirthRow" };
            // 씬에서 보이는 예시 값(시안 문구). 실행하면 View가 고른 사용자의 값으로 바꾼다.
            string[] sampleValues = { null, "ARTTI_유저", "오늘도 천천히 연습해요", "2000. 01. 01" };
            for (int i = 0; i < 4; i++)
            {
                float top = ProfileRowTops[i], yc = top + ProfileRowH / 2f;
                var (file, body) = ProfileRows[i];
                rowButtons[i] = ButtonBox(rowNames[i], stage, o, RowX0, top, RowX1, top + ProfileRowH, out var vis);
                var ro = new Vector2(RowX0, top);
                float s = ProfileRowH / body.height;
                SlicedByBody("Row", vis, ro, Dir + file + ".png", Size2172, body,
                    new Vector4(900, 0, Size2172.x - body.xMax + 80, 0), Rect.MinMaxRect(RowX0, top, RowX1, top + ProfileRowH), s, 4096);
                if (i == 0) avatarImage = AvatarThumb(vis, ro, new Vector2(ValueRight - AvatarD / 2f, yc));
                else
                {
                    values[i] = Label("Value", vis, ro, sampleValues[i], font, ValueSize, ValueColor, false, ValueLeft, ValueRight, yc, TextAlignmentOptions.Right);
                    values[i].overflowMode = TextOverflowModes.Ellipsis;
                }
                ImageByBody("Chevron", vis, ro, Dir + "75.png", Size1254, ChevronBody, new Vector2(ChevronX, yc), ChevronH, 512);
            }

            // ===== 훈련 설정 1: 카드 눌러 읽어주기 (행 전체가 스위치) =====
            float t0 = TrainingRowTops[0], y0c = t0 + TrainingRowH / 2f;
            var cardRow = ButtonBox("CardReadRow", stage, o, RowX0, t0, RowX1, t0 + TrainingRowH, out var cardVis);
            var c0 = new Vector2(RowX0, t0);
            TrainingRowBase(cardVis, c0, y0c, "icon_card_read", "카드 눌러 읽어주기", font);
            var toggleRect = Rect.MinMaxRect(ToggleX0, y0c - ToggleH / 2f, ToggleX1, y0c + ToggleH / 2f);
            SlicedByBody("TrackOff", cardVis, c0, Dir + "78.png", Size2172, TrackOffBody, PillBorder(TrackOffBody), toggleRect,
                ToggleH / TrackOffBody.height, 4096);
            var trackOn = SlicedByBody("TrackOn", cardVis, c0, Dir + "79.png", Size2172, TrackOnBody, PillBorder(TrackOnBody), toggleRect,
                ToggleH / TrackOnBody.height, 4096);
            float knobOff = ToggleX0 + ToggleH / 2f, knobOn = ToggleX1 - ToggleH / 2f;
            var knob = Box("Knob", cardVis, c0, knobOff - KnobD / 2f, y0c - KnobD / 2f, knobOff + KnobD / 2f, y0c + KnobD / 2f);
            var ko = new Vector2(knobOff - KnobD / 2f, y0c - KnobD / 2f);
            ImageByBody("KnobOff", knob, ko, Dir + "76.png", Size1254, KnobOffBody, new Vector2(knobOff, y0c), KnobD, 1024);
            var knobOnImg = ImageByBody("KnobOn", knob, ko, Dir + "77.png", Size1254, KnobOnBody, new Vector2(knobOff, y0c), KnobD, 1024);
            // 처음 모양 = 꺼짐(켜짐 트랙/손잡이 투명). 실행하면 View가 프로필 값으로 맞춘다.
            trackOn.color = new Color(1f, 1f, 1f, 0f);
            knobOnImg.color = new Color(1f, 1f, 1f, 0f);

            // ===== 훈련 설정 2: 점원 말 빠르기 =====
            float t1 = TrainingRowTops[1], y1c = t1 + TrainingRowH / 2f;
            var c1 = new Vector2(RowX0, t1);
            var speedRow = Box("SpeedRow", stage, o, RowX0, t1, RowX1, t1 + TrainingRowH);
            TrainingRowBase(speedRow, c1, y1c, "icon_speed", "점원 말 빠르기", font);
            var speedBtns = new Button[3];
            var speedNormal = new GameObject[3];
            var speedSelected = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var sb = SpeedButtons[i];
                float cx = SpeedCenters[i];
                speedBtns[i] = ButtonBox("Speed" + sb.name, speedRow, c1, cx - SpeedHitW / 2f, y1c - 21f, cx + SpeedHitW / 2f, y1c + 21f, out var sv);
                var so = new Vector2(cx - SpeedHitW / 2f, y1c - 21f);
                speedNormal[i] = ImageByBody("Normal", sv, so, Dir + sb.normal + ".png", Size2172, sb.normalBody, new Vector2(cx, y1c), SpeedH, 2048).gameObject;
                speedSelected[i] = ImageByBody("Selected", sv, so, Dir + sb.selected + ".png", Size2172, sb.selectedBody, new Vector2(cx, y1c), SpeedH, 2048).gameObject;
                speedSelected[i].SetActive(i == 1);
                speedNormal[i].SetActive(i != 1);
            }

            // ===== 훈련 설정 3: 읽어주는 목소리 (드롭다운 칩 + 미리듣기) =====
            float t2 = TrainingRowTops[2], y2c = t2 + TrainingRowH / 2f;
            var c2 = new Vector2(RowX0, t2);
            var voiceRow = Box("VoiceRow", stage, o, RowX0, t2, RowX1, t2 + TrainingRowH);
            TrainingRowBase(voiceRow, c2, y2c, "icon_voice", "읽어주는 목소리", font);

            float chipW = ChipH * ChipBody.width / ChipBody.height;
            var chip = ButtonBox("VoiceChip", voiceRow, c2, ChipX - chipW / 2f - 4f, y2c - 22f, ChipX + chipW / 2f + 4f, y2c + 22f, out var chipVis);
            var cho = new Vector2(ChipX - chipW / 2f - 4f, y2c - 22f);
            ImageByBody("Closed", chipVis, cho, Der + "dd_closed.png", new Vector2(570f, 340f), ChipBody, new Vector2(ChipX, y2c), ChipH, 1024);
            var chipOpen = ImageByBody("Open", chipVis, cho, Der + "dd_open.png", new Vector2(630f, 340f), ChipOpenBody, new Vector2(ChipX, y2c), ChipH, 1024).gameObject;
            chipOpen.SetActive(false);
            float labelX = ChipX - chipW / 2f + ChipLabelOffset;
            var voiceLabel = Label("Label", chipVis, cho, "여자", font, ChipLabelSize, White, false, labelX - 30f, labelX + 30f, y2c, TextAlignmentOptions.Center);

            float previewW = PreviewH * PreviewBody.width / PreviewBody.height;
            var preview = ButtonBox("PreviewBtn", voiceRow, c2, PreviewX - previewW / 2f, y2c - 22f, PreviewX + previewW / 2f, y2c + 22f, out var pv);
            var po = new Vector2(PreviewX - previewW / 2f, y2c - 22f);
            var previewIdle = ImageByBody("Idle", pv, po, Dir + "86.png", Size2172, PreviewBody, new Vector2(PreviewX, y2c), PreviewH, 2048).gameObject;
            var previewPlaying = ImageByBody("Playing", pv, po, Dir + "87.png", Size2172, PreviewOnBody, new Vector2(PreviewX, y2c), PreviewH, 2048).gameObject;
            previewPlaying.SetActive(false);

            // ===== 변경사항 저장 (79를 늘린 버튼 + 글자) =====
            var save = ButtonBox("SaveBtn", stage, o, SaveRect.xMin, SaveRect.yMin, SaveRect.xMax, SaveRect.yMax, out var saveVis);
            var sro = new Vector2(SaveRect.xMin, SaveRect.yMin);
            SlicedByBody("Bg", saveVis, sro, Dir + "79.png", Size2172, TrackOnBody, PillBorder(TrackOnBody), SaveRect,
                SaveRect.height / TrackOnBody.height, 4096);
            var saveLabel = Label("Label", saveVis, sro, "변경사항 저장", font, SaveLabelSize, White, true,
                SaveRect.xMin, SaveRect.xMax, SaveRect.center.y, TextAlignmentOptions.Center);

            // ===== 프로필 삭제 칩 (패널 오른쪽 위) =====
            float delH = DeleteW * DeleteChipBody.height / DeleteChipBody.width;
            var deleteBtn = ButtonBox("DeleteProfileBtn", stage, o,
                DeleteCenter.x - DeleteW / 2f - DeleteHitPad, DeleteCenter.y - delH / 2f - DeleteHitPad,
                DeleteCenter.x + DeleteW / 2f + DeleteHitPad, DeleteCenter.y + delH / 2f + DeleteHitPad, out var delVis);
            ImageByBody("Image", delVis, new Vector2(DeleteCenter.x - DeleteW / 2f - DeleteHitPad, DeleteCenter.y - delH / 2f - DeleteHitPad),
                DeleteChipPath, new Vector2(1536f, 1024f), DeleteChipBody, DeleteCenter, delH, 2048);

            // ===== 목소리 선택 목록 (칩 아래로 열림, 맨 위에 그린다) =====
            var list = BuildVoiceList(stage, o, chipW, y2c, out var blocker, out var optionBtns, out var optionNormal, out var optionSelected);

            // ===== 삭제 확인 팝업 (사용자 선택 화면에 있던 것과 같은 52~55 판, 캔버스 맨 위) =====
            var deletePopupGo = ProfileSelectSceneBuilder.BuildDeletePopup(canvasGo.transform, font, out var delInput, out var delCover,
                out var delConfirm, out var delCancel, out var delX);
            deletePopupGo.transform.SetAsLastSibling();
            var deletePopup = deletePopupGo.AddComponent<ProfileDeletePopup>();
            var dso = new SerializedObject(deletePopup);
            dso.FindProperty("input").objectReferenceValue = delInput;
            dso.FindProperty("fieldCover").objectReferenceValue = delCover;
            dso.FindProperty("confirmButton").objectReferenceValue = delConfirm;
            dso.FindProperty("cancelButton").objectReferenceValue = delCancel;
            dso.FindProperty("closeButton").objectReferenceValue = delX;
            dso.ApplyModifiedPropertiesWithoutUndo();

            // ===== View 와이어링 =====
            var audio = canvasGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var view = canvasGo.AddComponent<ProfileSettingsView>();
            var so2 = new SerializedObject(view);
            so2.FindProperty("userSelectButton").objectReferenceValue = userSelectBtn;
            so2.FindProperty("avatarRow").objectReferenceValue = rowButtons[0];
            so2.FindProperty("nicknameRow").objectReferenceValue = rowButtons[1];
            so2.FindProperty("introRow").objectReferenceValue = rowButtons[2];
            so2.FindProperty("birthRow").objectReferenceValue = rowButtons[3];
            so2.FindProperty("avatarImage").objectReferenceValue = avatarImage;
            so2.FindProperty("nicknameValue").objectReferenceValue = values[1];
            so2.FindProperty("introValue").objectReferenceValue = values[2];
            so2.FindProperty("birthValue").objectReferenceValue = values[3];
            so2.FindProperty("cardReadRow").objectReferenceValue = cardRow;
            so2.FindProperty("toggleTrackOn").objectReferenceValue = trackOn;
            so2.FindProperty("toggleKnobOn").objectReferenceValue = knobOnImg;
            so2.FindProperty("toggleKnob").objectReferenceValue = knob;
            so2.FindProperty("knobOffX").floatValue = knob.anchoredPosition.x;
            so2.FindProperty("knobOnX").floatValue = knob.anchoredPosition.x + (knobOn - knobOff) * K;
            SetArray(so2, "speedButtons", speedBtns);
            SetArray(so2, "speedNormal", speedNormal);
            SetArray(so2, "speedSelected", speedSelected);
            so2.FindProperty("voiceChip").objectReferenceValue = chip;
            so2.FindProperty("voiceChipOpen").objectReferenceValue = chipOpen;
            so2.FindProperty("voiceLabel").objectReferenceValue = voiceLabel;
            so2.FindProperty("voiceList").objectReferenceValue = list;
            so2.FindProperty("voiceListBlocker").objectReferenceValue = blocker;
            SetArray(so2, "voiceOptions", optionBtns);
            SetArray(so2, "voiceOptionNormal", optionNormal);
            SetArray(so2, "voiceOptionSelected", optionSelected);
            so2.FindProperty("previewButton").objectReferenceValue = preview;
            so2.FindProperty("previewIdle").objectReferenceValue = previewIdle;
            so2.FindProperty("previewPlaying").objectReferenceValue = previewPlaying;
            so2.FindProperty("previewSource").objectReferenceValue = audio;
            so2.FindProperty("saveButton").objectReferenceValue = save;
            so2.FindProperty("saveLabel").objectReferenceValue = saveLabel;
            so2.FindProperty("deleteButton").objectReferenceValue = deleteBtn;
            so2.FindProperty("deletePopup").objectReferenceValue = deletePopup;
            so2.ApplyModifiedProperties();

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            SceneBuilderUtils.SaveActiveSceneAgainAfterEditorTicks();   // 배경 맞춤이 다음 틱에 씬을 수정됨으로 만들어서
            Debug.Log("[ProfileSettingsSceneBuilder] 완료 (배경 62 + 왼쪽 메뉴 + 패널 70 + 내 프로필 71~74 + 훈련 설정(스위치/말 빠르기/목소리) + 저장 + 프로필 삭제)");
        }

        static void OpenOrCreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths.ProfileSettings) != null)
            {
                SceneBuilderUtils.OpenScene(ScenePaths.ProfileSettings);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePaths.ProfileSettings);
        }

        // 훈련 설정 행 공통: 행 바(row_blank) + 아이콘 + 이름
        static void TrainingRowBase(Transform parent, Vector2 origin, float yc, string icon, string label, TMP_FontAsset font)
        {
            float bodyH = RowBlankBody.height * (RowX1 - RowX0) / RowBlankBody.width;
            ImageByBody("RowBg", parent, origin, Der + "row_blank.png", new Vector2(1030f, 135f), RowBlankBody,
                new Vector2((RowX0 + RowX1) / 2f, yc), bodyH, 2048);
            ImageByBody("Icon", parent, origin, Der + icon + ".png", new Vector2(256f, 256f), new Rect(0f, 0f, 256f, 256f),
                new Vector2(IconX, yc), IconSize, 256);
            Label("Label", parent, origin, label, font, LabelSize, White, false, LabelX, LabelX + 260f, yc, TextAlignmentOptions.Left);
        }

        // 아바타 행 값: 둥글게 자른 아바타 얼굴(사용자가 고른 캐릭터)
        static Image AvatarThumb(Transform parent, Vector2 origin, Vector2 center)
        {
            float r = AvatarD / 2f;
            var mask = Box("AvatarMask", parent, origin, center.x - r, center.y - r, center.x + r, center.y + r);
            var maskImg = mask.gameObject.AddComponent<Image>();
            maskImg.sprite = SceneBuilderUtils.EnsureCircleSprite();
            maskImg.color = AvatarBack;
            maskImg.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var av = ChildRect("Avatar", mask);
            StretchFull(av);
            var img = av.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;   // 그림이 없으면 흰 원이 되므로 꺼 둔다. View가 아바타를 넣으며 켠다.
            return img;
        }

        // 목소리 선택 목록: 화면 전체 투명 막이(누르면 닫힘) + Panel(어두운 판 + 여자/남자). UIPopupMotion이 Panel을 키우며 연다.
        static GameObject BuildVoiceList(RectTransform stage, Vector2 stageOrigin, float chipW, float chipYc, out Button blocker,
            out Button[] options, out GameObject[] normal, out GameObject[] selected)
        {
            const float pad = 3000f;   // Stage가 줄어든 폰에서도 화면 전체를 덮게 Stage 밖으로 넉넉히
            var root = ChildRect("VoiceList", stage);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(-pad, -pad);
            root.offsetMax = new Vector2(pad, pad);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            blocker = root.gameObject.AddComponent<Button>();
            blocker.targetGraphic = hit;
            blocker.transition = Selectable.Transition.None;
            var lo = stageOrigin + new Vector2(-pad / K, -pad / K);   // 막이 좌상단(시안 px)

            float x0 = ChipX - 62f, x1 = ChipX + 62f;
            float y0 = chipYc + ChipH / 2f + 8f;
            float y1 = y0 + OptionGap + 2f * (OptionH + 8f) + OptionGap;
            var panel = Box("Panel", root, lo, x0, y0, x1, y1);
            RoundedFill(panel, "Line", new Vector2(4f, 4f), ListLine);
            RoundedFill(panel, "Fill", Vector2.zero, ListFill);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // 판 위를 눌러도 닫히지 않게

            options = new Button[2];
            normal = new GameObject[2];
            selected = new GameObject[2];
            var pnl = new Vector2(x0, y0);
            for (int i = 0; i < 2; i++)
            {
                var vo = VoiceOptions[i];
                float cy = y0 + OptionGap + (OptionH + 8f) * i + (OptionH + 8f) / 2f;
                options[i] = ButtonBox(vo.name, panel, pnl, ChipX - OptionHitW / 2f, cy - 20f, ChipX + OptionHitW / 2f, cy + 20f, out var ov);
                var oo = new Vector2(ChipX - OptionHitW / 2f, cy - 20f);
                normal[i] = ImageByBody("Normal", ov, oo, Dir + vo.normal + ".png", Size1774, vo.normalBody, new Vector2(ChipX, cy), OptionH, 2048).gameObject;
                selected[i] = ImageByBody("Selected", ov, oo, Dir + vo.selected + ".png", Size1774, vo.selectedBody, new Vector2(ChipX, cy), OptionH, 2048).gameObject;
                selected[i].SetActive(i == 0);
                normal[i].SetActive(i != 0);
            }
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        // 부모를 꽉 채우는(grow만큼 바깥으로) 둥근 판
        static void RoundedFill(RectTransform parent, string name, Vector2 grow, Color color)
        {
            var rt = ChildRect(name, parent);
            StretchFull(rt);
            rt.offsetMin = -grow;
            rt.offsetMax = grow;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2f;
            img.color = color;
            img.raycastTarget = false;
        }

        // ===== 배치 헬퍼 (좌표는 모두 시안 px, origin = 부모 좌상단의 시안 좌표) =====

        // 시안 좌표 -> 부모 좌상단 기준 Stage 단위 (y 위가 +)
        static Vector2 At(Vector2 p, Vector2 origin) => new Vector2((p.x - origin.x) * K, -(p.y - origin.y) * K);

        // Stage 좌표(px, 위에서부터 y) -> 시안 좌표
        static Vector2 StageToMock(Vector2 stagePx) => MockOrigin + stagePx / K;

        // 시안 사각형 (x0,y0)-(x1,y1) 자리의 빈 rect (부모 좌상단 앵커, 가운데 피벗)
        static RectTransform Box(string name, Transform parent, Vector2 origin, float x0, float y0, float x1, float y1)
        {
            var rt = ChildRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = At(new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f), origin);
            rt.sizeDelta = new Vector2(x1 - x0, y1 - y0) * K;
            return rt;
        }

        // 이미지 속 본체(body, px)의 가운데가 시안 center, 높이가 bodyH(시안 px)가 되게 놓는다(빛 여백은 본체 밖으로 넘친다).
        static Image ImageByBody(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body,
            Vector2 center, float bodyH, int maxSize)
        {
            float s = bodyH / body.height;   // 시안 px / 이미지 px
            var imgCenter = center + new Vector2((imgSize.x / 2f - body.center.x) * s, (imgSize.y / 2f - body.center.y) * s);
            var rt = Box(name, parent, origin, imgCenter.x - imgSize.x * s / 2f, imgCenter.y - imgSize.y * s / 2f,
                imgCenter.x + imgSize.x * s / 2f, imgCenter.y + imgSize.y * s / 2f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(path, maxSize);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // 본체 테두리가 시안 사각형 target에 오도록 9-slice로 늘린다. s = 시안 px / 이미지 px(테두리 쪽 배율),
        // border = 이미지 px (왼, 아래, 오른, 위). 테두리 쪽은 s배로 그대로, 가운데만 늘어난다.
        static Image SlicedByBody(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body,
            Vector4 border, Rect target, float s, int maxSize)
        {
            var sprite = LoadSlicedSprite(path, border, maxSize);
            var rt = Box(name, parent, origin, target.xMin - body.xMin * s, target.yMin - body.yMin * s,
                target.xMax + (imgSize.x - body.xMax) * s, target.yMax + (imgSize.y - body.yMax) * s);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            // 스프라이트 100 px/unit, 캔버스 100 px/unit 기준: 테두리 크기(Stage 단위) = 테두리 px / 배수 = 테두리 px * s * K
            img.pixelsPerUnitMultiplier = 1f / (s * K);
            img.raycastTarget = false;
            return img;
        }

        // 알약(양 끝 반원) 이미지의 9-slice 테두리: 좌우 = 본체 밖 여백 + 반원(본체 높이 절반), 위아래는 늘리지 않음(비율 그대로 씀)
        static Vector4 PillBorder(Rect body) =>
            new Vector4(body.xMin + body.height / 2f, 0f, Size2172.x - body.xMax + body.height / 2f, 0f);

        static Sprite LoadSlicedSprite(string path, Vector4 border, int maxSize)
        {
            LoadSprite(path, maxSize);   // 스프라이트 설정 + 임포트
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                // 9-slice는 사각 메시(FullRect)여야 테두리 경고 없이 늘어난다
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                if (ti.spriteBorder != border || st.spriteMeshType != SpriteMeshType.FullRect)
                {
                    st.spriteMeshType = SpriteMeshType.FullRect;
                    ti.SetTextureSettings(st);
                    ti.spriteBorder = border;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 투명 누르는 영역 + Visual(누르면 살짝 작아짐, HomeButtonGlow). Visual은 루트와 같은 크기라 자식은 루트와 같은 origin을 쓴다.
        static Button ButtonBox(string name, Transform parent, Vector2 origin, float x0, float y0, float x1, float y1, out RectTransform visual)
        {
            var root = Box(name, parent, origin, x0, y0, x1, y1);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            visual = ChildRect("Visual", root);
            StretchFull(visual);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<HomeButtonGlow>().Setup(null, visual);
            return btn;
        }

        // 본체 폭 bodyW(시안 px)의 이미지 버튼. 누르는 영역 = 본체.
        static Button ImageButton(string name, Transform parent, Vector2 origin, string path, Vector2 imgSize, Rect body,
            Vector2 center, float bodyW, int maxSize)
        {
            float bodyH = bodyW * body.height / body.width;
            var btn = ButtonBox(name, parent, origin, center.x - bodyW / 2f, center.y - bodyH / 2f, center.x + bodyW / 2f, center.y + bodyH / 2f, out var vis);
            ImageByBody("Image", vis, new Vector2(center.x - bodyW / 2f, center.y - bodyH / 2f), path, imgSize, body, center, bodyH, maxSize);
            return btn;
        }

        // 가운데 세로 정렬 글자. 가로 x0~x1, 세로 가운데 yc (시안 px). size = 시안 px.
        static TMP_Text Label(string name, Transform parent, Vector2 origin, string text, TMP_FontAsset font, float size, Color color,
            bool bold, float x0, float x1, float yc, TextAlignmentOptions align)
        {
            var rt = Box(name, parent, origin, x0, yc - size, x1, yc + size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size * K;
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void SetArray<T>(SerializedObject so, string prop, T[] items) where T : Object
        {
            var p = so.FindProperty(prop);
            p.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}
