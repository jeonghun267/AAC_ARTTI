using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 사용자 선택 화면: 가로 카드 줄(한 화면 3장. 적으면 가운데 정렬, 넘치면 옆으로 끌어서 넘김) + 맨 앞(왼쪽) "새 사용자" 카드.
    // 카드 탭 = 선택(강조). 다음 = 선택한 사용자로 시작. 프로필 설정 = 선택한 사용자 수정(없으면 새로 만들기).
    public class ProfileSelectView : MonoBehaviour
    {
        [Header("카드 줄")]
        [SerializeField] private RectTransform cardContainer; // 가로 레이아웃 Content (뷰포트 마스크 안)
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private RectTransform addCard;       // "새 사용자" 카드. 항상 줄 맨 앞(왼쪽 첫 칸).
        [SerializeField] private Button addCardButton;
        [SerializeField] private ScrollRect cardScroll;       // 카드 줄을 옆으로 끌어서 넘긴다

        [Header("버튼")]
        [SerializeField] private Button nextButton;           // 다음 -> proceedScene
        [SerializeField] private Button settingsButton;       // 프로필 설정 -> 선택한 사용자 수정
        [SerializeField] private Button backButton;           // X

        [Header("Delete Confirm Popup (가운데 모달, 52 판에 제목/안내/입력칸이 그려져 있음)")]
        [SerializeField] private GameObject deletePopup;
        [SerializeField] private TMP_InputField deleteInput;
        [SerializeField] private GameObject deleteFieldCover; // 52 입력칸 속 '동의한다' 안내 글자를 가리는 조각
        [SerializeField] private Button deleteConfirmButton; // 삭제
        [SerializeField] private Button deleteCancelButton;  // 취소
        [SerializeField] private Button deleteCloseButton;   // X

        [SerializeField] private string createScene = "ProfileCreateScene";
        [SerializeField] private string settingsScene = "ProfileSettingsScene";   // 변경점: 프로필 설정 화면
        [SerializeField] private string backScene = "MainScene";
        [SerializeField] private string proceedScene = "MainScene";

        private const string ConfirmWord = "동의한다";
        private const string DeleteHint = "'동의한다'를 정확히 입력해 주세요";

        private AvatarLibrary _avatarLib;
        private readonly List<ProfileCardView> _cards = new List<ProfileCardView>();
        private readonly List<string> _cardIds = new List<string>();
        private string _selectedId;
        private string _pendingDeleteId;
        private bool _deleteHintShown;

        private void Start()
        {
            EnsureBootstrap();
            _avatarLib = AvatarLibrary.Load();

            if (addCardButton != null) addCardButton.onClick.AddListener(OpenCreate);
            if (backButton != null) backButton.onClick.AddListener(() => SceneFader.Load(backScene));
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            RaiseDragThreshold();

            if (deleteConfirmButton != null) deleteConfirmButton.onClick.AddListener(OnDeleteConfirm);
            if (deleteCancelButton != null) deleteCancelButton.onClick.AddListener(CloseDelete);
            if (deleteCloseButton != null) deleteCloseButton.onClick.AddListener(CloseDelete);
            if (deleteInput != null) deleteInput.onValueChanged.AddListener(_ => UpdateFieldCover());
            if (deletePopup != null) deletePopup.SetActive(false);

            var active = ProfileManagerOrNull()?.ActiveProfile;
            _selectedId = active != null ? active.id : null;
            Refresh();
            PlayEntrance();
        }

        // 변경점: 화면 등장 — 카드 줄이 왼쪽("새 사용자")부터 차례로 톡 나타난다(처음 한 번만).
        private void PlayEntrance()
        {
            const float start = 0.12f, step = 0.07f;
            var ct = this.GetCancellationTokenOnDestroy();
            if (addCard != null) UIFx.PopScaleIn(addCard, start, 0.34f, 0.85f, ct).Forget();
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].PlayIntro(start + Mathf.Min(i + 1, 5) * step, ct);
        }

        public void Refresh()
        {
            var pm = ProfileManagerOrNull();
            var profiles = pm != null ? pm.Profiles : new List<ProfileData>();
            var active = pm != null ? pm.ActiveProfile : null;

            if (cardContainer == null || cardPrefab == null) return;
            // Destroy는 프레임 끝에 지워져, 바로 아래 줄 폭 계산(LayoutRow)에 남지 않도록 먼저 끈다
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.gameObject.SetActive(false);
                Destroy(c.gameObject);
            }
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
            if (addCard != null) addCard.SetAsFirstSibling();

            ApplySelection();
            LayoutRow();
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

        // 카드가 한 화면(3장)보다 적으면 가운데 정렬하고 끌기를 끈다. 새 사용자 카드가 가운데에서 시작해 카드가 늘수록
        // 왼쪽으로 간다. 넘치면 맨 왼쪽(새 사용자 카드)부터 보이고 옆으로 끌어서 넘긴다.
        // 콘텐츠는 뷰포트 왼쪽 기준(pivot x 0)이라 x가 곧 왼쪽 여백.
        private void LayoutRow()
        {
            if (cardScroll != null) cardScroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
            var viewport = cardContainer.parent as RectTransform;
            float viewW = viewport != null ? viewport.rect.width : 0f;
            float contentW = cardContainer.rect.width;
            bool overflow = contentW > viewW + 1f;
            if (cardScroll != null) cardScroll.horizontal = overflow;
            var p = cardContainer.anchoredPosition;
            p.x = overflow ? 0f : (viewW - contentW) * 0.5f;
            cardContainer.anchoredPosition = p;
        }

        // 태블릿은 dpi가 높아 기본 10px이면 살짝 흔들린 탭도 드래그로 잡혀 카드 선택이 안 된다. 약 2mm로 올린다.
        private static void RaiseDragThreshold()
        {
            var es = EventSystem.current;
            if (es != null && Screen.dpi > 0f)
                es.pixelDragThreshold = Mathf.Max(es.pixelDragThreshold, Mathf.RoundToInt(Screen.dpi * 0.08f));
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
            if (!string.IsNullOrEmpty(proceedScene)) SceneFader.Load(proceedScene);   // 변경점: 페이드 전환
        }

        // 프로필 설정: 고른 사용자의 프로필 설정 화면으로. 사용자가 없으면 새로 만들기.
        // 변경점: 바로 수정 화면(ProfileCreateScene)으로 가던 것을 프로필 설정 화면(ProfileSettingsScene)으로 바꿈
        private void OnSettings()
        {
            if (_selectedId == null) { OpenCreate(); return; }
            ProfileSettingsView.TargetId = _selectedId;
            SceneFader.Load(settingsScene);
        }

        private void OpenCreate()
        {
            ProfileCreateView.EditTargetId = null;
            SceneFader.Load(createScene);
        }

        // 삭제 칩 -> 가운데 모달 (타이핑 확인)
        private void OnDelete(string id)
        {
            _pendingDeleteId = id;
            if (deleteInput != null) deleteInput.text = "";
            SetDeleteHint(false);
            UIPopupMotion.Open(deletePopup);   // 변경점: 팝업 열기/닫기 연출
        }

        // "동의한다" 정확히 입력해야 삭제
        private void OnDeleteConfirm()
        {
            var typed = deleteInput != null ? deleteInput.text.Trim() : "";
            if (typed != ConfirmWord)
            {
                // 칸을 비우고 칸 안에 안내를 띄운다(다시 치기 시작하면 안내는 사라진다)
                if (deleteInput != null) deleteInput.text = "";
                SetDeleteHint(true);
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
            UIPopupMotion.Close(deletePopup);
        }

        // 틀렸다는 안내는 입력칸의 placeholder로 띄운다(칸이 비어 있을 때만 보임).
        private void SetDeleteHint(bool on)
        {
            _deleteHintShown = on;
            if (deleteInput != null && deleteInput.placeholder is TMP_Text ph) ph.text = on ? DeleteHint : "";
            UpdateFieldCover();
        }

        // 52에 그려진 '동의한다' 안내 글자는 글자를 치는 중이거나 틀렸다는 안내를 띄울 때 가린다.
        private void UpdateFieldCover()
        {
            if (deleteFieldCover == null) return;
            bool typing = deleteInput != null && !string.IsNullOrEmpty(deleteInput.text);
            deleteFieldCover.SetActive(typing || _deleteHintShown);
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
