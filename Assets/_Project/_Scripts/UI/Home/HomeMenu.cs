using UnityEngine;
using Artti.Common;

namespace Artti.UI
{
    // 메인 화면 좌상단 버튼 행("종료하기" / "레포트 보기")의 클릭 동작.
    public class HomeMenu : MonoBehaviour
    {
        [SerializeField] private string reportScene = "ReportScene";

        public void OpenReport()
        {
            SceneFader.Load(reportScene);   // 변경점: 페이드 전환
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
