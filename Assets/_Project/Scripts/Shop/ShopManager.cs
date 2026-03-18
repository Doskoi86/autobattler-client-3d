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
        [Tooltip("Espacement entre les tokens du shop")]
        [SerializeField] private float shopSpacing = 2.5f;

        [Header("Gameplay")]
        [SerializeField] private int buyCost = 3;
        [SerializeField] private int rerollCost = 1;

        [Header("Animation")]
        [SerializeField] private float sellAnimDuration = 0.25f;
        [SerializeField] private float rerollAnimDuration = 0.3f;
        [SerializeField] private float shopStaggerDelay = 0.08f;
        [SerializeField] private float freezePunchScale = 0.05f;
        [SerializeField] private float freezePunchDuration = 0.2f;

        // Tokens actuellement dans le shop
        private List<MinionTokenVisual> _shopTokens = new List<MinionTokenVisual>();
        private Dictionary<string, MinionTokenVisual> _shopTokenMap = new Dictionary<string, MinionTokenVisual>();
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
                return surface != null ? surface.GetShopCenter() : Vector3.zero;
            }
        }

        // =====================================================
        // ACTIONS JOUEUR
        // =====================================================

        public bool BuyCard(int shopIndex)
        {
            if (_currentGold < buyCost)
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
            if (_currentGold < rerollCost)
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

            var shopToken = _shopTokens.FirstOrDefault(t => t != null && t.Data?.InstanceId == instanceId);
            if (shopToken != null)
            {
                shopToken.AnimateDeath();
                _shopTokens[_shopTokens.IndexOf(shopToken)] = null;
                _shopTokenMap.Remove(instanceId);
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

            foreach (var token in _shopTokens)
            {
                if (token == null) continue;
                token.SetFrozen(_isFrozen);
                // Punch seulement si le token est déjà à sa taille finale (pas en cours de spawn)
                if (_isFrozen && token.transform.localScale.sqrMagnitude > 0.5f)
                    token.transform.DOPunchScale(Vector3.one * freezePunchScale, freezePunchDuration);
            }

            Debug.Log($"[Shop] Shop {(_isFrozen ? "gelé" : "dégelé")}");
        }

        private void HandlePhaseStarted(string phase, int turn, int duration)
        {
            OnPhaseInfo?.Invoke(phase, turn, duration);

            if (phase == "Combat" || phase == "Results")
            {
                // Cacher les tokens shop pendant le combat
                HideShopTokens();
            }
            else if (phase == "Recruiting")
            {
                _isFrozen = false;
                OnFreezeChanged?.Invoke(false);

                if (_currentTier < 1) _currentTier = 1;
                OnTierChanged?.Invoke(_currentTier, _upgradeCost);
            }
        }

        /// <summary>
        /// Cache tous les tokens visuels du shop (ils seront recréés au prochain OnShopRefreshed).
        /// </summary>
        private void HideShopTokens()
        {
            foreach (var token in _shopTokens)
            {
                if (token != null) token.gameObject.SetActive(false);
            }
            _shopTokens.Clear();
            _shopTokenMap.Clear();
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
            if (CardFactory.Instance == null) return;

            var center = ShopCenter;
            float totalWidth = (offers.Count - 1) * shopSpacing;
            float startX = center.x - totalWidth / 2f;

            // Identifier les tokens existants par InstanceId
            var existingIds = new HashSet<string>(offers.ConvertAll(o => o.InstanceId));

            // Retirer les tokens qui ne sont plus dans les offres
            var toRemove = new List<string>();
            foreach (var kvp in _shopTokenMap)
            {
                if (!existingIds.Contains(kvp.Key))
                {
                    if (kvp.Value != null)
                        kvp.Value.gameObject.SetActive(false);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
                _shopTokenMap.Remove(id);

            // Reconstruire la liste ordonnée
            _shopTokens.Clear();

            for (int i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                var targetPos = new Vector3(startX + i * shopSpacing, center.y, center.z);

                // Chercher un token existant pour cette offre
                if (_shopTokenMap.TryGetValue(offer.InstanceId, out var existingToken) && existingToken != null)
                {
                    // Token existant → glisser vers sa nouvelle position
                    existingToken.transform.DOKill();
                    existingToken.transform.DOMove(targetPos, rerollAnimDuration).SetEase(Ease.OutQuad);
                    existingToken.SetBasePosition(targetPos);

                    // Mettre à jour le ShopCardClick index
                    var shopClick = existingToken.gameObject.GetComponent<ShopCardClick>();
                    if (shopClick != null) shopClick.Setup(offer.Index, this);

                    _shopTokens.Add(existingToken);
                }
                else
                {
                    // Nouveau token → créer avec animation de spawn
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

                    var token = CardFactory.Instance.CreateToken(minionData, showTier: true);
                    if (token == null) continue;

                    var baseScale = token.transform.localScale;
                    token.transform.position = targetPos + Vector3.up * 2f;
                    token.transform.localScale = Vector3.zero;
                    token.transform.DOMove(targetPos, rerollAnimDuration).SetEase(Ease.OutQuad)
                        .SetDelay(i * shopStaggerDelay);
                    token.transform.DOScale(baseScale, rerollAnimDuration).SetEase(Ease.OutBack)
                        .SetDelay(i * shopStaggerDelay);

                    token.SetBasePosition(targetPos);

                    var shopClick = token.gameObject.GetComponent<ShopCardClick>();
                    if (shopClick != null)
                    {
                        shopClick.enabled = true;
                        shopClick.Setup(offer.Index, this);
                    }

                    _shopTokens.Add(token);
                    _shopTokenMap[offer.InstanceId] = token;
                }
            }
        }
    }
}
