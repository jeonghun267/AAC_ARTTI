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

            if (greetingAvatar != null)
            {
                Sprite sp = null;
                if (profile != null)
                    sp = AvatarLibrary.Load()?.GetById(profile.avatarId);
                if (sp != null) greetingAvatar.sprite = sp;
                greetingAvatar.gameObject.SetActive(sp != null);
            }
        }
    }
}
