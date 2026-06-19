using TMPro;
using UnityEngine;
using Artti.Common;

namespace Artti.UI
{
    // 캐릭터 말풍선: 첫 줄에 현재 활성 프로필 이름("○○님")을 붙여 응원 문구를 만든다.
    // 예) 활성 프로필이 "오정훈"이면 -> "오정훈님 정말 잘하고 있어요!\n계속 파이팅해요!"
    // ReportView를 건드리지 않도록 독립 컴포넌트로 활성 프로필을 직접 읽는다.
    [DisallowMultipleComponent]
    public class CharacterGreeting : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [TextArea]
        [SerializeField] private string message = "정말 잘하고 있어요!\n계속 파이팅해요!";

        private void Start()
        {
            if (label == null) return;
            var profile = AppBootstrap.Instance?.ProfileManager?.ActiveProfile;
            string nick = profile != null ? profile.nickname : null;
            label.text = string.IsNullOrEmpty(nick) ? message : $"{nick}님 {message}";
        }
    }
}
