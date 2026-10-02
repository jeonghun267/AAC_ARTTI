using UnityEngine;

namespace Artti.UI
{
    // 메인 화면 좌상단 버튼 행("종료하기" / "레포트 보기")의 클릭 동작.
    public class HomeMenu : MonoBehaviour
    {
        // 변경점: 모드별 리포트 중 마지막으로 연습한 모드의 리포트를 연다(기록이 없으면 정확도 모드). 왼쪽 메뉴로 서로 오간다
        public void OpenReport() => ReportNavigation.OpenLastTrained();

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
