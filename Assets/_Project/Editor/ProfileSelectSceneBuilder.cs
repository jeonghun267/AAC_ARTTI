using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Artti.Common;
using Artti.UI;

namespace Artti.Editor
{
    public static class ProfileSelectSceneBuilder
    {
        [MenuItem("Artti/Build ProfileSelectScene Hierarchy")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            SceneBuilderUtils.OpenScene(ScenePaths.ProfileSelect);
            SceneBuilderUtils.ClearRootObjects();

            // AppBootstrap (DontDestroyOnLoad in Awake)
            var bootstrap = new GameObject("[AppBootstrap]");
            bootstrap.AddComponent<AppBootstrap>();

            // EventSystem
            SceneBuilderUtils.CreateEventSystem();
            SceneBuilderUtils.EnsureAudioListener();

            // Canvas
            var canvasGo = SceneBuilderUtils.CreateCanvas();

            // Canvas 배경
            var bgPanel = SceneBuilderUtils.CreatePanel("Background", canvasGo.transform);
            bgPanel.AddComponent<Image>().color = new Color(0.96f, 0.96f, 0.98f, 1f);

            // Title
            var title = SceneBuilderUtils.CreateTMPText("Title", canvasGo.transform, "프로필 선택", 80);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -120);
            titleRect.sizeDelta = new Vector2(800, 140);

            // ProfileButton 프리팹 자동 생성
            var profileButtonPrefab = EnsureProfileButtonPrefab();

            // "+ 프로필 추가" 버튼 (우상단)
            var addBtn = SceneBuilderUtils.CreateButton("+ 추가", canvasGo.transform, 36);
            addBtn.gameObject.name = "AddProfileBtn";
            var addRect = addBtn.GetComponent<RectTransform>();
            addRect.anchorMin = new Vector2(1f, 1f);
            addRect.anchorMax = new Vector2(1f, 1f);
            addRect.pivot = new Vector2(1f, 1f);
            addRect.anchoredPosition = new Vector2(-32, -32);
            addRect.sizeDelta = new Vector2(200, 100);

            // AvatarGrid
            var gridGo = new GameObject("AvatarGrid");
            gridGo.transform.SetParent(canvasGo.transform, false);
            var gridRect = gridGo.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(900, 1200);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(280, 320);
            grid.spacing = new Vector2(24, 24);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            // TeacherModeModal — 반투명 오버레이 + 가운데 카드 컨테이너
            var modal = SceneBuilderUtils.CreatePanel("TeacherModeModal", canvasGo.transform);
            modal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // 반투명 오버레이

            // 카드 컨테이너 — 화면 좌우 80px 마진, 세로 중앙, 높이 720 고정
            // 화면 비율이 어떻든(폴드/플립/일반) 가로폭을 충분히 확보
            var card = new GameObject("Card");
            card.transform.SetParent(modal.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0.5f);
            cardRect.anchorMax = new Vector2(1f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(-160, 720); // -160 = 좌우 80px 마진
            card.AddComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
            SceneBuilderUtils.AddVerticalLayout(card, spacing: 32, padding: new RectOffset(60, 60, 60, 60), alignment: UnityEngine.TextAnchor.UpperCenter);

            var modalTitle = SceneBuilderUtils.CreateTMPText("ModalTitle", card.transform, "첫 프로필 만들기", 56);
            modalTitle.textWrappingMode = TextWrappingModes.NoWrap;
            modalTitle.overflowMode = TextOverflowModes.Overflow;
            SceneBuilderUtils.AddLayoutElement(modalTitle.gameObject, preferredHeight: 100);

            // InputField (TMP) — 표준 구조: Input → TextArea(RectMask2D) → (Placeholder, Text)
            // 비활성 상태로 빌드 → 모든 참조 할당 후 활성화 (OnEnable 시점에 ref가 null이면 placeholder가 안 잡힘)
            var inputGo = new GameObject("NicknameInput");
            inputGo.SetActive(false);
            inputGo.transform.SetParent(card.transform, false);
            inputGo.AddComponent<RectTransform>();
            inputGo.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.97f, 1f);
            SceneBuilderUtils.AddLayoutElement(inputGo, preferredHeight: 140);

            var textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(inputGo.transform, false);
            textAreaGo.AddComponent<RectTransform>();
            textAreaGo.AddComponent<RectMask2D>();
            SceneBuilderUtils.FillStretch(textAreaGo.GetComponent<RectTransform>(), padding: 24);

            var placeholder = SceneBuilderUtils.CreateTMPText("Placeholder", textAreaGo.transform, "닉네임 입력", 44);
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.overflowMode = TextOverflowModes.Ellipsis;
            placeholder.raycastTarget = false;
            SceneBuilderUtils.FillStretch(placeholder.rectTransform);

            var inputTextGo = SceneBuilderUtils.CreateTMPText("Text", textAreaGo.transform, "", 44);
            inputTextGo.alignment = TextAlignmentOptions.MidlineLeft;
            inputTextGo.textWrappingMode = TextWrappingModes.NoWrap;
            inputTextGo.overflowMode = TextOverflowModes.Ellipsis;
            inputTextGo.raycastTarget = false;
            SceneBuilderUtils.FillStretch(inputTextGo.rectTransform);

            var input = inputGo.AddComponent<TMP_InputField>();
            input.textViewport = textAreaGo.GetComponent<RectTransform>();
            input.textComponent = (TextMeshProUGUI)inputTextGo;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = SceneBuilderUtils.GetKoreanFont();
            input.pointSize = 44;
            input.text = "";
            inputGo.SetActive(true);
            input.ForceLabelUpdate();

            // 생성 버튼
            var createBtn = SceneBuilderUtils.CreateButton("프로필 생성", card.transform, 48);
            createBtn.gameObject.name = "CreateProfileBtn";
            SceneBuilderUtils.AddLayoutElement(createBtn.gameObject, preferredHeight: 140);
            var createBtnText = createBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (createBtnText != null)
            {
                createBtnText.textWrappingMode = TextWrappingModes.NoWrap;
                createBtnText.overflowMode = TextOverflowModes.Overflow;
            }

            modal.SetActive(false);

            // DeleteConfirmModal — 삭제 확인 다이얼로그
            var delModal = SceneBuilderUtils.CreatePanel("DeleteConfirmModal", canvasGo.transform);
            delModal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var delCard = new GameObject("Card");
            delCard.transform.SetParent(delModal.transform, false);
            var delCardRect = delCard.AddComponent<RectTransform>();
            delCardRect.anchorMin = new Vector2(0f, 0.5f);
            delCardRect.anchorMax = new Vector2(1f, 0.5f);
            delCardRect.pivot = new Vector2(0.5f, 0.5f);
            delCardRect.anchoredPosition = Vector2.zero;
            delCardRect.sizeDelta = new Vector2(-160, 560);
            delCard.AddComponent<Image>().color = Color.white;
            SceneBuilderUtils.AddVerticalLayout(delCard, spacing: 36, padding: new RectOffset(48, 48, 60, 48), alignment: TextAnchor.MiddleCenter);

            var delMsg = SceneBuilderUtils.CreateTMPText("Message", delCard.transform, "프로필을 삭제할까요?", 52);
            delMsg.textWrappingMode = TextWrappingModes.Normal;
            SceneBuilderUtils.AddLayoutElement(delMsg.gameObject, preferredHeight: 220);

            var delBtnRow = new GameObject("ButtonRow");
            delBtnRow.transform.SetParent(delCard.transform, false);
            delBtnRow.AddComponent<RectTransform>();
            var rowH = delBtnRow.AddComponent<HorizontalLayoutGroup>();
            rowH.spacing = 32;
            rowH.childControlWidth = true;
            rowH.childControlHeight = true;
            rowH.childForceExpandWidth = true;
            rowH.childForceExpandHeight = true;
            rowH.childAlignment = TextAnchor.MiddleCenter;
            SceneBuilderUtils.AddLayoutElement(delBtnRow, preferredHeight: 140);

            var cancelDelBtn = SceneBuilderUtils.CreateButton("취소", delBtnRow.transform, 44);
            cancelDelBtn.gameObject.name = "CancelDeleteBtn";

            var confirmDelBtn = SceneBuilderUtils.CreateButton("삭제", delBtnRow.transform, 44);
            confirmDelBtn.gameObject.name = "ConfirmDeleteBtn";
            var confirmImg = confirmDelBtn.GetComponent<Image>();
            if (confirmImg != null) confirmImg.color = new Color(0.9f, 0.25f, 0.25f, 1f);
            var confirmTextTmp = confirmDelBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (confirmTextTmp != null) confirmTextTmp.color = Color.white;

            delModal.SetActive(false);

            // ProfileSelectView
            var view = canvasGo.AddComponent<ProfileSelectView>();
            var so = new SerializedObject(view);
            so.FindProperty("avatarGrid").objectReferenceValue = gridRect;
            so.FindProperty("teacherModeModal").objectReferenceValue = modal;
            so.FindProperty("nicknameInput").objectReferenceValue = input;
            so.FindProperty("deleteConfirmModal").objectReferenceValue = delModal;
            so.FindProperty("deleteConfirmText").objectReferenceValue = delMsg;
            so.FindProperty("confirmDeleteButton").objectReferenceValue = confirmDelBtn;
            so.FindProperty("cancelDeleteButton").objectReferenceValue = cancelDelBtn;
            if (profileButtonPrefab != null)
                so.FindProperty("profileButtonPrefab").objectReferenceValue = profileButtonPrefab;
            so.ApplyModifiedProperties();

            // 생성 버튼 → CreateProfileFromInput
            WireViewMethod(createBtn, view, nameof(ProfileSelectView.CreateProfileFromInput));
            // "+ 추가" 버튼 → ShowCreateProfileModal
            WireViewMethod(addBtn, view, nameof(ProfileSelectView.ShowCreateProfileModal));

            SceneBuilderUtils.ForceRebuildCanvasLayouts(canvasGo);
            SceneBuilderUtils.SaveActiveScene();
            Debug.Log("[ProfileSelectSceneBuilder] 완료");
        }

        static void WireViewMethod(Button btn, ProfileSelectView view, string methodName)
        {
            var method = typeof(ProfileSelectView).GetMethod(methodName);
            if (method == null) { Debug.LogError($"[ProfileSelectSceneBuilder] {methodName} not found"); return; }
            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), view, method);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
        }

        const string ProfileButtonPrefabPath = "Assets/_Project/Prefabs/Profile/ProfileButton.prefab";

        static GameObject EnsureProfileButtonPrefab()
        {
            // 항상 새로 빌드 (ProfileButtonView 컴포넌트 추가 등 구조 변경에 대응)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProfileButtonPrefabPath) != null)
                AssetDatabase.DeleteAsset(ProfileButtonPrefabPath);

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/Profile"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Profile");

            var temp = new GameObject("ProfileButton");
            temp.AddComponent<RectTransform>();
            var bg = temp.AddComponent<Image>();
            bg.color = new Color(0.9f, 0.93f, 0.98f, 1f);
            var selectBtn = temp.AddComponent<Button>();
            selectBtn.targetGraphic = bg;
            var btnColors = selectBtn.colors;
            btnColors.normalColor = new Color(0.9f, 0.93f, 0.98f, 1f);
            btnColors.highlightedColor = new Color(0.8f, 0.88f, 1f, 1f);
            btnColors.pressedColor = new Color(0.6f, 0.78f, 1f, 1f);
            selectBtn.colors = btnColors;

            var font = SceneBuilderUtils.GetKoreanFont();

            var nickGo = new GameObject("Nickname");
            nickGo.transform.SetParent(temp.transform, false);
            var nickRect = nickGo.AddComponent<RectTransform>();
            nickRect.anchorMin = Vector2.zero;
            nickRect.anchorMax = Vector2.one;
            nickRect.offsetMin = new Vector2(16, 16);
            nickRect.offsetMax = new Vector2(-16, -16);
            var tmp = nickGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "닉네임";
            tmp.fontSize = 40;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;

            // 우상단 삭제(X) 버튼
            var delGo = new GameObject("DeleteButton");
            delGo.transform.SetParent(temp.transform, false);
            var delRect = delGo.AddComponent<RectTransform>();
            delRect.anchorMin = new Vector2(1f, 1f);
            delRect.anchorMax = new Vector2(1f, 1f);
            delRect.pivot = new Vector2(1f, 1f);
            delRect.anchoredPosition = new Vector2(-8, -8);
            delRect.sizeDelta = new Vector2(72, 72);
            var delImg = delGo.AddComponent<Image>();
            delImg.color = new Color(0.9f, 0.25f, 0.25f, 1f);
            var deleteBtn = delGo.AddComponent<Button>();
            deleteBtn.targetGraphic = delImg;
            var delColors = deleteBtn.colors;
            delColors.normalColor = Color.white;
            delColors.highlightedColor = new Color(1f, 0.85f, 0.85f, 1f);
            delColors.pressedColor = new Color(1f, 0.6f, 0.6f, 1f);
            deleteBtn.colors = delColors;

            var delTextGo = new GameObject("X");
            delTextGo.transform.SetParent(delGo.transform, false);
            var delTextRect = delTextGo.AddComponent<RectTransform>();
            delTextRect.anchorMin = Vector2.zero;
            delTextRect.anchorMax = Vector2.one;
            delTextRect.offsetMin = Vector2.zero;
            delTextRect.offsetMax = Vector2.zero;
            var delTmp = delTextGo.AddComponent<TextMeshProUGUI>();
            delTmp.text = "×";
            delTmp.fontSize = 56;
            delTmp.fontStyle = FontStyles.Bold;
            delTmp.alignment = TextAlignmentOptions.Center;
            delTmp.color = Color.white;
            delTmp.raycastTarget = false;
            if (font != null) delTmp.font = font;

            var pbv = temp.AddComponent<Artti.UI.ProfileButtonView>();
            pbv.selectButton = selectBtn;
            pbv.deleteButton = deleteBtn;
            pbv.nicknameText = tmp;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, ProfileButtonPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

    }
}
