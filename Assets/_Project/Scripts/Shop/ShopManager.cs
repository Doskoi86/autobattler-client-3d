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
    /// vente, reroll, freeze, level up. Connecté à IServerBridge.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Shop Layout")]
        [Tooltip("Position Y des cartes du shop")]
        [SerializeField] private float shopY = 0.1f;
        [Tooltip("Position Z des cartes du shop (devant le plateau)")]
        [SerializeField] private float shopZ = 5.5f;
        [Tooltip("Espacement entre les cartes du shop")]
        [SerializeField] private float shopSpacing = 1.8f;

        [Header("Hand Layout")]
        [Tooltip("Position Z de la zone de main")]
        [SerializeField] private float handZ = -3.5f;
        [Tooltip("Espacement des cartes en main")]
        [SerializeField] private float handSpacing = 1.6f;

        [Header("Animation")]
        [SerializeField] private float buyAnimDuration = 0.4f;
        [SerializeField] private float sellAnimDuration = 0.25f;
        [SerializeField] private float rerollAnimDuration = 0.3f;

        // État
        private List<CardVisual> _shopCards = new List<CardVisual>();
        private List<CardVisual> _handCards = new List<CardVisual>();
        private Dictionary<string, CardVisual> _allCards = new Dictionary<string, CardVisual>();
        private int _currentGold;
        private int _currentTier;
        private int _upgradeCost;
        private bool _isFrozen;

        // Events pour le HUD
        public event System.Action<int> OnGoldChanged;           // gold
        public event System.Action<int, int> OnTierChanged;      // tier, upgradeCost
        public event System.Action<bool> OnFreezeChanged;         // isFrozen
        public event System.Action<string, int, int> OnPhaseInfo; // phase, turn, duration

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
            server.OnHandUpdated += HandleHandUpdated;
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
            server.OnHandUpdated -= HandleHandUpdated;
            server.OnBoardUpdated -= HandleBoardUpdated;
            server.OnTavernUpgraded -= HandleTavernUpgraded;
            server.OnShopFrozen -= HandleShopFrozen;
            server.OnPhaseStarted -= HandlePhaseStarted;
            server.OnError -= HandleError;
        }

        // =====================================================
        // ACTIONS JOUEUR (appelées par les boutons UI)
        // =====================================================

        /// <summary>Acheter une carte du shop par son index. Retourne false si impossible.</summary>
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

        /// <summary>Vendre un minion par son InstanceId</summary>
        public void SellCard(string instanceId)
        {
            GameManager.Instance.Server?.SellMinionAsync(instanceId);
        }

        /// <summary>Reroll le shop</summary>
        public void Reroll()
        {
            if (_currentGold < 1)
            {
                Debug.Log("[Shop] Pas assez d'or pour reroll !");
                return;
            }
            GameManager.Instance.Server?.RefreshShopAsync();
        }

        /// <summary>Toggle freeze du shop</summary>
        public void ToggleFreeze()
        {
            GameManager.Instance.Server?.FreezeShopAsync();
        }

        /// <summary>Level up la taverne</summary>
        public void UpgradeTavern()
        {
            if (_currentGold < _upgradeCost)
            {
                Debug.Log("[Shop] Pas assez d'or pour level up !");
                return;
            }
            GameManager.Instance.Server?.UpgradeTavernAsync();
        }

        /// <summary>Prêt pour le combat</summary>
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

            // Retirer la carte du shop visuellement
            var shopCard = _shopCards.FirstOrDefault(c => c != null && c.Data?.InstanceId == instanceId);
            if (shopCard != null)
            {
                shopCard.AnimateDeath();
                _shopCards[_shopCards.IndexOf(shopCard)] = null;
            }

            Debug.Log($"[Shop] Acheté {name} — Or : {gold}");
        }

        private void HandleMinionSold(string instanceId, int gold)
        {
            _currentGold = gold;
            OnGoldChanged?.Invoke(_currentGold);

            // Retirer la carte visuellement
            if (_allCards.TryGetValue(instanceId, out var card) && card != null)
            {
                card.transform.DOScale(0f, sellAnimDuration).SetEase(Ease.InBack)
                    .OnComplete(() => Destroy(card.gameObject));
                _allCards.Remove(instanceId);
            }

            Debug.Log($"[Shop] Vendu — Or : {gold}");
        }

        private void HandleHandUpdated(List<MinionState> minions)
        {
            RefreshHandVisuals(minions);
        }

        private void HandleBoardUpdated(List<MinionState> minions)
        {
            // Le BoardManager gère le board — ici on met juste à jour le tracking
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

            // Effet visuel de gel sur les cartes du shop
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

                // Le tier est au minimum 1 dès le premier tour
                if (_currentTier < 1) _currentTier = 1;
                OnTierChanged?.Invoke(_currentTier, _upgradeCost);
            }
        }

        private void HandleError(string message)
        {
            Debug.LogWarning($"[Shop] Erreur : {message}");
        }

        // =====================================================
        // VISUELS
        // =====================================================

        private void RefreshShopVisuals(List<ShopOfferState> offers)
        {
            // Nettoyer les anciennes cartes
            foreach (var card in _shopCards)
            {
                if (card != null)
                {
                    _allCards.Remove(card.Data?.InstanceId ?? "");
                    Destroy(card.gameObject);
                }
            }
            _shopCards.Clear();

            if (CardFactory.Instance == null) return;

            // Créer les nouvelles cartes
            float totalWidth = (offers.Count - 1) * shopSpacing;
            float startX = -totalWidth / 2f;

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
                var targetPos = new Vector3(startX + i * shopSpacing, shopY, shopZ);

                // Animation d'apparition
                card.transform.position = targetPos + Vector3.up * 2f;
                card.transform.localScale = Vector3.zero;
                card.transform.DOMove(targetPos, rerollAnimDuration).SetEase(Ease.OutQuad)
                    .SetDelay(i * 0.08f);
                card.transform.DOScale(Vector3.one, rerollAnimDuration).SetEase(Ease.OutBack)
                    .SetDelay(i * 0.08f);

                card.SetBasePosition(targetPos);

                // Ajouter un composant pour identifier cette carte comme carte shop
                var clickHandler = card.gameObject.AddComponent<ShopCardClick>();
                clickHandler.Setup(i, this);

                _shopCards.Add(card);
                _allCards[offer.InstanceId] = card;
            }
        }

        private void RefreshHandVisuals(List<MinionState> minions)
        {
            // Nettoyer les cartes en main qui ne sont plus présentes
            var currentIds = new HashSet<string>(minions.Select(m => m.InstanceId));
            var toRemove = _handCards.Where(c => c != null && !currentIds.Contains(c.Data?.InstanceId ?? "")).ToList();
            foreach (var card in toRemove)
            {
                _allCards.Remove(card.Data?.InstanceId ?? "");
                Destroy(card.gameObject);
                _handCards.Remove(card);
            }

            // Ajouter les nouvelles cartes en main
            foreach (var minion in minions)
            {
                if (_allCards.ContainsKey(minion.InstanceId)) continue;

                if (CardFactory.Instance == null) continue;
                var card = CardFactory.Instance.CreateCard(minion);
                _handCards.Add(card);
                _allCards[minion.InstanceId] = card;
            }

            // Repositionner toutes les cartes en main
            float totalWidth = (_handCards.Count - 1) * handSpacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < _handCards.Count; i++)
            {
                if (_handCards[i] == null) continue;
                var targetPos = new Vector3(startX + i * handSpacing, shopY, handZ);
                _handCards[i].transform.DOMove(targetPos, 0.3f).SetEase(Ease.OutQuad);
                _handCards[i].SetBasePosition(targetPos);
            }
        }
    }
}
