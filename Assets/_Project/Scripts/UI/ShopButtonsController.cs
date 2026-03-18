using UnityEngine;
using TMPro;
using AutoBattler.Client.Shop;
using AutoBattler.Client.Core;

namespace AutoBattler.Client.UI
{
    /// <summary>
    /// Connecte les WorldButtons du shop aux actions du ShopManager.
    /// Met à jour les labels et l'état enabled/disabled selon l'or et le tier.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "ShopButtonsController" dans la Hierarchy
    /// 2. Add Component → ShopButtonsController
    /// 3. Créer 4 WorldButtons dans la scène (voir WorldButton.cs pour les instructions)
    /// 4. Glisser les 4 boutons vers les champs correspondants
    /// </summary>
    public class ShopButtonsController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private WorldButton refreshButton;
        [SerializeField] private WorldButton freezeButton;
        [SerializeField] private WorldButton upgradeButton;
        [SerializeField] private WorldButton readyButton;

        [Header("Info Displays")]
        [Tooltip("TextMeshPro 3D pour afficher l'or (optionnel, dans le monde)")]
        [SerializeField] private TextMeshPro goldDisplay;
        [Tooltip("TextMeshPro 3D pour afficher le tier (optionnel, dans le monde)")]
        [SerializeField] private TextMeshPro tierDisplay;

        private void Start()
        {
            // Connecter les clics
            if (refreshButton != null) refreshButton.OnClick += OnRefreshClick;
            if (freezeButton != null) freezeButton.OnClick += OnFreezeClick;
            if (upgradeButton != null) upgradeButton.OnClick += OnUpgradeClick;
            if (readyButton != null) readyButton.OnClick += OnReadyClick;

            // Écouter les events du ShopManager
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnGoldChanged += UpdateGoldDisplay;
                ShopManager.Instance.OnTierChanged += UpdateTierDisplay;
                ShopManager.Instance.OnFreezeChanged += UpdateFreezeDisplay;
                ShopManager.Instance.OnPhaseInfo += UpdatePhaseDisplay;
            }

            // Tout masqué au démarrage — les boutons apparaissent quand la phase Recruiting commence
            SetButtonsVisible(false);
        }

        private void OnDestroy()
        {
            if (refreshButton != null) refreshButton.OnClick -= OnRefreshClick;
            if (freezeButton != null) freezeButton.OnClick -= OnFreezeClick;
            if (upgradeButton != null) upgradeButton.OnClick -= OnUpgradeClick;
            if (readyButton != null) readyButton.OnClick -= OnReadyClick;

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
                ShopManager.Instance.OnTierChanged -= UpdateTierDisplay;
                ShopManager.Instance.OnFreezeChanged -= UpdateFreezeDisplay;
                ShopManager.Instance.OnPhaseInfo -= UpdatePhaseDisplay;
            }
        }

        // =====================================================
        // ACTIONS
        // =====================================================

        private void OnRefreshClick() => ShopManager.Instance?.Reroll();
        private void OnFreezeClick() => ShopManager.Instance?.ToggleFreeze();
        private void OnUpgradeClick() => ShopManager.Instance?.UpgradeTavern();
        private void OnReadyClick() => ShopManager.Instance?.ReadyForCombat();

        // =====================================================
        // MISES À JOUR VISUELLES
        // =====================================================

        private void UpdateGoldDisplay(int gold)
        {
            if (goldDisplay != null)
                goldDisplay.text = $"{gold}";

            // Mettre à jour l'état des boutons selon l'or disponible
            if (refreshButton != null)
                refreshButton.Interactable = gold >= 1;
            if (upgradeButton != null)
                upgradeButton.Interactable = gold >= ShopManager.Instance.UpgradeCost && ShopManager.Instance.UpgradeCost > 0;
        }

        private void UpdateTierDisplay(int tier, int upgradeCost)
        {
            if (tierDisplay != null)
                tierDisplay.text = $"T{tier}";

            if (upgradeButton != null)
            {
                upgradeButton.Interactable = upgradeCost > 0;
                upgradeButton.Label = upgradeCost > 0 ? $"Upgrade\n({upgradeCost}g)" : "MAX";
            }
        }

        private void UpdateFreezeDisplay(bool isFrozen)
        {
            if (freezeButton != null)
                freezeButton.Label = isFrozen ? "Unfreeze" : "Freeze";
        }

        private void UpdatePhaseDisplay(string phase, int turn, int duration)
        {
            bool isRecruiting = phase == "Recruiting";
            SetButtonsVisible(isRecruiting);

            if (readyButton != null && isRecruiting)
                readyButton.Label = $"{duration}s";
        }

        private void SetButtonsVisible(bool visible)
        {
            if (refreshButton != null) refreshButton.gameObject.SetActive(visible);
            if (freezeButton != null) freezeButton.gameObject.SetActive(visible);
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(visible);
            if (readyButton != null) readyButton.gameObject.SetActive(visible);
        }
    }
}
