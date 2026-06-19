using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.UI
{
    // 좌상단 토스트바(햄버거) 메뉴. 클릭하면 팝업을 토글하고,
    // 팝업 안의 "레포트 보기" / "종료하기" 버튼이 각 동작을 수행한다.
    public class HomeMenu : MonoBehaviour
    {
        [SerializeField] private GameObject popup;
        [SerializeField] private string reportScene = "ReportScene";

        public void SetPopup(GameObject p)
        {
            popup = p;
            if (popup != null) popup.SetActive(false);
        }

        public void Toggle()
        {
            if (popup != null) popup.SetActive(!popup.activeSelf);
        }

        public void OpenReport()
        {
            SceneManager.LoadScene(reportScene);
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
