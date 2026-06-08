using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 프로필 생성 폼: 이름(네이티브 키보드) + 생년/월 드롭다운 + 대표 아바타 선택.
    // 하단 버튼 → (유효성 통과) 생성 확인 팝업 → 확인 시 생성 후 ProfileSelectScene 이동.
    // 미입력 시 유효성 팝업.
    public class ProfileCreateView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Dropdown yearDropdown;
        [SerializeField] private TMP_Dropdown monthDropdown;
        [SerializeField] private RectTransform avatarGrid;
        [SerializeField] private Button createButton;
        [SerializeField] private Button backButton;

        [Header("Confirm Popup (프로필을 생성할까요?)")]
        [SerializeField] private GameObject confirmPopup;
        [SerializeField] private Button confirmYesButton;   // 확인 → 생성
        [SerializeField] private Button confirmNoButton;    // 취소 → 닫기
        [SerializeField] private Button confirmCloseButton; // X → 닫기

        [Header("Validation Popup (입력 정보를 확인해주세요!)")]
        [SerializeField] private GameObject validationPopup;
        [SerializeField] private Button validationOkButton;    // 확인 → 닫기
        [SerializeField] private Button validationCloseButton; // X → 닫기

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text confirmTitleText; // 확인 팝업 제목
        [SerializeField] private string nextScene = "ProfileSelectScene";
        [SerializeField] private string backScene = "SplashScene";
        [SerializeField] private int yearStart = 1980;
        [SerializeField] private int yearEnd = 2026;

        // 수정 모드: ProfileSelect의 수정 버튼이 대상 id를 설정. null이면 신규 생성.
        public static string EditTargetId;
        private ProfileData _editing;

        private AvatarItem[] _avatars;
        private AvatarItem _selected;

        private void Start()
        {
            EnsureBootstrap();
            PopulateYears();
            PopulateMonths();
            SetupAvatars();
            ApplyEditMode(); // 수정 모드면 기존 값 채우기

            if (createButton != null) createButton.onClick.AddListener(OnSubmit);
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(backScene));

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmCreate);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);
            if (confirmCloseButton != null) confirmCloseButton.onClick.AddListener(CloseConfirm);

            if (validationOkButton != null) validationOkButton.onClick.AddListener(CloseValidation);
            if (validationCloseButton != null) validationCloseButton.onClick.AddListener(CloseValidation);

            if (confirmPopup != null) confirmPopup.SetActive(false);
            if (validationPopup != null) validationPopup.SetActive(false);
        }

        // 하단 버튼: 유효성 검사 → 통과면 생성 확인 팝업, 실패면 유효성 팝업
        private void OnSubmit()
        {
            if (!IsValid())
            {
                if (validationPopup != null) validationPopup.SetActive(true);
                return;
            }
            if (confirmPopup != null) confirmPopup.SetActive(true);
        }

        // 이름 / 생년 / 월 중 하나라도 비면 false (드롭다운 index 0 = "선택" = 미선택)
        private bool IsValid()
        {
            if (nameInput == null || string.IsNullOrWhiteSpace(nameInput.text)) return false;
            if (yearDropdown == null || yearDropdown.value == 0) return false;
            if (monthDropdown == null || monthDropdown.value == 0) return false;
            return true;
        }

        private void OnConfirmCreate()
        {
            int year = yearEnd - (yearDropdown.value - 1); // index 1 = yearEnd
            int month = monthDropdown.value;               // index 1 = 1월
            string avatarId = _selected != null ? _selected.avatarId
                : (_avatars.Length > 0 ? _avatars[0].avatarId : "default");
            string nick = nameInput.text.Trim();
            var boot = AppBootstrap.Instance;

            if (_editing != null)
            {
                // 수정: 기존 프로필 갱신 (정보 업데이트 = 마지막 사용일자 갱신)
                _editing.nickname = nick;
                _editing.avatarId = avatarId;
                _editing.birthYear = year;
                _editing.birthMonth = month;
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

        // 수정 모드: 기존 프로필 값으로 폼 채우기
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

            if (yearDropdown != null)
            {
                int idx = yearEnd - _editing.birthYear + 1; // index 1 = yearEnd
                if (idx >= 1 && idx < yearDropdown.options.Count)
                {
                    yearDropdown.value = idx;
                    yearDropdown.RefreshShownValue();
                }
            }
            if (monthDropdown != null && _editing.birthMonth >= 1 && _editing.birthMonth <= 12)
            {
                monthDropdown.value = _editing.birthMonth; // index = month
                monthDropdown.RefreshShownValue();
            }
            if (_avatars != null)
                foreach (var a in _avatars)
                    if (a.avatarId == _editing.avatarId) { SelectAvatar(a); break; }
        }

        private void CloseConfirm() { if (confirmPopup != null) confirmPopup.SetActive(false); }
        private void CloseValidation() { if (validationPopup != null) validationPopup.SetActive(false); }

        private void SetupAvatars()
        {
            if (avatarGrid == null) { _avatars = new AvatarItem[0]; return; }
            _avatars = avatarGrid.GetComponentsInChildren<AvatarItem>(true);
            foreach (var a in _avatars)
            {
                var item = a;
                if (item.button != null) item.button.onClick.AddListener(() => SelectAvatar(item));
            }
            // 사용자가 따로 선택 안 하면 무조건 첫 번째 이미지로 고정
            if (_avatars.Length > 0) SelectAvatar(_avatars[0]);
        }

        private void SelectAvatar(AvatarItem item)
        {
            _selected = item;
            foreach (var a in _avatars)
                if (a.selectedMark != null) a.selectedMark.SetActive(a == item);
        }

        private void PopulateYears()
        {
            if (yearDropdown == null) return;
            yearDropdown.ClearOptions();
            var opts = new List<string> { "선택" };
            for (int y = yearEnd; y >= yearStart; y--) opts.Add(y + "년");
            yearDropdown.AddOptions(opts);
            yearDropdown.value = 0;
            yearDropdown.RefreshShownValue();
        }

        private void PopulateMonths()
        {
            if (monthDropdown == null) return;
            monthDropdown.ClearOptions();
            var opts = new List<string> { "선택" };
            for (int m = 1; m <= 12; m++) opts.Add(m + "월");
            monthDropdown.AddOptions(opts);
            monthDropdown.value = 0;
            monthDropdown.RefreshShownValue();
        }

        // Splash가 진입 씬이면 AppBootstrap이 아직 없을 수 있으므로 보장
        private void EnsureBootstrap()
        {
            if (AppBootstrap.Instance == null)
                new GameObject("[AppBootstrap]").AddComponent<AppBootstrap>();
        }
    }
}
