using UnityEngine;

namespace Artti.Common
{
    // 버튼 onClick에 연결해 앱을 종료한다. 에디터에서는 플레이 모드 정지.
    public class AppQuit : MonoBehaviour
    {
        public void Quit() => QuitApp();

        // 변경점: 코드에서 부르는 종료(편의점 훈련 완료 화면의 '종료하기' — 메인 화면의 같은 버튼과 같은 동작)
        public static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
