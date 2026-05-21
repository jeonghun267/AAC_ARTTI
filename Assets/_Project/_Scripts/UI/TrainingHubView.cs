using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Artti.UI
{
    public class TrainingHubView : MonoBehaviour
    {
        [SerializeField] private Button pharmacyBtn;
        [SerializeField] private Button convenienceBtn;
        [SerializeField] private Button restaurantBtn;

        private void Start()
        {
            pharmacyBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingPharmacyScene"));
            convenienceBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingConvenienceScene"));
            restaurantBtn.onClick.AddListener(() => SceneManager.LoadScene("TrainingRestaurantScene"));
        }
    }
}
