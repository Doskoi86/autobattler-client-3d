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
            Debug.Log($"[MockServer] Héros sélectionné : {heroId}");

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
            _shopFrozen = false;
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
            if (_shopFrozen)
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

            // Proposer des héros
            var heroes = new List<HeroOffer>
            {
                new HeroOffer { Id = "hero_human", Name = "Uther le Champion", Description = "Pouvoir héroïque : Donne +1/+1 à un allié aléatoire." },
                new HeroOffer { Id = "hero_dragon", Name = "Ysera la Rêveuse", Description = "Pouvoir héroïque : Les Dragons dans le shop coûtent 2 or." }
            };

            OnHeroOffered?.Invoke(heroes);
            Debug.Log($"[MockServer] Partie créée : {_gameId}");
        }

        private void StartRecruitingPhase()
        {
            _turnNumber++;
            _currentGold = Mathf.Min(startingGold + _turnNumber - 1, maxGold);

            // Réduire le coût d'upgrade de 1 par tour (minimum 0)
            _upgradeCost = Mathf.Max(0, _upgradeCost - 1);

            if (!_shopFrozen)
                GenerateShopOffers();
            _shopFrozen = false;

            int duration = Mathf.Max(15, (int)(shopPhaseDuration - (_turnNumber / 5) * 5));

            OnPhaseStarted?.Invoke("Recruiting", _turnNumber, duration);
            OnPlayersUpdated?.Invoke(GetAllPlayersState());
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

            // Simuler un combat simple
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

            // Simuler des events de combat basiques
            var combatEvents = SimulateCombatEvents(playerSnap, opponentSnap);

            // Résultat aléatoire pondéré par la force du board
            int playerPower = _board.Sum(m => m.Attack + m.Health);
            int opponentPower = opponentBoard.Sum(m => m.Attack + m.Health);
            bool didWin = playerPower >= opponentPower;
            int damage = didWin ? 0 : _currentTier + opponentBoard.Sum(m => m.Tier);

            OnCombatReplay?.Invoke(combatEvents, playerSnap, opponentSnap);

            StartCoroutine(DelayedAction(0.5f, () =>
            {
                if (!didWin)
                {
                    _heroHealth -= damage;
                    OnHeroHealthUpdated?.Invoke(_heroHealth, _heroArmor);
                }

                OnCombatResult?.Invoke("bot_1", didWin, damage,
                    didWin ? "Victoire !" : $"Défaite ! -{damage} PV");

                // Vérifier élimination
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

                // Éliminer des bots aléatoirement pour simuler la progression
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

        private List<CombatEventData> SimulateCombatEvents(
            List<BoardSnapshot> playerBoard, List<BoardSnapshot> opponentBoard)
        {
            var events = new List<CombatEventData>();

            // Simuler quelques attaques basiques pour que le client ait quelque chose à animer
            int rounds = Mathf.Min(playerBoard.Count + opponentBoard.Count, 8);
            for (int i = 0; i < rounds; i++)
            {
                bool playerAttacks = i % 2 == 0;
                var attackers = playerAttacks ? playerBoard : opponentBoard;
                var defenders = playerAttacks ? opponentBoard : playerBoard;

                if (attackers.Count == 0 || defenders.Count == 0) break;

                var attacker = attackers[i % attackers.Count];
                var defender = defenders[UnityEngine.Random.Range(0, defenders.Count)];

                events.Add(new CombatEventData
                {
                    Type = "Attack",
                    AttackerId = attacker.InstanceId,
                    TargetId = defender.InstanceId,
                    Damage = attacker.Attack,
                    DivineShieldPopped = false,
                    TargetDied = defender.Health <= attacker.Attack,
                    AttackerDied = attacker.Health <= defender.Attack,
                    AttackerName = attacker.Name,
                    TargetName = defender.Name,
                    AttackerAttack = attacker.Attack,
                    TargetAttack = defender.Attack,
                    AttackerHealthAfter = Mathf.Max(0, attacker.Health - defender.Attack),
                    TargetHealthAfter = Mathf.Max(0, defender.Health - attacker.Attack),
                    TokenName = ""
                });
            }

            return events;
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
            // Pool simplifié de minions pour le mock (un par tier par race)
            _minionPool = new List<MinionTemplate>
            {
                // Tier 1
                new MinionTemplate("Recrue Humaine",    1, 2, 1, "", "Human",  ""),
                new MinionTemplate("Louveteau",         2, 1, 1, "", "Beast",  ""),
                new MinionTemplate("Méca-Assembleur",   1, 3, 1, "", "Mech",   ""),
                new MinionTemplate("Diablotin",         2, 2, 1, "", "Demon",  "Battlecry : Votre héros perd 1 PV."),
                new MinionTemplate("Dragonnet",         1, 2, 1, "", "Dragon", ""),
                new MinionTemplate("Elfe Archer",       2, 1, 1, "", "Elf",    ""),

                // Tier 2
                new MinionTemplate("Garde Taurène",     2, 4, 2, "Taunt", "Tauren", "Taunt."),
                new MinionTemplate("Loup Alpha",        3, 2, 2, "", "Beast",  "Deathrattle : Invoque un 1/1."),
                new MinionTemplate("Méca-Blindé",       2, 3, 2, "DivineShield", "Mech", "Bouclier divin."),
                new MinionTemplate("Nécro-Servant",     3, 3, 2, "", "Undead", ""),
                new MinionTemplate("Fée Soigneuse",     1, 3, 2, "", "Fairy",  "Battlecry : Donne +1/+1 à un allié."),

                // Tier 3
                new MinionTemplate("Champion Orc",      4, 4, 3, "", "Orc",    ""),
                new MinionTemplate("Dragon de Bronze",  3, 5, 3, "", "Dragon", "Battlecry : +2/+2 si vous avez un Dragon."),
                new MinionTemplate("Golem de Fer",      3, 6, 3, "Taunt", "Mech", "Taunt."),
                new MinionTemplate("Démon Vorace",      5, 3, 3, "", "Demon",  "Battlecry : Héros perd 2 PV. Gagne +3/+3."),
                new MinionTemplate("Esprit Sylvestre",  2, 4, 3, "", "Elf",    "Deathrattle : Donne +2/+2 à un allié."),

                // Tier 4
                new MinionTemplate("Seigneur de Guerre", 5, 5, 4, "", "Orc",    ""),
                new MinionTemplate("Hydre Sauvage",     4, 4, 4, "Cleave", "Beast", "Cleave."),
                new MinionTemplate("Mécageant",         3, 8, 4, "Taunt", "Mech",  "Taunt."),
                new MinionTemplate("Liche Mineure",     4, 5, 4, "Reborn", "Undead", "Reborn."),

                // Tier 5
                new MinionTemplate("Roi Dragon",        6, 6, 5, "", "Dragon", "Battlecry : +3/+3 à tous les Dragons."),
                new MinionTemplate("Berserker Orc",     8, 4, 5, "Windfury", "Orc", "Windfury."),
                new MinionTemplate("Archi-Fée",         4, 7, 5, "", "Fairy",  "Fin de tour : +1/+1 à tous les alliés."),

                // Tier 6
                new MinionTemplate("Aspect Draconique", 8, 8, 6, "", "Dragon", "Tous vos Dragons ont +2/+2."),
                new MinionTemplate("Titan Mécanique",   6, 10, 6, "Taunt,DivineShield", "Mech", "Taunt. Bouclier divin."),
                new MinionTemplate("Archidémon",        10, 6, 6, "", "Demon", "Battlecry : Héros perd 5 PV."),
            };
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
