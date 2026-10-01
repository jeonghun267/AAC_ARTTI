using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 프로필 등록 2단계(프로필 설정) 폼: 이름(필수) + 생년월일(선택) + 한마디 소개(선택).
    // 1단계(ProfileAvatarStepView)에서 고른 아바타를 SetAvatar로 받아 왼쪽 카드에 보여 준다.
    // 다음 -> 이름이 비면 유효성 팝업, 아니면 저장 후 3단계(완료)를 보인다. 3단계가 없으면 nextScene으로 이동.
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

        [Header("3단계: 완료")]
        [SerializeField] private GameObject doneStep;
        [SerializeField] private TMP_Text doneName;
        [SerializeField] private Image doneAvatar;
        [SerializeField] private TMP_Text doneBirth;
        [SerializeField] private Button doneStartButton;   // 시작하기 -> startScene
        [SerializeField] private Button doneEditButton;    // 편집 배지 -> 1단계부터 다시
        [SerializeField] private Button doneCloseButton;   // X -> backScene
        [SerializeField] private string startScene = "MainScene";

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
        // 변경점: 프로필 설정 화면에서 들어온 수정. 닫기(X)로 돌아갈 씬과, 아바타 말고 다른 항목을 고치러 왔을 때 1단계(아바타)를 건너뛸지.
        //   둘 다 이 씬이 시작할 때 한 번 읽고 지운다. ReturnScene은 1단계 뷰(ProfileAvatarStepView.Awake)도 읽는다.
        public static string ReturnScene;
        public static bool SkipAvatarStep;
        private ProfileData _editing;

        private string _avatarId;
        private Sprite _avatarSprite;
        private System.Threading.CancellationTokenSource _doneCts;
        private Vector2? _doneStartHome;

        public void SetAvatar(string avatarId, Sprite sprite)
        {
            _avatarId = avatarId;
            _avatarSprite = sprite;
            if (previewAvatar != null && sprite != null) previewAvatar.sprite = sprite;
        }

        private void Start()
        {
            EnsureBootstrap();
            PopulateDropdown(yearDropdown, "연도", BuildYears());
            PopulateDropdown(monthDropdown, "월", BuildRange(1, 12, "월"));
            PopulateDropdown(dayDropdown, "일", BuildRange(1, 31, "일"));
            ApplyEditMode(); // 수정 모드면 기존 값 채우기

            // 변경점: 씬 전환은 페이드, 단계(1·3단계 전체 화면 패널)와 팝업은 UIPopupMotion으로 부드럽게 열고 닫는다
            if (createButton != null) createButton.onClick.AddListener(OnSubmit);
            if (backButton != null) backButton.onClick.AddListener(() => SceneFader.Load(backScene));
            if (changeAvatarButton != null && avatarStep != null)
                changeAvatarButton.onClick.AddListener(() => UIPopupMotion.Open(avatarStep));

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmCreate);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CloseConfirm);
            if (confirmCloseButton != null) confirmCloseButton.onClick.AddListener(CloseConfirm);

            if (doneStartButton != null) doneStartButton.onClick.AddListener(() => SceneFader.Load(startScene));
            if (doneCloseButton != null) doneCloseButton.onClick.AddListener(() => SceneFader.Load(backScene));
            if (doneEditButton != null) doneEditButton.onClick.AddListener(EditFromDone);
            if (doneStep != null) doneStep.SetActive(false);

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
                UIPopupMotion.Open(validationPopup);
                return;
            }
            if (confirmPopup != null) UIPopupMotion.Open(confirmPopup);
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
                // 3단계에서 편집으로 돌아와 다시 저장하면 새로 만들지 않고 이 프로필을 고친다
                _editing = profile;
            }

            CloseConfirm();
            if (doneStep != null) ShowDone(nick, year, month, day);
            else SceneFader.Load(nextScene);
        }

        private void ShowDone(string nick, int year, int month, int day)
        {
            if (doneName != null) doneName.text = nick;
            if (doneAvatar != null && _avatarSprite != null) doneAvatar.sprite = _avatarSprite;
            if (doneBirth != null) doneBirth.text = FormatBirth(year, month, day);
            UIPopupMotion.Open(doneStep);
            PlayDoneEntrance();
        }

        // 변경점: 3단계(완료) 등장 — 화면이 스며 나온 뒤 원 안 아바타가 톡 커지고, 이름과 시작하기 버튼이 뒤따른다.
        private void PlayDoneEntrance()
        {
            var ct = UIFx.Restart(ref _doneCts, this);
            if (doneAvatar != null && doneAvatar.rectTransform.parent is RectTransform avatarCircle)
                UIFx.BounceAround(avatarCircle, avatarCircle.anchoredPosition, avatarCircle.pivot, 0.12f, 0.45f, 0.6f, ct).Forget();
            if (doneName != null)
                UIFx.FadeGroup(UIFx.Group(doneName), 0f, 1f, 0.3f, 0.3f, ct).Forget();
            if (doneStartButton != null && doneStartButton.transform is RectTransform start)
            {
                if (_doneStartHome == null) _doneStartHome = start.anchoredPosition;   // 다시 열 때 움직이던 자리를 집으로 잡지 않게
                UIFx.MoveFadeIn(start, _doneStartHome.Value, new Vector2(0f, -30f), 0.4f, 0.4f, ct).Forget();
            }
        }

        // 3단계 편집 배지: 완료 화면을 닫고 1단계(아바타)부터 다시. 입력한 값은 2단계에 그대로 남아 있다.
        private void EditFromDone()
        {
            UIPopupMotion.Close(doneStep);
            UIPopupMotion.Open(avatarStep);
        }

        // 입력한 만큼만 보인다: 2001. 05. 20 / 2001. 05 / 2001년 / 05. 20 / 미입력
        // 변경점: 프로필 설정 화면(ProfileSettingsView)도 같은 표기를 쓰도록 internal
        internal static string FormatBirth(int year, int month, int day)
        {
            if (year > 0 && month > 0 && day > 0) return $"{year}. {month:D2}. {day:D2}";
            if (year > 0 && month > 0) return $"{year}. {month:D2}";
            if (year > 0) return $"{year}년";
            if (month > 0 && day > 0) return $"{month:D2}. {day:D2}";
            if (month > 0) return $"{month}월";
            return "생년월일 미입력";
        }

        // 수정 모드: 기존 프로필 값으로 폼 채우기 (아바타는 1단계 뷰가 Awake에서 미리 고른다)
        private void ApplyEditMode()
        {
            // 변경점: 프로필 설정 화면에서 온 수정이면 닫기를 그 화면으로 돌리고, 필요하면 1단계를 닫아 둔다(1회 소비)
            string returnScene = ReturnScene;
            bool skipAvatar = SkipAvatarStep;
            ReturnScene = null;
            SkipAvatarStep = false;

            if (string.IsNullOrEmpty(EditTargetId)) return;
            if (!string.IsNullOrEmpty(returnScene)) backScene = returnScene;
            if (skipAvatar && avatarStep != null) avatarStep.SetActive(false);
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

            // 변경점: 1단계를 건너뛰면 SetAvatar가 불리지 않아 왼쪽 카드/완료 화면에 기본 아바타가 남는다. 지금 아바타로 채워 둔다
            //   (1단계를 거치면 다음 버튼이 고른 아바타로 다시 덮는다).
            if (skipAvatar)
            {
                var lib = AvatarLibrary.Load();
                var sprite = lib != null ? lib.GetById(_editing.avatarId) : null;
                if (sprite != null) SetAvatar(_editing.avatarId, sprite);
            }
        }

        private static void SetDropdown(TMP_Dropdown dd, int index)
        {
            if (dd == null || index < 1 || index >= dd.options.Count) return;
            dd.value = index;
            dd.RefreshShownValue();
        }

        private void CloseConfirm() => UIPopupMotion.Close(confirmPopup);
        private void CloseValidation() => UIPopupMotion.Close(validationPopup);

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
