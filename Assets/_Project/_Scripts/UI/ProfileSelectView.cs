using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 사용자 선택 화면: 가로 카드 줄(한 번에 visibleCount장, < > 로 넘김) + 맨 끝 "새 사용자" 카드.
    // 카드 탭 = 선택(강조). 다음 = 선택한 사용자로 시작. 프로필 설정 = 선택한 사용자 수정(없으면 새로 만들기).
    public class ProfileSelectView : MonoBehaviour
    {
        [Header("카드 줄")]
        [SerializeField] private RectTransform cardContainer; // 가로 레이아웃 Content (뷰포트 마스크 안)
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private RectTransform addCard;       // "새 사용자" 카드. 항상 줄 맨 끝.
        [SerializeField] private Button addCardButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private int visibleCount = 5;
        [SerializeField] private float cardPitch = 190f;      // 카드 폭 + 간격

        [Header("버튼")]
        [SerializeField] private Button nextButton;           // 다음 -> proceedScene
        [SerializeField] private Button settingsButton;       // 프로필 설정 -> 선택한 사용자 수정
        [SerializeField] private Button backButton;           // X

        [Header("Delete Confirm Popup (가운데 모달)")]
        [SerializeField] private GameObject deletePopup;
        [SerializeField] private TMP_Text deleteMessageText;
        [SerializeField] private TMP_InputField deleteInput;
        [SerializeField] private Button deleteConfirmButton; // 삭제
        [SerializeField] private Button deleteCancelButton;  // 취소
        [SerializeField] private Button deleteCloseButton;   // X

        [SerializeField] private string createScene = "ProfileCreateScene";
        [SerializeField] private string backScene = "MainScene";
        [SerializeField] private string proceedScene = "MainScene";

        private const string ConfirmWord = "동의한다";
        private const string DeletePrompt = "정말 삭제할까요?\n동의하시면 아래에 '동의한다'를 입력하세요.";
        private const string DeleteHint = "'동의한다'를 정확히 입력해야 삭제됩니다.";

        private AvatarLibrary _avatarLib;
        private readonly List<ProfileCardView> _cards = new List<ProfileCardView>();
        private readonly List<string> _cardIds = new List<string>();
        private string _selectedId;
        private string _pendingDeleteId;
        private int _firstVisible;

        private void Start()
        {
            EnsureBootstrap();
            _avatarLib = AvatarLibrary.Load();

            if (addCardButton != null) addCardButton.onClick.AddListener(OpenCreate);
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(backScene));
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (prevButton != null) prevButton.onClick.AddListener(() => Scroll(-1));
            if (nextPageButton != null) nextPageButton.onClick.AddListener(() => Scroll(1));

            if (deleteConfirmButton != null) deleteConfirmButton.onClick.AddListener(OnDeleteConfirm);
            if (deleteCancelButton != null) deleteCancelButton.onClick.AddListener(CloseDelete);
            if (deleteCloseButton != null) deleteCloseButton.onClick.AddListener(CloseDelete);
            if (deletePopup != null) deletePopup.SetActive(false);

            var active = ProfileManagerOrNull()?.ActiveProfile;
            _selectedId = active != null ? active.id : null;
            Refresh();
        }

        public void Refresh()
        {
            var pm = ProfileManagerOrNull();
            var profiles = pm != null ? pm.Profiles : new List<ProfileData>();
            var active = pm != null ? pm.ActiveProfile : null;

            if (cardContainer == null || cardPrefab == null) return;
            foreach (var c in _cards) if (c != null) Destroy(c.gameObject);
            _cards.Clear();
            _cardIds.Clear();

            // 지금 쓰는 사용자 맨 앞 -> 그 다음 최근 사용 순
            var sorted = profiles
                .OrderByDescending(p => active != null && p.id == active.id)
                .ThenByDescending(p => p.lastUsedAtTicks)
                .ToList();

            // 고른 사용자가 없어졌으면 맨 앞 사용자를 고른다
            if (_selectedId == null || !sorted.Exists(p => p.id == _selectedId))
                _selectedId = sorted.Count > 0 ? sorted[0].id : null;

            foreach (var profile in sorted)
            {
                var go = Instantiate(cardPrefab, cardContainer);
                go.SetActive(true);
                var card = go.GetComponent<ProfileCardView>();
                if (card == null) continue;

                if (card.nameText != null) card.nameText.text = profile.nickname;
                if (card.introText != null)
                    card.introText.text = !string.IsNullOrWhiteSpace(profile.intro)
                        ? profile.intro
                        : "마지막 사용: " + FormatLastUsed(profile.lastUsedAtTicks);
                if (card.avatarImage != null && _avatarLib != null)
                {
                    var sp = _avatarLib.GetById(profile.avatarId);
                    if (sp != null) card.avatarImage.sprite = sp;
                }

                var pid = profile.id;
                if (card.selectButton != null) card.selectButton.onClick.AddListener(() => Select(pid));
                if (card.deleteButton != null) card.deleteButton.onClick.AddListener(() => OnDelete(pid));
                _cards.Add(card);
                _cardIds.Add(pid);
            }
            if (addCard != null) addCard.SetAsLastSibling();

            ApplySelection();
            _firstVisible = Mathf.Clamp(_firstVisible, 0, MaxFirstVisible());
            ApplyScroll();
        }

        private void Select(string id)
        {
            _selectedId = id;
            ApplySelection();
        }

        private void ApplySelection()
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetSelected(_cardIds[i] == _selectedId);
            if (nextButton != null) nextButton.interactable = _selectedId != null;
        }

        // 한 칸씩 넘긴다. 카드 수(+새 사용자 카드)가 visibleCount 이하면 화살표를 숨긴다.
        private void Scroll(int dir)
        {
            _firstVisible = Mathf.Clamp(_firstVisible + dir, 0, MaxFirstVisible());
            ApplyScroll();
        }

        private int MaxFirstVisible() => Mathf.Max(0, _cards.Count + 1 - visibleCount);

        // 카드가 한 화면보다 적으면 가운데 정렬, 많으면 _firstVisible만큼 왼쪽으로 민다.
        private void ApplyScroll()
        {
            int items = _cards.Count + 1;   // + 새 사용자 카드
            var p = cardContainer.anchoredPosition;
            p.x = items < visibleCount ? (visibleCount - items) * cardPitch * 0.5f : -_firstVisible * cardPitch;
            cardContainer.anchoredPosition = p;
            bool paged = MaxFirstVisible() > 0;
            if (prevButton != null) prevButton.gameObject.SetActive(paged && _firstVisible > 0);
            if (nextPageButton != null) nextPageButton.gameObject.SetActive(paged && _firstVisible < MaxFirstVisible());
        }

        private void OnNext()
        {
            if (_selectedId == null) return;
            var boot = AppBootstrap.Instance;
            if (boot != null)
            {
                boot.SwitchProfile(_selectedId);
                boot.ProfileManager.UpdateLastUsed(_selectedId);
            }
            if (!string.IsNullOrEmpty(proceedScene)) SceneManager.LoadScene(proceedScene);
        }

        // 프로필 설정: 고른 사용자를 수정 모드로 연다. 사용자가 없으면 새로 만들기.
        private void OnSettings()
        {
            if (_selectedId == null) { OpenCreate(); return; }
            ProfileCreateView.EditTargetId = _selectedId;
            SceneManager.LoadScene(createScene);
        }

        private void OpenCreate()
        {
            ProfileCreateView.EditTargetId = null;
            SceneManager.LoadScene(createScene);
        }

        // 삭제 칩 -> 가운데 모달 (타이핑 확인)
        private void OnDelete(string id)
        {
            _pendingDeleteId = id;
            if (deleteInput != null) deleteInput.text = "";
            if (deleteMessageText != null) deleteMessageText.text = DeletePrompt;
            if (deletePopup != null) deletePopup.SetActive(true);
        }

        // "동의한다" 정확히 입력해야 삭제
        private void OnDeleteConfirm()
        {
            var typed = deleteInput != null ? deleteInput.text.Trim() : "";
            if (typed != ConfirmWord)
            {
                if (deleteMessageText != null) deleteMessageText.text = DeleteHint;
                return;
            }

            var boot = AppBootstrap.Instance;
            if (boot != null && !string.IsNullOrEmpty(_pendingDeleteId))
                boot.ProfileManager.DeleteProfile(_pendingDeleteId);
            if (_pendingDeleteId == _selectedId) _selectedId = null;

            CloseDelete();
            Refresh();
        }

        private void CloseDelete()
        {
            _pendingDeleteId = null;
            if (deletePopup != null) deletePopup.SetActive(false);
        }

        private static ProfileManager ProfileManagerOrNull() =>
            AppBootstrap.Instance != null ? AppBootstrap.Instance.ProfileManager : null;

        private static string FormatLastUsed(long ticks)
        {
            if (ticks == 0) return "기록 없음";
            var dt = new DateTime(ticks);
            int days = (DateTime.Now.Date - dt.Date).Days;
            if (days <= 0) return "오늘";
            if (days == 1) return "어제";
            if (days < 7) return $"{days}일 전";
            return dt.ToString("yyyy.MM.dd");
        }

        private void EnsureBootstrap()
        {
            if (AppBootstrap.Instance == null)
                new GameObject("[AppBootstrap]").AddComponent<AppBootstrap>();
        }
    }
}
