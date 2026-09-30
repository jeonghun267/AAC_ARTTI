using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 프로필 등록 2단계(프로필 설정) 폼: 이름(필수) + 생년월일(선택) + 한마디 소개(선택).
    // 1단계(ProfileAvatarStepView)에서 고른 아바타를 SetAvatar로 받아 왼쪽 카드에 보여 준다.
    // 다음 -> 이름이 비면 유효성 팝업, 아니면 저장 후 nextScene으로 이동.
    public class ProfileCreateView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Dropdown yearDropdown;
        [SerializeField] private TMP_Dropdown monthDropdown;
        [SerializeField] private TMP_Dropdown dayDropdown;
        [SerializeField] private TMP_InputField introInput;
        [SerializeField] private Button createButton;          // 다음
        [SerializeField] private Button backButton;            // 닫기(X)

        [Header("1단계에서 고른 아바타")]
        [SerializeField] private Image previewAvatar;          // 왼쪽 카드
        [SerializeField] private Button changeAvatarButton;    // 왼쪽 카드 탭 -> 1단계로 돌아가 다시 고르기
        [SerializeField] private GameObject avatarStep;        // 1단계 패널

        [Header("Confirm Popup (없으면 확인 없이 바로 저장)")]
        [SerializeField] private GameObject confirmPopup;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;
        [SerializeField] private Button confirmCloseButton;

        [Header("Validation Popup (이름을 입력해 주세요)")]
        [SerializeField] private GameObject validationPopup;
        [SerializeField] private Button validationOkButton;
        [SerializeField] private Button validationCloseButton;

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text confirmTitleText;
        [SerializeField] private string nextScene = "ProfileSelectScene";
        [SerializeField] private string backScene = "ProfileSelectScene";
        [SerializeField] private int yearStart = 1940;
        [SerializeField] private int yearEnd = 2026;

        // 수정 모드: ProfileSelect의 수정 버튼이 대상 id를 설정. null이면 신규 생성.
        public static string EditTargetId;
        private ProfileData _editing;

        private string _avatarId;

        public void SetAvatar(string avatarId, Sprite sprite)
        {
            _avatarId = avatarId;
            if (previewAvatar != null && sprite != null) previewAvatar.sprite = sprite;
        }

        private void Start()
        {
            EnsureBootstrap();
            PopulateDropdown(yearDropdown, "연도", BuildYears());
            PopulateDropdown(monthDropdown, "월", BuildRange(1, 12, "월"));
            PopulateDropdown(dayDropdown, "일", BuildRange(1, 31, "일"));
            ApplyEditMode(); // 수정 모드면 기존 값 채우기

            if (createButton != null) createButton.onClick.AddListener(OnSubmit);
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(backScene));
            if (changeAvatarButton != null && avatarStep != null)
                changeAvatarButton.onClick.AddListener(() => avatarStep.SetActive(true));

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmCreate);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);
            if (confirmCloseButton != null) confirmCloseButton.onClick.AddListener(CloseConfirm);

            if (validationOkButton != null) validationOkButton.onClick.AddListener(CloseValidation);
            if (validationCloseButton != null) validationCloseButton.onClick.AddListener(CloseValidation);

            if (confirmPopup != null) confirmPopup.SetActive(false);
            if (validationPopup != null) validationPopup.SetActive(false);
        }

        // 다음: 이름이 비면 유효성 팝업, 아니면 확인 팝업(있을 때) 또는 바로 저장
        private void OnSubmit()
        {
            if (nameInput == null || string.IsNullOrWhiteSpace(nameInput.text))
            {
                if (validationPopup != null) validationPopup.SetActive(true);
                return;
            }
            if (confirmPopup != null) confirmPopup.SetActive(true);
            else OnConfirmCreate();
        }

        private void OnConfirmCreate()
        {
            // 드롭다운 index 0은 "연도/월/일" 안내 문구 = 입력 안 함 -> 0으로 저장
            int year = yearDropdown != null && yearDropdown.value > 0 ? yearEnd - (yearDropdown.value - 1) : 0;
            int month = monthDropdown != null ? monthDropdown.value : 0;
            int day = dayDropdown != null ? dayDropdown.value : 0;
            string intro = introInput != null ? introInput.text.Trim() : "";
            string avatarId = !string.IsNullOrEmpty(_avatarId) ? _avatarId
                : (_editing != null ? _editing.avatarId : "default");
            string nick = nameInput.text.Trim();
            var boot = AppBootstrap.Instance;

            if (_editing != null)
            {
                // 수정: 기존 프로필 갱신 (정보 업데이트 = 마지막 사용일자 갱신)
                _editing.nickname = nick;
                _editing.avatarId = avatarId;
                _editing.birthYear = year;
                _editing.birthMonth = month;
                _editing.birthDay = day;
                _editing.intro = intro;
                _editing.lastUsedAtTicks = System.DateTime.Now.Ticks;
                if (boot != null) boot.ProfileManager.Save();
            }
            else
            {
                var profile = new ProfileData
                {
                    nickname = nick,
                    avatarId = avatarId,
                    colorHex = "#FFFFFF",
                    birthYear = year,
                    birthMonth = month,
                    birthDay = day,
                    intro = intro,
                    lastUsedAtTicks = System.DateTime.Now.Ticks
                };
                if (boot != null)
                {
                    boot.ProfileManager.AddProfile(profile);
                    boot.SwitchProfile(profile.id);
                }
            }

            CloseConfirm();
            SceneManager.LoadScene(nextScene);
        }

        // 수정 모드: 기존 프로필 값으로 폼 채우기 (아바타는 1단계 뷰가 Awake에서 미리 고른다)
        private void ApplyEditMode()
        {
            if (string.IsNullOrEmpty(EditTargetId)) return;
            var pm = AppBootstrap.Instance != null ? AppBootstrap.Instance.ProfileManager : null;
            _editing = pm != null ? pm.Profiles.Find(p => p.id == EditTargetId) : null;
            EditTargetId = null; // 1회 소비
            if (_editing == null) return;

            if (titleText != null) titleText.text = "프로필 수정";
            if (confirmTitleText != null) confirmTitleText.text = "프로필을 수정할까요?";
            if (nameInput != null) nameInput.text = _editing.nickname;
            if (introInput != null) introInput.text = _editing.intro ?? "";
            if (_editing.birthYear > 0) SetDropdown(yearDropdown, yearEnd - _editing.birthYear + 1); // index 1 = yearEnd
            SetDropdown(monthDropdown, _editing.birthMonth);
            SetDropdown(dayDropdown, _editing.birthDay);
        }

        private static void SetDropdown(TMP_Dropdown dd, int index)
        {
            if (dd == null || index < 1 || index >= dd.options.Count) return;
            dd.value = index;
            dd.RefreshShownValue();
        }

        private void CloseConfirm() { if (confirmPopup != null) confirmPopup.SetActive(false); }
        private void CloseValidation() { if (validationPopup != null) validationPopup.SetActive(false); }

        private List<string> BuildYears()
        {
            var list = new List<string>();
            for (int y = yearEnd; y >= yearStart; y--) list.Add(y + "년");
            return list;
        }

        private static List<string> BuildRange(int from, int to, string suffix)
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(i + suffix);
            return list;
        }

        // index 0은 안내 문구("연도"/"월"/"일"), 1부터 실제 값
        private static void PopulateDropdown(TMP_Dropdown dd, string placeholder, List<string> values)
        {
            if (dd == null) return;
            dd.ClearOptions();
            var opts = new List<string> { placeholder };
            opts.AddRange(values);
            dd.AddOptions(opts);
            dd.value = 0;
            dd.RefreshShownValue();
        }

        // Splash가 진입 씬이면 AppBootstrap이 아직 없을 수 있으므로 보장
        private void EnsureBootstrap()
        {
            if (AppBootstrap.Instance == null)
                new GameObject("[AppBootstrap]").AddComponent<AppBootstrap>();
        }
    }
}
