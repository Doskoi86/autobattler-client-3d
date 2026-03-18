using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using AutoBattler.Client.Network.Protocol;

namespace AutoBattler.Client.Network
{
    /// <summary>
    /// Simulation locale du serveur pour développer et tester le client
    /// sans connexion réseau. Simule le flux complet d'une partie :
    /// lobby → héros → boucle (shop → combat → résultats) → game over.
    /// </summary>
    public class MockServerBridge : MonoBehaviour, IServerBridge
    {
        [Header("Settings")]
        [SerializeField] private float shopPhaseDuration = 45f;
        [SerializeField] private float resultsPhaseDuration = 5f;
        [SerializeField] private int startingGold = 3;
        [SerializeField] private int maxGold = 10;

        [Header("Data")]
        [Tooltip("JSON contenant les templates de minions (Assets/_Project/Data/mock_minions.json)")]
        [SerializeField] private TextAsset minionDataJson;

        // --- État interne ---
        private string _playerId;
        private string _gameId;
        private int _currentGold;
        private int _currentTier = 1;
        private int _turnNumber;
        private int _heroHealth = 30;
        private int _heroArmor = 5;
        private int _upgradeCost = 5;
        private bool _shopFrozen;
        private List<MinionState> _board = new List<MinionState>();
        private List<MinionState> _hand = new List<MinionState>();
        private List<ShopOfferState> _currentShopOffers = new List<ShopOfferState>();
        private List<PlayerPublicState> _opponents = new List<PlayerPublicState>();

        // Héros sélectionné
        private HeroOffer _selectedHero;
        private bool _heroPowerUsedThisTurn;
        private List<HeroOffer> _lastHeroOffers;

        // Données de minions pour la simulation
        private List<MinionTemplate> _minionPool = new List<MinionTemplate>();

        // --- IServerBridge : État ---
        public bool IsConnected { get; private set; }
        public string PlayerId => _playerId;
        public string GameId => _gameId;

        // --- IServerBridge : Events ---
        public event Action<string, string> OnConnected;
        public event Action<List<PlayerInfo>, List<string>> OnLobbyJoined;
        public event Action<string, string> OnLobbyPlayerJoined;
        public event Action<string> OnLobbyPlayerReady;
        public event Action<string> OnLobbyPlayerLeft;
        public event Action<string, List<PlayerInfo>> OnGameFound;
        public event Action<List<HeroOffer>> OnHeroOffered;
        public event Action OnAllHeroesSelected;
        public event Action<string, int, int> OnPhaseStarted;
        public event Action<List<PlayerPublicState>> OnPlayersUpdated;
        public event Action<List<ShopOfferState>, int, int> OnShopRefreshed;
        public event Action<string, string, int> OnMinionBought;
        public event Action<string, int> OnMinionSold;
        public event Action OnShopFrozen;
        public event Action<int, int, int> OnTavernUpgraded;
        public event Action<string, int> OnMinionPlayed;
        public event Action<List<MinionState>> OnBoardUpdated;
        public event Action<List<MinionState>> OnHandUpdated;
        public event Action<int, int> OnHeroHealthUpdated;
        public event Action<string, string> OnTripleFormed;
        public event Action<List<CombatEventData>, List<BoardSnapshot>, List<BoardSnapshot>> OnCombatReplay;
        public event Action<int, bool> OnHeroPowerUsed;
        public event Action<string, bool, int, string> OnCombatResult;
        public event Action<int> OnEliminated;
        public event Action<string, int> OnPlayerEliminated;
        public event Action OnGameWon;
        public event Action<List<RankingEntry>> OnGameOver;
        public event Action<string> OnError;

        private void Awake()
        {
            InitMinionPool();
            InitOpponents();
        }

        // =====================================================
        // CONNEXION
        // =====================================================

        public Task ConnectAsync(string playerName)
        {
            _playerId = Guid.NewGuid().ToString();
            _gameId = null;
            IsConnected = true;
            OnConnected?.Invoke(_playerId, playerName);
            Debug.Log($"[MockServer] Connecté : {playerName} ({_playerId})");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            Debug.Log("[MockServer] Déconnecté");
            return Task.CompletedTask;
        }

        // =====================================================
        // LOBBY
        // =====================================================

        public Task JoinLobbyAsync()
        {
            var players = new List<PlayerInfo>
            {
                new PlayerInfo { Id = _playerId, Name = "Vous" },
                new PlayerInfo { Id = "bot_1", Name = "Bob le Barman" }
            };
            OnLobbyJoined?.Invoke(players, new List<string>());
            Debug.Log("[MockServer] Lobby rejoint (2 joueurs)");
            return Task.CompletedTask;
        }

        public Task LobbyReadyAsync()
        {
            OnLobbyPlayerReady?.Invoke(_playerId);

            // Simuler que le bot est aussi prêt → lancer la partie
            StartCoroutine(DelayedAction(0.5f, () =>
            {
                OnLobbyPlayerReady?.Invoke("bot_1");
                StartGame();
            }));

            return Task.CompletedTask;
        }

        public Task LeaveLobbyAsync()
        {
            Debug.Log("[MockServer] Lobby quitté");
            return Task.CompletedTask;
        }

        // =====================================================
        // SÉLECTION HÉROS
        // =====================================================

        public Task SelectHeroAsync(string heroId)
        {
            // Retrouver l'offre sélectionnée pour stocker les infos du pouvoir
            _selectedHero = _lastHeroOffers?.Find(h => h.Id == heroId);
            Debug.Log($"[MockServer] Héros sélectionné : {heroId} ({_selectedHero?.Name})");

            StartCoroutine(DelayedAction(0.5f, () =>
            {
                OnAllHeroesSelected?.Invoke();
                StartRecruitingPhase();
            }));

            return Task.CompletedTask;
        }

        // =====================================================
        // ACTIONS SHOP
        // =====================================================

        public Task BuyMinionAsync(int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= _currentShopOffers.Count)
            {
                OnError?.Invoke("Index d'offre invalide.");
                return Task.CompletedTask;
            }

            var offer = _currentShopOffers[offerIndex];
            if (offer == null)
            {
                OnError?.Invoke("Ce slot est déjà vide.");
                return Task.CompletedTask;
            }

            if (_currentGold < 3)
            {
                OnError?.Invoke("Pas assez d'or.");
                return Task.CompletedTask;
            }

            _currentGold -= 3;

            var minion = new MinionState
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

            _hand.Add(minion);
            _currentShopOffers[offerIndex] = null;

            OnMinionBought?.Invoke(minion.InstanceId, minion.Name, _currentGold);
            OnHandUpdated?.Invoke(new List<MinionState>(_hand));
            OnShopRefreshed?.Invoke(
                _currentShopOffers.Where(o => o != null).ToList(),
                _currentGold,
                _upgradeCost
            );

            Debug.Log($"[MockServer] Acheté {minion.Name} — or restant : {_currentGold}");
            return Task.CompletedTask;
        }

        public Task SellMinionAsync(string instanceId)
        {
            var fromBoard = _board.FirstOrDefault(m => m.InstanceId == instanceId);
            var fromHand = _hand.FirstOrDefault(m => m.InstanceId == instanceId);

            if (fromBoard != null)
            {
                _board.Remove(fromBoard);
                _currentGold = Mathf.Min(_currentGold + 1, maxGold);
                OnMinionSold?.Invoke(instanceId, _currentGold);
                OnBoardUpdated?.Invoke(new List<MinionState>(_board));
                Debug.Log($"[MockServer] Vendu {fromBoard.Name} du board — or : {_currentGold}");
            }
            else if (fromHand != null)
            {
                _hand.Remove(fromHand);
                _currentGold = Mathf.Min(_currentGold + 1, maxGold);
                OnMinionSold?.Invoke(instanceId, _currentGold);
                OnHandUpdated?.Invoke(new List<MinionState>(_hand));
                Debug.Log($"[MockServer] Vendu {fromHand.Name} de la main — or : {_currentGold}");
            }
            else
            {
                OnError?.Invoke("Minion non trouvé.");
            }

            return Task.CompletedTask;
        }

        public Task PlayMinionAsync(string instanceId, int slotIndex)
        {
            if (_board.Count >= 7)
            {
                OnError?.Invoke("Le plateau est plein (7 max).");
                return Task.CompletedTask;
            }

            var minion = _hand.FirstOrDefault(m => m.InstanceId == instanceId);
            if (minion == null)
            {
                OnError?.Invoke("Minion non trouvé en main.");
                return Task.CompletedTask;
            }

            _hand.Remove(minion);

            int actualSlot = slotIndex >= 0 ? Mathf.Min(slotIndex, _board.Count) : _board.Count;
            _board.Insert(actualSlot, minion);

            OnMinionPlayed?.Invoke(instanceId, actualSlot);
            OnBoardUpdated?.Invoke(new List<MinionState>(_board));
            OnHandUpdated?.Invoke(new List<MinionState>(_hand));

            Debug.Log($"[MockServer] Posé {minion.Name} en slot {actualSlot}");
            return Task.CompletedTask;
        }

        public Task MoveMinionAsync(string instanceId, int toIndex)
        {
            var minion = _board.FirstOrDefault(m => m.InstanceId == instanceId);
            if (minion == null)
            {
                OnError?.Invoke("Minion non trouvé sur le board.");
                return Task.CompletedTask;
            }

            _board.Remove(minion);
            int clampedIndex = Mathf.Clamp(toIndex, 0, _board.Count);
            _board.Insert(clampedIndex, minion);

            OnBoardUpdated?.Invoke(new List<MinionState>(_board));
            Debug.Log($"[MockServer] Déplacé {minion.Name} vers slot {clampedIndex}");
            return Task.CompletedTask;
        }

        public Task RefreshShopAsync()
        {
            if (_currentGold < 1)
            {
                OnError?.Invoke("Pas assez d'or pour reroll.");
                return Task.CompletedTask;
            }

            _currentGold -= 1;
            if (_shopFrozen)
            {
                _shopFrozen = false;
                OnShopFrozen?.Invoke();
            }
            GenerateShopOffers();

            OnShopRefreshed?.Invoke(
                new List<ShopOfferState>(_currentShopOffers),
                _currentGold,
                _upgradeCost
            );

            Debug.Log($"[MockServer] Shop reroll — or restant : {_currentGold}");
            return Task.CompletedTask;
        }

        public Task FreezeShopAsync()
        {
            _shopFrozen = !_shopFrozen;
            OnShopFrozen?.Invoke();
            Debug.Log($"[MockServer] Shop {(_shopFrozen ? "gelé" : "dégelé")}");
            return Task.CompletedTask;
        }

        public Task UpgradeTavernAsync()
        {
            if (_currentGold < _upgradeCost)
            {
                OnError?.Invoke("Pas assez d'or pour level up.");
                return Task.CompletedTask;
            }

            if (_currentTier >= 6)
            {
                OnError?.Invoke("Tier maximum atteint.");
                return Task.CompletedTask;
            }

            _currentGold -= _upgradeCost;
            _currentTier++;
            _upgradeCost = GetUpgradeCost(_currentTier);

            OnTavernUpgraded?.Invoke(_currentTier, _currentGold, _upgradeCost);
            Debug.Log($"[MockServer] Taverne niveau {_currentTier} — or restant : {_currentGold}");
            return Task.CompletedTask;
        }

        public Task ReadyForCombatAsync()
        {
            Debug.Log("[MockServer] Prêt pour le combat");
            StartCombatPhase();
            return Task.CompletedTask;
        }

        public Task UseHeroPowerAsync()
        {
            if (_selectedHero == null)
            {
                OnError?.Invoke("Aucun héros sélectionné.");
                return Task.CompletedTask;
            }

            if (_heroPowerUsedThisTurn)
            {
                OnError?.Invoke("Pouvoir héroïque déjà utilisé ce tour.");
                return Task.CompletedTask;
            }

            bool isPassive = _selectedHero.HeroPowerType == "PassiveAlways" || _selectedHero.HeroPowerType == "Triggered";
            if (isPassive)
            {
                OnError?.Invoke("Ce pouvoir est passif.");
                return Task.CompletedTask;
            }

            int cost = _selectedHero.HeroPowerCost;
            if (_currentGold < cost)
            {
                OnError?.Invoke("Pas assez d'or pour le pouvoir héroïque.");
                return Task.CompletedTask;
            }

            _currentGold -= cost;
            _heroPowerUsedThisTurn = true;

            // Simuler l'effet selon le héros
            switch (_selectedHero.Id)
            {
                case "hero_warlock":
                    // Perdez 2 PV, gagnez 1 or
                    _heroHealth -= 2;
                    _currentGold += 1;
                    OnHeroHealthUpdated?.Invoke(_heroHealth, _heroArmor);
                    break;

                case "hero_paladin":
                    // +1/+1 à un allié aléatoire
                    if (_board.Count > 0)
                    {
                        var target = _board[UnityEngine.Random.Range(0, _board.Count)];
                        target.Attack += 1;
                        target.Health += 1;
                        OnBoardUpdated?.Invoke(new List<MinionState>(_board));
                    }
                    break;

                case "hero_necro":
                    // Placeholder : on donne juste un minion 1/1
                    if (_board.Count < 7)
                    {
                        _board.Add(new MinionState
                        {
                            InstanceId = System.Guid.NewGuid().ToString(),
                            Name = "Esprit Ressuscité",
                            Attack = 1, Health = 1, Tier = 1,
                            Keywords = "", Tribes = "Undead", IsGolden = false
                        });
                        OnBoardUpdated?.Invoke(new List<MinionState>(_board));
                    }
                    break;

                default:
                    // Pour les autres héros actifs, effet placeholder
                    Debug.Log($"[MockServer] Pouvoir {_selectedHero.Name} activé (effet placeholder)");
                    break;
            }

            OnHeroPowerUsed?.Invoke(_currentGold, false);
            Debug.Log($"[MockServer] Pouvoir héroïque utilisé — Or : {_currentGold}");
            return Task.CompletedTask;
        }

        public Task AnimationDoneAsync()
        {
            Debug.Log("[MockServer] Animations terminées");
            StartRecruitingPhase();
            return Task.CompletedTask;
        }

        // =====================================================
        // LOGIQUE INTERNE — FLUX DE JEU
        // =====================================================

        private void StartGame()
        {
            _gameId = Guid.NewGuid().ToString();
            _turnNumber = 0;

            var players = new List<PlayerInfo>
            {
                new PlayerInfo { Id = _playerId, Name = "Vous" },
                new PlayerInfo { Id = "bot_1", Name = "Bob le Barman" }
            };

            OnGameFound?.Invoke(_gameId, players);

            // Proposer 4 héros parmi les 10 disponibles
            var allHeroes = new List<HeroOffer>
            {
                new HeroOffer { Id = "hero_paladin", Name = "Le Paladin", Description = "Donne +1/+1 à un allié aléatoire.", HeroPowerCost = 2, HeroPowerType = "ActiveCost" },
                new HeroOffer { Id = "hero_ranger", Name = "La Rôdeuse", Description = "Gagne 1 or après chaque victoire au combat.", HeroPowerCost = 0, HeroPowerType = "Triggered" },
                new HeroOffer { Id = "hero_shaman", Name = "Le Chaman Tauren", Description = "Vos minions adjacents au centre gagnent +1/+1.", HeroPowerCost = 0, HeroPowerType = "PassiveAlways" },
                new HeroOffer { Id = "hero_warchief", Name = "Le Chef de Guerre", Description = "Quand un allié meurt au combat, un allié aléatoire gagne +1 ATQ.", HeroPowerCost = 0, HeroPowerType = "PassiveAlways" },
                new HeroOffer { Id = "hero_necro", Name = "Le Nécromancien", Description = "Ressuscite le dernier allié mort au combat.", HeroPowerCost = 1, HeroPowerType = "ActiveCost" },
                new HeroOffer { Id = "hero_dragon", Name = "Le Seigneur Dragon", Description = "Au début du combat, tous vos Dragons gagnent +1/+1.", HeroPowerCost = 0, HeroPowerType = "PassiveAlways" },
                new HeroOffer { Id = "hero_warlock", Name = "Le Démoniste", Description = "Perdez 2 PV, gagnez 1 or.", HeroPowerCost = 0, HeroPowerType = "ActiveFree" },
                new HeroOffer { Id = "hero_illusionist", Name = "L'Illusionniste", Description = "Copie un minion ennemi aléatoire dans votre shop.", HeroPowerCost = 2, HeroPowerType = "ActiveCost" },
                new HeroOffer { Id = "hero_engineer", Name = "L'Ingénieur", Description = "Vos Mécas gagnent automatiquement des pièces détachées.", HeroPowerCost = 0, HeroPowerType = "PassiveAlways" },
                new HeroOffer { Id = "hero_beastqueen", Name = "La Reine des Bêtes", Description = "Invoque un jeton Bête 1/1 au début de chaque tour.", HeroPowerCost = 0, HeroPowerType = "PassiveAlways" }
            };

            // Mélanger et prendre 4
            for (int i = allHeroes.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (allHeroes[i], allHeroes[j]) = (allHeroes[j], allHeroes[i]);
            }
            var heroes = allHeroes.GetRange(0, Mathf.Min(4, allHeroes.Count));
            _lastHeroOffers = heroes;

            OnHeroOffered?.Invoke(heroes);
            Debug.Log($"[MockServer] Partie créée : {_gameId}");
        }

        private void StartRecruitingPhase()
        {
            _turnNumber++;
            _currentGold = Mathf.Min(startingGold + _turnNumber - 1, maxGold);
            _heroPowerUsedThisTurn = false;

            // Réduire le coût d'upgrade de 1 par tour (minimum 0)
            _upgradeCost = Mathf.Max(0, _upgradeCost - 1);

            if (!_shopFrozen)
                GenerateShopOffers();
            _shopFrozen = false;

            int duration = Mathf.Max(15, (int)(shopPhaseDuration - (_turnNumber / 5) * 5));

            OnPhaseStarted?.Invoke("Recruiting", _turnNumber, duration);
            OnPlayersUpdated?.Invoke(GetAllPlayersState());

            // Renvoyer l'état du board et de la main pour repositionner les tokens
            // après le combat (le board n'a pas changé mais la phase a changé → positions différentes)
            OnBoardUpdated?.Invoke(new List<MinionState>(_board));
            OnHandUpdated?.Invoke(new List<MinionState>(_hand));

            OnShopRefreshed?.Invoke(
                new List<ShopOfferState>(_currentShopOffers),
                _currentGold,
                _upgradeCost
            );

            Debug.Log($"[MockServer] Tour {_turnNumber} — Recrutement ({duration}s) — Or : {_currentGold}");
        }

        private void StartCombatPhase()
        {
            OnPhaseStarted?.Invoke("Combat", _turnNumber, 0);

            var opponentBoard = GenerateOpponentBoard();

            var playerSnap = _board.Select(m => new BoardSnapshot
            {
                InstanceId = m.InstanceId,
                Name = m.Name,
                Attack = m.Attack,
                Health = m.Health,
                Tier = m.Tier
            }).ToList();

            var opponentSnap = opponentBoard.Select(m => new BoardSnapshot
            {
                InstanceId = m.InstanceId,
                Name = m.Name,
                Attack = m.Attack,
                Health = m.Health,
                Tier = m.Tier
            }).ToList();

            // Simuler le combat avec suivi des PV et morts
            var combatEvents = SimulateCombatEvents(
                _board.ToList(), opponentBoard, playerSnap, opponentSnap);

            // Déterminer le résultat à partir de la simulation
            int playerSurvivors = playerSnap.Count(s =>
                combatEvents.All(e => !(e.TargetId == s.InstanceId && e.TargetDied) &&
                                      !(e.AttackerId == s.InstanceId && e.AttackerDied)));
            int opponentSurvivors = opponentSnap.Count(s =>
                combatEvents.All(e => !(e.TargetId == s.InstanceId && e.TargetDied) &&
                                      !(e.AttackerId == s.InstanceId && e.AttackerDied)));

            bool didWin;
            int damage;

            if (playerSurvivors > 0 && opponentSurvivors == 0)
            {
                didWin = true;
                damage = 0;
            }
            else if (opponentSurvivors > 0 && playerSurvivors == 0)
            {
                didWin = false;
                damage = _currentTier + opponentBoard.Sum(m => m.Tier);
            }
            else
            {
                // Égalité ou les deux ont des survivants (timer)
                int playerPower = _board.Sum(m => m.Attack + m.Health);
                int opponentPower = opponentBoard.Sum(m => m.Attack + m.Health);
                didWin = playerPower >= opponentPower;
                damage = didWin ? 0 : _currentTier + opponentBoard.Sum(m => m.Tier);
            }

            OnCombatReplay?.Invoke(combatEvents, playerSnap, opponentSnap);

            StartCoroutine(DelayedAction(0.3f, () =>
            {
                if (!didWin && damage > 0)
                {
                    _heroHealth -= damage;
                    OnHeroHealthUpdated?.Invoke(_heroHealth, _heroArmor);
                }

                OnCombatResult?.Invoke("bot_1", didWin, damage,
                    didWin ? "Victoire !" : (damage == 0 ? "Égalité !" : $"Défaite ! -{damage} PV"));

                if (_heroHealth <= 0)
                {
                    OnEliminated?.Invoke(2);
                    OnGameOver?.Invoke(new List<RankingEntry>
                    {
                        new RankingEntry { PlayerId = "bot_1", PlayerName = "Bob le Barman", FinalRank = 1 },
                        new RankingEntry { PlayerId = _playerId, PlayerName = "Vous", FinalRank = 2 }
                    });
                    return;
                }

                SimulateOpponentEliminations();
                OnPhaseStarted?.Invoke("Results", _turnNumber, (int)resultsPhaseDuration);
                Debug.Log($"[MockServer] Combat terminé — {(didWin ? "Victoire" : $"Défaite (-{damage} PV)")} — HP : {_heroHealth}");
            }));
        }

        // =====================================================
        // GÉNÉRATION DE DONNÉES
        // =====================================================

        private void GenerateShopOffers()
        {
            int slotCount = GetShopSlotCount(_currentTier);
            _currentShopOffers.Clear();

            var availableMinions = _minionPool.Where(m => m.Tier <= _currentTier).ToList();

            for (int i = 0; i < slotCount; i++)
            {
                var template = availableMinions[UnityEngine.Random.Range(0, availableMinions.Count)];
                _currentShopOffers.Add(new ShopOfferState
                {
                    Index = i,
                    InstanceId = Guid.NewGuid().ToString(),
                    Name = template.Name,
                    Attack = template.Attack,
                    Health = template.Health,
                    Tier = template.Tier,
                    Frozen = false,
                    Keywords = template.Keywords,
                    Description = template.Description,
                    Tribes = template.Tribe
                });
            }
        }

        private List<MinionState> GenerateOpponentBoard()
        {
            var board = new List<MinionState>();
            int count = Mathf.Min(_turnNumber + 1, 7);
            var available = _minionPool.Where(m => m.Tier <= _currentTier).ToList();

            for (int i = 0; i < count; i++)
            {
                var template = available[UnityEngine.Random.Range(0, available.Count)];
                board.Add(new MinionState
                {
                    InstanceId = Guid.NewGuid().ToString(),
                    Name = template.Name,
                    Attack = template.Attack + _turnNumber / 3,
                    Health = template.Health + _turnNumber / 3,
                    Tier = template.Tier,
                    Keywords = template.Keywords,
                    Description = template.Description,
                    Tribes = template.Tribe,
                    IsGolden = false
                });
            }

            return board;
        }

        /// <summary>
        /// Simule un combat complet avec suivi des PV, morts, Taunt et DivineShield.
        /// Produit des events séquentiels qui correspondent à ce qui se passe réellement.
        /// </summary>
        private List<CombatEventData> SimulateCombatEvents(
            List<MinionState> playerMinions, List<MinionState> opponentMinions,
            List<BoardSnapshot> playerSnap, List<BoardSnapshot> opponentSnap)
        {
            var events = new List<CombatEventData>();

            // Copies de travail avec PV mutables
            var pSide = playerSnap.Select(s => new CombatMinion(s,
                playerMinions.FirstOrDefault(m => m.InstanceId == s.InstanceId)?.Keywords ?? "")).ToList();
            var oSide = opponentSnap.Select(s => new CombatMinion(s,
                opponentMinions.FirstOrDefault(m => m.InstanceId == s.InstanceId)?.Keywords ?? "")).ToList();

            bool playerTurn = UnityEngine.Random.value > 0.5f;
            int pNextAttacker = 0;
            int oNextAttacker = 0;
            int maxIterations = 100;

            while (pSide.Count > 0 && oSide.Count > 0 && maxIterations-- > 0)
            {
                var attackers = playerTurn ? pSide : oSide;
                var defenders = playerTurn ? oSide : pSide;
                int nextIdx = playerTurn ? pNextAttacker : oNextAttacker;

                if (attackers.Count == 0 || defenders.Count == 0) break;

                // Round-robin attacker
                nextIdx = nextIdx % attackers.Count;
                var atk = attackers[nextIdx];

                // Cibler un Taunt en priorité, sinon aléatoire
                var taunts = defenders.Where(d => d.HasTaunt).ToList();
                var def = taunts.Count > 0
                    ? taunts[UnityEngine.Random.Range(0, taunts.Count)]
                    : defenders[UnityEngine.Random.Range(0, defenders.Count)];

                // Résoudre l'attaque
                bool defShieldPopped = false;
                bool atkShieldPopped = false;

                // Dégâts sur le défenseur
                if (def.HasDivineShield)
                {
                    def.HasDivineShield = false;
                    defShieldPopped = true;
                }
                else
                {
                    def.Health -= atk.Attack;
                }

                // Contre-attaque sur l'attaquant
                if (atk.HasDivineShield)
                {
                    atk.HasDivineShield = false;
                    atkShieldPopped = true;
                }
                else
                {
                    atk.Health -= def.Attack;
                }

                bool defDied = def.Health <= 0;
                bool atkDied = atk.Health <= 0;

                events.Add(new CombatEventData
                {
                    Type = "Attack",
                    AttackerId = atk.Id,
                    TargetId = def.Id,
                    Damage = atk.Attack,
                    DivineShieldPopped = defShieldPopped,
                    TargetDied = defDied,
                    AttackerDied = atkDied,
                    AttackerName = atk.Name,
                    TargetName = def.Name,
                    AttackerAttack = atk.Attack,
                    TargetAttack = def.Attack,
                    AttackerHealthAfter = Mathf.Max(0, atk.Health),
                    TargetHealthAfter = Mathf.Max(0, def.Health),
                    TokenName = ""
                });

                // Retirer les morts
                if (defDied) defenders.Remove(def);
                if (atkDied) attackers.Remove(atk);

                // Avancer l'index d'attaque
                if (playerTurn)
                    pNextAttacker = atkDied ? nextIdx : nextIdx + 1;
                else
                    oNextAttacker = atkDied ? nextIdx : nextIdx + 1;

                playerTurn = !playerTurn;
            }

            return events;
        }

        /// <summary>Minion mutable pour la simulation de combat.</summary>
        private class CombatMinion
        {
            public string Id;
            public string Name;
            public int Attack;
            public int Health;
            public bool HasDivineShield;
            public bool HasTaunt;

            public CombatMinion(BoardSnapshot snap, string keywords)
            {
                Id = snap.InstanceId;
                Name = snap.Name;
                Attack = snap.Attack;
                Health = snap.Health;
                HasDivineShield = keywords?.Contains("DivineShield") ?? false;
                HasTaunt = keywords?.Contains("Taunt") ?? false;
            }
        }

        private void SimulateOpponentEliminations()
        {
            // Chaque tour, 10% de chance d'éliminer un bot restant
            var alive = _opponents.Where(o => !o.IsEliminated).ToList();
            foreach (var opponent in alive)
            {
                if (UnityEngine.Random.value < 0.1f)
                {
                    opponent.IsEliminated = true;
                    int remainingCount = _opponents.Count(o => !o.IsEliminated) + 1;
                    OnPlayerEliminated?.Invoke(opponent.PlayerId, remainingCount + 1);
                }
            }
        }

        private List<PlayerPublicState> GetAllPlayersState()
        {
            var states = new List<PlayerPublicState>
            {
                new PlayerPublicState
                {
                    PlayerId = _playerId,
                    Health = _heroHealth,
                    Armor = _heroArmor,
                    TavernTier = _currentTier,
                    IsEliminated = false
                }
            };

            states.AddRange(_opponents);
            return states;
        }

        // =====================================================
        // INITIALISATION DES DONNÉES
        // =====================================================

        private void InitMinionPool()
        {
            if (minionDataJson == null)
            {
                Debug.LogError("[MockServer] minionDataJson non assigné ! Glisser mock_minions.json depuis Assets/_Project/Data/ dans l'Inspector.");
                return;
            }

            var data = JsonUtility.FromJson<MinionTemplateList>(minionDataJson.text);
            _minionPool = new List<MinionTemplate>();

            foreach (var entry in data.minions)
            {
                _minionPool.Add(new MinionTemplate(
                    entry.name, entry.attack, entry.health, entry.tier,
                    entry.keywords, entry.tribe, entry.description
                ));
            }

            Debug.Log($"[MockServer] {_minionPool.Count} minions chargés depuis JSON");
        }

        /// <summary>Wrapper pour la désérialisation JSON du pool de minions</summary>
        [System.Serializable]
        private class MinionTemplateList
        {
            public MinionJsonEntry[] minions;
        }

        [System.Serializable]
        private class MinionJsonEntry
        {
            public string name;
            public int attack;
            public int health;
            public int tier;
            public string keywords;
            public string tribe;
            public string description;
        }

        private void InitOpponents()
        {
            string[] botNames = { "Bob le Barman", "Patches le Pirate", "Ragnaros",
                                  "Dame Vashj", "Roi-Liche", "Millhouse", "Kel'Thuzad" };

            _opponents = new List<PlayerPublicState>();
            for (int i = 0; i < botNames.Length; i++)
            {
                _opponents.Add(new PlayerPublicState
                {
                    PlayerId = $"bot_{i + 1}",
                    Health = 30,
                    Armor = UnityEngine.Random.Range(0, 10),
                    TavernTier = 1,
                    IsEliminated = false
                });
            }
        }

        // =====================================================
        // UTILITAIRES
        // =====================================================

        private int GetShopSlotCount(int tier)
        {
            if (tier <= 1) return 3;
            if (tier <= 3) return 4;
            if (tier <= 5) return 5;
            return 6;
        }

        private int GetUpgradeCost(int currentTier)
        {
            switch (currentTier)
            {
                case 1: return 5;   // vers tier 2
                case 2: return 7;   // vers tier 3
                case 3: return 8;   // vers tier 4
                case 4: return 9;   // vers tier 5
                case 5: return 11;  // vers tier 6
                default: return 0;  // déjà tier 6
            }
        }

        private IEnumerator DelayedAction(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        /// <summary>
        /// Template interne pour la génération de minions mock.
        /// </summary>
        private class MinionTemplate
        {
            public string Name;
            public int Attack;
            public int Health;
            public int Tier;
            public string Keywords;
            public string Tribe;
            public string Description;

            public MinionTemplate(string name, int attack, int health, int tier,
                string keywords, string tribe, string description)
            {
                Name = name;
                Attack = attack;
                Health = health;
                Tier = tier;
                Keywords = keywords;
                Tribe = tribe;
                Description = description;
            }
        }
    }
}
