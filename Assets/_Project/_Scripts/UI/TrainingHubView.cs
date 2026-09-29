using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Artti.UI
{
    public class TrainingHubView : MonoBehaviour
    {
        [SerializeField] private Button convenienceBtn;

        private void Start()
        {
            convenienceBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingConvenienceScene"));
        }
    }
}
