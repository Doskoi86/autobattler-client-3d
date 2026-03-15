using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AutoBattler.Client.Shop;
using AutoBattler.Client.Core;

namespace AutoBattler.Client.UI.HUD
{
    /// <summary>
    /// HUD principal du jeu : affiche l'or, le tier, le timer,
    /// et les boutons d'action (reroll, freeze, level up, ready).
    /// Connecté au ShopManager via events.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Text Displays")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI tierText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI upgradeCostText;

        [Header("Buttons")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button freezeButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button readyButton;

        [Header("Button Texts")]
        [SerializeField] private TextMeshProUGUI rerollButtonText;
        [SerializeField] private TextMeshProUGUI freezeButtonText;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;

        // Timer
        private float _timerRemaining;
        private bool _timerActive;

        private void Start()
        {
            // Connecter les boutons
            if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClick);
            if (freezeButton != null) freezeButton.onClick.AddListener(OnFreezeClick);
            if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClick);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClick);

            // S'abonner aux events du ShopManager
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnGoldChanged += UpdateGold;
                ShopManager.Instance.OnTierChanged += UpdateTier;
                ShopManager.Instance.OnFreezeChanged += UpdateFreeze;
                ShopManager.Instance.OnPhaseInfo += UpdatePhase;
            }
        }

        private void Update()
        {
            if (_timerActive && _timerRemaining > 0)
            {
                _timerRemaining -= Time.deltaTime;
                UpdateTimerDisplay();

                if (_timerRemaining <= 0)
                {
                    _timerActive = false;
                    _timerRemaining = 0;
                    UpdateTimerDisplay();
                }
            }
        }

        private void OnDestroy()
        {
            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnGoldChanged -= UpdateGold;
                ShopManager.Instance.OnTierChanged -= UpdateTier;
                ShopManager.Instance.OnFreezeChanged -= UpdateFreeze;
                ShopManager.Instance.OnPhaseInfo -= UpdatePhase;
            }
        }

        // =====================================================
        // UPDATES VISUELS
        // =====================================================

        private void UpdateGold(int gold)
        {
            if (goldText == null) return;
            goldText.text = $"{gold}";

            // Animation punch sur changement
            goldText.transform.DOKill();
            goldText.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f).SetEase(Ease.OutElastic);
        }

        private void UpdateTier(int tier, int upgradeCost)
        {
            if (tierText != null)
                tierText.text = $"Tier {tier}";

            if (upgradeCostText != null)
                upgradeCostText.text = upgradeCost > 0 ? $"Upgrade: {upgradeCost}g" : "MAX";

            if (upgradeButtonText != null)
                upgradeButtonText.text = upgradeCost > 0 ? $"Level Up ({upgradeCost}g)" : "MAX";
        }

        private void UpdateFreeze(bool isFrozen)
        {
            if (freezeButtonText != null)
                freezeButtonText.text = isFrozen ? "Unfreeze" : "Freeze";
        }

        private void UpdatePhase(string phase, int turn, int duration)
        {
            if (phaseText != null)
                phaseText.text = $"{phase} — Tour {turn}";

            if (duration > 0)
            {
                _timerRemaining = duration;
                _timerActive = true;
            }
            else
            {
                _timerActive = false;
            }

            UpdateTimerDisplay();

            // Masquer/afficher les boutons selon la phase
            bool isRecruiting = phase == "Recruiting";
            if (rerollButton != null) rerollButton.gameObject.SetActive(isRecruiting);
            if (freezeButton != null) freezeButton.gameObject.SetActive(isRecruiting);
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(isRecruiting);
            if (readyButton != null) readyButton.gameObject.SetActive(isRecruiting);
        }

        private void UpdateTimerDisplay()
        {
            if (timerText == null) return;

            int seconds = Mathf.CeilToInt(_timerRemaining);
            timerText.text = $"{seconds}s";

            // Urgence visuelle quand < 10s
            if (seconds <= 10 && seconds > 0)
                timerText.color = Color.red;
            else
                timerText.color = Color.white;
        }

        // =====================================================
        // BOUTONS
        // =====================================================

        private void OnRerollClick()
        {
            ShopManager.Instance?.Reroll();
        }

        private void OnFreezeClick()
        {
            ShopManager.Instance?.ToggleFreeze();
        }

        private void OnUpgradeClick()
        {
            ShopManager.Instance?.UpgradeTavern();
        }

        private void OnReadyClick()
        {
            ShopManager.Instance?.ReadyForCombat();
        }
    }
}
