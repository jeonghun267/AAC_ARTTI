using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Artti.AAC;

namespace Artti.UI
{
    public class ARFieldUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject cameraPanel;
        [SerializeField] private GameObject resultConfirmPanel;
        [SerializeField] private GameObject manualSelectPanel;
        [SerializeField] private GameObject categoryPanel;
        [SerializeField] private GameObject subCategoryPanel;

        [Header("Manual Select Buttons")]
        [SerializeField] private Button convenienceBtn;
        [SerializeField] private Button pharmacyBtn;
        [SerializeField] private Button restaurantBtn;

        [Header("Category Card Slots")]
        [SerializeField] private AACCardSlotUI[] categorySlots;

        [Header("SubCategory Card Slots")]
        [SerializeField] private AACCardSlotUI[] subCategorySlots;

        public event Action<string> OnManualPlaceSelected;
        public event Action<AACCard> OnCategorySelected;
        public event Action<AACCard> OnSubCategorySelected;

        private void Awake()
        {
            if (convenienceBtn != null)
                convenienceBtn.onClick.AddListener(() => OnManualPlaceSelected?.Invoke(ScenarioIds.Convenience));
            if (pharmacyBtn != null)
                pharmacyBtn.onClick.AddListener(() => OnManualPlaceSelected?.Invoke(ScenarioIds.Pharmacy));
            if (restaurantBtn != null)
                restaurantBtn.onClick.AddListener(() => OnManualPlaceSelected?.Invoke(ScenarioIds.Restaurant));
        }

        private void Start()
        {
            ShowCamera();
        }

        public void ShowCamera() => SetActiveOnly(cameraPanel);
        public void ShowResultConfirm() => SetActiveOnly(resultConfirmPanel);
        public void ShowManualSelect() => SetActiveOnly(manualSelectPanel);
        public void GoToCategoryPanel() => SetActiveOnly(categoryPanel);
        public void GoToSubCategoryPanel() => SetActiveOnly(subCategoryPanel);

        public void ShowCategory(IReadOnlyList<AACCard> cards)
        {
            SetActiveOnly(categoryPanel);
            RefreshSlots(categorySlots, cards, OnCategorySelected);
        }

        public void ShowSubCategory(IReadOnlyList<AACCard> cards)
        {
            SetActiveOnly(subCategoryPanel);
            RefreshSlots(subCategorySlots, cards, OnSubCategorySelected);
        }

        private void SetActiveOnly(GameObject target)
        {
            if (cameraPanel != null) cameraPanel.SetActive(cameraPanel == target);
            if (resultConfirmPanel != null) resultConfirmPanel.SetActive(resultConfirmPanel == target);
            if (manualSelectPanel != null) manualSelectPanel.SetActive(manualSelectPanel == target);
            if (categoryPanel != null) categoryPanel.SetActive(categoryPanel == target);
            if (subCategoryPanel != null) subCategoryPanel.SetActive(subCategoryPanel == target);
        }

        private void RefreshSlots(AACCardSlotUI[] slots, IReadOnlyList<AACCard> cards, Action<AACCard> callback)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                bool active = i < cards.Count;
                slots[i].gameObject.SetActive(active);
                if (active)
                    slots[i].Setup(cards[i], callback);
            }
        }
    }
}
