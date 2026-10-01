using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 사용자 선택 화면: 가로 카드 줄(한 화면 3장. 적으면 가운데 정렬, 넘치면 옆으로 끌어서 넘김) + 맨 앞(왼쪽) "새 사용자" 카드.
    // 카드 탭 = 선택(강조). 다음 = 선택한 사용자로 시작. 프로필 설정 = 선택한 사용자의 프로필 설정 화면(없으면 새로 만들기).
    // 변경점: 사용자 삭제는 프로필 설정 화면으로 옮겼다(카드 삭제 칩과 이 화면의 삭제 팝업은 쓰지 않는다).
    // 변경점: 사용자가 많아 줄이 넘치면(3명 이상 = 카드 4장 이상) 카드가 원통 둘레에 놓인 회전 줄이 된다(CylinderCarousel).
    //   끌면 원통이 돌고, 놓으면 가장 가까운 카드가 앞에 와서 멈추며 그 카드가 선택된다. 옆 카드를 탭하면 앞으로 돌아온다.
    //   옆 카드는 원근으로 작아지고 뒤로 넘어가 흐려졌다가 반대편에서 다시 나온다(끝에서 잘려 숨지 않음).
    //   "새 사용자"도 원통의 한 칸이다(앞에 오면 선택 없음 = 다음 버튼 꺼짐, 탭하면 등록 화면).
    //   선택한 사용자 카드는 위로 떠오른다(ProfileCardView).
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

        [SerializeField] private string createScene = "ProfileCreateScene";
        [SerializeField] private string settingsScene = "ProfileSettingsScene";   // 변경점: 프로필 설정 화면
        [SerializeField] private string backScene = "MainScene";
        [SerializeField] private string proceedScene = "MainScene";

        private AvatarLibrary _avatarLib;
        private readonly List<ProfileCardView> _cards = new List<ProfileCardView>();
        private readonly List<string> _cardIds = new List<string>();
        private string _selectedId;
        private CylinderCarousel _cylinder;   // 줄이 넘칠 때 원통 회전 줄

        private void Start()
        {
            EnsureBootstrap();
            _avatarLib = AvatarLibrary.Load();

            if (addCardButton != null) addCardButton.onClick.AddListener(OpenCreate);
            if (backButton != null) backButton.onClick.AddListener(() => SceneFader.Load(backScene));
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            RaiseDragThreshold();

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
                _cards.Add(card);
                _cardIds.Add(pid);
            }
            if (addCard != null) addCard.SetAsFirstSibling();

            ApplySelection();
            LayoutRow();
        }

        // 카드 탭: 원통이면 그 카드를 앞으로 돌린다(앞에 멈추면 OnFrontChanged가 선택). 아니면 바로 선택.
        private void Select(string id)
        {
            if (_cylinder != null)
            {
                int i = _cardIds.IndexOf(id);
                if (i >= 0) _cylinder.RotateTo(i + 1);   // 0번 칸은 "새 사용자"
                return;
            }
            _selectedId = id;
            ApplySelection();
        }

        // 원통 앞에 멈춘 카드 = 선택. 0번(새 사용자)이 앞이면 선택 없음.
        private void OnFrontChanged(int index)
        {
            _selectedId = index >= 1 && index - 1 < _cardIds.Count ? _cardIds[index - 1] : null;
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
            if (overflow) EnableCylinder(CollectRowCards());
        }

        // 줄 카드(새 사용자 + 사용자 카드) 순서대로
        private List<RectTransform> CollectRowCards()
        {
            var list = new List<RectTransform>();
            if (addCard != null) list.Add(addCard);
            foreach (var c in _cards) if (c != null) list.Add((RectTransform)c.transform);
            return list;
        }

        // 원통 회전 줄로 바꾼다. 앞 카드 = 지금 고른 사용자(없으면 첫 사용자).
        private void EnableCylinder(List<RectTransform> cards)
        {
            int front = _selectedId != null ? _cardIds.IndexOf(_selectedId) + 1 : 1;
            if (front < 1) front = 1;
            var viewport = cardContainer.parent as RectTransform;
            _cylinder = CylinderCarousel.Attach(viewport, cardContainer, cards, front, OnFrontChanged);
        }

        // 에디터 캡처용: 줄의 카드(에디터에서 직접 놓은 프리팹 포함)로 원통을 만들고 extraDegrees만큼 돌린 모양으로 그린다.
        // 실행 중에는 Start -> Refresh -> LayoutRow가 같은 일을 한다.
        public void PreviewCylinder(float extraDegrees)
        {
            if (cardContainer == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainer);
            var cards = new List<RectTransform>();
            for (int i = 0; i < cardContainer.childCount; i++)
                if (cardContainer.GetChild(i) is RectTransform rt && rt.gameObject.activeSelf) cards.Add(rt);
            if (cards.Count <= 3) return;   // 한 화면(3장)에 들어가면 원통을 쓰지 않는다
            EnableCylinder(cards);
            if (_cylinder != null) _cylinder.PreviewAngle(extraDegrees);
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
