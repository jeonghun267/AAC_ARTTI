using UnityEngine;

namespace Artti.Common
{
    public class SceneBackButton : MonoBehaviour
    {
        [SerializeField] private string targetScene;

        public void SetTarget(string scene) => targetScene = scene;

        public void GoBack()
        {
            if (!string.IsNullOrEmpty(targetScene))
                SceneFader.Load(targetScene);   // 변경점: 페이드 전환
        }
    }
}
