using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Artti.Common;

namespace Artti.UI
{
    // 사용자 선택 화면: 카드 목록(선택 카드 맨 위 + 강조), 빈 상태, 새로 등록하기.
    public class ProfileSelectView : MonoBehaviour
    {
        [SerializeField] private RectTransform cardContainer; // 스크롤 Content (VerticalLayout)
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private GameObject cardScroll;       // 카드 영역 루트 (목록 있을 때 표시)
        [SerializeField] private GameObject emptyState;       // 0명일 때 표시
        [SerializeField] private Button addButton;            // + 새로 등록하기
        [SerializeField] private Button backButton;

        [SerializeField] private string createScene = "ProfileCreateScene";
        [SerializeField] private string backScene = "SplashScene";
        // 비어있으면 카드 탭 시 선택만(강조+맨위). "MainScene" 등을 넣으면 탭 시 해당 씬으로 이동.
        [SerializeField] private string proceedScene = "";

        private AvatarLibrary _avatarLib;

        private void Start()
        {
            EnsureBootstrap();
            _avatarLib = AvatarLibrary.Load();

            if (addButton != null) addButton.onClick.AddListener(() => SceneManager.LoadScene(createScene));
            if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene(backScene));

            Refresh();
        }

        public void Refresh()
        {
            var pm = AppBootstrap.Instance != null ? AppBootstrap.Instance.ProfileManager : null;
            var profiles = pm != null ? pm.Profiles : new System.Collections.Generic.List<ProfileData>();
            var active = pm != null ? pm.ActiveProfile : null;

            bool empty = profiles.Count == 0;
            if (emptyState != null) emptyState.SetActive(empty);
            if (cardScroll != null) cardScroll.SetActive(!empty);

            if (cardContainer == null || cardPrefab == null) return;
            foreach (Transform child in cardContainer) Destroy(child.gameObject);
            if (empty) return;

            // 선택(활성) 카드 맨 위 → 그 다음 최근 사용 순
            var sorted = profiles
                .OrderByDescending(p => active != null && p.id == active.id)
                .ThenByDescending(p => p.lastUsedAtTicks)
                .ToList();

            foreach (var profile in sorted)
            {
                var go = Instantiate(cardPrefab, cardContainer);
                go.SetActive(true);
                var card = go.GetComponent<ProfileCardView>();
                if (card == null) continue;

                if (card.nameText != null) card.nameText.text = profile.nickname;
                if (card.dateText != null) card.dateText.text = "마지막 사용: " + FormatLastUsed(profile.lastUsedAtTicks);
                if (card.avatarImage != null && _avatarLib != null)
                {
                    var sp = _avatarLib.GetById(profile.avatarId);
                    if (sp != null) card.avatarImage.sprite = sp;
                }
                card.SetSelected(active != null && profile.id == active.id);

                var pid = profile.id;
                if (card.selectButton != null)
                {
                    card.selectButton.onClick.RemoveAllListeners();
                    card.selectButton.onClick.AddListener(() => OnSelect(pid));
                }
                if (card.editButton != null)
                {
                    card.editButton.onClick.RemoveAllListeners();
                    card.editButton.onClick.AddListener(() => OnEdit(pid));
                }
                if (card.reportButton != null)
                {
                    card.reportButton.onClick.RemoveAllListeners();
                    card.reportButton.onClick.AddListener(() => OnReport(pid));
                }
            }
        }

        private void OnSelect(string id)
        {
            var boot = AppBootstrap.Instance;
            if (boot != null)
            {
                boot.SwitchProfile(id);
                boot.ProfileManager.UpdateLastUsed(id);
            }
            if (!string.IsNullOrEmpty(proceedScene)) SceneManager.LoadScene(proceedScene);
            else Refresh(); // 선택만: 강조 + 맨 위 재정렬
        }

        private void OnEdit(string id)
        {
            ProfileCreateView.EditTargetId = id; // 수정 모드로 ProfileCreate 진입
            SceneManager.LoadScene(createScene);
        }

        private void OnReport(string id)
        {
            Debug.Log($"[ProfileSelectView] 리포트 클릭 (id={id}) — 전환 대상 미설정 (리포트 화면 연결 예정)");
        }

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
