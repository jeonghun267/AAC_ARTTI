using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Artti.Common;

namespace Artti.UI
{
    public class MainSceneView : MonoBehaviour
    {
        [SerializeField] private Button trainingModeBtn;
        [SerializeField] private Button arFieldModeBtn;
        [SerializeField] private Button reportBtn;

        [Header("Greeting")]
        [SerializeField] private TMP_Text greetingText;
        [SerializeField] private Image greetingAvatar;

        [Header("Profile button (좌측 프로필 칩)")]
        [SerializeField] private TMP_Text profileNameLabel;   // 활성 프로필 이름으로 갱신
        [SerializeField] private Image profileButtonAvatar;   // 활성 프로필 아바타

        private void Start()
        {
            trainingModeBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingHubScene"));
            arFieldModeBtn.onClick.AddListener(() => SceneManager.LoadScene("ARFieldScene"));
            // 레포트는 모달이 아닌 전용 씬으로 진입 (401/402 시안)
            if (reportBtn != null) reportBtn.onClick.AddListener(() => SceneManager.LoadScene("ReportScene"));

            UpdateGreeting();
        }

        private void UpdateGreeting()
        {
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;

            if (greetingText != null)
                greetingText.text = profile != null && !string.IsNullOrEmpty(profile.nickname)
                    ? $"반갑습니다 {profile.nickname} 님!"
                    : "반갑습니다!";

            Sprite avatarSprite = (profile != null) ? AvatarLibrary.Load()?.GetById(profile.avatarId) : null;

            if (greetingAvatar != null)
            {
                if (avatarSprite != null) greetingAvatar.sprite = avatarSprite;
                greetingAvatar.gameObject.SetActive(avatarSprite != null);
            }

            // 좌측 프로필 칩: 이름 라벨 + 아바타를 활성 프로필로 갱신
            if (profileNameLabel != null)
                profileNameLabel.text = profile != null && !string.IsNullOrEmpty(profile.nickname)
                    ? profile.nickname
                    : "프로필";

            if (profileButtonAvatar != null)
            {
                if (avatarSprite != null) profileButtonAvatar.sprite = avatarSprite;
                profileButtonAvatar.gameObject.SetActive(avatarSprite != null);
            }
        }
    }
}
