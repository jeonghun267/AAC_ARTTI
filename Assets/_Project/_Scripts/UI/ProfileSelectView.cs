using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Artti.Common;
using System.Collections.Generic;
using TMPro;

namespace Artti.UI
{
    public class ProfileSelectView : MonoBehaviour
    {
        [SerializeField] private RectTransform avatarGrid;
        [SerializeField] private GameObject profileButtonPrefab;
        [SerializeField] private GameObject teacherModeModal;
        [SerializeField] private TMP_InputField nicknameInput;

        [Header("Delete Confirm")]
        [SerializeField] private GameObject deleteConfirmModal;
        [SerializeField] private TMP_Text deleteConfirmText;
        [SerializeField] private Button confirmDeleteButton;
        [SerializeField] private Button cancelDeleteButton;

        private string _pendingDeleteId;

        private void Start()
        {
            if (confirmDeleteButton != null) confirmDeleteButton.onClick.AddListener(ConfirmDelete);
            if (cancelDeleteButton != null) cancelDeleteButton.onClick.AddListener(HideDeleteConfirm);

            RefreshProfiles();

            if (AppBootstrap.Instance.ProfileManager.Profiles.Count == 0)
            {
                teacherModeModal.SetActive(true);
            }
        }

        public void RefreshProfiles()
        {
            foreach (Transform child in avatarGrid)
            {
                Destroy(child.gameObject);
            }

            var profiles = AppBootstrap.Instance.ProfileManager.Profiles;
            foreach (var profile in profiles)
            {
                var go = Instantiate(profileButtonPrefab, avatarGrid);
                var view = go.GetComponent<ProfileButtonView>();
                if (view == null)
                {
                    Debug.LogError("[ProfileSelectView] ProfileButton prefab missing ProfileButtonView component");
                    continue;
                }

                if (view.nicknameText != null) view.nicknameText.text = profile.nickname;

                var pid = profile.id;
                var pname = profile.nickname;
                if (view.selectButton != null)
                {
                    view.selectButton.onClick.RemoveAllListeners();
                    view.selectButton.onClick.AddListener(() => OnProfileSelected(pid));
                }
                if (view.deleteButton != null)
                {
                    view.deleteButton.onClick.RemoveAllListeners();
                    view.deleteButton.onClick.AddListener(() => ShowDeleteConfirm(pid, pname));
                }
            }
        }

        private void OnProfileSelected(string id)
        {
            AppBootstrap.Instance.SwitchProfile(id);
            SceneManager.LoadScene("MainScene");
        }

        public void OnCreateProfile(string nickname, string avatarId)
        {
            var newProfile = new ProfileData
            {
                nickname = nickname,
                avatarId = avatarId,
                colorHex = "#FFFFFF"
            };
            AppBootstrap.Instance.ProfileManager.AddProfile(newProfile);
            RefreshProfiles();
            teacherModeModal.SetActive(false);
        }

        public void CreateProfileFromInput()
        {
            var name = nicknameInput != null ? nicknameInput.text : "";
            if (string.IsNullOrWhiteSpace(name)) name = "사용자";
            OnCreateProfile(name, "default");
            if (nicknameInput != null) nicknameInput.text = "";
        }

        // "+ 프로필 추가" 버튼이 호출
        public void ShowCreateProfileModal()
        {
            if (teacherModeModal != null) teacherModeModal.SetActive(true);
        }

        // 모달 닫기 (취소)
        public void HideCreateProfileModal()
        {
            if (teacherModeModal != null) teacherModeModal.SetActive(false);
        }

        private void ShowDeleteConfirm(string id, string nickname)
        {
            _pendingDeleteId = id;
            if (deleteConfirmText != null)
                deleteConfirmText.text = $"'{nickname}' 프로필을\n삭제할까요?";
            if (deleteConfirmModal != null)
                deleteConfirmModal.SetActive(true);
        }

        public void ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(_pendingDeleteId))
            {
                AppBootstrap.Instance.ProfileManager.DeleteProfile(_pendingDeleteId);
                _pendingDeleteId = null;
                RefreshProfiles();
            }
            HideDeleteConfirm();
        }

        public void HideDeleteConfirm()
        {
            if (deleteConfirmModal != null) deleteConfirmModal.SetActive(false);
            _pendingDeleteId = null;
        }
    }
}
