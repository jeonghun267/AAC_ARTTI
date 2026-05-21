using UnityEngine;
using UnityEngine.SceneManagement;

namespace Artti.Common
{
    public class SceneBackButton : MonoBehaviour
    {
        [SerializeField] private string targetScene;

        public void SetTarget(string scene) => targetScene = scene;

        public void GoBack()
        {
            if (!string.IsNullOrEmpty(targetScene))
                SceneManager.LoadScene(targetScene);
        }
    }
}
