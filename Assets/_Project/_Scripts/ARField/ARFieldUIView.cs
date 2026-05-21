using UnityEngine;
using Artti.ARField;
using Artti.AAC;

namespace Artti.ARField
{
    public class ARFieldUIView : MonoBehaviour
    {
        [SerializeField] private GameObject cameraPanel;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private GameObject subCategoryPanel;

        [SerializeField] private AACCardButton[] categorySlots;
        [SerializeField] private AACCardButton[] subCategorySlots;

        public void UpdateUI(ARFieldState state)
        {
            cameraPanel.SetActive(state == ARFieldState.CameraScan);
            confirmPanel.SetActive(state == ARFieldState.ConfirmRecognition);
            categoryPanel.SetActive(state == ARFieldState.SelectCategory);
            subCategoryPanel.SetActive(state == ARFieldState.SelectSubCategory);
        }

        public void SetCategoryCards(AACCard[] cards)
        {
            for (int i = 0; i < categorySlots.Length; i++)
            {
                if (i < cards.Length)
                {
                    categorySlots[i].gameObject.SetActive(true);
                    categorySlots[i].SetCard(cards[i]);
                }
                else
                {
                    categorySlots[i].gameObject.SetActive(false);
                }
            }
        }

        public void SetSubCategoryCards(AACCard[] cards)
        {
            for (int i = 0; i < subCategorySlots.Length; i++)
            {
                if (i < cards.Length)
                {
                    subCategorySlots[i].gameObject.SetActive(true);
                    subCategorySlots[i].SetCard(cards[i]);
                }
                else
                {
                    subCategorySlots[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
