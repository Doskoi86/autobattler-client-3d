using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using AutoBattler.Client.Core;
using AutoBattler.Client.Cards;
using AutoBattler.Client.Board;
using AutoBattler.Client.Network;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Shop
{
    /// <summary>
    /// Gère la phase Shop : affichage des offres, achat par clic,
    /// vente, reroll, freeze, level up.
    ///
    /// Le positionnement des cartes est basé sur la ShopZone du BoardSurface.
    /// La zone est un GameObject visible dans la scène — déplacer la zone
    /// dans l'éditeur déplace automatiquement les cartes du shop.
    ///
    /// 📋 DANS UNITY EDITOR :
    /// 1. Créer un GameObject "ShopManager" dans la Hierarchy
    /// 2. Add Component → ShopManager
    /// 3. Les cartes du shop apparaîtront centrées sur la ShopZone du BoardSurface
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Shop Layout")]
        [Tooltip("Espacement entre les tokens du shop (adapté aux tokens compacts ~1.2u)")]
        [SerializeField] private float shopSpacing = 2.0f;

        [Header("Animation")]
        [SerializeField] private float sellAnimDuration = 0.25f;
        [SerializeField] private float rerollAnimDuration = 0.3f;

        // Cartes actuellement dans le shop
        private List<CardVisual> _shopCards = new List<CardVisual>();
        private Dictionary<string, CardVisual> _shopCardMap = new Dictionary<string, CardVisual>();
        private int _currentGold;
        private int _currentTier;
        private int _upgradeCost;
        private bool _isFrozen;

        // Events pour le HUD
        public event System.Action<int> OnGoldChanged;
        public event System.Action<int, int> OnTierChanged;
        public event System.Action<bool> OnFreezeChanged;
        public event System.Action<string, int, int> OnPhaseInfo;

        public int CurrentGold => _currentGold;
        public int CurrentTier => _currentTier;
        public int UpgradeCost => _upgradeCost;
        public bool IsFrozen => _isFrozen;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnShopRefreshed += HandleShopRefreshed;
            server.OnMinionBought += HandleMinionBought;
            server.OnMinionSold += HandleMinionSold;
            server.OnBoardUpdated += HandleBoardUpdated;
            server.OnTavernUpgraded += HandleTavernUpgraded;
            server.OnShopFrozen += HandleShopFrozen;
            server.OnPhaseStarted += HandlePhaseStarted;
            server.OnError += HandleError;
        }

        private void OnDestroy()
        {
            var server = GameManager.Instance?.Server;
            if (server == null) return;

            server.OnShopRefreshed -= HandleShopRefreshed;
            server.OnMinionBought -= HandleMinionBought;
            server.OnMinionSold -= HandleMinionSold;
            server.OnBoardUpdated -= HandleBoardUpdated;
            server.OnTavernUpgraded -= HandleTavernUpgraded;
            server.OnShopFrozen -= HandleShopFrozen;
            server.OnPhaseStarted -= HandlePhaseStarted;
            server.OnError -= HandleError;
        }

        // =====================================================
        // ZONE DE RÉFÉRENCE
        // =====================================================

        /// <summary>Position centrale de la zone shop (depuis le BoardSurface)</summary>
        private Vector3 ShopCenter
        {
            get
            {
                var surface = BoardSurface.Instance;
                return surface != null ? surface.GetShopCenter() : new Vector3(0.9f, 0.01f, 5.41f);
            }
        }

        // =====================================================
        // ACTIONS JOUEUR
        // =====================================================

        public bool BuyCard(int shopIndex)
        {
            if (_currentGold < 3)
            {
                Debug.Log("[Shop] Pas assez d'or !");
                return false;
            }
            GameManager.Instance.Server?.BuyMinionAsync(shopIndex);
            return true;
        }

        public void SellCard(string instanceId)
        {
            GameManager.Instance.Server?.SellMinionAsync(instanceId);
        }

        public void Reroll()
        {
            if (_currentGold < 1)
            {
                Debug.Log("[Shop] Pas assez d'or pour reroll !");
                return;
            }
            GameManager.Instance.Server?.RefreshShopAsync();
        }

        public void ToggleFreeze()
        {
            GameManager.Instance.Server?.FreezeShopAsync();
        }

        public void UpgradeTavern()
        {
            if (_currentGold < _upgradeCost)
            {
                Debug.Log("[Shop] Pas assez d'or pour level up !");
                return;
            }
            GameManager.Instance.Server?.UpgradeTavernAsync();
        }

        public void ReadyForCombat()
        {
            GameManager.Instance.Server?.ReadyForCombatAsync();
        }

        // =====================================================
        // HANDLERS SERVEUR
        // =====================================================

        private void HandleShopRefreshed(List<ShopOfferState> offers, int gold, int upgradeCost)
        {
            _currentGold = gold;
            _upgradeCost = upgradeCost;
            OnGoldChanged?.Invoke(_currentGold);
            OnTierChanged?.Invoke(_currentTier, _upgradeCost);

            RefreshShopVisuals(offers);
        }

        private void HandleMinionBought(string instanceId, string name, int gold)
        {
            _currentGold = gold;
            OnGoldChanged?.Invoke(_currentGold);

            var shopCard = _shopCards.FirstOrDefault(c => c != null && c.Data?.InstanceId == instanceId);
            if (shopCard != null)
            {
                shopCard.AnimateDeath();
                _shopCards[_shopCards.IndexOf(shopCard)] = null;
                _shopCardMap.Remove(instanceId);
            }

            Debug.Log($"[Shop] Acheté {name} — Or : {gold}");
        }

        private void HandleMinionSold(string instanceId, int gold)
        {
            _currentGold = gold;
            OnGoldChanged?.Invoke(_currentGold);
            Debug.Log($"[Shop] Vendu — Or : {gold}");
        }

        private void HandleBoardUpdated(List<MinionState> minions)
        {
            Debug.Log($"[Shop] Board mis à jour : {minions.Count} minions");
        }

        private void HandleTavernUpgraded(int tier, int gold, int upgradeCost)
        {
            _currentTier = tier;
            _currentGold = gold;
            _upgradeCost = upgradeCost;

            OnGoldChanged?.Invoke(_currentGold);
            OnTierChanged?.Invoke(_currentTier, _upgradeCost);

            Debug.Log($"[Shop] Taverne niveau {tier} ! Or : {gold}, prochain upgrade : {upgradeCost}");
        }

        private void HandleShopFrozen()
        {
            _isFrozen = !_isFrozen;
            OnFreezeChanged?.Invoke(_isFrozen);

            foreach (var card in _shopCards)
            {
                if (card == null) continue;
                if (_isFrozen)
                    card.transform.DOPunchScale(Vector3.one * 0.05f, 0.2f);
            }

            Debug.Log($"[Shop] Shop {(_isFrozen ? "gelé" : "dégelé")}");
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            OnPhaseInfo?.Invoke(phase, turn, duration);

            if (phase == "Recruiting")
            {
                _isFrozen = false;
                OnFreezeChanged?.Invoke(false);

                if (_currentTier < 1) _currentTier = 1;
                OnTierChanged?.Invoke(_currentTier, _upgradeCost);
            }
        }

        private void HandleError(string message)
        {
            Debug.LogWarning($"[Shop] Erreur : {message}");
        }

        // =====================================================
        // VISUELS SHOP
        // =====================================================

        private void RefreshShopVisuals(List<ShopOfferState> offers)
        {
            foreach (var card in _shopCards)
            {
                if (card != null)
                {
                    _shopCardMap.Remove(card.Data?.InstanceId ?? "");
                    card.gameObject.SetActive(false);
                }
            }
            _shopCards.Clear();

            if (CardFactory.Instance == null) return;

            var center = ShopCenter;
            float totalWidth = (offers.Count - 1) * shopSpacing;
            float startX = center.x - totalWidth / 2f;

            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                var minionData = new MinionState
                {
                    InstanceId = offer.InstanceId,
                    Name = offer.Name,
                    Attack = offer.Attack,
                    Health = offer.Health,
                    Tier = offer.Tier,
                    Keywords = offer.Keywords,
                    Description = offer.Description,
                    Tribes = offer.Tribes,
                    IsGolden = false
                };

                var card = CardFactory.Instance.CreateCard(minionData);
                if (card == null) continue;

                var targetPos = new Vector3(startX + i * shopSpacing, center.y, center.z);

                card.transform.position = targetPos + Vector3.up * 2f;
                card.transform.localScale = Vector3.zero;
                card.transform.DOMove(targetPos, rerollAnimDuration).SetEase(Ease.OutQuad)
                    .SetDelay(i * 0.08f);
                card.transform.DOScale(Vector3.one, rerollAnimDuration).SetEase(Ease.OutBack)
                    .SetDelay(i * 0.08f);

                card.SetBasePosition(targetPos);

                var clickHandler = card.GetComponent<ShopCardClick>();
                if (clickHandler != null)
                {
                    clickHandler.enabled = true;
                    clickHandler.Setup(i, this);
                }

                _shopCards.Add(card);
                _shopCardMap[offer.InstanceId] = card;
            }
        }
    }
}
