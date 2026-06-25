using UnityEngine;

namespace Artti.Common
{
    // 버튼 onClick에 연결해 앱을 종료한다. 에디터에서는 플레이 모드 정지.
    public class AppQuit : MonoBehaviour
    {
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
